package dev.phonewheel

import android.os.SystemClock
import java.net.InetAddress
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

data class ControllerSnapshot(val controls: Controls = Controls(), val sensorValid: Boolean = false,
    val foreground: Boolean = false, val touchReady: Boolean = false, val arm: Boolean = false,
    val epoch: UInt = 1u, val sensorTimestampNs: Long = 0)

class UdpControllerClient(
    host: String, private val port: Int, private val session: ULong, private val key: ByteArray,
    private val onStatus: (StatusFrame) -> Unit, private val onHaptic: (HapticFrame) -> Unit,
    private val onDisconnected: () -> Unit = {},
    private val nowNanos: () -> Long = { SystemClock.elapsedRealtimeNanos() },
    private val flight: ControllerFlightRecorder? = null
) : AutoCloseable {
    private val address = InetAddress.getByName(host)
    private fun newTransport(): PacketTransport = WifiPacketTransport(address, port)
    private val socket = AtomicReference<PacketTransport?>(newTransport())
    private val executor = Executors.newScheduledThreadPool(2) { task ->
        Thread({
            runCatching { android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_MORE_FAVORABLE) }
            task.run()
        }, "mdrive-controller-io")
    }
    private val running = AtomicBoolean(true)
    private val snapshot = AtomicReference(ControllerSnapshot())
    private val history = LinkedHashMap<UInt, Long>()
    @Volatile private var sequence = 0u
    @Volatile private var peerSequence = 0u
    @Volatile private var peerSequenceSeen = false
    @Volatile private var helloAccepted = false
    @Volatile private var lastHelloNs = Long.MIN_VALUE
    @Volatile private var lastPeerNs = 0L
    @Volatile var sendFailures = 0; private set
    @Volatile var maxSendGapMs = 0L; private set
    @Volatile var receivedStatuses = 0L; private set
    @Volatile var maxStatusGapMs = 0L; private set
    private var previousSendNs = 0L
    private var previousReady = false
    private var hostWasActive = false

    init {
        executor.execute(::receiveLoop)
        executor.scheduleWithFixedDelay(::sendSafely, 0, 8_333_333, java.util.concurrent.TimeUnit.NANOSECONDS)
    }
    fun update(value: ControllerSnapshot) { snapshot.set(value) }
    private fun sendSafely() {
        // Scheduled executors suppress every future run after an uncaught exception.
        // Never let an optional callback/invalid snapshot silently kill the sender.
        try { sendLatest() }
        catch (_: Exception) { sendFailures++; flight?.record(FlightEntry(FlightEvent.SENDER_ERROR), true) }
    }

    private fun sendLatest() {
        if (!running.get()) return
        val active = socket.get() ?: return
        // Capture the immutable input BEFORE sampling the clock. Otherwise a
        // sensor update between these reads can appear to come from the future,
        // clearing READY for one packet and disarming the PC during normal input.
        val s = snapshot.get()
        val nowNs = nowNanos()
        val gapMs = if (previousSendNs == 0L) 0 else (nowNs - previousSendNs) / 1_000_000
        if (previousSendNs != 0L) maxSendGapMs = maxOf(maxSendGapMs, (nowNs - previousSendNs) / 1_000_000)
        previousSendNs = nowNs
        if (helloAccepted && nowNs - lastPeerNs > 1_000_000_000) {
            helloAccepted = false; lastHelloNs = Long.MIN_VALUE
            flight?.record(FlightEntry(FlightEvent.RECONNECT, reason = 1), s.arm)
            onDisconnected()
        }
        val paired = helloAccepted
        if (!paired && lastHelloNs != Long.MIN_VALUE && nowNs - lastHelloNs < 333_333_333) return
        if (!paired) lastHelloNs = nowNs
        sequence++
        // Receive can complete the handshake concurrently. Header and body must
        // use one snapshot, otherwise a codec failure stops the scheduled sender.
        val controlKind = if (s.controls.lookX != 0f || s.controls.lookY != 0f) PacketKind.CONTROL_LOOK else PacketKind.CONTROL
        val header = Header(if (paired) controlKind else PacketKind.HELLO, session, sequence, (nowNs / 1000).toULong(), peerSequence)
        val bytes = if (!paired) Pwr1.encodeHello(header, key) else {
            val sensorFresh = s.sensorValid && nowNs - s.sensorTimestampNs in 0..100_000_000
            val flags = Pwr1.FAST_RECOVERY or (if (s.arm) Pwr1.ARM else 0) or (if (sensorFresh) 2 else 0) or (if (s.foreground) 4 else 0) or (if (s.touchReady) 8 else 0)
            val safeControls = if (sensorFresh && s.foreground && s.touchReady) s.controls else Controls()
            synchronized(history) {
                history[sequence] = nowNs; while (history.size > 512) history.remove(history.keys.first())
                history.entries.removeIf { nowNs - it.value > 10_000_000_000 }
            }
            val ready = flags and Pwr1.READY == Pwr1.READY
            flight?.record(FlightEntry(FlightEvent.SEND, sequence = sequence.toLong(), ack = header.ack.toLong(),
                sendGapMs = gapMs, sensorAgeMs = (nowNs - s.sensorTimestampNs) / 1_000_000, flags = flags,
                arm = if (s.arm) 1 else 0, nonNeutral = if (s.controls.isNeutral()) 0 else 1), previousReady && !ready && s.arm)
            previousReady = ready
            Pwr1.encodeControl(ControlFrame(header, safeControls, flags.toUShort(), s.epoch), key)
        }
        // An ICMP/network error must not permanently cancel the periodic sender.
        try { active.send(bytes) }
        catch (_: java.io.IOException) { sendFailures++; flight?.record(FlightEntry(FlightEvent.TRANSPORT_ERROR, reason = 1), s.arm) }
    }

    private fun receiveLoop() {
        while (running.get()) try {
            if (socket.get() == null) {
                Thread.sleep(500)
                if (!running.get()) break
                val replacement = newTransport()
                socket.set(replacement)
                if (!running.get()) { socket.getAndSet(null)?.close(); break }
            }
            val data = socket.get()?.receive() ?: continue
            when (Pwr1.peekKind(data)) {
                PacketKind.STATUS -> {
                    val status = Pwr1.decodeStatus(data, key, session)
                    if (acceptPeerSequence(status.header.sequence)) {
                        val receivedAt = nowNanos()
                        val sent = synchronized(history) { history[status.header.ack] }
                        flight?.record(FlightEntry(FlightEvent.STATUS, sequence = status.header.sequence.toLong(), ack = status.header.ack.toLong(),
                            accepted = 1, reason = status.reason, statusGapMs = if (lastPeerNs == 0L) 0 else (receivedAt - lastPeerNs) / 1_000_000,
                            ackAgeMs = sent?.let { (receivedAt - it) / 1_000_000 } ?: -1, hostArmed = status.state), hostWasActive && status.state == 0)
                        hostWasActive = status.state == 1
                        if (lastPeerNs != 0L) maxStatusGapMs = maxOf(maxStatusGapMs, (receivedAt - lastPeerNs) / 1_000_000)
                        lastPeerNs = receivedAt; helloAccepted = true; receivedStatuses++; onStatus(status)
                    } else flight?.record(FlightEntry(FlightEvent.REJECT, sequence = status.header.sequence.toLong(), accepted = 0, reason = 1))
                }
                PacketKind.HAPTIC -> {
                    val haptic = Pwr1.decodeHaptic(data, key, session)
                    val sent = synchronized(history) { history[haptic.header.ack] }
                    if (sent != null && nowNanos() - sent in 0..100_000_000 && acceptPeerSequence(haptic.header.sequence)) onHaptic(haptic)
                }
                else -> Unit
            }
        } catch (_: java.io.IOException) {
            if (running.get()) Thread.sleep(10)
        }
        catch (_: Exception) { flight?.record(FlightEntry(FlightEvent.REJECT, accepted = 0, reason = 2)); if (running.get()) Thread.sleep(10) }
    }

    @Synchronized private fun acceptPeerSequence(candidate: UInt): Boolean {
        if (peerSequenceSeen && !Pwr1.newer(candidate, peerSequence)) return false
        peerSequence = candidate; peerSequenceSeen = true; return true
    }

    override fun close() { running.set(false); socket.getAndSet(null)?.close(); executor.shutdownNow() }
}

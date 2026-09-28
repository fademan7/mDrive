package dev.phonewheel

import java.io.File
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong
import java.util.concurrent.atomic.AtomicReference
import java.util.concurrent.locks.ReentrantLock

enum class FlightEvent { SEND, STATUS, REJECT, RELEASE, ARM, SENSOR, SENSOR_REJECT, SENSOR_FAILURE, FOCUS, RESUME, PAUSE, RECONNECT, TRANSPORT_ERROR, SENDER_ERROR }
data class FlightEntry(val event: FlightEvent, val atMs: Long = 0, val sequence: Long = -1, val ack: Long = -1,
    val accepted: Int = -1, val reason: Int = -1, val sendGapMs: Long = -1, val statusGapMs: Long = -1,
    val ackAgeMs: Long = -1, val sensorAgeMs: Long = -1, val flags: Int = -1, val arm: Int = -1,
    val hostArmed: Int = -1, val focused: Int = -1, val nonNeutral: Int = -1)

/** Fixed-schema metadata only. No keys, addresses, audio, text or device IDs. */
class ControllerFlightRecorder(private val directory: File, private val nowMs: () -> Long = { System.nanoTime() / 1_000_000 }) : AutoCloseable {
    private val lock = ReentrantLock()
    private val ring = arrayOfNulls<FlightEntry>(8192)
    private var head = 0; private var count = 0
    private data class Incident(val at: Long, val cause: FlightEntry)
    private val trigger = AtomicReference<Incident?>(null)
    @Volatile private var nextTrigger = 0L
    val dropped = AtomicLong(0)
    val saveFailures = AtomicLong(0)
    @Volatile var lastFile: String? = null; private set
    private val writer = Executors.newSingleThreadScheduledExecutor { task -> Thread(task, "mdrive-flight-writer").apply { priority = Thread.MIN_PRIORITY; isDaemon = true } }
    init { writer.scheduleWithFixedDelay({ flush(false) }, 100, 100, TimeUnit.MILLISECONDS) }
    fun record(entry: FlightEntry, incident: Boolean = false) {
        val now = nowMs()
        if (incident && now >= nextTrigger) trigger.compareAndSet(null, Incident(now, entry.copy(atMs = now)))
        if (!lock.tryLock()) { dropped.incrementAndGet(); return }
        try {
            val at = trigger.get()
            val cutoff = (at?.at ?: now) - 10000
            while (count > 0 && ring[head]!!.atMs < cutoff) { ring[head] = null; head = (head + 1) % ring.size; count-- }
            if (count == ring.size) { head = (head + 1) % ring.size; count--; dropped.incrementAndGet() }
            ring[(head + count++) % ring.size] = entry.copy(atMs = now)
        } finally { lock.unlock() }
    }
    private fun flush(force: Boolean) {
        val incident = trigger.get() ?: return
        val at = incident.at
        if (!force && nowMs() - at < 3000) return
        val captured: List<FlightEntry>
        lock.lock()
        try {
            captured = (0 until count).mapNotNull { ring[(head + it) % ring.size] }.filter { it.atMs in (at - 10000)..(at + 3000) }
            nextTrigger = nowMs(); trigger.set(null)
        } finally { lock.unlock() }
        try {
            directory.mkdirs()
            val file = File(directory, "controller-${System.currentTimeMillis()}-${java.util.UUID.randomUUID()}.csv")
            file.bufferedWriter().use { out ->
                out.write("# schema=1,platform=Android,triggerMs=$at,cause=${incident.cause.event},reason=${incident.cause.reason},dropped=${dropped.get()}\n")
                out.write("timestampMs,event,sequence,ack,accepted,reason,sendGapMs,statusGapMs,ackAgeMs,sensorAgeMs,flags,arm,hostArmed,focused,nonNeutral\n")
                captured.forEach { e -> out.write("${e.atMs},${e.event},${e.sequence},${e.ack},${e.accepted},${e.reason},${e.sendGapMs},${e.statusGapMs},${e.ackAgeMs},${e.sensorAgeMs},${e.flags},${e.arm},${e.hostArmed},${e.focused},${e.nonNeutral}\n") }
            }
            lastFile = file.name
            directory.listFiles { f -> f.name.startsWith("controller-") && f.extension == "csv" }
                ?.sortedByDescending { it.lastModified() }?.drop(8)?.forEach { it.delete() }
        } catch (_: Exception) { saveFailures.incrementAndGet() }
    }
    override fun close() { writer.execute { flush(true) }; writer.shutdown() }
}

/** Ignore individual non-new or impossible samples without refreshing freshness.
 * No reordering buffer and no latency; loss of valid samples still expires at 100ms.
 */
object SensorTimestampGate {
    fun rejection(timestamp: Long, previous: Long, now: Long): Int = when {
        timestamp <= previous -> 1 // duplicate / out of order
        timestamp > now -> 2 // impossible future sample
        now - timestamp > 100_000_000 -> 3 // stale queued sample
        else -> 0
    }
}

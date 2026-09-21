package dev.phonewheel

import java.io.DataInputStream
import java.io.DataOutputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicReference
import org.json.JSONObject

internal data class WendyStatus(val connected: Boolean = false, val telemetry: Boolean = false, val flag: String = "UNKNOWN", val text: String = "", val kind: String = "status", val heard: String = "", val intent: String = "", val source: String = "", val fault: String = "")
internal data class VoiceRequest(val text: String, val confidence: Float)

// One independent low-rate thread. No controller executor, locks, or queues.
internal class WendyClient(private val details: PairingDetails, private val onStatus: (WendyStatus) -> Unit) : AutoCloseable {
    @Volatile private var running = true
    @Volatile private var socket: Socket? = null
    @Volatile var state = "IDLE"
    @Volatile var allowAlerts = false
    private val pending = AtomicReference<VoiceRequest?>(null)
    private var lastStatus: WendyStatus? = null
    private fun emit(value: WendyStatus) { if (running && value != lastStatus) { lastStatus = value; onStatus(value) } }
    private val key = java.util.Base64.getDecoder().decode(details.keyBase64)
    private val worker = Thread(::loop, "wendy-link").apply { isDaemon = true; start() }
    fun query(text: String, confidence: Float) { if (text.length in 1..240) pending.set(VoiceRequest(text, confidence)) }
    private fun loop() {
        android.os.Process.setThreadPriority(android.os.Process.THREAD_PRIORITY_BACKGROUND)
        while (running) {
            try {
                Socket().use { active ->
                    socket = active
                    active.tcpNoDelay = true; active.soTimeout = 2000
                    active.connect(InetSocketAddress(if (details.usb) "127.0.0.1" else details.host, 26762), 1500)
                    val input = DataInputStream(active.getInputStream()); val output = DataOutputStream(active.getOutputStream())
                    val nonce = ByteArray(32); input.readFully(nonce)
                    var sequence = 0L
                    while (running) {
                        val request = pending.getAndSet(null)
                        val json = JSONObject().put("state", state).put("allowAlerts", allowAlerts)
                        if (request != null) json.put("text", request.text).put("confidence", if (request.confidence.isFinite()) request.confidence.toDouble() else -1.0)
                        val packet = WendyWire.encode(json.toString().toByteArray(Charsets.UTF_8), key, nonce, 0, ++sequence)
                        output.writeInt(packet.size); output.write(packet); output.flush()
                        val length = input.readInt(); require(length in 42..WendyWire.MAX_JSON + 40)
                        val data = ByteArray(length); input.readFully(data)
                        val response = JSONObject(String(WendyWire.decode(data, key, nonce, 1, sequence), Charsets.UTF_8))
                        require(response.getBoolean("enabled"))
                        val flag = response.getString("flag")
                        require(flag in setOf("UNKNOWN", "GREEN", "YELLOW", "RED", "BLUE", "SC", "VSC", "CHECKERED"))
                        val message = response.getString("text"); require(message.length <= 400)
                        val heard = response.optString("heard"); val intent = response.optString("intent"); val source = response.optString("source")
                        require(heard.length <= 240 && intent.length <= 64 && source.length <= 64)
                        emit(WendyStatus(true, response.getBoolean("telemetry"), flag, message, response.getString("kind"), heard, intent, source))
                        Thread.sleep(200)
                    }
                }
            } catch (_: InterruptedException) { break }
            catch (ex: Exception) {
                pending.set(null) // Never replay a command after reconnect.
                // Metadata only: never log pairing keys, audio or transcripts.
                android.util.Log.w("WendyLink", "Reconnect: ${ex.javaClass.simpleName}")
                emit(WendyStatus(fault = ex.javaClass.simpleName))
                try { Thread.sleep(3000) } catch (_: InterruptedException) { break }
            } finally { socket = null }
        }
    }
    override fun close() { running = false; pending.set(null); socket?.close(); worker.interrupt() }
}

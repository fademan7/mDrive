package dev.phonewheel

import java.nio.ByteBuffer
import java.nio.ByteOrder
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

enum class PacketKind(val wire: Int) { HELLO(0), CONTROL(1), HAPTIC(2), STATUS(3), CONTROL_LOOK(4) }
data class Header(val kind: PacketKind, val session: ULong, val sequence: UInt, val sentUs: ULong, val ack: UInt)
data class Controls(val steer: Float = 0f, val throttle: Float = 0f, val brake: Float = 0f, val buttons: UShort = 0u, val lookX: Float = 0f, val lookY: Float = 0f) {
    fun validate() {
        require(steer.isFinite() && throttle.isFinite() && brake.isFinite())
        require(steer in -1f..1f && throttle in 0f..1f && brake in 0f..1f)
        require((buttons.toInt() and 0xF3FF.inv()) == 0)
        require(lookX.isFinite() && lookY.isFinite() && lookX in -1f..1f && lookY in -1f..1f)
    }
    fun isNeutral() = kotlin.math.abs(steer) <= 0.05f && throttle == 0f && brake == 0f && buttons == 0.toUShort() && lookX == 0f && lookY == 0f
}
data class ControlFrame(val header: Header, val controls: Controls, val flags: UShort, val epoch: UInt)
data class StatusFrame(val header: Header, val state: Int, val reason: Int)
data class HapticFrame(val header: Header, val event: Int, val level: Int, val leaseMs: Int)

object Pwr1 {
    const val HEADER = 32
    const val TAG = 16
    const val ARM = 1
    const val READY = 0xE
    const val FAST_RECOVERY = 0x10
    const val RECOVERING = 9

    fun newer(candidate: UInt, previous: UInt): Boolean {
        val distance = candidate - previous
        return distance != 0u && distance < 0x80000000u
    }

    fun encodeHello(header: Header, key: ByteArray) = encode(header, byteArrayOf(), key)

    fun encodeControl(frame: ControlFrame, key: ByteArray): ByteArray {
        frame.controls.validate()
        require((frame.flags.toInt() and 0x1F.inv()) == 0)
        val look = frame.header.kind == PacketKind.CONTROL_LOOK
        require(look || (frame.controls.lookX == 0f && frame.controls.lookY == 0f))
        val p = ByteBuffer.allocate(if (look) 28 else 20).order(ByteOrder.LITTLE_ENDIAN)
        p.putFloat(frame.controls.steer).putFloat(frame.controls.throttle).putFloat(frame.controls.brake)
        p.putShort(frame.controls.buttons.toShort()).putShort(frame.flags.toShort()).putInt(frame.epoch.toInt())
        if (look) p.putFloat(frame.controls.lookX).putFloat(frame.controls.lookY)
        return encode(frame.header, p.array(), key)
    }

    fun encodeStatus(frame: StatusFrame, key: ByteArray): ByteArray {
        require(frame.state in 0..1 && frame.reason in 0..9)
        return encode(frame.header, byteArrayOf(frame.state.toByte(), frame.reason.toByte(), 0, 0), key)
    }

    fun encodeHaptic(frame: HapticFrame, key: ByteArray): ByteArray {
        validateHaptic(frame.event, frame.level, frame.leaseMs)
        val p = ByteBuffer.allocate(8).order(ByteOrder.LITTLE_ENDIAN)
        p.put(frame.event.toByte()).put(frame.level.toByte()).putShort(frame.leaseMs.toShort()).putInt(0)
        return encode(frame.header, p.array(), key)
    }

    fun decodeControl(packet: ByteArray, key: ByteArray, session: ULong): ControlFrame {
        val look = packet.size > 5 && packet[5] == 4.toByte()
        val (h, p) = decode(packet, key, session, if (look) PacketKind.CONTROL_LOOK else PacketKind.CONTROL, if (look) 28 else 20)
        val b = ByteBuffer.wrap(p).order(ByteOrder.LITTLE_ENDIAN)
        var c = Controls(b.float, b.float, b.float, b.short.toUShort())
        val flags = b.short.toUShort(); val epoch = b.int.toUInt()
        if (look) c = c.copy(lookX = b.float, lookY = b.float)
        c.validate(); require((flags.toInt() and 0x1F.inv()) == 0)
        return ControlFrame(h, c, flags, epoch)
    }

    fun decodeHello(packet: ByteArray, key: ByteArray, session: ULong): Header =
        decode(packet, key, session, PacketKind.HELLO, 0).first

    fun decodeStatus(packet: ByteArray, key: ByteArray, session: ULong): StatusFrame {
        val (h, p) = decode(packet, key, session, PacketKind.STATUS, 4)
        require(p[2] == 0.toByte() && p[3] == 0.toByte())
        val state = p[0].toUByte().toInt(); val reason = p[1].toUByte().toInt()
        require(state in 0..1 && reason in 0..9)
        return StatusFrame(h, state, reason)
    }

    fun decodeHaptic(packet: ByteArray, key: ByteArray, session: ULong): HapticFrame {
        val (h, p) = decode(packet, key, session, PacketKind.HAPTIC, 8)
        val b = ByteBuffer.wrap(p).order(ByteOrder.LITTLE_ENDIAN)
        val event = b.get().toUByte().toInt(); val level = b.get().toUByte().toInt(); val lease = b.short.toUShort().toInt()
        require(b.int == 0); validateHaptic(event, level, lease)
        return HapticFrame(h, event, level, lease)
    }

    fun peekKind(packet: ByteArray): PacketKind {
        require(packet.size >= HEADER + TAG && packet.copyOfRange(0, 4).contentEquals("PWR1".toByteArray()))
        return PacketKind.entries.first { it.wire == packet[5].toUByte().toInt() }
    }

    private fun encode(header: Header, payload: ByteArray, key: ByteArray): ByteArray {
        require(key.size == 32 && payload.size <= UShort.MAX_VALUE.toInt())
        val body = ByteBuffer.allocate(HEADER + payload.size).order(ByteOrder.LITTLE_ENDIAN)
        body.put("PWR1".toByteArray()).put(1).put(header.kind.wire.toByte()).putShort(payload.size.toShort())
        body.putLong(header.session.toLong()).putInt(header.sequence.toInt()).putLong(header.sentUs.toLong()).putInt(header.ack.toInt()).put(payload)
        val tag = hmac(key, body.array())
        return body.array() + tag.copyOfRange(0, TAG)
    }

    private fun decode(packet: ByteArray, key: ByteArray, session: ULong, kind: PacketKind, payloadSize: Int): Pair<Header, ByteArray> {
        require(key.size == 32 && packet.size == HEADER + payloadSize + TAG)
        val body = packet.copyOfRange(0, packet.size - TAG)
        require(java.security.MessageDigest.isEqual(packet.copyOfRange(packet.size - TAG, packet.size), hmac(key, body).copyOfRange(0, TAG)))
        val b = ByteBuffer.wrap(body).order(ByteOrder.LITTLE_ENDIAN)
        val magic = ByteArray(4); b.get(magic)
        require(magic.contentEquals("PWR1".toByteArray()) && b.get().toInt() == 1 && b.get().toUByte().toInt() == kind.wire)
        require(b.short.toUShort().toInt() == payloadSize)
        val actualSession = b.long.toULong(); require(actualSession == session)
        val h = Header(kind, actualSession, b.int.toUInt(), b.long.toULong(), b.int.toUInt())
        val p = ByteArray(payloadSize); b.get(p)
        return h to p
    }

    private fun hmac(key: ByteArray, data: ByteArray): ByteArray = Mac.getInstance("HmacSHA256").run {
        init(SecretKeySpec(key, "HmacSHA256")); doFinal(data)
    }
    private fun validateHaptic(event: Int, level: Int, lease: Int) {
        require(event in 0..8)
        if (event == 0) require(level == 0 && lease == 0) else require(level in 1..3 && lease in 1..150)
    }
}

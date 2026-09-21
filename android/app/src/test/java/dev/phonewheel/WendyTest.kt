package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class WendyTest {
    private val key = ByteArray(32) { it.toByte() }
    @Test fun independentWireFixtureAndAuthentication() {
        val nonce = ByteArray(32) { 7 }
        val json = "{\"state\":\"IDLE\"}".toByteArray()
        val encoded = WendyWire.encode(json, key, nonce, 0, 1)
        assertEquals("00000000000000017b227374617465223a2249444c45227d7824d12b9ccd917168d40c8ee8da1b39ade0950145ac4cfe64bb6db754ae9ea6", encoded.joinToString("") { "%02x".format(it) })
        assertArrayEquals(json, WendyWire.decode(encoded, key, nonce, 0, 1))
        assertThrows(IllegalArgumentException::class.java) { WendyWire.decode(encoded, key, nonce, 1, 1) }
        assertThrows(IllegalArgumentException::class.java) { WendyWire.decode(encoded, key, nonce, 0, 2) }
        assertThrows(IllegalArgumentException::class.java) { WendyWire.decode(encoded, key, ByteArray(32), 0, 1) }
        encoded[10] = (encoded[10].toInt() xor 1).toByte()
        assertThrows(IllegalArgumentException::class.java) { WendyWire.decode(encoded, key, nonce, 0, 1) }
    }
    @Test fun rightStickRoundTripAndNeutral() {
        val controls = Controls(.5f, .3f, .7f, 0x1000u, -1f, .25f)
        val frame = ControlFrame(Header(PacketKind.CONTROL_LOOK, 1uL, 1u, 1uL, 1u), controls, 0xEu, 1u)
        val encoded = Pwr1.encodeControl(frame, key)
        assertEquals(76, encoded.size); assertEquals(frame, Pwr1.decodeControl(encoded, key, 1uL))
        assertFalse(Controls(lookX = .1f).isNeutral())
        assertThrows(IllegalArgumentException::class.java) { Controls(lookY = Float.NaN).validate() }
        val stick = LookStickPreview(); val brake = Pedal(1000f); val throttle = Pedal(1000f)
        brake.down(1, 500f); throttle.down(2, 100f); stick.down(3); stick.move(3, -.5f, .5f)
        stick.up(3); assertEquals(.5f, brake.value, .0001f); assertEquals(1f, throttle.value, .0001f)
        assertEquals(0f, stick.x, 0f); assertEquals(0f, stick.y, 0f)
    }
}

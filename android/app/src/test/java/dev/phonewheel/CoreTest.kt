package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test
import kotlin.math.cos
import kotlin.math.sin
import java.io.File

class CoreTest {
    @Test fun amplified116RangeIsLinearUntilFullLock() {
        for (direction in listOf(-1, 1)) {
            val estimator = SteeringEstimator(116.0, 0.0, -1.0, 0.0, 1.0)
            estimator.calibrate(Quaternion(1.0, 0.0, 0.0, 0.0))
            for ((index, degrees) in listOf(0.0, 30.0, 60.0, 90.0, 116.0, 180.0).withIndex()) {
                val halfAngle = Math.toRadians(degrees * direction) / 2
                val output = estimator.update(Quaternion(cos(halfAngle), 0.0, 0.0, sin(halfAngle)), (index + 1) * 5_000_000L)
                assertEquals((-direction * (degrees / 116).coerceAtMost(1.0)).toFloat(), output, .0001f)
            }
        }
    }

    private val key = ByteArray(32) { it.toByte() }
    private val session = 0x0102030405060708uL

    @Test fun csharpPythonGoldenControlVector() {
        val header = Header(PacketKind.CONTROL, session, 0x01020304u, 0x0102030405060708uL, 0x0A0B0C0Du)
        val packet = Pwr1.encodeControl(ControlFrame(header, Controls(-0.5f, 1f, 0.5f, 0x1000u), 0xEu, 1u), key)
        assertEquals("505752310101140008070605040302010403020108070605040302010d0c0b0a000000bf0000803f0000003f00100e0001000000878d8614939d0013a0a5574d5f0252a6", packet.toHex())
        assertEquals(-0.5f, Pwr1.decodeControl(packet, key, session).controls.steer)
    }

    @Test fun allBidirectionalCodecsAndInvalidReservedFields() {
        fun h(k: PacketKind) = Header(k, session, 1u, 2u, 3u)
        val hello = h(PacketKind.HELLO)
        assertEquals(hello, Pwr1.decodeHello(Pwr1.encodeHello(hello, key), key, session))
        val status = StatusFrame(h(PacketKind.STATUS), 1, 0)
        assertEquals(status, Pwr1.decodeStatus(Pwr1.encodeStatus(status, key), key, session))
        val haptic = HapticFrame(h(PacketKind.HAPTIC), 1, 3, 100)
        assertEquals(haptic, Pwr1.decodeHaptic(Pwr1.encodeHaptic(haptic, key), key, session))
        val rumble = HapticFrame(h(PacketKind.HAPTIC), 8, 3, 100)
        assertEquals(rumble, Pwr1.decodeHaptic(Pwr1.encodeHaptic(rumble, key), key, session))
        assertTrue(Pwr1.newer(0u, UInt.MAX_VALUE)); assertFalse(Pwr1.newer(9u, 9u)); assertFalse(Pwr1.newer(0x80000000u, 0u))
    }

    @Test fun relativeRotationAndFilter() {
        val e = SteeringEstimator(halfRange = 90.0); e.calibrate(Quaternion(1.0, 0.0, 0.0, 0.0))
        fun z(deg: Double): Quaternion { val h = Math.toRadians(deg) / 2; return Quaternion(cos(h), 0.0, 0.0, sin(h)) }
        assertEquals(1f, e.update(z(-90.0), 1_000_000), 0.0001f)
    }

    @Test fun configurableRangeAndCurveImproveCenterResolution() {
        fun z(deg: Double): Quaternion { val h = Math.toRadians(deg) / 2; return Quaternion(cos(h), 0.0, 0.0, sin(h)) }
        val linear = SteeringEstimator(120.0, 0.0, -1.0, 0.0, 1.0)
        val progressive = SteeringEstimator(120.0, 0.0, -1.0, 0.0, 1.4)
        linear.calibrate(z(0.0)); progressive.calibrate(z(0.0))
        assertEquals(0.25f, linear.update(z(-30.0), 1_000_000), .0001f)
        assertTrue(progressive.update(z(-30.0), 1_000_000) < 0.25f)
        assertEquals(1f, progressive.update(z(-120.0), 2_000_000), .0001f)
    }

    @Test fun pointersStayOwnedAndReleaseIndependently() {
        val left = Pedal(100f); val right = Pedal(100f)
        assertTrue(left.down(8, 100f)); assertTrue(right.down(3, 100f))
        assertEquals(1f, left.move(8, 0f)); assertEquals(0.5f, right.move(3, 50f))
        assertFalse(left.down(3, 0f)); left.up(3); assertEquals(1f, left.value)
        left.up(8); assertEquals(0f, left.value); assertEquals(0.5f, right.value)
    }

    @Test fun fullTurnBoundaryNeverSwapsLeftAndRight() {
        val e = SteeringEstimator(180.0, 0.0, -1.0, 0.0, 1.0)
        e.calibrate(Quaternion(1.0, 0.0, 0.0, 0.0))
        val path = listOf(0.0, 45.0, 90.0, 135.0, 179.0, 180.0, 181.0, 179.0, 90.0, 0.0,
            -45.0, -90.0, -135.0, -179.0, -180.0, -181.0, -179.0, -90.0, 0.0)
        path.forEachIndexed { index, clockwise ->
            val h = Math.toRadians(-clockwise) / 2
            // Android may return either quaternion hemisphere for one pose.
            val hemisphere = if (index % 2 == 0) 1.0 else -1.0
            val q = Quaternion(hemisphere * cos(h), 0.0, 0.0, hemisphere * sin(h))
            val value = e.update(q, (index + 1) * 5_000_000L)
            assertEquals("clockwise=$clockwise", (clockwise / 180.0).coerceIn(-1.0, 1.0).toFloat(), value, .0001f)
            assertEquals(-clockwise, e.angleDegrees, .0001)
        }
    }

    @Test fun missingRotationHistoryRequiresExplicitCalibration() {
        val e = SteeringEstimator(smoothingMs = 0.0)
        val neutral = Quaternion(1.0, 0.0, 0.0, 0.0)
        e.calibrate(neutral); e.update(neutral, 1_000_000L)
        assertThrows(IllegalStateException::class.java) { e.update(neutral, 200_000_000L) }
        assertThrows(IllegalArgumentException::class.java) { e.update(neutral, 205_000_000L) }
        e.calibrate(neutral)
        assertEquals(0f, e.update(neutral, 210_000_000L), .0001f)
    }

    @Test fun pedalMapsComfortableTravelLinearly() {
        val screenHeight = 800f
        val pedal = Pedal(screenHeight)
        pedal.down(9, 800f)
        assertEquals(.25f, pedal.move(9, 560f), .0001f)
        assertEquals(200f, pedal.value * screenHeight, .0001f)
        assertEquals(.5f, pedal.move(9, 400f), .0001f)
        pedal.up(9); assertEquals(0f, pedal.value, .0001f)
    }

    @Test fun pedalUsesTenPercentMarginsAndNoEarlySnap() {
        val pedal = Pedal(1f)
        PedalResponse.configure(pedal, 1000)
        pedal.down(1, 1000f)
        assertEquals(0f, pedal.value, .0001f)
        assertEquals(0f, pedal.move(1, 900f), .0001f)
        assertEquals(.25f, pedal.move(1, 700f), .0001f)
        assertEquals(.875f, pedal.move(1, 200f), .0001f)
        assertEquals(.95f, pedal.move(1, 140f), .0001f)
        assertEquals(1f, pedal.move(1, 100f), .0001f)
        assertEquals(1f, pedal.move(1, 50f), .0001f)
        assertEquals(1f, pedal.move(1, 10f), .0001f)
        assertEquals(.9f, pedal.move(1, 180f), .0001f)
        pedal.up(1)
        // A midpoint touch must not silently use a different gain.
        pedal.down(2, 500f)
        assertEquals(.5f, pedal.value, .0001f)
        assertEquals(.875f, pedal.move(2, 200f), .0001f)
        pedal.up(2); assertEquals(0f, pedal.value, .0001f)
    }

    @Test fun pedalPositionDoesNotDependOnTouchStart() {
        val pedal = Pedal(1f)
        PedalResponse.configure(pedal, 1000)
        pedal.down(1, 900f)
        assertEquals(.375f, pedal.move(1, 600f), .0001f)
        assertEquals(.59375f, pedal.move(1, 425f), .0001f)
        pedal.up(1)
        pedal.down(2, 200f)
        assertEquals(.5f, pedal.move(2, 500f), .0001f)
        pedal.up(2); assertEquals(0f, pedal.value, .0001f)
    }

    @Test fun defaultSteeringIsLinearWithoutExtraDeadzoneOrFilter() {
        val e = SteeringEstimator()
        e.calibrate(Quaternion(1.0, 0.0, 0.0, 0.0))
        listOf(0.1, 30.0, 45.0, 90.0, 135.0, 179.0, 180.0, 90.0, 0.0,
            -45.0, -90.0, -135.0, -180.0, -90.0, 0.0).forEachIndexed { index, angle ->
            val h = Math.toRadians(-angle) / 2
            val value = e.update(Quaternion(cos(h), 0.0, 0.0, sin(h)), (index + 1) * 5_000_000L)
            assertEquals("angle=$angle", (angle / 180.0).toFloat(), value, .0001f)
        }
    }

    @Test fun steeringUsesScreenNormalAtAnArbitraryCenterPose() {
        val center = Quaternion(.7, .2, -.4, .3).normalized()
        val e = SteeringEstimator(smoothingMs = 0.0, deadzone = 0.0, responseCurve = 1.0)
        e.calibrate(center)
        listOf(0.0, 45.0, 90.0, 135.0, 180.0, 135.0, 90.0, 45.0, 0.0, -45.0).forEachIndexed { index, angle ->
            val h = Math.toRadians(-angle) / 2
            val pose = center * Quaternion(cos(h), 0.0, 0.0, sin(h))
            assertEquals((angle / 180).toFloat(), e.update(pose, (index + 1) * 5_000_000L), .0001f)
        }
    }

    @Test fun readsCsharpDatagramAndWritesKotlinDatagram() {
        val directory = System.getProperty("interop.dir")?.let(::File) ?: return
        val fromCsharp = File(directory, "csharp-control.bin").readBytes()
        val decoded = Pwr1.decodeControl(fromCsharp, key, session)
        assertEquals(1f, decoded.controls.throttle)
        assertEquals(0.5f, decoded.controls.brake)
        val header = Header(PacketKind.CONTROL, session, 0x01020304u, 0x0102030405060708uL, 0x0A0B0C0Du)
        directory.mkdirs()
        File(directory, "kotlin-control.bin").writeBytes(
            Pwr1.encodeControl(ControlFrame(header, Controls(-0.5f, 1f, 0.5f, 0x1000u), 0xEu, 1u), key)
        )
    }
}

private fun ByteArray.toHex() = joinToString("") { "%02x".format(it) }

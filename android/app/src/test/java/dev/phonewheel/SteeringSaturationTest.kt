package dev.phonewheel

import kotlin.math.cos
import kotlin.math.sin
import kotlin.math.pow
import org.junit.Assert.*
import org.junit.Test

class SteeringSaturationTest {
    private fun pose(clockwise: Double, hemisphere: Double = 1.0): Quaternion {
        val h = Math.toRadians(-clockwise) / 2
        return Quaternion(hemisphere * cos(h), 0.0, 0.0, hemisphere * sin(h))
    }

    @Test fun requestedMappingSurvivesBothExistingPacketKindsWithHeldPedals() {
        for (direction in listOf(-1, 1)) {
            val e = SteeringEstimator().apply { calibrate(pose(0.0)) }
            listOf(0.0 to 0f, 45.0 to .5f, 90.0 to 1f, 120.0 to 1f, 180.0 to 1f)
                .forEachIndexed { index, (angle, expected) ->
                    val lx = e.update(pose(angle * direction), (index + 1) * 5_000_000L)
                    assertEquals(expected * direction, lx, .0001f)
                    assertEquals(-angle * direction, e.angleDegrees, .0001)
                    for (kind in listOf(PacketKind.CONTROL, PacketKind.CONTROL_LOOK)) {
                        val controls = Controls(lx, 1f, .3f, 0u,
                            if (kind == PacketKind.CONTROL_LOOK) .5f else 0f)
                        val frame = ControlFrame(Header(kind, 7uL, index.toUInt(), 1uL, 2u), controls, 15u, 1u)
                        val key = ByteArray(32) { it.toByte() }
                        val decoded = Pwr1.decodeControl(Pwr1.encodeControl(frame, key), key, 7uL)
                        assertEquals(frame, decoded)
                    }
                }
        }
    }

    @Test fun denseRotationPast180AndReturnNeverClampsTrackingOrFlipsSign() {
        val e = SteeringEstimator().apply { calibrate(pose(0.0)) }
        val path = (0..190).toList() + (189 downTo -190).toList() + (-189..0).toList()
        path.forEachIndexed { index, angle ->
            val lx = e.update(pose(angle.toDouble(), if (index % 2 == 0) 1.0 else -1.0),
                (index + 1) * 5_000_000L)
            assertEquals("phone=$angle", (angle / 90.0).coerceIn(-1.0, 1.0).toFloat(), lx, .0001f)
            assertEquals(-angle.toDouble(), e.angleDegrees, .0001)
        }
    }

    @Test fun jitterAtFullScaleAndLongHoldHaveNoInversionOrDrift() {
        for (direction in listOf(-1, 1)) {
            val e = SteeringEstimator().apply { calibrate(pose(0.0)) }
            val path = listOf(0.0, 45.0, 89.9, 90.1, 90.0, 89.8, 90.2, 120.0, 180.0) +
                List(12_000) { 180.0 + if (it % 2 == 0) .1 else -.1 } + listOf(120.0, 90.0, 45.0, 0.0)
            path.forEachIndexed { index, angle ->
                val lx = e.update(pose(angle * direction), (index + 1) * 5_000_000L)
                assertEquals((direction * angle / 90).coerceIn(-1.0, 1.0).toFloat(), lx, .0001f)
                assertEquals(-angle * direction, e.angleDegrees, .0001)
            }
        }
    }

    @Test fun outputRangeAndCurveDoNotChangeSensorTracking() {
        for (range in listOf(45.0, 90.0, 120.0, 180.0)) for (curve in listOf(.6, 1.0, 1.4, 2.0)) {
            val e = SteeringEstimator(halfRange = range, responseCurve = curve).apply { calibrate(pose(0.0)) }
            val path = listOf(0.0, 22.5, 45.0, 90.0, 120.0, 180.0, 181.0, 120.0, 45.0, 0.0)
            path.forEachIndexed { index, angle ->
                val lx = e.update(pose(angle), (index + 1) * 5_000_000L)
                assertEquals((angle / range).coerceAtMost(1.0).pow(curve).toFloat(), lx, .0001f)
                assertEquals(-angle, e.angleDegrees, .0001)
            }
        }
    }
}

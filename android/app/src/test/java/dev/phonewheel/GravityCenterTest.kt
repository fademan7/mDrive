package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test
import kotlin.math.*

class GravityCenterTest {
    private fun z(degrees: Double): Quaternion {
        val a = Math.toRadians(degrees) / 2
        return Quaternion(cos(a), 0.0, 0.0, sin(a))
    }
    @Test fun allDisplayRotationsAndTiltUseGravityNotCurrentTurn() {
        for (display in listOf(0, 90, 180, 270)) for (tilt in listOf(30.0, 60.0, 90.0, 120.0)) {
            val a = Math.toRadians(tilt) / 2
            val level = Quaternion(cos(a), sin(a), 0.0, 0.0) * z(display.toDouble())
            for (roll in listOf(-140.0, -90.0, -30.0, 0.0, 30.0, 90.0, 140.0)) {
                val pose = level * z(roll)
                val center = GravityCenter.reference(pose, display)
                assertNotNull(center)
                val estimator = SteeringEstimator(); estimator.calibrate(center!!)
                assertEquals((-roll / 180).toFloat(), estimator.update(pose, 1L), .0001f)
                assertEquals(0f, estimator.update(level, 10_000_001L), .0001f)
            }
        }
    }
    @Test fun flatAndInvertedCannotGuessCenter() {
        assertNull(GravityCenter.reference(Quaternion(1.0, 0.0, 0.0, 0.0), 90))
        val level = Quaternion(sqrt(.5), sqrt(.5), 0.0, 0.0)
        assertNull(GravityCenter.reference(level * z(180.0), 0))
    }
}

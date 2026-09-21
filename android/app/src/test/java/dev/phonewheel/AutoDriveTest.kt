package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test
import kotlin.math.cos
import kotlin.math.sin

class AutoDriveTest {
    private val q = Quaternion(1.0, 0.0, 0.0, 0.0)
    private fun tick(a: AutoDrive, t: Long, touch: Boolean = false, connected: Boolean = true,
                     neutral: Boolean = true, active: Boolean = false, fresh: Boolean = true,
                     focus: Boolean = true, pose: Quaternion = q) =
        a.tick(t, pose, fresh, focus, touch, connected, neutral, active)

    @Test fun stableCenterThenNeutralDwellAutomaticallyArmsOnce() {
        val a = AutoDrive(); a.request(true)
        for (t in 0L..450L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertEquals(AutoDriveAction.CENTER, tick(a, 500))
        for (t in 550L..1200L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertEquals(AutoDriveAction.ARM, tick(a, 1250))
        assertEquals(AutoDriveAction.NONE, tick(a, 1300, active = true))
        // Never recenter while driving/turning/holding pedals.
        for (t in 1350L..2500L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t, touch = true, neutral = false, active = true))
    }

    @Test fun movementAndAnyTouchPreventCalibration() {
        val a = AutoDrive(); a.request(true)
        for (t in 0L..600L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t, touch = true))
        for (t in 650L..2000L step 50) {
            val h = Math.toRadians(t / 10.0) / 2
            assertEquals(AutoDriveAction.NONE, tick(a, t, pose = Quaternion(cos(h), 0.0, 0.0, sin(h))))
        }
    }

    @Test fun disconnectedOrHeldPedalCannotArm() {
        val a = AutoDrive(); a.request(false)
        for (t in 0L..1000L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t, connected = false))
        for (t in 1050L..2050L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t, touch = true))
        for (t in 2100L..3100L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t, neutral = false))
        for (t in 3150L..3800L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertEquals(AutoDriveAction.ARM, tick(a, 3850))
    }

    @Test fun explicitStopDoesNotRestartAndUiGapDoesNotCountAsDwell() {
        val a = AutoDrive(); a.request(false)
        tick(a, 0); assertEquals(AutoDriveAction.NONE, tick(a, 1000))
        a.stop()
        for (t in 1050L..3000L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertFalse(a.enabled)
    }

    @Test fun faultsReleaseAndNetworkFaultsPrepareNeutralRecovery() {
        for (fault in listOf("sensor", "connection", "focus", "host")) {
            val a = AutoDrive(); a.request(false)
            for (t in 0L..650L step 50) tick(a, t)
            assertEquals(AutoDriveAction.ARM, tick(a, 700))
            tick(a, 750, active = true)
            assertEquals(AutoDriveAction.RELEASE, tick(a, 800, fresh = fault != "sensor",
                connected = fault != "connection", focus = fault != "focus", active = fault != "host"))
            assertEquals(fault == "connection" || fault == "host", a.enabled)
            assertEquals(AutoDriveAction.NONE, tick(a, 850))
        }
    }

    @Test fun networkRecoveryNeverResumesHeldInputsOrRecenters() {
        val a = AutoDrive(); a.request(false)
        for (t in 0L..650L step 50) tick(a, t)
        assertEquals(AutoDriveAction.ARM, tick(a, 700)); tick(a, 750, active = true)
        assertEquals(AutoDriveAction.RELEASE, tick(a, 800, connected = false))
        for (t in 850L..1850L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t, touch = true, neutral = false))
        for (t in 1900L..2550L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertEquals(AutoDriveAction.ARM, tick(a, 2600))
    }

    @Test fun rejectedRequestLowersArmBeforeRetryingNeutralDwell() {
        val a = AutoDrive(); a.request(false)
        for (t in 0L..650L step 50) tick(a, t)
        assertEquals(AutoDriveAction.ARM, tick(a, 700))
        for (t in 750L..1650L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertEquals(AutoDriveAction.RELEASE, tick(a, 1700))
        assertTrue(a.enabled)
        for (t in 1750L..2400L step 50) assertEquals(AutoDriveAction.NONE, tick(a, t))
        assertEquals(AutoDriveAction.ARM, tick(a, 2450))
    }

    @Test fun activeUiDelayDoesNotInterruptHealthySensorAndTransportWorkers() {
        val a = AutoDrive(); a.request(false)
        for (t in 0L..650L step 50) tick(a, t)
        assertEquals(AutoDriveAction.ARM, tick(a, 700))
        tick(a, 750, active = true)
        assertEquals(AutoDriveAction.NONE, tick(a, 1500, active = true, touch = true, neutral = false))
        assertTrue(a.enabled)
    }
}

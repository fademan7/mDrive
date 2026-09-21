package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class AlwaysListenPolicyTest {
    @Test fun defaultOffAndLifecycleGates() {
        val p = AlwaysListenPolicy()
        assertFalse(p.canStart(true, true, true, "IDLE"))
        p.enabled = true
        assertTrue(p.canStart(true, true, true, "IDLE"))
        assertFalse(p.canStart(false, true, true, "IDLE"))
        assertFalse(p.canStart(true, false, true, "IDLE"))
        assertFalse(p.canStart(true, true, false, "IDLE"))
        for (state in listOf("SPEAKING", "PROCESSING", "LISTENING", "ERROR")) assertFalse(p.canStart(true, true, true, state))
    }
    @Test fun quietDoesNotSpinAndFailuresBackOff() {
        val p = AlwaysListenPolicy(); p.enabled = true
        repeat(50) { assertEquals(1200L, p.retryDelay(true)) }
        assertEquals(3000L, p.retryDelay(false))
        assertEquals(6000L, p.retryDelay(false))
        assertEquals(12000L, p.retryDelay(false))
        assertEquals(24000L, p.retryDelay(false))
        repeat(20) { assertEquals(30000L, p.retryDelay(false)) }
        assertTrue(p.canStart(true, true, true, "IDLE"))
        assertEquals(1200L, p.retryDelay(true))
        assertEquals(3000L, p.retryDelay(false))
        p.reset(); assertTrue(p.canStart(true, true, true, "IDLE"))
        assertNull(p.retryDelay(false, true)); assertTrue(p.suspended)
    }
}

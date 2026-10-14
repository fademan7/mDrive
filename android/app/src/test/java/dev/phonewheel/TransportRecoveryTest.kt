package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class TransportRecoveryTest {
    @Test fun shortJitterDoesNotReplaceSocket() {
        val p=TransportRecovery(0)
        assertFalse(p.due(100_000_000,0))
        assertFalse(p.due(170_000_000,0))
        assertFalse(p.due(2_100_000_000,2_000_000_000))
    }
    @Test fun sustainedSilenceRetriesWithoutManualRestart() {
        val p=TransportRecovery(0)
        assertTrue(p.due(2_000_000_000,0))
        assertFalse(p.due(2_010_000_000,0))
        assertTrue(p.due(4_000_000_000,0))
        assertFalse(p.due(6_000_000_000,5_950_000_000))
    }
    @Test fun resumeRequestsRecoveryOnlyForStaleLink() {
        val p=TransportRecovery(0)
        assertFalse(p.due(1_000_000_000,900_000_000,true))
        assertTrue(p.due(3_000_000_000,900_000_000,true))
        assertFalse(p.due(3_000_000_001,900_000_000,true))
        assertFalse(p.due(2_000_000_000,900_000_000,true))
    }
}

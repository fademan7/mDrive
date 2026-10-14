package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class ControllerPacketSequenceTest {
    @Test fun recreatedClientStartsAheadOfOldSession() {
        var old=1000u
        for(ms in 1008L..4000L step 8)old=ControllerPacketSequence.next(old,ms)
        val fresh=ControllerPacketSequence.next(4100u,4101)
        assertTrue(Pwr1.newer(fresh,old))
    }
    @Test fun wrapAndRepeatedClockRemainMonotonic() {
        assertEquals(1u,ControllerPacketSequence.next(UInt.MAX_VALUE,4294967297))
        assertEquals(101u,ControllerPacketSequence.next(100u,100))
        assertEquals(102u,ControllerPacketSequence.next(101u,99))
    }
}

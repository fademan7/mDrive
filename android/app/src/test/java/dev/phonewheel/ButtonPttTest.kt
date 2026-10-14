package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class ButtonPttTest {
    @Test fun onlyLbEdgesStartAndStopVoice() {
        val ptt=ButtonPtt()
        assertNull(ptt.update(0u))
        assertNull(ptt.update(0x1000u))
        assertEquals(true,ptt.update(0x1100u))
        assertNull(ptt.update(0x8100u))
        assertNull(ptt.update(0x100u))
        assertEquals(false,ptt.update(0x8000u))
        assertNull(ptt.update(0u))
    }
    @Test fun cancelReleasesOnceAndNextPressWorks() {
        val ptt=ButtonPtt()
        assertEquals(true,ptt.update(0x100u))
        assertEquals(false,ptt.update(0u))
        assertNull(ptt.update(0u))
        assertEquals(true,ptt.update(0x100u))
    }
}

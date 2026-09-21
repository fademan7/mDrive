package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class FlagStyleTest {
    @Test fun regularFlagsUseDistinctSolidColors() {
        val flags = listOf("GREEN", "YELLOW", "RED", "BLUE").map(FlagStyle::forFlag)
        assertEquals(4, flags.map { it.color }.distinct().size)
        assertTrue(flags.all { it.label.isEmpty() && !it.checkered })
    }
    @Test fun safetyCarsCheckeredAndUnknownRemainDistinct() {
        assertEquals("SC", FlagStyle.forFlag("SC").label)
        assertEquals("VSC", FlagStyle.forFlag("VSC").label)
        assertEquals(FlagStyle.forFlag("YELLOW").color, FlagStyle.forFlag("SC").color)
        assertTrue(FlagStyle.forFlag("CHECKERED").checkered)
        assertEquals("—", FlagStyle.forFlag("UNKNOWN").label)
        assertEquals(FlagStyle.forFlag("UNKNOWN"), FlagStyle.forFlag("invalid"))
        assertNotEquals(FlagStyle.forFlag("GREEN").color, FlagStyle.forFlag("UNKNOWN").color)
    }
}

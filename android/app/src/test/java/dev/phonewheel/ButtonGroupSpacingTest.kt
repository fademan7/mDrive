package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class ButtonGroupSpacingTest {
    @Test fun narrowPedalsAllowSymmetricOutwardTranslation() {
        assertEquals(-.025f, ButtonGroupSpacing.shift(true, .26f, .455f, .2025f), .00001f)
        assertEquals(.025f, ButtonGroupSpacing.shift(false, .545f, .74f, .2025f), .00001f)
    }

    @Test fun widePedalsKeepAVisibleGapWithoutOverlap() {
        val left = ButtonGroupSpacing.shift(true, .26f, .455f, .25f)
        val right = ButtonGroupSpacing.shift(false, .545f, .74f, .25f)
        assertEquals(-.008f, left, .00001f)
        assertEquals(.008f, right, .00001f)
        assertEquals(.252f, .26f + left, .00001f)
        assertEquals(.748f, .74f + right, .00001f)
        assertTrue(.455f + left < .5f - .043f)
        assertTrue(.545f + right > .5f + .043f)
    }

    @Test fun alreadySpacedOrPedalBlockedGroupsAreNotMoved() {
        assertEquals(0f, ButtonGroupSpacing.shift(true, .235f, .43f, .2025f), .00001f)
        assertEquals(0f, ButtonGroupSpacing.shift(false, .57f, .765f, .2025f), .00001f)
        assertEquals(0f, ButtonGroupSpacing.shift(true, .252f, .447f, .25f), .00001f)
        assertEquals(0f, ButtonGroupSpacing.shift(false, .553f, .748f, .25f), .00001f)
    }
}

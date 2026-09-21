package dev.phonewheel

import org.junit.Assert.*
import org.junit.Test

class LookStickPreviewTest {
    @Test fun ownsOnePointerAndClampsToCircle() {
        val stick = LookStickPreview()
        assertTrue(stick.down(1)); assertFalse(stick.down(2))
        stick.move(2, 1f, 0f); assertEquals(0f, stick.x, 0f)
        stick.move(1, 3f, 4f)
        assertEquals(.6f, stick.x, .0001f); assertEquals(.8f, stick.y, .0001f)
        stick.up(2); assertEquals(1, stick.pointerId)
        stick.up(1); assertNull(stick.pointerId); assertEquals(0f, stick.x, 0f)
    }
    @Test fun releaseCancelAndInvalidCoordinatesAreSafe() {
        val stick = LookStickPreview(); stick.down(4); stick.move(4, -.5f, .25f)
        stick.move(4, Float.NaN, 0f); assertEquals(-.5f, stick.x, 0f)
        stick.cancel(); assertNull(stick.pointerId)
        assertEquals(0f, stick.x, 0f); assertEquals(0f, stick.y, 0f)
    }
}

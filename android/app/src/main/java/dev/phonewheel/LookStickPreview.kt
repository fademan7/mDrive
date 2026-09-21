package dev.phonewheel

import kotlin.math.hypot

/** Pointer-owned virtual right stick. Kept separate from gyro and pedal math. */
internal class LookStickPreview {
    var pointerId: Int? = null; private set
    var x = 0f; private set
    var y = 0f; private set
    fun down(id: Int): Boolean {
        if (pointerId != null) return false
        pointerId = id; return true
    }
    fun move(id: Int, dx: Float, dy: Float) {
        if (id != pointerId || !dx.isFinite() || !dy.isFinite()) return
        val scale = maxOf(1f, hypot(dx, dy))
        x = dx / scale; y = dy / scale
    }
    fun up(id: Int) { if (id == pointerId) cancel() }
    fun cancel() { pointerId = null; x = 0f; y = 0f }
}

package dev.phonewheel

import kotlin.math.*

/** Establish screen-level roll without treating an already turned wheel as zero.
 * Rotation-vector axes stay in the device's natural orientation. Never run this
 * continuously during driving: the established reference must remain unchanged.
 */
object GravityCenter {
    fun reference(pose: Quaternion, displayDegrees: Int): Quaternion? {
        require(displayDegrees in listOf(0, 90, 180, 270))
        val q = pose.normalized()
        val upX = 2 * (q.x * q.z - q.w * q.y)
        val upY = 2 * (q.y * q.z + q.w * q.x)
        // A flat phone has no gravity-defined roll in the screen plane.
        if (hypot(upX, upY) < .25) return null
        val angle = ((90.0 - displayDegrees - Math.toDegrees(atan2(upY, upX)) + 180).mod(360.0) - 180)
        if (abs(angle) >= 170) return null // startup direction is ambiguous near inverted
        val half = Math.toRadians(-angle) / 2
        return (q * Quaternion(cos(half), 0.0, 0.0, sin(half))).normalized()
    }
}

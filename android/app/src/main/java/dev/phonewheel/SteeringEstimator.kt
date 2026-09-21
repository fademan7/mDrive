package dev.phonewheel

import kotlin.math.*

data class Quaternion(val w: Double, val x: Double, val y: Double, val z: Double) {
    fun normalized(): Quaternion {
        val n = hypot(hypot(w, x), hypot(y, z)); require(n.isFinite() && n >= 1e-8)
        return Quaternion(w / n, x / n, y / n, z / n)
    }
    fun inverseUnit() = Quaternion(w, -x, -y, -z)
    operator fun times(b: Quaternion) = Quaternion(
        w*b.w-x*b.x-y*b.y-z*b.z, w*b.x+x*b.w+y*b.z-z*b.y,
        w*b.y-x*b.z+y*b.w+z*b.x, w*b.z+x*b.y-y*b.x+z*b.w)
}

class SteeringEstimator(
    private val halfRange: Double = 180.0,
    private val deadzone: Double = 0.0,
    private val sign: Double = -1.0,
    private val smoothingMs: Double = 0.0,
    private val responseCurve: Double = 1.0
) {
    init {
        require(halfRange in 30.0..180.0)
        require(deadzone >= 0.0 && deadzone < halfRange)
        require(smoothingMs in 0.0..50.0)
        require(responseCurve in 0.5..2.5)
    }
    private var center: Quaternion? = null
    private var filtered = 0.0
    private var lastTimestampNs: Long? = null
    private var previousWrapped = 0.0
    var angleDegrees = 0.0; private set
    @Synchronized fun calibrate(q: Quaternion) {
        center = q.normalized(); filtered = 0.0; lastTimestampNs = null
        previousWrapped = 0.0; angleDegrees = 0.0
    }
    @Synchronized fun clear() {
        center = null; filtered = 0.0; lastTimestampNs = null
        previousWrapped = 0.0; angleDegrees = 0.0
    }
    @Synchronized
    fun update(q: Quaternion, timestampNs: Long): Float {
        val base = requireNotNull(center) { "Center calibration required" }
        val previousTs = lastTimestampNs
        val dtMs = previousTs?.let { (timestampNs - it) / 1_000_000.0 }
        if (dtMs != null && (dtMs <= 0 || dtMs > 100)) {
            clear()
            throw IllegalStateException("Sensor continuity lost; center calibration required")
        }
        val rel = (base.inverseUnit() * q.normalized()).normalized()
        if (hypot(rel.w, rel.z) < 1e-6) {
            clear(); throw IllegalStateException("Undefined twist; center calibration required")
        }
        val wrapped = wrap180(Math.toDegrees(2.0 * atan2(rel.z, rel.w)))
        // Accumulate the shortest change between adjacent samples, not the
        // absolute wrapped angle. +179 -> -179 therefore means +2, not -358.
        val delta = wrap180(wrapped - previousWrapped)
        if (abs(delta) >= 179.0) {
            clear(); throw IllegalStateException("Ambiguous rotation; center calibration required")
        }
        angleDegrees += delta
        previousWrapped = wrapped
        val angle = angleDegrees
        val linear = ((abs(angle) - deadzone).coerceAtLeast(0.0) / (halfRange - deadzone)).coerceAtMost(1.0)
        val magnitude = linear.pow(responseCurve)
        val raw = sign * if (angle < 0) -magnitude else magnitude
        filtered = if (previousTs == null) raw else {
            if (smoothingMs == 0.0) raw else filtered + -expm1(-dtMs!! / smoothingMs) * (raw - filtered)
        }
        lastTimestampNs = timestampNs
        return filtered.toFloat()
    }
    private fun wrap180(degrees: Double) = (degrees + 180.0).mod(360.0) - 180.0
}

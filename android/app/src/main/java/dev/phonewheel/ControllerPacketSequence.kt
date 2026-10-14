package dev.phonewheel

/** Monotonic boot-clock sequence keeps a recreated client ahead of its old
 * packets. Never resets the receiver's replay window or authentication state.
 * A phone reboot still requires a new PC pairing session. */
object ControllerPacketSequence {
    fun next(previous: UInt, elapsedMs: Long): UInt {
        val clock=elapsedMs.toUInt()
        return if(Pwr1.newer(clock,previous)) clock else previous+1u
    }
}

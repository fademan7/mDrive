package dev.phonewheel

/** Observe LB edges without consuming or modifying any gamepad bit. */
internal class ButtonPtt {
    private var held = false
    fun update(buttons: UShort): Boolean? {
        val down = buttons.toInt() and 0x0100 != 0
        if (down == held) return null
        held = down
        return down
    }
}

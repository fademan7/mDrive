package dev.phonewheel

// Normalized screen coordinates. Translate each whole diamond without resizing
// it, keeping a small gap to the user's existing full-height pedal area.
internal object ButtonGroupSpacing {
    val leftIds = setOf("dleft", "dup", "ddown", "dright")
    val rightIds = setOf("x", "y", "a", "b")
    const val PEDAL_GAP = .002f
    const val MAX_SHIFT = .025f

    fun shift(left: Boolean, minX: Float, maxX: Float, pedalWidth: Float): Float {
        val desired = if (left) maxX - .43f else .57f - minX
        val available = if (left) minX - pedalWidth - PEDAL_GAP
            else 1f - pedalWidth - PEDAL_GAP - maxX
        val distance = minOf(desired, available, MAX_SHIFT).coerceAtLeast(0f)
        return if (left) -distance else distance
    }
}

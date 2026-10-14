package dev.phonewheel

/** Local microphone ownership only. Never changes controller/session/ARM state. */
internal class GameRadioGate {
    companion object { const val RELEASE_GRACE_MS = 8000L }
    private var held = false
    private var resumeAt = Long.MIN_VALUE
    fun edge(down: Boolean, now: Long) {
        if (down == held) return
        held = down
        resumeAt = if (down) Long.MAX_VALUE else now + RELEASE_GRACE_MS
    }
    fun blocked(now: Long) = held || now < resumeAt
    fun remaining(now: Long) = if (held) RELEASE_GRACE_MS else if (resumeAt == Long.MIN_VALUE) 0L else (resumeAt - now).coerceAtLeast(0)
}

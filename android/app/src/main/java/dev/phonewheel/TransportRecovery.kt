package dev.phonewheel

/** Socket replacement is for sustained silence, not short jitter. No input or
 * protocol freshness/sequence state is reset by this policy. */
class TransportRecovery(startNs: Long) {
    private var lastAttemptNs = startNs
    @Synchronized fun due(nowNs: Long, lastReplyNs: Long, resumed: Boolean = false): Boolean {
        val silence = nowNs - maxOf(lastReplyNs, lastAttemptNs)
        val limit = if (resumed) 1_000_000_000L else 2_000_000_000L
        if (silence < limit || nowNs - lastAttemptNs < limit) return false
        lastAttemptNs = nowNs
        return true
    }
}

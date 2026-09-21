package dev.phonewheel

// Pure lifecycle/retry policy, separate from Android speech service callbacks.
internal class AlwaysListenPolicy {
    var enabled = false
    var suspended = false; private set
    private var failures = 0
    fun reset() { suspended = false; failures = 0 }
    fun canStart(active: Boolean, connected: Boolean, wendy: Boolean, state: String) =
        enabled && !suspended && active && connected && wendy && state == "IDLE"
    fun retryDelay(quiet: Boolean, fatal: Boolean = false): Long? {
        if (fatal) { suspended = true; return null }
        if (quiet) { failures = 0; return 1200L }
        // Service/network/busy errors can recover without the driver touching
        // settings. Do not silently disable AUTO after three transient errors.
        failures = (failures + 1).coerceAtMost(5)
        return minOf(30000L, 3000L shl (failures - 1))
    }
}

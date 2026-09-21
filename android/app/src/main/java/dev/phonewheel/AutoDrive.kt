package dev.phonewheel

import kotlin.math.abs
import kotlin.math.acos

enum class AutoDriveAction { NONE, CENTER, ARM, RELEASE }

/** UI-thread state machine. Never moves the center while driving. */
class AutoDrive {
    private enum class Stage { STOPPED, CENTER, NEUTRAL, REQUESTED, ACTIVE }
    private var stage = Stage.STOPPED
    private var since: Long? = null
    private var lastTick: Long? = null
    private var anchor: Quaternion? = null
    val enabled get() = stage != Stage.STOPPED

    fun request(recenter: Boolean) {
        stage = if (recenter) Stage.CENTER else Stage.NEUTRAL
        since = null; anchor = null; lastTick = null
    }
    fun stop() { stage = Stage.STOPPED; since = null; anchor = null; lastTick = null }

    fun tick(nowMs: Long, pose: Quaternion?, sensorFresh: Boolean, focused: Boolean,
             touching: Boolean, connected: Boolean, neutral: Boolean, hostActive: Boolean): AutoDriveAction {
        if (!enabled) return AutoDriveAction.NONE
        val gap = lastTick?.let { nowMs - it }
        lastTick = nowMs
        if (gap != null && (gap < 0 || gap > 150)) {
            since = null; anchor = null
            if (stage == Stage.REQUESTED) { stage = Stage.NEUTRAL; return AutoDriveAction.RELEASE }
        }
        if (!sensorFresh || !focused || pose == null) {
            since = null; anchor = null
            if (stage == Stage.ACTIVE || stage == Stage.REQUESTED) { stop(); return AutoDriveAction.RELEASE }
            return AutoDriveAction.NONE
        }
        if (stage == Stage.ACTIVE) {
            if (!connected || !hostActive) { request(false); return AutoDriveAction.RELEASE }
            return AutoDriveAction.NONE
        }
        if (stage == Stage.REQUESTED) {
            if (!connected) { request(false); return AutoDriveAction.RELEASE }
            if (hostActive) { stage = Stage.ACTIVE; return AutoDriveAction.NONE }
            if (nowMs - (since ?: nowMs) >= 1000) {
                // Retry only after another neutral dwell with ARM low.
                stage = Stage.NEUTRAL; since = null
                return AutoDriveAction.RELEASE
            }
            return AutoDriveAction.NONE
        }
        if (touching) { since = null; anchor = null; return AutoDriveAction.NONE }
        if (stage == Stage.CENTER) {
            val q = pose.normalized()
            val a = anchor
            val angle = if (a == null) 180.0 else Math.toDegrees(2 * acos(abs(a.w*q.w + a.x*q.x + a.y*q.y + a.z*q.z).coerceIn(0.0, 1.0)))
            if (a == null || angle > 2.0) { anchor = q; since = nowMs; return AutoDriveAction.NONE }
            if (nowMs - (since ?: nowMs) < 500) return AutoDriveAction.NONE
            stage = Stage.NEUTRAL; since = null; anchor = null
            return AutoDriveAction.CENTER
        }
        if (!connected || !neutral) { since = null; return AutoDriveAction.NONE }
        if (since == null) since = nowMs
        if (nowMs - since!! < 700) return AutoDriveAction.NONE
        stage = Stage.REQUESTED; since = nowMs
        return AutoDriveAction.ARM
    }
}

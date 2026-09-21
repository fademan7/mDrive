package dev.phonewheel

import android.content.Context
import android.os.*

class HapticScheduler(context: Context) {
    private val vibrator = context.getSystemService(VibratorManager::class.java).defaultVibrator
    private val handler = Handler(Looper.getMainLooper())
    private val priority = mapOf(1 to 100, 2 to 90, 3 to 80, 4 to 60, 5 to 30, 6 to 10, 7 to 0)
    private val enabled = setOf(1, 2, 8)
    // Raw game rumble can include engine/shift effects. Keep it opt-in;
    // it is not the unvalidated lock/spin telemetry detector.
    var gameRumbleEnabled = context.getSharedPreferences("phonewheel_settings", Context.MODE_PRIVATE).getBoolean("gameRumble", false)
        set(value) { field = value; if (!value) cancel() }
    private var event = 0
    private var generation = 0

    fun offer(frame: HapticFrame) {
        if (frame.event == 0) { cancel(); return }
        if (frame.event == 8 && !gameRumbleEnabled) return
        if (frame.event !in enabled || (event != 0 && (priority[frame.event] ?: 0) < (priority[event] ?: 0))) return
        val same = frame.event == event
        event = frame.event; val mine = ++generation
        handler.postDelayed({ if (generation == mine) cancel() }, frame.leaseMs.toLong())
        if (!same) play(frame.event, frame.level)
    }

    private fun play(type: Int, level: Int) {
        val amplitude = intArrayOf(64, 128, 220)[level - 1]
        val effect = if (type == 8)
            VibrationEffect.createWaveform(longArrayOf(0, 25, 25), intArrayOf(0, amplitude, 0), 0)
        else if (type == 1)
            VibrationEffect.createWaveform(longArrayOf(0, 20, 60, 20), intArrayOf(0, amplitude, 0, amplitude), -1)
        else VibrationEffect.createOneShot(60, amplitude)
        vibrator.vibrate(effect)
    }
    fun cancel() { generation++; event = 0; vibrator.cancel() }

    fun test(): Boolean {
        cancel()
        if (!vibrator.hasVibrator()) return false
        vibrator.vibrate(VibrationEffect.createOneShot(250, VibrationEffect.DEFAULT_AMPLITUDE))
        return true
    }
}

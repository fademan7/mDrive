package dev.phonewheel

import android.app.Instrumentation
import android.content.Intent
import android.os.Bundle
import android.os.SystemClock
import android.view.MotionEvent

/** Controller-only, null receiver only. Deliberately releases the test pointer
 * after any disarm, waits for ordinary neutral re-arm, then starts a NEW touch. */
object ControllerSoak {
    fun run(test: Instrumentation, args: Bundle) {
        val result = Bundle(); var release: (() -> Unit)? = null
        var diagnostics: (() -> Unit)? = null
        try {
            require(args.getString("nullBackend") == "true")
            val seconds = requireNotNull(args.getString("soakSeconds")).toInt().coerceIn(10, 1800)
            val monitor = test.addMonitor(MainActivity::class.java.name, null, false)
            val opened = test.startActivitySync(Intent(test.targetContext, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) as MainActivity
            Thread.sleep(1800)
            val activity = (monitor.lastActivity as? MainActivity) ?: opened
            fun field(name: String) = MainActivity::class.java.getDeclaredField(name).apply { isAccessible = true }.get(activity)
            diagnostics = {
                test.runOnMainSync {
                    result.putString("status", (field("status") as android.widget.TextView).text.toString())
                    result.putString("preparation", "calibrated=${field("calibrated")}; sensorValid=${field("sensorValid")}; foreground=${field("foreground")}; focus=${activity.hasWindowFocus()}; steer=${field("steer")}; arm=${field("arm")}; hostActive=${field("hostActive")}")
                    val q = field("latestQuaternion") as? Quaternion
                    result.putString("gravity", q?.let { "upX=${2*(it.x*it.z-it.w*it.y)}; upY=${2*(it.y*it.z+it.w*it.x)}; display=${activity.display?.rotation}" } ?: "no pose")
                }
            }
            test.runOnMainSync {
                MainActivity::class.java.getDeclaredMethod("connect", String::class.java, String::class.java, String::class.java, Int::class.javaPrimitiveType)
                    .apply { isAccessible = true }.invoke(activity, args.getString("host"), args.getString("session"), args.getString("key"), 26760)
            }
            fun waitArm(): Boolean {
                val until = SystemClock.elapsedRealtime() + 30000
                while ((field("arm") != true || field("hostActive") != true) && SystemClock.elapsedRealtime() < until) Thread.sleep(50)
                return field("arm") == true && field("hostActive") == true
            }
            check(waitArm()) { "initial arm timeout" }
            val controller = field("client") as UdpControllerClient
            val pedals = field("pedals") as PedalTouchView
            var downAt = SystemClock.uptimeMillis()
            fun touch(action: Int) = test.runOnMainSync {
                if (action == MotionEvent.ACTION_DOWN) downAt = SystemClock.uptimeMillis()
                val event = MotionEvent.obtain(downAt, SystemClock.uptimeMillis(), action, pedals.width * .9f, pedals.height * .5f, 0)
                try { activity.dispatchTouchEvent(event) } finally { event.recycle() }
            }
            release = { touch(MotionEvent.ACTION_UP) }
            touch(MotionEvent.ACTION_DOWN)
            check((field("throttle") as Float) in .49f.. .51f) { "held throttle missing" }
            val began = SystemClock.elapsedRealtime()
            val releases = mutableListOf<String>()
            var previousProgress = -1L
            while (SystemClock.elapsedRealtime() - began < seconds * 1000L) {
                Thread.sleep(25)
                check(field("client") === controller) { "controller object replaced" }
                val elapsed = SystemClock.elapsedRealtime() - began
                if (field("arm") != true) {
                    releases.add("${elapsed}ms: ${field("lastControlRelease")}")
                    release.invoke() // Never re-arm a held pedal.
                    check(waitArm()) { "neutral recovery timeout" }
                    touch(MotionEvent.ACTION_DOWN)
                }
                if (elapsed / 10000 != previousProgress) {
                    previousProgress = elapsed / 10000
                    test.sendStatus(0, Bundle().apply { putString("progress", "${elapsed / 1000}s; disarms=${releases.size}; sendMax=${controller.maxSendGapMs}ms; statusMax=${controller.maxStatusGapMs}ms") })
                }
            }
            release.invoke(); release = null
            result.putString("result", "SOAK COMPLETE (inspect disarms; not an in-game fix claim)")
            result.putInt("disarms", releases.size)
            result.putString("releaseEvents", releases.joinToString("\n"))
            result.putLong("maxSendGapMs", controller.maxSendGapMs)
            result.putLong("maxStatusGapMs", controller.maxStatusGapMs)
            result.putLong("ackCount", controller.receivedStatuses)
            result.putInt("sendFailures", controller.sendFailures)
            test.finish(0, result)
        } catch (e: Throwable) {
            release?.invoke(); diagnostics?.invoke(); result.putString("failure", e.cause?.message ?: e.message); test.finish(1, result)
        }
    }
}

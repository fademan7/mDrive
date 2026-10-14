package dev.phonewheel

import android.app.Instrumentation
import android.content.Intent
import android.os.Bundle
import android.speech.SpeechRecognizer
import android.speech.RecognitionSupport
import android.speech.RecognitionSupportCallback
import android.speech.RecognizerIntent
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

/** Explicit development-only test APK. Requires a PC null-backend receiver.
 * Microphone tests require the explicit microphone=true argument. No game output.
 */
class ControllerIsolationInstrumentation : Instrumentation() {
    private lateinit var args: Bundle
    override fun onCreate(arguments: Bundle?) { super.onCreate(arguments); args = arguments ?: Bundle(); start() }
    override fun onStart() {
        if (args.getString("signingMigration") == "true") { SigningMigrationTest.run(this, args); return }
        if (args.getString("hapticOnly") == "true") {
            val result=Bundle()
            try {
                startActivitySync(Intent(targetContext,MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
                Thread.sleep(1500)
                val scheduler=HapticScheduler(targetContext)
                check(scheduler.gameRumbleEnabled){"Game rumble preference disabled"}
                val manager=targetContext.getSystemService(android.os.VibratorManager::class.java)
                check(manager.defaultVibrator.hasVibrator()){"No phone vibrator"}
                val header=Header(PacketKind.HAPTIC,1uL,1u,0uL,0u)
                for(level in 1..3) {
                    repeat(12) {runOnMainSync{scheduler.offer(HapticFrame(header,8,level,100))};Thread.sleep(50)}
                    Thread.sleep(350)
                }
                runOnMainSync{scheduler.cancel()}
                result.putString("result","PASS default game rumble levels submitted; physical sensation requires user confirmation")
                finish(0,result)
            } catch(e:Throwable) {result.putString("failure",e.message);finish(1,result)}
            return
        }
        if (args.getString("resumeOnly") == "true") { ControllerResumeTest.run(this, args); return }
        if (args.getString("uiOnly") == "true") { ControllerSurfaceTest.run(this); return }
        if (args.getString("soakSeconds") != null) { ControllerSoak.run(this, args); return }
        val result = Bundle()
        var failureDiagnostics: (() -> Unit)? = null
        var releaseTouch: (() -> Unit)? = null
        try {
            require(args.getString("nullBackend") == "true") { "Use only a null-backend test receiver" }
            val monitor = addMonitor(MainActivity::class.java.name, null, false)
            val launched = startActivitySync(Intent(targetContext, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) as MainActivity
            Thread.sleep(1800) // Let the Fold's initial orientation/configuration recreation finish.
            val activity = (monitor.lastActivity as? MainActivity) ?: launched
            check(!activity.isDestroyed) { "Test captured a destroyed Activity" }
            fun call(name: String) = MainActivity::class.java.getDeclaredMethod(name).apply { isAccessible = true }.invoke(activity)
            fun field(name: String): Any? = MainActivity::class.java.getDeclaredField(name).apply { isAccessible = true }.get(activity)
            runOnMainSync {
                MainActivity::class.java.getDeclaredMethod("connect", String::class.java, String::class.java, String::class.java, Int::class.javaPrimitiveType)
                    .apply { isAccessible = true }.invoke(activity, args.getString("host"), args.getString("session"), args.getString("key"), 26760)
            }
            // Cold Android resolver/service startup can exceed the old fixed 2.5s.
            repeat(100) { if (field("client") == null) Thread.sleep(100) }
            Thread.sleep(2500)
            val controller = field("client") as? UdpControllerClient ?: error("Controller not created (destroyed=${activity.isDestroyed}): ${(field("status") as android.widget.TextView).text}")
            failureDiagnostics = {
                result.putLong("maxSendGapMs", controller.maxSendGapMs)
                result.putLong("maxStatusGapMs", controller.maxStatusGapMs)
                result.putInt("sendFailures", controller.sendFailures)
                result.putString("lastControlRelease", field("lastControlRelease").toString())
            }
            check(controller.receivedStatuses > 10) { "No real Wi-Fi ACKs" }
            val before = controller.receivedStatuses
            val epochBefore = field("epoch")
            check(field("arm") == true) { "Controller not armed before isolation test" }
            // Hold a real pedal-view pointer through voice and panel changes.
            // Null receiver only: no gamepad is created or driven by this test.
            val pedals = field("pedals") as PedalTouchView
            val downAt = android.os.SystemClock.uptimeMillis()
            fun touch(action: Int) = runOnMainSync {
                val event = android.view.MotionEvent.obtain(downAt, android.os.SystemClock.uptimeMillis(), action,
                    pedals.width * .9f, pedals.height * .5f, 0)
                try { activity.dispatchTouchEvent(event) } finally { event.recycle() }
            }
            releaseTouch = { touch(android.view.MotionEvent.ACTION_UP) }
            touch(android.view.MotionEvent.ACTION_DOWN)
            check((field("throttle") as Float) in .49f.. .51f) { "Pedal pointer did not reach 50 percent" }
            val originalValues = pedals.onValues
            var interrupted = false
            runOnMainSync { pedals.onValues = { b,t,k -> if(t !in .49f.. .51f) interrupted=true; originalValues?.invoke(b,t,k) } }
            repeat(20) {
                runOnMainSync {
                    val properties = arrayOf(android.view.MotionEvent.PointerProperties().apply { id=0; toolType=1 }, android.view.MotionEvent.PointerProperties().apply { id=1; toolType=1 })
                    val coords = arrayOf(android.view.MotionEvent.PointerCoords().apply { x=pedals.width*.9f; y=pedals.height*.5f; pressure=1f; size=1f }, android.view.MotionEvent.PointerCoords().apply { x=pedals.width*.5f; y=pedals.height*.968f; pressure=1f; size=1f })
                    for (action in listOf(android.view.MotionEvent.ACTION_POINTER_DOWN, android.view.MotionEvent.ACTION_POINTER_UP)) {
                        val event=android.view.MotionEvent.obtain(downAt,android.os.SystemClock.uptimeMillis(),action or (1 shl 8),2,properties,coords,0,0,1f,1f,0,0,android.view.InputDevice.SOURCE_TOUCHSCREEN,0)
                        try { activity.dispatchTouchEvent(event) } finally { event.recycle() }
                    }
                    check((field("buttons") as Number).toInt() == 0) { "Former switch area generated a button" }
                    check(!interrupted && (field("throttle") as Float) in .49f.. .51f && field("client") === controller && field("epoch") == epochBefore && field("arm") == true) { "Empty bottom area interrupted held controls or session" }
                }
                Thread.sleep(50)
            }
            runOnMainSync { pedals.onValues = originalValues }
            result.putInt("emptyBottomHeldPedalTaps",20)
            val voice = field("wendy") as WendyVoice
            runOnMainSync { voice.enable(true) }
            Thread.sleep(1200)
            if (args.getString("microphone") == "true") {
                val linkBefore = WendyVoice::class.java.getDeclaredField("link").apply { isAccessible = true }.get(voice)
                runOnMainSync { voice.setAlwaysListening(true, false) }
                repeat(260) {
                    Thread.sleep(100)
                    check(field("client") === controller && field("arm") == true && field("epoch") == epochBefore) { "Continuous AUTO disturbed controller" }
                }
                check(voice.recognitionStarts >= 2 && voice.recognitionReady >= 2) { "AUTO did not restart after silence: ${voice.health}" }
                runOnMainSync { voice.setAlwaysListening(false) }
                result.putInt("continuousAutoStarts", voice.recognitionStarts)
                repeat(4) {
                    runOnMainSync { call("showWendyOptions"); voice.setAlwaysListening(true, false) }
                    Thread.sleep(3500)
                    runOnMainSync {
                        check(voice.alwaysListening) { "AUTO state missing" }
                        voice.setAlwaysListening(false)
                        check(!voice.alwaysListening) { "PTT mode not restored" }
                        call("closeInlinePanel")
                    }
                    check(field("client") === controller && field("arm") == true && field("epoch") == epochBefore) { "AUTO/PTT disturbed controller" }
                    check(WendyVoice::class.java.getDeclaredField("link").apply { isAccessible = true }.get(voice) === linkBefore) { "Mode switch restarted Wendy TCP" }
                }
                result.putInt("autoStarts", voice.recognitionStarts)
                result.putInt("autoReady", voice.recognitionReady)
                result.putInt("autoErrors", voice.recognitionErrors)
                result.putString("voiceHealth", voice.health)
                check(voice.recognitionStarts >= 4 && voice.recognitionReady >= 1) { "AUTO microphone never ready: ${voice.health}" }
            }
            repeat(12) {
                runOnMainSync { call("showOptions"); call("showWendyOptions"); voice.setAlwaysListening(false) }
                Thread.sleep(150)
                runOnMainSync { call("closeInlinePanel") }
                check(field("client") === controller) { "Settings replaced controller connection" }
                check(field("arm") == true && field("epoch") == epochBefore) { "Settings disarmed or recentered controller" }
            }
            runOnMainSync {
                WendyVoice::class.java.getDeclaredMethod("speak", String::class.java).apply { isAccessible = true }.invoke(voice, "Controller connection test. Wendy is speaking while Wi-Fi stays connected.")
            }
            var spoke = false
            repeat(65) { runOnMainSync { if (voice.voiceState == "SPEAKING") spoke = true }; Thread.sleep(100) }
            check(spoke) { "TTS never entered SPEAKING" }
            check(field("client") === controller && controller.receivedStatuses > before + 200) { "ACK stream stopped during settings or speech" }
            check(controller.sendFailures == 0) { "Controller sender failures: ${controller.sendFailures}" }
            check(controller.maxSendGapMs < 150) { "Send scheduling exceeded safety timeout: ${controller.maxSendGapMs} ms" }
            check(controller.maxStatusGapMs < 150) { "Wi-Fi ACK gap exceeded safety timeout: ${controller.maxStatusGapMs} ms" }
            check(field("arm") == true && field("epoch") == epochBefore) { "Speech disarmed or recentered controller" }
            check((field("throttle") as Float) in .49f.. .51f && pedals.hasActiveTouch) { "Voice/settings cancelled held pedal" }
            releaseTouch.invoke(); releaseTouch = null
            check(field("throttle") == 0f) { "Pedal did not release" }
            result.putBoolean("heldPedalMaintainedAndReleased", true)
            result.putString("result", "PASS Wi-Fi ACKs across settings, TTS and requested AUTO/PTT tests; same controller client")
            result.putLong("ackCount", controller.receivedStatuses - before)
            result.putLong("maxSendGapMs", controller.maxSendGapMs)
            result.putLong("maxStatusGapMs", controller.maxStatusGapMs)
            result.putBoolean("ttsSpeakingObserved", spoke)
            result.putBoolean("armedThroughout", true)
            val supportReady = CountDownLatch(1)
            var probe: SpeechRecognizer? = null
            runOnMainSync {
                result.putBoolean("systemSpeechAvailable", SpeechRecognizer.isRecognitionAvailable(activity))
                result.putBoolean("onDeviceSpeechAvailable", SpeechRecognizer.isOnDeviceRecognitionAvailable(activity))
                voice.enable(false)
                if (SpeechRecognizer.isOnDeviceRecognitionAvailable(activity)) {
                    probe = SpeechRecognizer.createOnDeviceSpeechRecognizer(activity)
                    probe!!.checkRecognitionSupport(Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
                        putExtra(RecognizerIntent.EXTRA_LANGUAGE, "en-US")
                        putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                    }, activity.mainExecutor, object : RecognitionSupportCallback {
                        override fun onSupportResult(support: RecognitionSupport) {
                            result.putBoolean("onDeviceEnglishInstalled", support.installedOnDeviceLanguages.any { it.startsWith("en", true) })
                            supportReady.countDown()
                        }
                        override fun onError(error: Int) { result.putInt("supportCheckError", error); supportReady.countDown() }
                    })
                } else supportReady.countDown()
            }
            if (!supportReady.await(3, TimeUnit.SECONDS)) result.putString("supportCheck", "timeout")
            runOnMainSync { probe?.destroy() }
            finish(0, result)
        } catch (ex: Throwable) { releaseTouch?.invoke(); failureDiagnostics?.invoke(); result.putString("failure", ex.cause?.message ?: ex.message ?: ex.javaClass.simpleName); finish(1, result) }
    }
}

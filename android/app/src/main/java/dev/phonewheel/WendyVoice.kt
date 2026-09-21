package dev.phonewheel

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.speech.RecognitionListener
import android.speech.RecognitionSupport
import android.speech.RecognitionSupportCallback
import android.speech.RecognizerIntent
import android.speech.SpeechRecognizer
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import java.util.Locale

// Android system speech recognition; only text goes to the PC's CPU classifier.
// Hands-free uses the explicitly selected provider. System mode may use the cloud.
internal class WendyVoice(private val activity: Activity, private val update: (String, String, String) -> Unit) : AutoCloseable {
    companion object { const val AUDIO_REQUEST = 81 }
    private val main = Handler(Looper.getMainLooper())
    private var details: PairingDetails? = null
    private var link: WendyClient? = null
    private var recognizer: SpeechRecognizer? = null
    private var tts: TextToSpeech? = null
    private var ttsReady = false
    private var connected = false
    private var active = true
    private var generation = 0
    private var utterance = 0
    private var flag = "UNKNOWN"
    private var state = "IDLE"
    private val auto = AlwaysListenPolicy()
    private var speechStarted = false
    private var recognitionTicket = 0
    var recognitionStarts = 0; private set
    var recognitionReady = 0; private set
    var recognitionErrors = 0; private set
    var linkConnections = 0; private set
    private var retryAt = Long.MAX_VALUE
    private var lastFault = "None"
    var deviceEnglishReady: Boolean? = null; private set
    private var supportChecked = false
    private var supportProbe: SpeechRecognizer? = null
    val alwaysListening get() = auto.enabled
    var onDeviceOnly = false; private set
    val voiceState get() = state
    val health get() = "Engineer link: ${if (connected) "connected" else "waiting"} · mic starts $recognitionStarts / ready $recognitionReady / errors $recognitionErrors\nOn-device English: ${when(deviceEnglishReady) { true -> "installed"; false -> "not installed"; null -> "not confirmed" }}\nLast fault: $lastFault"
    val providerDescription get() = if (onDeviceOnly) "On-device English" else "System English (same as PTT; may use internet)"
    private val listenAgain = Runnable {
        retryAt = Long.MAX_VALUE
        if (auto.canStart(active, connected, enabled, state)) startListening(true)
    }
    // Recover a cancelled/missed restart after asynchronous TTS/provider callbacks.
    // Never override an intentional backoff, fatal error, or active utterance.
    private val supervision = object : Runnable {
        override fun run() {
            if (auto.canStart(active, connected, enabled, state) && retryAt == Long.MAX_VALUE) scheduleListening()
            main.postDelayed(this, 1000)
        }
    }
    init { main.postDelayed(supervision, 1000) }
    var enabled = false; private set
    var lastMessage = "Enable F1 Engineer on the receiver, then enable Wendy here."; private set
    var diagnostics = "Heard: —\nIntent: —\nResponse: —"; private set
    private val timeout = Runnable {
        val quiet = alwaysListening && state == "LISTENING" && !speechStarted
        cancelSpeech()
        recognitionFailure(quiet, false, "Voice request timed out. Please try again.")
    }

    fun setAlwaysListening(value: Boolean, offline: Boolean = false) {
        if (value && offline && deviceEnglishReady == false) {
            show(state, "On-device English is not installed. Select AUTO system, or install the English model in Android speech settings.")
            return
        }
        val providerChanged = onDeviceOnly != offline
        cancelSpeech(); auto.enabled = value; auto.reset()
        if (providerChanged) disposeRecognizer()
        onDeviceOnly = offline
        if (value && !enabled) enable(true)
        if (value && activity.checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED)
            activity.requestPermissions(arrayOf(Manifest.permission.RECORD_AUDIO), AUDIO_REQUEST)
        show("IDLE", if (value) "Hands-free ON: $providerDescription. Headset recommended." else "Push to talk mode.")
        scheduleListening()
    }
    fun checkDeviceSupport() {
        if (supportChecked) return
        supportChecked = true
        if (!SpeechRecognizer.isOnDeviceRecognitionAvailable(activity)) { deviceEnglishReady = false; return }
        try {
            val probe = SpeechRecognizer.createOnDeviceSpeechRecognizer(activity)
            supportProbe = probe
            fun finish(ready: Boolean?) {
                if (supportProbe !== probe) return
                deviceEnglishReady = ready; supportProbe = null; probe.destroy()
                update(state, flag, lastMessage)
            }
            probe.checkRecognitionSupport(Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
                putExtra(RecognizerIntent.EXTRA_LANGUAGE, "en-US")
                putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
            }, activity.mainExecutor, object : RecognitionSupportCallback {
                override fun onSupportResult(support: RecognitionSupport) = finish(support.installedOnDeviceLanguages.any { it.equals("en-US", true) || it.equals("en", true) })
                override fun onError(error: Int) = finish(null)
            })
            main.postDelayed({ finish(null) }, 3000)
        } catch (_: Exception) { runCatching { supportProbe?.destroy() }; supportProbe = null }
    }
    private fun scheduleListening(delay: Long = 1200) {
        main.removeCallbacks(listenAgain)
        retryAt = Long.MAX_VALUE
        if (auto.canStart(active, connected, enabled, state)) {
            retryAt = android.os.SystemClock.elapsedRealtime() + delay
            main.postDelayed(listenAgain, delay)
        }
    }
    private fun recognitionFailure(quiet: Boolean, fatal: Boolean, message: String) {
        main.removeCallbacks(timeout)
        stopRecognition()
        if (!quiet) { lastFault = message; recognitionErrors++ }
        if (!alwaysListening) { show("ERROR", message); return }
        val delay = auto.retryDelay(quiet, fatal)
        show(if (delay == null) "ERROR" else "IDLE", if (quiet) "Hands-free waiting…" else message + if (delay != null) " Retrying in ${delay / 1000}s." else " Tap Retry after correcting this.")
        if (delay != null) scheduleListening(delay)
    }
    private fun stopRecognition() {
        recognitionTicket++; speechStarted = false; link?.allowAlerts = false
        runCatching { recognizer?.cancel() }
    }
    private fun disposeRecognizer() { runCatching { recognizer?.destroy() }; recognizer = null }

    fun connection(value: PairingDetails?) {
        if (details == value) return
        details = value; restartLink()
    }
    fun enable(value: Boolean) { if (enabled == value) return; enabled = value; auto.reset(); restartLink() }
    fun foreground(value: Boolean) {
        if (active == value) return
        active = value
        // A transient focus change pauses the microphone, not the authenticated
        // engineer connection. Controller networking is never owned here.
        if (!value) { cancelSpeech(); link?.state = "ERROR"; link?.allowAlerts = false }
        else if (link == null) restartLink()
        else { link?.state = state; scheduleListening() }
    }
    private fun restartLink() {
        generation++; link?.close(); link = null; connected = false; flag = "UNKNOWN"
        cancelSpeech()
        if (!enabled || !active) {
            disposeRecognizer(); runCatching { tts?.shutdown() }; tts = null; ttsReady = false
            show("IDLE", if (enabled) "Wendy paused." else "Wendy OFF."); return
        }
        val target = details ?: run { show("ERROR", "Connect the controller first."); return }
        val current = generation
        show("IDLE", "Waiting for PC F1 Engineer.")
        link = WendyClient(target) { status -> main.post {
            if (generation != current || !enabled) return@post
            val wasConnected = connected
            connected = status.connected; flag = status.flag
            if (!connected) {
                cancelSpeech(); lastFault = "Engineer TCP: ${status.fault}"
                show("ERROR", "Engineer link reconnecting (${status.fault}). Check Receiver Engineer ON and the current PC address. Controller link is separate.")
            }
            else {
                if (status.heard.isNotBlank()) diagnostics = "Heard: ${status.heard}\nIntent: ${status.intent}\nSource: ${status.source}\nResponse: ${status.text}"
                if (!wasConnected) { linkConnections++; show("IDLE", if (status.telemetry) "Wendy ready." else "Telemetry waiting. Use F1 UDP format 2025."); scheduleListening() }
                else update(state, flag, lastMessage)
                if (active && status.text.isNotBlank() && (if (status.kind == "alert") state == "IDLE" || (alwaysListening && state == "LISTENING" && !speechStarted) else state == "PROCESSING")) {
                    if (state == "LISTENING") { stopRecognition(); show("IDLE") }
                    speak(status.text)
                }
            }
        } }
    }
    private fun show(value: String, message: String = lastMessage) {
        state = value; lastMessage = message; link?.state = if (active) value else "ERROR"
        link?.allowAlerts = alwaysListening && value == "LISTENING" && !speechStarted
        update(state, flag, lastMessage)
        android.util.Log.d("WendyVoice", "state=$value auto=$alwaysListening provider=${if (onDeviceOnly) "device" else "system"}")
    }
    fun press() {
        if (alwaysListening && state == "LISTENING") return
        startListening(false)
    }
    private fun startListening(automatic: Boolean) {
        if (!enabled || !active || !connected) { show("ERROR", "Enable Wendy and PC F1 Engineer, then try again."); return }
        if (state == "LISTENING" || state == "PROCESSING") return
        if (activity.checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            if (automatic) { recognitionFailure(false, true, "Allow microphone access, then enable hands-free again."); return }
            activity.requestPermissions(arrayOf(Manifest.permission.RECORD_AUDIO), AUDIO_REQUEST)
            show("IDLE", "Allow microphone access, then hold PTT again."); return
        }
        if (alwaysListening && onDeviceOnly && !SpeechRecognizer.isOnDeviceRecognitionAvailable(activity)) { recognitionFailure(false, true, "No on-device recognizer. Select Hands-free: system in Wendy settings."); return }
        if (!SpeechRecognizer.isRecognitionAvailable(activity)) { recognitionFailure(false, true, "No Android speech recognition service available."); return }
        cancelSpeech()
        try {
            val ticket = ++recognitionTicket
            fun valid() = ticket == recognitionTicket && active && enabled && connected
            if (recognizer == null) {
                recognizer = if (alwaysListening && onDeviceOnly) SpeechRecognizer.createOnDeviceSpeechRecognizer(activity) else SpeechRecognizer.createSpeechRecognizer(activity)
            }
                recognizer!!.setRecognitionListener(object : RecognitionListener {
                    override fun onReadyForSpeech(params: Bundle?) { if (valid()) { recognitionReady++; show("LISTENING", "Microphone ready · speak English.") } }
                    override fun onBeginningOfSpeech() { if (valid()) { speechStarted = true; link?.allowAlerts = false } }
                    override fun onRmsChanged(rmsdB: Float) {}
                    override fun onBufferReceived(buffer: ByteArray?) {}
                    override fun onEndOfSpeech() { if (valid() && state == "LISTENING") show("PROCESSING") }
                    override fun onError(error: Int) { if (valid() && (state == "LISTENING" || state == "PROCESSING")) {
                        android.util.Log.w("WendyVoice", "Recognition error=$error deviceOnly=$onDeviceOnly")
                        recognitionFailure(error == SpeechRecognizer.ERROR_NO_MATCH || error == SpeechRecognizer.ERROR_SPEECH_TIMEOUT,
                            error == SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS || error == SpeechRecognizer.ERROR_LANGUAGE_NOT_SUPPORTED || error == SpeechRecognizer.ERROR_LANGUAGE_UNAVAILABLE,
                            when (error) {
                                SpeechRecognizer.ERROR_LANGUAGE_UNAVAILABLE -> "English speech model is not installed. Select AUTO system or install English in Android speech settings."
                                SpeechRecognizer.ERROR_LANGUAGE_NOT_SUPPORTED -> "This provider does not support English. Select AUTO system or another Android speech provider."
                                SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS -> "Microphone permission is unavailable. Allow it in Android app settings."
                                else -> "English recognition failed ($error). Provider: $providerDescription."
                            })
                    } }
                    override fun onResults(results: Bundle?) {
                        if (!valid() || (state != "LISTENING" && state != "PROCESSING")) return
                        main.removeCallbacks(timeout)
                        val alternatives = results?.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION).orEmpty()
                        val text = alternatives.firstOrNull()?.trim().orEmpty()
                        if (text.isBlank() || text.length > 240) { recognitionFailure(true, false, "No short English request recognized."); return }
                        auto.reset(); stopRecognition()
                        val confidence = results?.getFloatArray(SpeechRecognizer.CONFIDENCE_SCORES)?.firstOrNull() ?: -1f
                        // If alternatives disagree on numbers, the server must reject mutation.
                        val numbers = alternatives.map { Regex("[0-9]+").findAll(it).map { m -> m.value }.toList() }.distinct()
                        show("PROCESSING", text)
                        diagnostics = "Heard: $text\nIntent: PROCESSING\nResponse: —"
                        link?.query(text, if (numbers.size > 1) -1f else confidence)
                        main.postDelayed(timeout, 20000) // Includes first CPU model load; network polls remain 200 ms.
                    }
                    override fun onPartialResults(partialResults: Bundle?) {}
                    override fun onEvent(eventType: Int, params: Bundle?) {}
                })
            recognitionStarts++
            show("LISTENING", "Listening in English…")
            recognizer!!.startListening(Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
                putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                putExtra(RecognizerIntent.EXTRA_LANGUAGE, "en-US")
                putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, false)
                putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3)
            })
            main.postDelayed(timeout, 10000)
        } catch (_: Exception) { recognitionFailure(false, false, "English recognition service could not start.") }
    }
    fun release() { if (!alwaysListening && state == "LISTENING") { runCatching { recognizer?.stopListening() }.onFailure { show("ERROR", "Speech service stopped unexpectedly.") }; if (state == "LISTENING") show("PROCESSING") } }
    fun cancelSpeech() {
        main.removeCallbacks(timeout); main.removeCallbacks(listenAgain); retryAt = Long.MAX_VALUE
        utterance++; stopRecognition()
        if (state == "SPEAKING" || state == "PROCESSING") runCatching { tts?.stop() }
        if (state != "IDLE") show("IDLE")
    }
    fun microphonePermissionResult(granted: Boolean) {
        auto.reset()
        if (granted) { show("IDLE", "Microphone ready."); scheduleListening() }
        else recognitionFailure(false, true, "Microphone permission denied. Enable it in Android app settings.")
    }
    fun retry() { auto.reset(); cancelSpeech(); disposeRecognizer(); supportChecked = false; checkDeviceSupport(); show("IDLE", "Retrying voice service…"); if (alwaysListening) scheduleListening() else press() }
    private fun speechFailure(text: String, reason: String) {
        main.removeCallbacks(timeout); utterance++; lastFault = reason
        // A missing/broken output voice must not permanently turn off AUTO input.
        show(if (alwaysListening) "IDLE" else "ERROR", "$text ($reason)")
        if (alwaysListening) scheduleListening(3000)
    }
    private fun speak(text: String) {
        try { speakSafely(text) }
        catch (_: Exception) { speechFailure(text, "Speech service unavailable.") }
    }
    private fun speakSafely(text: String) {
        main.removeCallbacks(timeout)
        main.removeCallbacks(listenAgain)
        if (state == "LISTENING" || !active || !enabled) return
        stopRecognition()
        lastMessage = text
        if (tts == null) {
            show("PROCESSING", text)
            main.postDelayed(timeout, 20000)
            val current = generation
            val ticket = utterance
            tts = TextToSpeech(activity) { result -> main.post {
                if (generation != current || !enabled || !active) return@post
                val engine = tts ?: return@post
                ttsReady = runCatching {
                    val voice = engine.voices?.firstOrNull { it.locale.language == "en" && !it.isNetworkConnectionRequired }
                    if (result != TextToSpeech.SUCCESS || voice == null) false
                    else { engine.voice = voice; engine.setSpeechRate(1.05f); true }
                }.getOrDefault(false)
                if (ticket == utterance) {
                    if (ttsReady) speak(text)
                    else speechFailure(text, "Install an English TTS voice in Android settings.")
                }
            } }
            return
        }
        if (!ttsReady) { speechFailure(text, "English TTS unavailable."); return }
        val id = (++utterance).toString()
        tts!!.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
            override fun onStart(utteranceId: String?) { main.post { if (utteranceId == utterance.toString()) show("SPEAKING", text) } }
            override fun onDone(utteranceId: String?) { main.post { if (utteranceId == utterance.toString()) { main.removeCallbacks(timeout); show("IDLE", text); scheduleListening() } } }
            @Deprecated("Legacy TTS callback") override fun onError(utteranceId: String?) { main.post { if (utteranceId == utterance.toString()) speechFailure(text, "TTS failed.") } }
        })
        show("SPEAKING", text)
        main.postDelayed(timeout, 20000)
        if (runCatching { tts!!.speak(text, TextToSpeech.QUEUE_FLUSH, null, id) }.getOrDefault(TextToSpeech.ERROR) == TextToSpeech.ERROR) speechFailure(text, "TTS failed.")
    }
    override fun close() { enabled = false; active = false; restartLink(); runCatching { supportProbe?.destroy() }; supportProbe = null; main.removeCallbacksAndMessages(null) }
}

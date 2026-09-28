package dev.phonewheel

import android.app.Activity
import android.app.AlertDialog
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.HandlerThread
import android.os.Process
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.view.Window
import android.view.WindowInsets
import android.view.WindowManager
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.FrameLayout
import android.widget.ScrollView
import android.widget.TextView
import java.util.Base64
import java.util.concurrent.atomic.AtomicReference
import com.google.zxing.integration.android.IntentIntegrator

class MainActivity : Activity(), SensorEventListener {
    companion object {
        private const val SETTINGS = "phonewheel_settings"
        private const val LOCAL_NETWORK_REQUEST = 37
    }

    private lateinit var pedals: PedalTouchView
    private lateinit var status: TextView
    private lateinit var wendy: WendyVoice
    private lateinit var wendyLabel: TextView
    private lateinit var pttButton: Button
    private lateinit var flagBadge: FlagBadgeView
    private lateinit var editBar: LinearLayout
    private lateinit var rootContent: FrameLayout
    private lateinit var wifiLease: ControllerWifiLease
    private var inlinePanel: View? = null
    private var wendyInfo: TextView? = null
    private lateinit var estimator: SteeringEstimator
    private lateinit var sensorManager: SensorManager
    private lateinit var flight: ControllerFlightRecorder
    private lateinit var sensorThread: HandlerThread
    private var rotationSensor: Sensor? = null
    @Volatile private var latestQuaternion: Quaternion? = null
    @Volatile private var steer = 0f
    @Volatile private var brake = 0f
    @Volatile private var throttle = 0f
    @Volatile private var buttons = 0.toUShort()
    @Volatile private var sensorValid = false
    @Volatile private var foreground = false
    @Volatile private var arm = false
    @Volatile private var lastControlRelease = "Not started"
    @Volatile private var calibrated = false
    @Volatile private var epoch = 1u
    @Volatile private var sensorTimestampNs = 0L
    @Volatile private var client: UdpControllerClient? = null
    @Volatile private var connectionGeneration = 0
    private var connectedDetails: PairingDetails? = null
    private data class ReceivedStatus(val frame: StatusFrame, val receivedMs: Long)
    private val receivedStatus = AtomicReference<ReceivedStatus?>(null)
    private var renderedStatus: ReceivedStatus? = null
    private val lastStatusMs get() = receivedStatus.get()?.receivedMs ?: 0L
    private var hostActive = false
    private val autoDrive = AutoDrive()
    private val connectionHandler = Handler(android.os.Looper.getMainLooper())
    private val automaticPreparation = object : Runnable {
        override fun run() {
            if (::pedals.isInitialized) {
                val now = android.os.SystemClock.elapsedRealtime()
                val latestStatus = receivedStatus.get()
                hostActive = latestStatus?.frame?.state == 1
                if (latestStatus != null && latestStatus !== renderedStatus && now - latestStatus.receivedMs < 200) {
                    renderedStatus = latestStatus
                    val label = "Wi-Fi"
                    status.text = if (hostActive) "$label · Driving active"
                        else if (latestStatus.frame.reason == Pwr1.RECOVERING) "$label · recovering fresh input"
                        else "$label · release controls and center to start"
                }
                // Sensor worker can publish concurrently: read its timestamp
                // before the clock so a fresh sample cannot look future-dated.
                val sensorStamp = sensorTimestampNs
                val fresh = sensorValid && android.os.SystemClock.elapsedRealtimeNanos() - sensorStamp in 0..100_000_000
                when (autoDrive.tick(now, latestQuaternion, fresh, foreground && hasWindowFocus() && !pedals.editMode,
                    pedals.hasActiveTouch, client != null && lastStatusMs > 0 && now - lastStatusMs < 200,
                    calibrated && currentControls().isNeutral(), hostActive,
                    latestStatus?.frame?.reason == Pwr1.RECOVERING)) {
                    AutoDriveAction.CENTER -> latestQuaternion?.let { q ->
                        val rotation = when (display?.rotation) {
                            android.view.Surface.ROTATION_90 -> 90
                            android.view.Surface.ROTATION_180 -> 180
                            android.view.Surface.ROTATION_270 -> 270
                            else -> 0
                        }
                        val center = GravityCenter.reference(q, rotation)
                        if (center == null) {
                            autoDrive.request(true)
                            status.text = "Hold the phone upright to find gravity center"
                        } else {
                            arm = false; estimator.calibrate(center)
                            steer = estimator.update(q, sensorStamp); calibrated = true; epoch++
                            status.text = "Gravity centered · level wheel and release controls to start"; publish()
                        }
                    }
                    AutoDriveAction.ARM -> { arm = true; flight.record(FlightEntry(FlightEvent.ARM, arm = 1)); status.text = "Starting · waiting for receiver"; publish() }
                    AutoDriveAction.RELEASE -> {
                        flight.record(FlightEntry(FlightEvent.RELEASE, reason = latestStatus?.frame?.reason ?: -1,
                            sensorAgeMs = (android.os.SystemClock.elapsedRealtimeNanos() - sensorStamp) / 1_000_000,
                            arm = 0, hostArmed = if (hostActive) 1 else 0, focused = if (hasWindowFocus()) 1 else 0), arm)
                        lastControlRelease = "Auto release: sensorFresh=$fresh, focused=${hasWindowFocus()}, ACK age=${now - lastStatusMs} ms, PC reason=${latestStatus?.frame?.reason}"
                        android.util.Log.w("PhoneWheel", lastControlRelease)
                        arm = false; haptics.cancel(); status.text = "Controls released · let go and center"; publish()
                        if (!autoDrive.enabled && foreground && hasWindowFocus() && !pedals.editMode)
                            autoDrive.request(!calibrated)
                    }
                    AutoDriveAction.NONE -> Unit
                }
            }
            connectionHandler.postDelayed(this, 50)
        }
    }
    private val connectionHealth = object : Runnable {
        override fun run() {
            if (client != null && android.os.SystemClock.elapsedRealtime() - lastStatusMs > 2000) {
                if (arm) disarmOutput("No PC reply · controls released")
                status.text = "No Wi-Fi reply · check PC/network"
            }
            connectionHandler.postDelayed(this, 1000)
        }
    }
    private lateinit var haptics: HapticScheduler
    private var halfRange = 180.0
    private var deadzone = 0.0
    private var smoothingMs = 0.0
    private var responseCurve = 1.0
    private var connectionHost = ""
    private var connectionSession = ""
    private var connectionKey = ""

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        flight = ControllerFlightRecorder(java.io.File(getExternalFilesDir(null) ?: filesDir, "controller-diagnostics")) { android.os.SystemClock.elapsedRealtime() }
        requestWindowFeature(Window.FEATURE_NO_TITLE)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        window.attributes = window.attributes.apply {
            layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_ALWAYS
        }
        loadSettings(); rebuildEstimator()
        sensorManager = getSystemService(Context.SENSOR_SERVICE) as SensorManager
        rotationSensor = sensorManager.getDefaultSensor(Sensor.TYPE_GAME_ROTATION_VECTOR)
            ?: sensorManager.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR)
        haptics = HapticScheduler(this)
        wifiLease = ControllerWifiLease(this)
        buildUi()
        connectionHandler.postDelayed(connectionHealth, 5000)
        connectionHandler.post(automaticPreparation)
        requestLocalNetworkPermission()
    }

    private fun buildUi() {
        val root = FrameLayout(this).apply {
            setBackgroundColor(Color.rgb(12, 16, 20))
            setOnApplyWindowInsetsListener { view, insets ->
                // The pedal surface reaches the physical edges; only the
                // central settings panel contains small targets.
                view.setPadding(0, 0, 0, 0); insets
            }
        }
        rootContent = root
        val panel = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL; gravity = Gravity.CENTER
            setPadding(dp(8), dp(4), dp(8), dp(4)); setBackgroundColor(0xFF202830.toInt())
        }
        status = TextView(this).apply {
            text = "Auto center · release controls and hold still"; setTextColor(Color.WHITE); textSize = 13f; maxLines = 1
        }
        fun action(label: String, work: () -> Unit) = Button(this).apply {
            text = label; textSize = 13f; isAllCaps = false; minWidth = 0; minimumWidth = 0; setPadding(dp(4), 0, dp(4), 0)
            setOnClickListener { work() }
        }
        panel.addView(status, LinearLayout.LayoutParams(-1, dp(24)))
        panel.addView(LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER
            addView(LinearLayout(this@MainActivity).apply {
                orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL or Gravity.START
                pttButton = action("PTT") { }.apply {
                    contentDescription = "Wendy English push to talk. Hold to speak."
                    setOnTouchListener { view, event ->
                        when (event.actionMasked) {
                            android.view.MotionEvent.ACTION_DOWN -> { if (wendy.enabled) wendy.press() else showWendyOptions() }
                            android.view.MotionEvent.ACTION_UP -> { wendy.release(); view.performClick() }
                            android.view.MotionEvent.ACTION_CANCEL -> wendy.cancelSpeech()
                        }; true
                    }
                }
                addView(pttButton, LinearLayout.LayoutParams(dp(52), dp(52)))
                wendyLabel = TextView(this@MainActivity).apply {
                    text = "PTT\nOFF"; textSize = 12f; setTextColor(Color.rgb(115, 222, 206))
                    gravity = Gravity.CENTER_VERTICAL or Gravity.START; maxLines = 2
                    setOnClickListener { showWendyOptions() }
                }
                addView(wendyLabel, LinearLayout.LayoutParams(0, dp(52), 1f).apply { leftMargin = dp(8) })
            }, LinearLayout.LayoutParams(0, dp(52), 1f))
            flagBadge = FlagBadgeView(this@MainActivity)
            addView(flagBadge, LinearLayout.LayoutParams(dp(44), dp(44)).apply { setMargins(dp(8), 0, dp(8), 0) })
            // Equal left/right weights keep the flag at the screen's true center.
            addView(LinearLayout(this@MainActivity).apply {
                orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER_VERTICAL or Gravity.END
                addView(action("Center") { calibrateCenter() }, LinearLayout.LayoutParams(dp(52), dp(52)).apply { rightMargin = dp(6) })
                addView(action("Options") { showOptions() }, LinearLayout.LayoutParams(dp(52), dp(52)))
            }, LinearLayout.LayoutParams(0, dp(52), 1f))
        })

        editBar = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL; gravity = Gravity.CENTER
            setPadding(dp(8), dp(3), dp(8), dp(3)); setBackgroundColor(0xFF3B3421.toInt()); visibility = View.GONE
        }
        val editActions = listOf(
            action("Smaller") { if (!pedals.resizeSelected(.90f)) status.text = "Select a control first" },
            action("Larger") { if (!pedals.resizeSelected(1.10f)) status.text = "Select a control first" },
            action("Reset") { pedals.resetLayout(); status.text = "Default layout restored" },
            action("Done") { toggleEditMode() })
        editActions.forEach { editBar.addView(it, LinearLayout.LayoutParams(0, dp(40), 1f)) }
        panel.addView(editBar)

        pedals = PedalTouchView(this).apply {
            onValues = { b, t, keys -> brake = b; throttle = t; buttons = keys; publish() }
            onSelectionChanged = { label -> if (editMode && label != null) {
                status.text = if (label == "Brake" || label == "Throttle") "$label width · 20–25%" else "$label · drag to move / resize"
            } }
        }
        root.addView(pedals, FrameLayout.LayoutParams(-1, -1))
        root.addView(panel, FrameLayout.LayoutParams(1, -2, Gravity.TOP or Gravity.CENTER_HORIZONTAL))
        root.addOnLayoutChangeListener { _, _, _, _, _, _, _, _, _ ->
            val panelWidth = ((root.width - root.paddingLeft - root.paddingRight) * .48f).toInt()
            if (panel.layoutParams.width != panelWidth) {
                panel.layoutParams = FrameLayout.LayoutParams(panelWidth, -2, Gravity.TOP or Gravity.CENTER_HORIZONTAL)
            }
        }
        wendy = WendyVoice(this) { state, flag, message ->
            pttButton.visibility = if (wendy.alwaysListening) View.GONE else View.VISIBLE
            wendyLabel.text = "${if (wendy.alwaysListening) "AUTO" else "PTT"}\n${if (wendy.enabled) state else "OFF"}"
            flagBadge.setFlag(flag)
            wendyLabel.contentDescription = "Wendy $state. $message"
            updateWendyInfo()
        }
        setContentView(root); root.requestApplyInsets()
        root.post {
            root.windowInsetsController?.apply {
                hide(WindowInsets.Type.systemBars())
                systemBarsBehavior = android.view.WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
            }
        }
    }

    private fun toggleEditMode() {
        val enabling = !pedals.editMode
        if (enabling) releaseInputs("Editing layout · controls released")
        pedals.setEditMode(enabling)
        editBar.visibility = if (enabling) View.VISIBLE else View.GONE
        if (!enabling) { autoDrive.request(!calibrated); status.text = "Layout saved · preparing" }
    }

    private fun showOptions() {
        val fields = inlineFields("mDrive 0.5.5 · Options")
        arrayOf("QR / PC connection", "Steering / pedals", "Edit control layout", "Test phone vibration",
            if (haptics.gameRumbleEnabled) "Game vibration: ON" else "Game vibration: OFF", "Wendy F1 Engineer", "About / Creator", "Controller diagnostics").forEachIndexed { which, label ->
            fields.addView(Button(this).apply { text = label; isAllCaps = false; setOnClickListener {
                closeInlinePanel()
                when (which) {
                    0 -> AlertDialog.Builder(this@MainActivity).setTitle("PC connection")
                        .setItems(arrayOf("Scan QR (recommended)", "Manual entry")) { _, method ->
                            if (method == 0) scanConnection() else showConnectionDialog()
                        }.setNegativeButton("Cancel", null).show()
                    1 -> showSteeringDialog()
                    2 -> { if (!pedals.editMode) toggleEditMode() }
                    3 -> status.text = if (haptics.test()) "Vibration test started" else "No vibrator available"
                    4 -> {
                        haptics.gameRumbleEnabled = !haptics.gameRumbleEnabled
                        getSharedPreferences(SETTINGS, Context.MODE_PRIVATE).edit().putBoolean("gameRumble", haptics.gameRumbleEnabled).apply()
                        status.text = if (haptics.gameRumbleEnabled) "Game vibration ON (not TC/ABS detection)" else "Game vibration OFF"
                    }
                    5 -> showWendyOptions()
                    6 -> showAbout()
                    7 -> showControllerDiagnostics()
                }
            } })
        }
        showInlinePanel(fields)
    }

    private fun showAbout() {
        val fields = inlineFields("mDrive · Created by fademan7 / neojshin")
        fields.addView(TextView(this).apply {
            text = "Wheel controller & Wendy F1 Engineer\n\nhttps://fademan7.github.io/\n\nneojshin@gmail.com\n\nOpen external links only with the game paused."
            textSize = 16f; setTextColor(Color.WHITE); setLinkTextColor(Color.rgb(115, 222, 206))
            android.text.util.Linkify.addLinks(this, android.text.util.Linkify.WEB_URLS or android.text.util.Linkify.EMAIL_ADDRESSES)
            movementMethod = android.text.method.LinkMovementMethod.getInstance()
        })
        showInlinePanel(fields)
    }

    private fun showControllerDiagnostics() {
        val fields = inlineFields("Controller diagnostics · snapshot")
        fields.addView(TextView(this).apply {
            text = "Flight recorder: ${flight.lastFile ?: "armed (RAM only)"}\nDropped records: ${flight.dropped.get()} · Save errors: ${flight.saveFailures.get()}\nFiles: Android/data/dev.phonewheel/files/controller-diagnostics\n10s before / 3s after incident; no audio or pairing data."
            setTextColor(Color.WHITE); textSize = 12f
        })
        val now = android.os.SystemClock.elapsedRealtime()
        val ack = receivedStatus.get()
        fields.addView(TextView(this).apply {
            textSize = 14f; setTextColor(Color.WHITE); setTextIsSelectable(true)
            text = "Controller: ${if (arm) "armed" else "released"}\nForeground: $foreground · focus: ${hasWindowFocus()}\nSensor valid: $sensorValid · age: ${(android.os.SystemClock.elapsedRealtimeNanos() - sensorTimestampNs) / 1_000_000} ms\nPC ACK age: ${if (ack == null) "none" else "${now - ack.receivedMs} ms"}\nPC state/reason: ${ack?.frame?.state} / ${ack?.frame?.reason}\nMax send gap: ${client?.maxSendGapMs ?: 0} ms\nMax ACK gap: ${client?.maxStatusGapMs ?: 0} ms\nSend failures: ${client?.sendFailures ?: 0}\nLast release: $lastControlRelease\n\nPC reason codes: 0 normal, 1 timeout, 2 sensor, 3 inactive, 4 calibration, 5 user/neutral, 6 session, 7 output error, 8 permission.\n\nWendy and controller links are independent. For recovery, release controls and center the phone."
        })
        fields.addView(Button(this).apply { text = "Refresh"; setOnClickListener { showControllerDiagnostics() } })
        showInlinePanel(fields)
    }

    private fun showConnectionDialog() {
        releaseInputs("Connection setup · controls released")
        val fields = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(dp(20), dp(4), dp(20), 0) }
        val host = field("PC IP", connectionHost)
        val session = field("Session hex", connectionSession)
        val key = field("Key Base64", connectionKey).apply { inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD }
        fields.addView(host); fields.addView(session); fields.addView(key)
        AlertDialog.Builder(this).setTitle("PC connection").setView(fields)
            .setPositiveButton("Connect") { _, _ ->
                connectionHost = host.text.toString().trim(); connectionSession = session.text.toString().trim(); connectionKey = key.text.toString().trim()
                connect(connectionHost, connectionSession, connectionKey)
            }.setNegativeButton("Cancel", null).show()
    }

    private fun showWendyOptions() {
        // In-window panel: does not cancel pedal pointers, release inputs, steal
        // activity focus, or restart either network connection.
        val fields = inlineFields("Wendy · English")
        wendy.checkDeviceSupport()
        wendyInfo = TextView(this).apply { setTextColor(Color.WHITE); textSize = 13f }
        fields.addView(wendyInfo)
        fun option(label: String, action: () -> Unit) { fields.addView(Button(this).apply { text = label; isAllCaps = false; setOnClickListener { action(); updateWendyInfo() } }) }
        option(if (wendy.enabled) "Wendy OFF" else "Wendy ON") { wendy.enable(!wendy.enabled); showWendyOptions() }
        option("Push to talk") { wendy.setAlwaysListening(false) }
        option("AUTO: system (recommended)") { wendy.setAlwaysListening(true, false) }
        option("AUTO: on-device (English download required)") { wendy.setAlwaysListening(true, true) }
        option("Retry voice service") { wendy.retry() }
        fields.addView(TextView(this).apply {
            setTextColor(Color.LTGRAY); textSize = 12f
            text = "System mode uses your PTT provider and may send speech to its online service. Selecting it enables repeated recognition while this app is visible. On-device mode needs an installed English model. No audio files are saved by mDrive. Use a headset; battery use may increase. Wendy pauses its microphone while speaking.\n\nController connection stays active while this panel is open. Box box is a reminder, not an in-game pit request."
        })
        showInlinePanel(fields, preserveInfo = true); updateWendyInfo()
    }
    private fun inlineFields(title: String) = LinearLayout(this).apply {
        orientation = LinearLayout.VERTICAL; setPadding(dp(12), dp(8), dp(12), dp(8)); setBackgroundColor(0xFF202830.toInt())
        addView(TextView(this@MainActivity).apply { text = title; setTextColor(Color.WHITE); textSize = 17f })
    }
    private fun showInlinePanel(fields: LinearLayout, preserveInfo: Boolean = false) {
        inlinePanel?.let { rootContent.removeView(it) }; if (!preserveInfo) wendyInfo = null
        fields.addView(Button(this).apply { text = "Close"; setOnClickListener { closeInlinePanel() } })
        val scroll = ScrollView(this).apply { isFillViewport = false; addView(fields) }
        inlinePanel = scroll
        rootContent.addView(scroll, FrameLayout.LayoutParams((rootContent.width * .48f).toInt(), (rootContent.height * .92f).toInt(), Gravity.CENTER))
    }
    private fun closeInlinePanel() { inlinePanel?.let { rootContent.removeView(it) }; inlinePanel = null; wendyInfo = null }
    private fun updateWendyInfo() {
        if (!::wendy.isInitialized) return
        wendyInfo?.text = "${wendy.voiceState} · ${wendy.providerDescription}\n${wendy.lastMessage}\n${wendy.health}\nController send gaps: max ${client?.maxSendGapMs ?: 0} ms / failures ${client?.sendFailures ?: 0}\n\n${wendy.diagnostics}"
    }

    @Suppress("DEPRECATION")
    private fun scanConnection() {
        releaseInputs("QR scan · controls released")
        IntentIntegrator(this).setDesiredBarcodeFormats(listOf("QR_CODE"))
            .setPrompt("Scan the QR in the PC receiver")
            .setBeepEnabled(false).setBarcodeImageEnabled(false).setOrientationLocked(true).initiateScan()
    }

    @Deprecated("Legacy scanner activity result")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        val result = IntentIntegrator.parseActivityResult(requestCode, resultCode, data)
        if (result != null) {
            val contents = result.contents
            if (contents == null) { status.text = "QR scan cancelled · controls released"; return }
            try {
                val details = PairingDetails.parse(contents)
                connectionHost = details.host; connectionSession = details.session.toString(16); connectionKey = details.keyBase64
                connect(details.host, connectionSession, details.keyBase64, details.port)
            } catch (_: Exception) { status.text = "Not a PhoneWheel pairing QR" }
        } else super.onActivityResult(requestCode, resultCode, data)
    }

    private fun showSteeringDialog() {
        releaseInputs("Steering setup · controls released")
        val fields = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(dp(20), dp(4), dp(20), 0) }
        val range = labeledField(fields, "Steering half-range (45–180°, lower = more sensitive)", halfRange.toString())
        val dead = labeledField(fields, "Center deadzone (0–5°)", deadzone.toString())
        val smooth = labeledField(fields, "Sensor smoothing (0–30 ms)", smoothingMs.toString())
        val curve = labeledField(fields, "Response curve (0.6–2.0, 1=linear)", responseCurve.toString())
        fields.addView(TextView(this).apply { text = "Pedals: bottom 10%=0%, top 10%=100%, linear in between. The white line shows the detected touch.\nLinear steering: curve 1 / deadzone 0 / smoothing 0. At 180° half-range: 45°=25%, 90°=50%, 180°=100%." })
        val scroll = ScrollView(this).apply { addView(fields) }
        AlertDialog.Builder(this).setTitle("Steering / pedal settings")
            .setNeutralButton("Steering gain 1.55x") { _, _ -> applySettings("116", "0", "0", "1") }
            .setView(scroll).setPositiveButton("Apply") { _, _ ->
                applySettings(range.text.toString(), dead.text.toString(), smooth.text.toString(), curve.text.toString())
            }.setNegativeButton("Cancel", null).show()
    }

    private fun applySettings(rangeText: String, deadText: String, smoothText: String, curveText: String) {
        val range = rangeText.toDoubleOrNull(); val dead = deadText.toDoubleOrNull(); val smooth = smoothText.toDoubleOrNull()
        val curve = curveText.toDoubleOrNull()
        if (range == null || range !in 45.0..180.0 || dead == null || dead !in 0.0..5.0 || dead >= range ||
            smooth == null || smooth !in 0.0..30.0 || curve == null || curve !in .6..2.0) {
            status.text = "Check the allowed setting ranges"; return
        }
        releaseInputs("Settings applied · centering required")
        halfRange = range; deadzone = dead; smoothingMs = smooth; responseCurve = curve
        getSharedPreferences(SETTINGS, Context.MODE_PRIVATE).edit()
            .putFloat("halfRange180", range.toFloat()).putFloat("deadzone", dead.toFloat()).putFloat("smoothing", smooth.toFloat())
            .putFloat("curve", curve.toFloat()).apply()
        calibrated = false; rebuildEstimator()
        epoch++; autoDrive.request(true); status.text = "Total ${(range * 2).toInt()}° · preparing auto center"
    }

    private fun loadSettings() {
        val p = getSharedPreferences(SETTINGS, Context.MODE_PRIVATE)
        halfRange = p.getFloat("halfRange180", 180f).toDouble(); deadzone = p.getFloat("deadzone", 0f).toDouble()
        smoothingMs = p.getFloat("smoothing", 0f).toDouble(); responseCurve = p.getFloat("curve", 1f).toDouble()
    }

    private fun rebuildEstimator() { estimator = SteeringEstimator(halfRange, deadzone, -1.0, smoothingMs, responseCurve) }

    private fun calibrateCenter() {
        val q = latestQuaternion
        if (q == null || !sensorValid) { status.text = "Wait for the sensor to stabilize, then center again"; return }
        releaseInputs("Centered · release controls to start"); estimator.calibrate(q); calibrated = true; epoch++
        autoDrive.request(false); publish()
    }

    private fun connect(host: String, sessionHex: String, keyBase64: String, port: Int = 26760) {
        releaseInputs("Connecting to PC…")
        try {
            val decoded = Base64.getDecoder().decode(keyBase64); require(decoded.size == 32)
            val session = sessionHex.toULong(16); require(session != 0uL)
            val details = PairingDetails(host, port, session, keyBase64)
            if (details == connectedDetails && client != null) { autoDrive.request(!calibrated); status.text = "Checking existing PC connection…"; return }
            val generation = ++connectionGeneration
            receivedStatus.set(null); renderedStatus = null
            client?.close(); client = null; connectedDetails = null; wendy.connection(null)
            // DNS and socket setup must not run on Android's main thread.
            Thread({
                try {
                    android.util.Log.i("PhoneWheel", "Controller connection worker started")
                    val pending = UdpControllerClient(host, port, session, decoded,
                        { frame ->
                            if (generation == connectionGeneration) {
                                val previous = receivedStatus.getAndSet(ReceivedStatus(frame, android.os.SystemClock.elapsedRealtime()))
                                if (previous == null || previous.frame.state != frame.state || previous.frame.reason != frame.reason)
                                    android.util.Log.i("PhoneWheel", "Host state=${frame.state} reason=${frame.reason}")
                            }
                        },
                        { frame ->
                            val receivedAt = android.os.SystemClock.elapsedRealtime()
                            runOnUiThread {
                                val remaining = frame.leaseMs - (android.os.SystemClock.elapsedRealtime() - receivedAt)
                                if (generation == connectionGeneration && !isDestroyed && foreground) {
                                    if (frame.event == 0) haptics.cancel()
                                    else if (remaining > 0) haptics.offer(frame.copy(leaseMs = remaining.toInt()))
                                }
                            }
                        }, {
                            val lostAt = android.os.SystemClock.elapsedRealtime()
                            runOnUiThread {
                            if (generation == connectionGeneration && !isDestroyed) {
                                // A delayed UI callback must not erase a newer recovered ACK.
                                val observed = receivedStatus.get()
                                if ((observed == null || observed.receivedMs <= lostAt) && receivedStatus.compareAndSet(observed, null))
                                    disarmOutput("Wi-Fi reply lost · checking connection")
                            }
                        } }, flight = flight)
                    android.util.Log.i("PhoneWheel", "Controller transport created")
                    runOnUiThread {
                        if (generation != connectionGeneration || isDestroyed) pending.close()
                        else {
                            client = pending; connectedDetails = details; wendy.connection(details)
                            wifiLease.update(true, foreground)
                            calibrated = false; estimator.clear(); hostActive = false
                            autoDrive.request(true)
                            status.text = "QR accepted · waiting for PC…"; publish()
                        }
                    }
                } catch (e: Exception) { runOnUiThread {
                    android.util.Log.w("PhoneWheel", "Controller setup: ${e.javaClass.simpleName}")
                    if (generation == connectionGeneration && !isDestroyed) {
                        status.text = when (e) {
                            is SecurityException -> "PC connection failed · network permission required"
                            is java.net.UnknownHostException -> "Check the PC address"
                            else -> "Connection setup failed (${e.javaClass.simpleName})"
                        }
                    }
                } }
            }, "phonewheel-connect").start()
        } catch (_: Exception) { status.text = "Check the pairing information format" }
    }

    override fun onResume() {
        super.onResume(); foreground = true
        flight.record(FlightEntry(FlightEvent.RESUME))
        if (::wifiLease.isInitialized) wifiLease.update(connectedDetails != null, true)
        if (::wendy.isInitialized) wendy.foreground(true)
        sensorThread = HandlerThread("phonewheel-sensor", Process.THREAD_PRIORITY_MORE_FAVORABLE).apply { start() }
        rotationSensor?.let { sensorManager.registerListener(this, it, 5_000, 0, Handler(sensorThread.looper)) }
        sensorValid = false; publish()
        autoDrive.request(true)
    }

    override fun onPause() {
        flight.record(FlightEntry(FlightEvent.PAUSE, arm = if (arm) 1 else 0), arm)
        if (::wifiLease.isInitialized) wifiLease.close()
        if (::wendy.isInitialized) wendy.foreground(false)
        calibrated = false; estimator.clear()
        releaseInputs("Controls released", 3); foreground = false; sensorManager.unregisterListener(this)
        if (::sensorThread.isInitialized) sensorThread.quitSafely(); publish(); super.onPause()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (::flight.isInitialized) flight.record(FlightEntry(FlightEvent.FOCUS, focused = if (hasFocus) 1 else 0, arm = if (arm) 1 else 0), !hasFocus && arm)
        android.util.Log.i("PhoneWheel", "Window focus=$hasFocus foreground=$foreground")
        if (!::pedals.isInitialized) return
        if (!hasFocus) { releaseInputs("Paused", 3); if (::wendy.isInitialized) wendy.foreground(false) }
        else {
            window.decorView.windowInsetsController?.apply {
                hide(WindowInsets.Type.systemBars())
                systemBarsBehavior = android.view.WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
            }
            if (foreground && !pedals.editMode) autoDrive.request(!calibrated)
            if (foreground && ::wendy.isInitialized) wendy.foreground(true)
        }
    }

    override fun onDestroy() {
        if (::wifiLease.isInitialized) wifiLease.close()
        if (::wendy.isInitialized) wendy.close()
        connectionGeneration++; connectionHandler.removeCallbacksAndMessages(null)
        releaseInputs("Controls released"); client?.close(); haptics.cancel(); flight.close(); super.onDestroy()
    }

    override fun onSensorChanged(event: SensorEvent) {
        val now = android.os.SystemClock.elapsedRealtimeNanos()
        val rejected = SensorTimestampGate.rejection(event.timestamp, sensorTimestampNs, now)
        if (rejected != 0) {
            flight.record(FlightEntry(FlightEvent.SENSOR_REJECT, accepted = 0, reason = rejected, sensorAgeMs = (now - event.timestamp) / 1_000_000))
            return // Never reset estimator or refresh freshness using a rejected sample.
        }
        val q = FloatArray(4); SensorManager.getQuaternionFromVector(q, event.values)
        latestQuaternion = Quaternion(q[0].toDouble(), q[1].toDouble(), q[2].toDouble(), q[3].toDouble())
        if (calibrated) try { steer = estimator.update(latestQuaternion!!, event.timestamp); sensorValid = true }
        catch (_: Exception) {
            flight.record(FlightEntry(FlightEvent.SENSOR_FAILURE, reason = 1, sensorAgeMs = (now - event.timestamp) / 1_000_000, arm = if (arm) 1 else 0), arm)
            steer = 0f; sensorValid = false; calibrated = false; arm = false; estimator.clear()
            runOnUiThread { disarmOutput("Rotation tracking stopped · center required", 2) }
        }
        else { steer = 0f; sensorValid = event.accuracy != SensorManager.SENSOR_STATUS_UNRELIABLE }
        sensorTimestampNs = event.timestamp // Publish timestamp only after computing this sample.
        flight.record(FlightEntry(FlightEvent.SENSOR, accepted = 1, sensorAgeMs = (now - event.timestamp) / 1_000_000, arm = if (arm) 1 else 0))
        publish()
        if (::pedals.isInitialized) pedals.setSteering(steer, -estimator.angleDegrees)
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) { sensorValid = accuracy != SensorManager.SENSOR_STATUS_UNRELIABLE }

    private fun currentControls() = Controls(steer, throttle, brake, buttons,
        if (::pedals.isInitialized) pedals.lookX else 0f, if (::pedals.isInitialized) pedals.lookY else 0f)
    @Synchronized private fun publish() = client?.update(ControllerSnapshot(currentControls(), sensorValid, foreground, true, arm, epoch, sensorTimestampNs))

    private fun releaseInputs(message: String, reason: Int = 5) {
        if (::flight.isInitialized) flight.record(FlightEntry(FlightEvent.RELEASE, reason = reason, arm = 0), arm)
        lastControlRelease = message
        autoDrive.stop(); hostActive = false
        arm = false; steer = 0f; brake = 0f; throttle = 0f; buttons = 0.toUShort()
        if (::pedals.isInitialized) { pedals.cancelAll(); pedals.setSteering(0f) }
        if (::haptics.isInitialized) haptics.cancel(); if (::status.isInitialized) status.text = message; publish()
    }

    private fun disarmOutput(message: String, reason: Int = 1) {
        flight.record(FlightEntry(FlightEvent.RELEASE, reason = reason, arm = 0), arm)
        lastControlRelease = message
        // Network/sensor safety must stop game output, not invalidate a held
        // pointer once a second. Re-arm only after untouched neutral dwell.
        autoDrive.stop(); hostActive = false; arm = false; steer = 0f
        if (foreground && !pedals.editMode) autoDrive.request(!calibrated)
        android.util.Log.w("PhoneWheel", message)
        if (::haptics.isInitialized) haptics.cancel()
        if (::status.isInitialized) status.text = message
        publish()
    }

    private fun requestLocalNetworkPermission() {
        if (Build.VERSION.SDK_INT >= 37 && checkSelfPermission("android.permission.ACCESS_LOCAL_NETWORK") != PackageManager.PERMISSION_GRANTED)
            requestPermissions(arrayOf("android.permission.ACCESS_LOCAL_NETWORK"), LOCAL_NETWORK_REQUEST)
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == WendyVoice.AUDIO_REQUEST && ::wendy.isInitialized)
            wendy.microphonePermissionResult(grantResults.firstOrNull() == PackageManager.PERMISSION_GRANTED)
        if (requestCode == LOCAL_NETWORK_REQUEST && grantResults.firstOrNull() != PackageManager.PERMISSION_GRANTED)
            releaseInputs("Local network permission required")
    }

    private fun field(hint: String, value: String) = EditText(this).apply { this.hint = hint; setText(value); setSingleLine() }
    private fun labeledField(parent: LinearLayout, label: String, value: String): EditText {
        parent.addView(TextView(this).apply { text = label; setTextColor(Color.DKGRAY) })
        return field(label, value).apply { inputType = InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_FLAG_DECIMAL; parent.addView(this) }
    }
    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
}

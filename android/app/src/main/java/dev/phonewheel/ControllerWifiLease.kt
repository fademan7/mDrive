package dev.phonewheel

import android.content.Context
import android.net.wifi.WifiManager

// A foreground-only OS latency request, not a promise against radio loss.
internal class ControllerWifiLease(context: Context) : AutoCloseable {
    private val manager = context.applicationContext.getSystemService(WifiManager::class.java)
    private var lease: WifiManager.WifiLock? = null
    fun update(wifiController: Boolean, foreground: Boolean) {
        if (!wifiController || !foreground) { close(); return }
        if (lease?.isHeld == true) return
        runCatching {
            lease = manager.createWifiLock(WifiManager.WIFI_MODE_FULL_LOW_LATENCY, "mDrive:controller").apply {
                setReferenceCounted(false); acquire()
            }
        }.onFailure { android.util.Log.w("PhoneWheel", "Wi-Fi latency lock unavailable: ${it.javaClass.simpleName}") }
    }
    override fun close() { runCatching { if (lease?.isHeld == true) lease?.release() }; lease = null }
}

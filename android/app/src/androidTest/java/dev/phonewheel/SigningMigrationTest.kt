package dev.phonewheel

import android.app.Instrumentation
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager
import android.os.Bundle
import java.io.File
import java.security.MessageDigest

/** Read-only migration check. No Activity, radio, microphone or controls started. */
internal object SigningMigrationTest {
    fun run(test: Instrumentation, args: Bundle) {
        val result = Bundle()
        try {
            val context = test.targetContext
            check(context.applicationInfo.flags and ApplicationInfo.FLAG_DEBUGGABLE == 0) { "Release is debuggable" }
            fun hash(bytes: ByteArray) = MessageDigest.getInstance("SHA-256").digest(bytes).joinToString("") { "%02x".format(it) }
            val packageInfo = context.packageManager.getPackageInfo(context.packageName, PackageManager.GET_SIGNING_CERTIFICATES)
            val signatures = checkNotNull(packageInfo.signingInfo).apkContentsSigners
            check(signatures.size == 1 && hash(signatures[0].toByteArray()) == args.getString("certificateSha256")) { "Current signing identity mismatch" }
            for ((name, argument) in listOf("phonewheel_settings" to "settingsSha256", "phonewheel_controls" to "layoutSha256")) {
                val expected = requireNotNull(args.getString(argument)) { "Missing pre-update hash" }
                val file = File(context.applicationInfo.dataDir, "shared_prefs/$name.xml")
                val actual = if (file.exists()) hash(file.readBytes()) else "absent"
                check(actual == expected) { "Saved $name changed during update" }
            }
            result.putString("result", "PASS permanent release signer, non-debuggable APK, original settings and layout preserved")
            test.finish(0, result)
        } catch (e: Throwable) {
            result.putString("failure", e.message)
            test.finish(1, result)
        }
    }
}

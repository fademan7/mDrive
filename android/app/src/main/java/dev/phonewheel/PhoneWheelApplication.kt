package dev.phonewheel

import android.app.Application
import android.app.LocaleManager
import android.os.LocaleList

class PhoneWheelApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        // Before any controller Activity starts, so a locale switch cannot
        // recreate an already connected driving screen. Also covers QR UI.
        val locales = getSystemService(LocaleManager::class.java)
        if (!locales.applicationLocales.toLanguageTags().startsWith("en"))
            locales.applicationLocales = LocaleList.forLanguageTags("en")
    }
}

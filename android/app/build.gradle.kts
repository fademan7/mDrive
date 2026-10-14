plugins {
    id("com.android.application")
}

// Supply secrets only from the build environment, never from tracked files.
val releaseKeystore = providers.environmentVariable("MDRIVE_KEYSTORE")
val releaseStorePassword = providers.environmentVariable("MDRIVE_STORE_PASSWORD")
val releaseKeyAlias = providers.environmentVariable("MDRIVE_KEY_ALIAS")
val releaseKeyPassword = providers.environmentVariable("MDRIVE_KEY_PASSWORD")
val releaseSigningReady = listOf(releaseKeystore, releaseStorePassword, releaseKeyAlias, releaseKeyPassword)
    .all { !it.orNull.isNullOrBlank() }

android {
    namespace = "dev.phonewheel"
    compileSdk = 37
    testBuildType = providers.gradleProperty("instrumentationBuildType").getOrElse("debug")

    defaultConfig {
        applicationId = "dev.phonewheel"
        minSdk = 33
        targetSdk = 37
        versionCode = 28
        versionName = "0.5.17"
        testInstrumentationRunner = "dev.phonewheel.ControllerIsolationInstrumentation"
    }

    signingConfigs {
        if (releaseSigningReady) {
            create("permanentRelease") {
                storeFile = file(releaseKeystore.get())
                storePassword = releaseStorePassword.get()
                keyAlias = releaseKeyAlias.get()
                keyPassword = releaseKeyPassword.get()
            }
        }
    }
    buildTypes {
        release {
            isDebuggable = false
            if (releaseSigningReady) signingConfig = signingConfigs.getByName("permanentRelease")
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    testOptions { unitTests.isReturnDefaultValues = true }
    sourceSets.getByName("main").assets.directories.add(layout.buildDirectory.dir("generated/licenseAssets").get().asFile.path)
}

val prepareLicenseAssets by tasks.registering(Copy::class) {
    from(rootProject.file("../LICENSE"), rootProject.file("../COPYRIGHT"))
    into(layout.buildDirectory.dir("generated/licenseAssets"))
}
tasks.matching { it.name == "preBuild" }.configureEach { dependsOn(prepareLicenseAssets) }

val validateReleaseCredentials by tasks.registering {
    doLast {
        check(releaseSigningReady) {
            "Release signing requires MDRIVE_KEYSTORE, MDRIVE_STORE_PASSWORD, MDRIVE_KEY_ALIAS and MDRIVE_KEY_PASSWORD. No debug-key fallback is allowed."
        }
        check(file(releaseKeystore.get()).isFile) { "Release keystore does not exist." }
    }
}
tasks.matching { it.name == "preReleaseBuild" }.configureEach {
    dependsOn(validateReleaseCredentials)
}

dependencies {
    implementation("com.journeyapps:zxing-android-embedded:4.3.0")
    implementation("androidx.core:core-ktx:1.17.0")
    testImplementation("junit:junit:4.13.2")
}

tasks.withType<org.gradle.api.tasks.testing.Test>().configureEach {
    providers.gradleProperty("interopDir").orNull?.let { systemProperty("interop.dir", file(it).absolutePath) }
}

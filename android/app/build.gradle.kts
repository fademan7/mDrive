plugins {
    id("com.android.application")
}

android {
    namespace = "dev.phonewheel"
    compileSdk = 37

    defaultConfig {
        applicationId = "dev.phonewheel"
        minSdk = 33
        targetSdk = 37
        versionCode = 18
        versionName = "0.5.6"
        testInstrumentationRunner = "dev.phonewheel.ControllerIsolationInstrumentation"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    testOptions { unitTests.isReturnDefaultValues = true }
}

dependencies {
    implementation("com.journeyapps:zxing-android-embedded:4.3.0")
    implementation("androidx.core:core-ktx:1.17.0")
    testImplementation("junit:junit:4.13.2")
}

tasks.withType<org.gradle.api.tasks.testing.Test>().configureEach {
    providers.gradleProperty("interopDir").orNull?.let { systemProperty("interop.dir", file(it).absolutePath) }
}

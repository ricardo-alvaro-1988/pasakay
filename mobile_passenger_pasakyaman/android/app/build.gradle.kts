plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

fun readLocalProperty(name: String): String? {
    val localFile = rootProject.file("local.properties")
    if (!localFile.exists()) return null
    return localFile.readLines()
        .asSequence()
        .map { it.trim() }
        .filter { it.isNotEmpty() && !it.startsWith("#") && it.contains("=") }
        .map {
            val idx = it.indexOf('=')
            it.substring(0, idx).trim() to it.substring(idx + 1).trim()
        }
        .firstOrNull { it.first == name }
        ?.second
}

android {
    namespace = "com.pasakyaman.passenger"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId = "com.pasakyaman.passenger"
        minSdk = maxOf(flutter.minSdkVersion, 21)
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        val mapsKey = (project.findProperty("MAPS_API_KEY") as String?)
            ?: System.getenv("MAPS_API_KEY")
            ?: readLocalProperty("MAPS_API_KEY")
            ?: ""
        manifestPlaceholders["MAPS_API_KEY"] = mapsKey
    }

    buildTypes {
        release {
            signingConfig = signingConfigs.getByName("debug")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}

import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

val signingPropsFile = rootProject.file("signing.properties")
val signingProps = Properties().apply {
    if (signingPropsFile.exists()) {
        signingPropsFile.inputStream().use { load(it) }
    }
}

fun signingValue(envKey: String, propKey: String): String? {
    return System.getenv(envKey)?.takeIf { it.isNotBlank() }
        ?: signingProps.getProperty(propKey)?.takeIf { it.isNotBlank() }
}

val releaseStoreFilePath = signingValue("RYANDPI_KEYSTORE_PATH", "storeFile")
val releaseStorePassword = signingValue("RYANDPI_KEYSTORE_PASSWORD", "storePassword")
val releaseKeyAlias = signingValue("RYANDPI_KEY_ALIAS", "keyAlias")
val releaseKeyPassword = signingValue("RYANDPI_KEY_PASSWORD", "keyPassword")
val hasReleaseSigning = !releaseStoreFilePath.isNullOrBlank() &&
    !releaseStorePassword.isNullOrBlank() &&
    !releaseKeyAlias.isNullOrBlank() &&
    !releaseKeyPassword.isNullOrBlank() &&
    file(releaseStoreFilePath).exists()

android {
    namespace = "com.ryandpi.android"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.ryandpi.android"
        minSdk = 29
        targetSdk = 35
        versionCode = 314
        versionName = "3.1.4"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables {
            useSupportLibrary = true
        }
    }

    signingConfigs {
        if (hasReleaseSigning) {
            create("release") {
                storeFile = file(releaseStoreFilePath!!)
                storePassword = releaseStorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
                enableV1Signing = true
                enableV2Signing = true
                enableV3Signing = true
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            if (hasReleaseSigning) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
        debug {
            isMinifyEnabled = false
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    packaging {
        jniLibs {
            // Required so Android extracts libciadpi.so into nativeLibraryDir where SELinux permits exec()
            useLegacyPackaging = true
        }
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

dependencies {
    // Official prebuilt native TUN <-> SOCKS5 tunnel (all 4 ABIs, 16KB page aligned)
    implementation(files("libs/hev-socks5-tunnel.aar"))

    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.8.7")
    implementation("androidx.activity:activity-compose:1.10.0")

    val composeBom = platform("androidx.compose:compose-bom:2025.01.01")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-graphics")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")

    // HTTP/2 connection pooling for Encrypted DoH DNS, Self-Test, and GitHub Auto-Updater
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")

    testImplementation("junit:junit:4.13.2")
    testImplementation("org.json:json:20240303")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.9.0")
}

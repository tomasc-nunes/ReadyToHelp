# ReadyToHelp Mobile

Native Android client for ReadyToHelp, built with Kotlin and Jetpack Compose. It connects to the ReadyToHelp API for authentication and occurrence-related features and uses Mapbox for maps.

See the [project overview](../README.md) and [API setup guide](../API/README.md). Commands below use PowerShell and run from `MOBILE` unless stated otherwise.

## Prerequisites

Install:

- [Git](https://git-scm.com/downloads).
- [Android Studio](https://developer.android.com/studio), with support for Android Gradle Plugin **8.13.1**.
- A **JDK 17** installation for running Gradle. Configure Android Studio's Gradle JDK and the terminal's `JAVA_HOME` consistently. The app's Java/Kotlin bytecode target of 11 does not mean Gradle should run with Java 11.
- Android SDK Platform **36**, SDK Build-Tools **35.0.0**, and Android SDK Platform-Tools through Android Studio's SDK Manager. See the [AGP 8.13 compatibility requirements](https://developer.android.com/build/releases/agp-8-13-0-release-notes).
- Android Emulator and an appropriate system image, or a physical Android device running **Android 9 / API 28 or newer**. A Google APIs emulator image is suitable for the location dependencies.
- Internet access for the initial Gradle/dependency downloads and Mapbox services.

The repository includes the **Gradle 8.13 wrapper**. Do not install Gradle separately. Kotlin **2.0.21**, Compose, Retrofit, Moshi, OkHttp, and Mapbox dependencies are resolved by Gradle.

## 1. Get the code

From the directory where you want to store the project:

```powershell
git clone https://gitlab.com/grupo_07-lds-2526/lds_25_26.git ReadyToHelp
cd ReadyToHelp/MOBILE
```

For an existing checkout, open a terminal in `MOBILE`.

## 2. Configure Android Studio and the SDK

1. In Android Studio, choose **Open** and select the `MOBILE` directory containing `settings.gradle.kts`.
2. Open **SDK Manager** and install the SDK packages listed above. Accept the SDK licenses when prompted.
3. In **Settings > Build, Execution, Deployment > Build Tools > Gradle**, select JDK 17 as the Gradle JDK.
4. Let Android Studio synchronize the project. The first sync downloads the wrapper distribution and dependencies.

Android Studio normally creates `local.properties` with your SDK location. For command-line setup, create `MOBILE/local.properties` using your actual path, for example:

```properties
sdk.dir=C:/Users/YOUR_USERNAME/AppData/Local/Android/Sdk
```

Keep this machine-specific file out of version control. For terminal builds, set `JAVA_HOME` to your installed JDK 17 directory and add its `bin` directory to `PATH`. Add the SDK's `platform-tools` directory to `PATH` to use `adb`.

Verify:

```powershell
java -version
./gradlew.bat --version
adb version
```

On macOS/Linux, use `./gradlew` instead of `./gradlew.bat` in all commands. If necessary, make it executable with `chmod +x gradlew`.

## 3. Configure the API connection

The base URL is defined in:

```text
app/src/main/java/com/example/readytohelpmobile/network/NetworkClient.kt
```

The current value is `https://readytohelp-api.up.railway.app/api/`. Keep it to use the hosted service, subject to its availability and account access. Requests affect that service's data.

For local development, follow the [API README](../API/README.md) to start PostgreSQL/PostGIS, apply migrations, and launch the API. Confirm [Swagger on the development computer](http://localhost:5134/swagger) works first.

Change `BASE_URL` according to the device:

| Device / connection | Base URL |
| --- | --- |
| Standard Android Emulator, API running locally | `http://10.0.2.2:5134/api/` |
| Standard Android Emulator, API exposed by Docker on port 8080 | `http://10.0.2.2:8080/api/` |
| Physical device using USB and `adb reverse` | `http://127.0.0.1:5134/api/` |
| Physical device on the same network | `http://YOUR_COMPUTER_LAN_IP:5134/api/` |

Preserve the `/api/` suffix and trailing slash required by the Retrofit base URL. Rebuild and relaunch after changing it. There is no environment-variable or build-variant URL configuration in the current project. For Docker, resolve the port-mapping issue described in the API README first.

### Android Emulator

`localhost` inside the emulator refers to the emulator itself. `10.0.2.2` reaches the development computer from the standard Android Emulator.

### Physical device over USB

Enable Developer options and USB debugging, connect the device, accept its debugging authorization prompt, then run:

```powershell
adb devices
adb reverse tcp:5134 tcp:5134
```

Set `BASE_URL` to `http://127.0.0.1:5134/api/`. Repeat the reverse command if the device reconnects. To remove the forwarding later:

```powershell
adb reverse --remove tcp:5134
```

### Physical device over Wi-Fi

Use the computer's LAN IP and ensure the device can reach the API through the local firewall. The API currently binds to `0.0.0.0:5134`.

Local HTTP exceptions are configured in `app/src/main/res/xml/network_security_config.xml` for `localhost`, `127.0.0.1`, and `10.0.2.2`. For a LAN IP, add that exact host to the existing `domain-config` for development:

```xml
<domain includeSubdomains="false">192.168.1.100</domain>
```

Replace the example with your computer's IP. The manifest references this network-security configuration; its `usesCleartextTraffic` flag alone is not a substitute for the host exception. Use HTTPS for deployed services.

## 4. Configure Mapbox

The public map token is stored in:

```text
app/src/main/res/values/mapbox.xml
```

For your own setup, replace the `mapbox_access_token` value with a public token from your [Mapbox account](https://account.mapbox.com/):

```xml
<resources>
    <string name="mapbox_access_token" translatable="false">YOUR_PUBLIC_MAPBOX_TOKEN</string>
</resources>
```

The app currently uses Mapbox Maps/Compose **11.16.4**. Its Maven repository is already listed in `settings.gradle.kts`. The build does not currently read a `MAPBOX_DOWNLOADS_TOKEN` property. Do not place secret download tokens in Android resources.

Map tiles require internet access. Allow location access when requested to use location features, and set a simulated location in the emulator's extended controls when testing movement or nearby occurrences.

## 5. Build and run

### From Android Studio

1. Open **Device Manager**, create an emulator with API 28 or newer, and start it; alternatively connect a USB-debugging device.
2. Select the `app` run configuration and your device.
3. Click **Run**. Android Studio builds, installs, and launches the debug app.

### From the terminal

Build the debug APK:

```powershell
./gradlew.bat :app:assembleDebug
```

The APK is generated at:

```text
app/build/outputs/apk/debug/app-debug.apk
```

With an emulator running or a device connected:

```powershell
adb devices
./gradlew.bat :app:installDebug
adb shell am start -n com.example.readytohelpmobile/.MainActivity
```

Alternatively, install the generated APK directly:

```powershell
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

If multiple devices are connected, use Android Studio's device selector or `adb -s DEVICE_SERIAL` for adb commands. Use an account registered on the selected backend; this README does not provide seeded credentials.

To stop the running app:

```powershell
adb shell am force-stop com.example.readytohelpmobile
```

### Release build

```powershell
./gradlew.bat :app:assembleRelease
```

Release artifacts are written under `app/build/outputs/apk/release/`. No release signing configuration is defined in `app/build.gradle.kts`. For distribution, configure signing or use Android Studio's **Generate Signed App Bundle / APK** workflow with your own keystore.

## 6. Tests and code quality

### Local JVM tests

```powershell
./gradlew.bat :app:testDebugUnitTest
```

These do not need a device. The current checkout's system tests are under `app/src/androidTest`; the JVM task may report no test sources. When tests exist, reports are generated under `app/build/reports/tests/`.

### Android instrumented tests

Start an emulator or connect a device, then run:

```powershell
./gradlew.bat :app:connectedDebugAndroidTest
```

The existing system tests cover registration, login, and maps. Inspect their backend/mocking setup before running them against shared data. Reports are generated under `app/build/reports/androidTests/connected/`.

### Coverage

The repository defines this custom JaCoCo task (its name is intentionally kept as defined in Gradle):

```powershell
./gradlew.bat :app:gerarRelatorioCoverage
```

It depends on `connectedDebugAndroidTest`, so it also requires a running emulator or device. HTML output is under `app/build/reports/jacoco/gerarRelatorioCoverage/html/`, with XML output in the same report directory.

### Android lint

```powershell
./gradlew.bat :app:lintDebug
```

Reports are generated under `app/build/reports/`.

## Project structure

```text
app/src/main/java/com/example/readytohelpmobile/
  MainActivity.kt       Application entry activity
  model/                Data models
  network/              Retrofit client and authentication interceptor
  services/             API service interfaces
  ui/                   Compose screens and UI resources
  utils/                Shared utilities and token storage
  viewmodel/            UI state and application logic
app/src/main/res/       Android resources, Mapbox token, network policy
app/src/androidTest/   Device/emulator system tests
app/build.gradle.kts   Android configuration, dependencies, coverage task
gradle/libs.versions.toml  Shared dependency and plugin versions
gradle/wrapper/        Pinned Gradle distribution and wrapper
settings.gradle.kts    Modules and dependency repositories
```

## Troubleshooting

- **Gradle requires Java 17:** check both `java -version` and `./gradlew.bat --version`, and align `JAVA_HOME` with Android Studio's Gradle JDK.
- **SDK location not found:** open the project in Android Studio or set the correct `sdk.dir` in `local.properties`.
- **Missing SDK packages or licenses:** install API 36 and the required Build-Tools in SDK Manager and accept the licenses. The emulator's Android version can differ from the compile SDK.
- **Dependencies cannot download:** check connectivity, proxy settings, and Gradle offline mode, then synchronize again.
- **Device missing or unauthorized:** check `adb devices`, USB debugging authorization, and the emulator's boot status.
- **API connection refused or timed out:** verify Swagger on the computer, the selected base URL, backend availability, USB forwarding or firewall rules, and the trailing `/api/`.
- **Cleartext HTTP blocked:** check that the exact host is listed in `network_security_config.xml`; use the emulator or USB examples above.
- **Blank map or missing location:** verify the public Mapbox token, network access, device location settings, runtime permission, and the emulator's simulated location.
- **401/403 after changing backends:** log in again against the selected API so the app obtains a matching token.

Inspect runtime errors in Android Studio's **Logcat**. The network client logs HTTP bodies, so review logs before sharing them to avoid exposing account information or tokens.

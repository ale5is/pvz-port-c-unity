# Android updates and signing

PavoHorizon keeps the Android application ID stable:

    com.ale5is.pavohorizon

The launcher version shown by Android is read from PAVO_VERSION (currently 1.0.0). The underlying Horizon engine remains pinned to 0.7.8; these are different version numbers.

The build assigns a monotonically increasing Android versionCode using 100000 + GITHUB_RUN_NUMBER. This lets Android recognize later APKs as upgrades rather than downgrades.

## One-time signing key setup

Android only permits an APK to update an installed app when both APKs have the same application ID and signing certificate. A new random debug keystore on each GitHub Actions run would break updates.

Create one persistent keystore locally. Keep the file private and back it up securely.

Windows PowerShell (JDK 17 keytool):

    keytool -genkeypair -keystore pavo-release.keystore -storepass android -keypass android -alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=PavoHorizon"

Convert it to Base64:

    [Convert]::ToBase64String([IO.File]::ReadAllBytes("$PWD/pavo-release.keystore")) | Set-Clipboard

In the GitHub repository, open Settings → Secrets and variables → Actions → New repository secret. Set its name to:

    PAVO_ANDROID_KEYSTORE_BASE64

Paste the clipboard value as the secret. Do not commit the keystore or its Base64 contents.

The build uses the fixed Android Gradle signing alias and passwords above. Do not change or regenerate this key after publishing the first APK signed with it.

## Important transition from earlier CI APKs

Earlier CI runs generated a temporary debug keystore on the GitHub runner and did not preserve it. Their private signing key is not recoverable from the APK. Therefore an APK built with the new persistent key cannot be installed over one of those earlier temporary-key builds.

If one of those earlier APKs is installed, back up/export worlds first, uninstall that APK once, then install the first APK built with the persistent key. After that one-time transition, future PavoHorizon APKs can be installed over the existing app without clearing its data, provided the application ID and persistent key remain unchanged and the versionCode increases.

Android normally retains the app's private data on a valid in-place update. Do not clear storage or uninstall during subsequent updates.

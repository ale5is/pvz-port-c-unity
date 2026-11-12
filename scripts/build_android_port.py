#!/usr/bin/env python3
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
HORIZON_REV = "68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29"
MIXINS_REV = "a2af5c0dd4117d51ecd70066ba7a00b4a2c10dab"
MIXINS_REPO = "https://github.com/horizon-rust/horizon-mixins"

def run(command, cwd=None):
    print("+", " ".join(map(str, command)), flush=True)
    subprocess.run([str(x) for x in command], cwd=cwd, check=True)

def install_mixins():
    cargo = shutil.which("cargo")
    if not cargo:
        raise SystemExit("Rust/Cargo is required. Install Rust first.")
    root = ROOT / ".build/horizon-mixins"
    executable = root / "bin" / ("horizon-mixins.exe" if os.name == "nt" else "horizon-mixins")
    marker = root / ".pinned-revision"
    if executable.is_file() and marker.is_file() and marker.read_text(encoding="utf-8").strip() == MIXINS_REV:
        return executable
    rustup = shutil.which("rustup")
    if rustup:
        run([rustup, "toolchain", "install", "1.95.0"])
        cargo_command = [cargo, "+1.95.0"]
    else:
        cargo_command = [cargo]
    run([*cargo_command, "install", "--locked", "--git", MIXINS_REPO, "--rev", MIXINS_REV,
         "--root", root, "--force", "horizon-mixins-cli"])
    if not executable.is_file():
        raise SystemExit(f"Pinned horizon-mixins binary was not created: {executable}")
    root.mkdir(parents=True, exist_ok=True)
    marker.write_text(MIXINS_REV + "\n", encoding="utf-8")
    return executable

def verify_source(source):
    hooks = source / "crates/horizon-client/src/game/native_hooks.rs"
    build = source / "tools/android_build.py"
    if not hooks.is_file() or not build.is_file():
        raise SystemExit("The selected folder is not a complete Horizon 0.7.8 source tree.")
    text = hooks.read_text(encoding="utf-8")
    marker = "pub(crate) const NATIVE_HOOKS_API: u32 = 80;"
    if marker not in text:
        raise SystemExit("The source does not expose native hooks API 80; refusing to apply this port.")
    git = shutil.which("git")
    if git and (source / ".git").exists():
        revision = subprocess.check_output([git, "rev-parse", "HEAD"], cwd=source, text=True).strip()
        if revision != HORIZON_REV:
            raise SystemExit(f"Wrong Horizon revision: {revision}. Expected {HORIZON_REV} (v0.7.8).")
    else:
        marker = source / ".horizon-revision"
        if not marker.is_file() or marker.read_text(encoding="utf-8").strip() != HORIZON_REV:
            raise SystemExit("Use a git checkout at the exact v0.7.8 commit, or add a verified .horizon-revision marker.")

def copy_prepared_data(source, stage):
    prepared = source / ".local/horizon-client"
    if prepared.is_dir():
        destination = stage / ".local/horizon-client"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(prepared, destination, dirs_exist_ok=True)
    controls = source / ".local/android/bedrock-controls.zip"
    if controls.is_file():
        destination = stage / ".local/android/bedrock-controls.zip"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(controls, destination)

def patch_android_microphone(stage):
    manifest = stage / "packaging/android/AndroidManifest.xml"
    text = manifest.read_text(encoding="utf-8")
    permission = '    <uses-permission android:name="android.permission.RECORD_AUDIO" />'
    if "android.permission.RECORD_AUDIO" not in text:
        needle = '    <uses-permission android:name="android.permission.INTERNET" />'
        if needle not in text:
            raise SystemExit("Could not locate the INTERNET permission in AndroidManifest.xml.")
        text = text.replace(needle, needle + "\n" + permission)
    feature = '    <uses-feature android:name="android.hardware.microphone" android:required="false" />'
    if "android.hardware.microphone" not in text:
        needle = '    <uses-feature android:name="android.hardware.touchscreen" android:required="false" />'
        if needle not in text:
            raise SystemExit("Could not locate the touchscreen feature in AndroidManifest.xml.")
        text = text.replace(needle, needle + "\n" + feature)
    manifest.write_text(text, encoding="utf-8")

    java = stage / "packaging/android/java/dev/horizon/android/HorizonActivity.java"
    source = ROOT / "android/HorizonActivity.java"
    if not source.is_file():
        raise SystemExit(f"Missing microphone permission adapter: {source}")
    shutil.copy2(source, java)

def main():
    parser = argparse.ArgumentParser(description="Apply the API-80 voice-chat mixin and build a Horizon Android APK.")
    parser.add_argument("--horizon-source", type=Path, required=True, help="Clean Horizon v0.7.8 source checkout")
    parser.add_argument("--out", type=Path, default=ROOT / ".build/horizon-078-android")
    parser.add_argument("--abi", choices=["arm64-v8a", "armeabi-v7a", "x86", "x86_64"], default="arm64-v8a")
    parser.add_argument("--sdk", type=Path, default=Path(os.environ["ANDROID_HOME"]) if os.environ.get("ANDROID_HOME") else Path(os.environ["ANDROID_SDK_ROOT"]) if os.environ.get("ANDROID_SDK_ROOT") else None)
    parser.add_argument("--java-home", type=Path, default=Path(os.environ["JAVA_HOME"]) if os.environ.get("JAVA_HOME") else None)
    parser.add_argument("--ndk-version", default="28.2.13676358")
    parser.add_argument("--build-tools-version", default="35.0.0")
    parser.add_argument("--profile", choices=["fast", "release", "dev"], default="fast")
    parser.add_argument("--check-only", action="store_true", help="Run cargo check for the Android ABI instead of producing an APK")
    parser.add_argument("--trust-native-code", action="store_true", help="Allow Cargo to compile and execute the reviewed native mod build scripts")
    args = parser.parse_args()

    source = args.horizon_source.expanduser().resolve()
    stage = args.out.expanduser().resolve()
    verify_source(source)
    if args.sdk is None or not args.sdk.is_dir():
        raise SystemExit("Pass --sdk or set ANDROID_HOME to the installed Android SDK.")
    if args.java_home is None or not args.java_home.is_dir():
        raise SystemExit("Pass --java-home or set JAVA_HOME to a JDK directory.")
    if not args.trust_native_code:
        raise SystemExit("This build executes native mod code and build scripts. Review the source and pass --trust-native-code.")

    mixins = install_mixins()
    stage.parent.mkdir(parents=True, exist_ok=True)
    run([
        mixins, "apply",
        "--workspace", source,
        "--mods", ROOT / "mod",
        "--out", stage
    ])
    copy_prepared_data(source, stage)
    patch_android_microphone(stage)

    command = [
        sys.executable,
        stage / "tools/android_build.py",
        "--abi", args.abi,
        "--sdk", args.sdk,
        "--java-home", args.java_home,
        "--ndk-version", args.ndk_version,
        "--build-tools-version", args.build_tools_version,
    ]
    if args.check_only:
        command.append("--check")
    else:
        command.extend(["--profile", args.profile])
        if not (stage / ".local/horizon-client/26.3/horizon-resources.json").is_file():
            raise SystemExit(
                "Prepared engine data is missing. Run tools/android_prepare.py in the Horizon source checkout, "
                "then rerun this script so it can copy .local/horizon-client into the staged workspace."
            )
    run(command, cwd=stage)
    if not args.check_only:
        apk = stage / ".local/android/apk" / args.abi / "horizon-android.apk"
        print(f"APK: {apk}", flush=True)

if __name__ == "__main__":
    main()

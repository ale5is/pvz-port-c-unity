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
    executable = shutil.which("horizon-mixins")
    if executable:
        return Path(executable)
    cargo = shutil.which("cargo")
    if not cargo:
        raise SystemExit("Rust/Cargo is required. Install Rust first.")
    run([cargo, "install", "--locked", "--git", MIXINS_REPO, "--rev", MIXINS_REV, "horizon-mixins-cli"])
    executable = shutil.which("horizon-mixins")
    if not executable:
        raise SystemExit("horizon-mixins installation completed but the executable is not on PATH.")
    return Path(executable)

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

def main():
    parser = argparse.ArgumentParser(description="Apply the API-80 voice-chat mixin and build a Horizon Android APK.")
    parser.add_argument("--horizon-source", type=Path, required=True, help="Clean Horizon v0.7.8 source checkout")
    parser.add_argument("--out", type=Path, default=ROOT / ".build/horizon-078-android")
    parser.add_argument("--abi", choices=["arm64-v8a", "armeabi-v7a", "x86", "x86_64"], default="arm64-v8a")
    parser.add_argument("--sdk", type=Path, default=Path(os.environ.get("ANDROID_HOME", os.environ.get("ANDROID_SDK_ROOT", ""))))
    parser.add_argument("--java-home", type=Path, default=Path(os.environ.get("JAVA_HOME", "")))
    parser.add_argument("--ndk-version", default="28.2.13676358")
    parser.add_argument("--build-tools-version", default="35.0.0")
    parser.add_argument("--profile", choices=["fast", "release", "dev"], default="fast")
    parser.add_argument("--check-only", action="store_true", help="Run cargo check for the Android ABI instead of producing an APK")
    args = parser.parse_args()

    source = args.horizon_source.expanduser().resolve()
    stage = args.out.expanduser().resolve()
    verify_source(source)
    if not args.sdk or not args.sdk.is_dir():
        raise SystemExit("Pass --sdk or set ANDROID_HOME to the installed Android SDK.")
    if not args.java_home or not args.java_home.is_dir():
        raise SystemExit("Pass --java-home or set JAVA_HOME to a JDK directory.")

    mixins = install_mixins()
    stage.parent.mkdir(parents=True, exist_ok=True)
    run([
        mixins, "apply",
        "--workspace", source,
        "--mods", ROOT / "mod",
        "--out", stage
    ])
    copy_prepared_data(source, stage)

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

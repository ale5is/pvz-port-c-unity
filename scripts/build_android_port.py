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

def verify_jade_compatibility(jade_source):
    import json
    import re

    native_path = jade_source / "horizon-native.json"
    manifest_path = jade_source / "mixins/jade/horizon-mixin.toml"
    if not native_path.is_file() or not manifest_path.is_file():
        raise SystemExit("The selected Jade source is incomplete.")

    native = json.loads(native_path.read_text(encoding="utf-8"))
    manifest = manifest_path.read_text(encoding="utf-8")
    revision_match = re.search(r'^horizon_revision\s*=\s*"([0-9a-f]+)"', manifest, re.MULTILINE)
    manifest_revision = revision_match.group(1) if revision_match else "missing"
    native_revision = native.get("horizon", "missing")
    native_hooks = native.get("hooks", "missing")

    if manifest_revision != HORIZON_REV or native_revision != HORIZON_REV or native_hooks != 80:
        raise SystemExit(
            "Jade is not compatible with the pinned Horizon 0.7.8 source. "
            f"Jade manifest revision={manifest_revision}, native revision={native_revision}, "
            f"hooks API={native_hooks}; required revision={HORIZON_REV}, hooks API=80. "
            "Do not rewrite Jade metadata or force-apply hooks: its Rust source uses newer Horizon APIs. "
            "Port Jade's implementation to Horizon 0.7.8 before building."
        )


def expose_pinned_jade_hooks(source):
    import json

    registry_path = source / "native-hooks.json"
    native_hooks = source / "crates/horizon-client/src/game/native_hooks.rs"
    if not registry_path.is_file() or not native_hooks.is_file():
        raise SystemExit("Horizon native hook registry or implementation file is missing.")

    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    if not isinstance(registry.get("hooks"), list):
        raise SystemExit("Unexpected Horizon native-hooks.json schema.")

    fragments = [
        "screen_mouse_scrolled.json",
        "screen_key_input.json",
        "client_resources_reloaded.json",
        "native_perform_console_command.json",
    ]
    known = {item.get("name") for item in registry["hooks"]}
    implementation = native_hooks.read_text(encoding="utf-8")
    additions = []
    for filename in fragments:
        path = source / "tools/native-hook-fragments" / filename
        if not path.is_file():
            raise SystemExit(f"Missing pinned Horizon hook fragment: {path}")
        fragment = json.loads(path.read_text(encoding="utf-8"))
        for hook in fragment.get("hooks", []):
            name = hook.get("name")
            target = hook.get("target", "")
            method = target.rsplit("::", 1)[-1]
            if not name or not method or method not in implementation:
                raise SystemExit(f"Jade hook implementation is absent in pinned Horizon: {name}")
            if name not in known:
                hook["version"] = int(hook.get("version", 1))
                additions.append(hook)
                known.add(name)

    if additions:
        registry["hooks"].extend(additions)
        registry_path.write_text(json.dumps(registry, indent=2) + "\n", encoding="utf-8")
    print("Pinned Horizon hook registry: added " + ", ".join(x["name"] for x in additions), flush=True)


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

def patch_android_identity(stage):
    import re

    manifest = stage / "packaging/android/AndroidManifest.xml"
    manifest_text = manifest.read_text(encoding="utf-8")
    manifest_text, label_count = re.subn(
        r'android:label="Horizon Android"',
        'android:label="PavoHorizon"',
        manifest_text,
        count=1,
    )
    if label_count != 1:
        raise SystemExit("Could not uniquely locate the Horizon Android application label.")
    manifest.write_text(manifest_text, encoding="utf-8")

    gradle = stage / "packaging/android/build.gradle"
    gradle_text = gradle.read_text(encoding="utf-8")
    old_app_id = "applicationId providers.gradleProperty('appId').getOrElse('dev.horizon.android')"
    new_app_id = "applicationId providers.gradleProperty('appId').getOrElse('com.ale5is.pavohorizon')"
    if old_app_id not in gradle_text:
        raise SystemExit("Could not locate the default Android application ID.")
    gradle.write_text(gradle_text.replace(old_app_id, new_app_id, 1), encoding="utf-8")

    android_build = stage / "tools/android_build.py"
    build_text = android_build.read_text(encoding="utf-8")
    old_check_arg = "*(['--app-id', 'dev.horizon.android.dev'] if args.dev_build else [])"
    new_check_arg = "*(['--app-id', 'dev.horizon.android.dev'] if args.dev_build else ['--app-id', 'com.ale5is.pavohorizon'])"
    if old_check_arg not in build_text:
        raise SystemExit("Could not locate the upstream Android package audit argument.")
    android_build.write_text(build_text.replace(old_check_arg, new_check_arg, 1), encoding="utf-8")

    checker = stage / "tools/check_android_release.py"
    checker_text = checker.read_text(encoding="utf-8")
    old_default = "def audit(path, aapt2, public=False, app_id='dev.horizon.android'):"
    new_default = "def audit(path, aapt2, public=False, app_id='com.ale5is.pavohorizon'):"
    old_choices = "parser.add_argument('--app-id', choices=['dev.horizon.android', 'dev.horizon.android.dev'], default='dev.horizon.android')"
    new_choices = "parser.add_argument('--app-id', choices=['dev.horizon.android', 'dev.horizon.android.dev', 'com.ale5is.pavohorizon'], default='com.ale5is.pavohorizon')"
    if old_default not in checker_text or old_choices not in checker_text:
        raise SystemExit("Could not locate both Android package identity defaults and CLI choices in the release audit.")
    checker_text = checker_text.replace(old_default, new_default, 1)
    checker_text = checker_text.replace(old_choices, new_choices, 1)
    checker.write_text(checker_text, encoding="utf-8")

    print("Android identity: label=PavoHorizon, applicationId=com.ale5is.pavohorizon; release audit aligned; Gradle project name preserved for upstream build compatibility.", flush=True)



def patch_android_launcher_branding(stage):
    launcher = stage / "crates/horizon-client/src/android/launcher_pages.rs"
    source = launcher.read_text(encoding="utf-8")

    replacements = [
        (
            '    StarlingDownload,\n',
            '',
            "StarlingDownload action",
        ),
        (
            '''                ui.text(
                    Face::SemiBold,
                    15.0,
                    content_x + 22.0,
                    card_y + 84.0,
                    my3_text(),
                    "Horizon Android is becoming Starling",
                );
                let notice = "Some Horizon Android features are still being transferred and merged. Export your worlds through Android Files (SAF) before moving.";''',
            '''                ui.text(
                    Face::SemiBold,
                    15.0,
                    content_x + 22.0,
                    card_y + 84.0,
                    my3_text(),
                    "PavoHorizon para Android",
                );
                let notice = "Tu instancia de PavoHorizon está lista. Puedes exportar tus mundos desde Android Files (SAF) cuando lo necesites.";''',
            "Android launcher migration notice",
        ),
        (
            '''                let migration_y = line_y + 24.0;
                let migration_w = (content_w - 52.0) * 0.5;
                button(
                    &mut ui,
                    &mut self.targets,
                    [content_x + 22.0, migration_y, migration_w, 34.0],
                    "Download Starling",
                    Action::StarlingDownload,
                    false,
                );
                button(
                    &mut ui,
                    &mut self.targets,
                    [
                        content_x + 30.0 + migration_w,
                        migration_y,
                        migration_w,
                        34.0,
                    ],
                    "Export worlds (SAF)",
                    Action::WorldFiles,
                    false,
                );''',
            '''                let migration_y = line_y + 24.0;
                button(
                    &mut ui,
                    &mut self.targets,
                    [content_x + 22.0, migration_y, content_w - 44.0, 34.0],
                    "Exportar mundos (SAF)",
                    Action::WorldFiles,
                    false,
                );''',
            "Android launcher migration buttons",
        ),
        (
            '''            Some(Action::StarlingDownload) => {
                if !crate::platform::open_url(
                    "https://github.com/JuanLuci/starling/releases/latest",
                ) {
                    self.catalog.status =
                        "Open github.com/JuanLuci/starling/releases to download Starling.".into();
                }
            }
''',
            '',
            "Starling download action handler",
        ),
        (
            '''                    "In Android Files, select a complete world folder, then Copy to a folder you choose. Keep Horizon Android installed until you verify your worlds in Starling."
                } else { "Open system Files > Horizon Android > Worlds. Copy complete world folders while the game is closed." }.into();''',
            '''                    "En Android Files, selecciona una carpeta de mundo completa y cópiala a la ubicación que prefieras."
                } else { "Abre Archivos > PavoHorizon > Worlds. Copia carpetas completas de mundos con el juego cerrado." }.into();''',
            "World export migration text",
        ),
        (
            '''                } else { "Open system Files > Horizon Android." }.into();''',
            '''                } else { "Abre Archivos del sistema > PavoHorizon." }.into();''',
            "Game files fallback label",
        ),
    ];

    for (old, new, label) in replacements:
        count = source.count(old)
        if count != 1:
            raise SystemExit(f"Could not uniquely locate {label} in pinned Horizon source (found {count}).")
        source = source.replace(old, new, 1)

    if "Starling" in source or "starling" in source:
        raise SystemExit("Starling references remain in the patched Android launcher source.")

    launcher.write_text(source, encoding="utf-8")
    print("Android launcher branding: PavoHorizon; Starling migration/download entry removed; world export retained.", flush=True)

def verify_no_starlight_in_apk(apk):
    import zipfile

    needles = (b"starlight", "starlight".encode("utf-16le"), "starlight".encode("utf-16be"))
    matches = []
    with zipfile.ZipFile(apk) as archive:
        for entry in archive.infolist():
            name = entry.filename.lower().encode("utf-8", errors="ignore")
            if b"starlight" in name:
                matches.append(entry.filename)
                continue
            try:
                payload = archive.read(entry)
            except Exception:
                continue
            if any(needle in payload.lower() for needle in needles):
                matches.append(entry.filename)
                continue
            for encoding in ("utf-16le", "utf-16be"):
                try:
                    decoded = payload.decode(encoding, errors="ignore").lower()
                except Exception:
                    continue
                if "starlight" in decoded:
                    matches.append(entry.filename)
                    break
    if matches:
        raise SystemExit("Starlight references remain in APK entries: " + ", ".join(matches))
    print("Starlight scan: no references found in APK entries.", flush=True)


def main():
    parser = argparse.ArgumentParser(description="Apply the API-80 voice-chat mixin and build a Horizon Android APK.")
    parser.add_argument("--horizon-source", type=Path, required=True, help="Clean Horizon v0.7.8 source checkout")
    parser.add_argument("--out", type=Path, default=ROOT / ".build/horizon-078-android")
    parser.add_argument("--abi", choices=["arm64-v8a", "armeabi-v7a", "x86", "x86_64"], default="arm64-v8a")
    parser.add_argument("--sdk", type=Path, default=Path(os.environ["ANDROID_HOME"]) if os.environ.get("ANDROID_HOME") else Path(os.environ["ANDROID_SDK_ROOT"]) if os.environ.get("ANDROID_SDK_ROOT") else None)
    parser.add_argument("--java-home", type=Path, default=Path(os.environ["JAVA_HOME"]) if os.environ.get("JAVA_HOME") else None)
    parser.add_argument("--ndk-version", default="28.2.13676358")
    parser.add_argument("--build-tools-version", default="35.0.0")
    parser.add_argument("--gradle", type=Path, help="Explicit Gradle 8.14 executable")
    parser.add_argument("--profile", choices=["fast", "release", "dev"], default="fast")
    parser.add_argument("--check-only", action="store_true", help="Run cargo check for the Android ABI instead of producing an APK")
    parser.add_argument("--trust-native-code", action="store_true", help="Allow Cargo to compile and execute the reviewed native mod build scripts")
    parser.add_argument("--jade-source", type=Path, help="Pinned Jade-Horizon source checkout; adds Jade adapted to the pinned Horizon API")
    parser.add_argument("--public", action="store_true", help="Build a distributable APK with generated tables only and Mojang resources downloaded by the app")
    args = parser.parse_args()

    source = args.horizon_source.expanduser().resolve()
    stage = args.out.expanduser().resolve()
    verify_source(source)
    if args.jade_source is not None:
        verify_jade_compatibility(args.jade_source.expanduser().resolve())
    if args.sdk is None or not args.sdk.is_dir():
        raise SystemExit("Pass --sdk or set ANDROID_HOME to the installed Android SDK.")
    if args.java_home is None or not args.java_home.is_dir():
        raise SystemExit("Pass --java-home or set JAVA_HOME to a JDK directory.")
    if not args.trust_native_code:
        raise SystemExit("This build executes native mod code and build scripts. Review the source and pass --trust-native-code.")

    mixins = install_mixins()
    stage.parent.mkdir(parents=True, exist_ok=True)
    mods_root = stage.parent / "mod-bundles"
    bundle = mods_root / "simple-voice-chat"
    if mods_root.exists():
        shutil.rmtree(mods_root)
    mods_root.mkdir(parents=True, exist_ok=True)
    shutil.copytree(ROOT / "mod", bundle)
    if args.jade_source is not None:
        jade_source = args.jade_source.expanduser().resolve()
        jade_bundle_source = jade_source / "mixins" / "jade"
        jade_manifest = jade_bundle_source / "horizon-mixin.toml"
        jade_native = jade_source / "horizon-native.json"
        jade_license = jade_source / "LICENSE"
        if not jade_manifest.is_file() or not jade_native.is_file() or not jade_license.is_file():
            raise SystemExit("The selected Jade source is incomplete; expected mixins/jade, horizon-native.json and LICENSE.")
        jade_bundle = mods_root / "jade"
        shutil.copytree(jade_bundle_source, jade_bundle)
        shutil.copy2(jade_license, jade_bundle / "LICENSE")
    run([
        mixins, "apply",
        "--workspace", source,
        "--mods", mods_root,
        "--out", stage
    ])
    copy_prepared_data(source, stage)
    if args.jade_source is not None:
        notices = stage / "THIRD_PARTY_NOTICES.md"
        jade_license_text = (args.jade_source.expanduser().resolve() / "LICENSE").read_text(encoding="utf-8")
        with notices.open("a", encoding="utf-8") as stream:
            stream.write(
                "\n\n## Jade for Horizon (unofficial port)\n\n"
                "Upstream: https://github.com/Joeypc6/Jade-Horizon\n"
                "Source revision: 4ee222801ca156b508344ef3b2d739a0b0f2de6c\n"
                "Jade by Snownee; unofficial Horizon port. Licensed CC BY-NC-SA 4.0. "
                "Non-commercial use only; adaptations must retain the same licence.\n\n"
            )
            stream.write(jade_license_text)
            stream.write("\n")
    patch_android_microphone(stage)
    patch_android_identity(stage)
    patch_android_launcher_branding(stage)

    command = [
        sys.executable,
        stage / "tools/android_build.py",
        "--abi", args.abi,
        "--sdk", args.sdk,
        "--java-home", args.java_home,
        "--ndk-version", args.ndk_version,
        "--build-tools-version", args.build_tools_version,
    ]
    if args.gradle is not None:
        gradle = args.gradle.expanduser().resolve()
        if not gradle.is_file():
            raise SystemExit(f"Gradle executable does not exist: {gradle}")
        command.extend(["--gradle", gradle])
    if args.check_only:
        command.append("--check")
    else:
        command.extend(["--profile", args.profile])
        if args.public:
            command.append("--public")
        if not (stage / ".local/horizon-client/26.3/horizon-resources.json").is_file():
            raise SystemExit(
                "Prepared engine data is missing. Run tools/android_prepare.py in the Horizon source checkout, "
                "then rerun this script so it can copy .local/horizon-client into the staged workspace."
            )
    run(command, cwd=stage)
    if not args.check_only:
        apk_name = "horizon-android-public.apk" if args.public else "horizon-android.apk"
        apk = stage / ".local/android/apk" / args.abi / apk_name
        verify_no_starlight_in_apk(apk)
        print(f"APK: {apk}", flush=True)

if __name__ == "__main__":
    main()

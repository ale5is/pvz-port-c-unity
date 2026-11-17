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



def write_pavo_launcher_icon(path):
    import struct
    import zlib

    size = 192
    pixels = bytearray()
    dark = (22, 24, 31, 255)
    light = (236, 237, 243, 255)
    muted = (236, 237, 243, 92)
    accent = (154, 134, 255, 255)

    def inside_round_tile(x, y):
        radius = 42
        cx = min(max(x, radius), size - radius - 1)
        cy = min(max(y, radius), size - radius - 1)
        return (x - cx) ** 2 + (y - cy) ** 2 <= radius ** 2

    def in_p(x, y):
        stem = 55 <= x < 78 and 39 <= y < 153
        top = 78 <= x < 128 and 39 <= y < 62
        right = 128 <= x < 151 and 62 <= y < 96
        middle = 78 <= x < 128 and 96 <= y < 119
        return stem or top or right or middle

    for y in range(size):
        pixels.append(0)
        for x in range(size):
            if not inside_round_tile(x, y):
                pixels.extend((0, 0, 0, 0))
                continue
            color = dark
            if in_p(x, y):
                color = light if y < 96 else muted
            if 0 <= y < 12:
                color = accent
            pixels.extend(color)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(pixels), 9))
    png += chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


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
                let notice = match self.catalog.config.language.as_str() {
                    "es_es" => "Tu instancia de PavoHorizon está lista. Puedes exportar tus mundos desde Archivos de Android (SAF) cuando lo necesites.",
                    "pt_br" => "Sua instância do PavoHorizon está pronta. Você pode exportar seus mundos pelos Arquivos do Android (SAF) quando quiser.",
                    "fr_fr" => "Votre instance PavoHorizon est prête. Vous pouvez exporter vos mondes depuis les fichiers Android (SAF) à tout moment.",
                    "it_it" => "La tua istanza PavoHorizon è pronta. Puoi esportare i tuoi mondi dai File Android (SAF) quando vuoi.",
                    "de_de" => "Deine PavoHorizon-Instanz ist bereit. Du kannst deine Welten jederzeit über Android-Dateien (SAF) exportieren.",
                    "ja_jp" => "PavoHorizon の準備ができました。Android のファイル (SAF) からいつでもワールドをエクスポートできます。",
                    "zh_cn" => "PavoHorizon 已准备就绪。你可以随时通过 Android 文件 (SAF) 导出世界。",
                    _ => "Your PavoHorizon instance is ready. You can export your worlds from Android Files (SAF) whenever you need.",
                };''',
            "Android launcher migration notice",
        ),
        (
            '        ui.horizon_logo(18.0, 12.0, 28.0);',
            '        ui.rounded(18.0, 12.0, 28.0, 28.0, 8.0, my3_accent());\n        ui.text(Face::SemiBold, 20.0, 24.0, 14.0, my3_on_accent(), "P");',
            "Replace Android launcher Horizon logo with P mark",
        ),
        (
            '        ui.text(Face::SemiBold, 16.0, 54.0, 10.0, my3_text(), "Horizon");',
            '        ui.text(Face::SemiBold, 16.0, 54.0, 10.0, my3_text(), "PavoHorizon");',
            "Android launcher top-left title",
        ),
        (
            '''        let version_line = format!("Android {}", crate::backend::VERSION);
        let fitted_version = ui.fit_text(Face::Regular, 11.0, &version_line, brand_w);
        ui.text(
            Face::Regular,
            11.0,
            54.0,
            30.0,
            my3_muted(),
            &fitted_version,
        );
''',
            '''        ui.text(Face::Regular, 11.0, 54.0, 30.0, my3_muted(), "1.0.0");
''',
            "Show PavoHorizon app version below wordmark",
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
            '''                let migration_y = (line_y + 24.0).min(card_y + card_h - 40.0);
                let export_width = (content_w - 182.0).max(80.0);
                button(
                    &mut ui,
                    &mut self.targets,
                    [content_x + 22.0, migration_y, export_width, 34.0],
                    "Exportar mundos (SAF)",
                    Action::WorldFiles,
                    false,
                );
                let language_label = match self.catalog.config.language.as_str() {
                    "es_es" => "Español v",
                    "pt_br" => "Português v",
                    "fr_fr" => "Français v",
                    "it_it" => "Italiano v",
                    "de_de" => "Deutsch v",
                    "ja_jp" => "日本語 v",
                    "zh_cn" => "简体中文 v",
                    _ => "English v",
                };
                button(
                    &mut ui,
                    &mut self.targets,
                    [content_x + content_w - 140.0, migration_y, 118.0, 34.0],
                    language_label,
                    Action::CycleLanguage,
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
        (
            '''                "Exit Horizon Android?",''',
            '''                "Exit PavoHorizon?",''',
            "Exit confirmation title",
        ),
        (
            '''                    "Open the system Files app and choose Horizon Android.".into()''',
            '''                    "Open the system Files app and choose PavoHorizon.".into()''',
            "System Files fallback label",
        ),
        (
            '''                    "Reports are saved under Horizon Android > Reports.".into()''',
            '''                    "Reports are saved under PavoHorizon > Reports.".into()''',
            "Reports folder fallback label",
        ),
    ];

    for (old, new, label) in replacements:
        count = source.count(old)
        if count != 1:
            raise SystemExit(f"Could not uniquely locate {label} in pinned Horizon source (found {count}).")
        source = source.replace(old, new, 1)

    if "Starling" in source or "starling" in source:
        raise SystemExit("Starling references remain in the patched Android launcher source.")
    if "Horizon Android" in source:
        raise SystemExit("Old Horizon Android app branding remains in the patched launcher source.")

    launcher.write_text(source, encoding="utf-8")
    write_pavo_launcher_icon(stage / "packaging/android/res/drawable-nodpi/launcher_icon.png")
    print("Android launcher branding: PavoHorizon P icon; Starling migration/download entry removed; world export retained.", flush=True)


def patch_pavo_native_builds(source):
    launcher = source / "launcher" / "src"
    module_file = launcher / "native_mods.rs"
    module_dir = launcher / "native_mods"
    main_file = launcher / "main.rs"
    mods_page = launcher / "mods_page.rs"

    required = [module_file, module_dir, main_file, mods_page]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise SystemExit("Pinned launcher native-build sources are incomplete: " + ", ".join(missing))

    for path in (source / "launcher").rglob("*.rs"):
        text = path.read_text(encoding="utf-8")
        renamed = text.replace("native_mods", "pavo_native_builds")
        if renamed != text:
            path.write_text(renamed, encoding="utf-8")

    renamed_file = launcher / "pavo_native_builds.rs"
    renamed_dir = launcher / "pavo_native_builds"
    if renamed_file.exists() or renamed_dir.exists():
        raise SystemExit("Pavo Native Builds paths already exist; refusing to overwrite launcher sources.")
    module_file.rename(renamed_file)
    module_dir.rename(renamed_dir)

    module = renamed_file.read_text(encoding="utf-8")
    anchor = "use std::process::{Command, Stdio};\n"
    if module.count(anchor) != 1:
        raise SystemExit("Could not locate Pavo Native Builds module imports.")
    module = module.replace(anchor, anchor + "\n/// Experimental native mod compilation is intentionally disabled for this port.\npub const ENABLED: bool = false;\n", 1)
    renamed_file.write_text(module, encoding="utf-8")

    main = main_file.read_text(encoding="utf-8")
    build_anchor = "    fn build_native(&mut self, anyway: bool) {\n"
    if main.count(build_anchor) != 1:
        raise SystemExit("Could not locate the native-build entry point in the launcher.")
    main = main.replace(
        build_anchor,
        build_anchor
        + '        if !pavo_native_builds::ENABLED {\n'
        + '            self.native_error = Some(tr("Pavo Native Builds is disabled in this version.").into());\n'
        + '            return;\n'
        + '        }\n',
        1,
    )
    main_file.write_text(main, encoding="utf-8")

    page = mods_page.read_text(encoding="utf-8")
    page_anchor = "    pub(super) fn native_build(&self, cx: &mut Context<Self>) -> Option<Div> {\n"
    if page.count(page_anchor) != 1:
        raise SystemExit("Could not locate the native-build launcher panel.")
    page = page.replace(
        page_anchor,
        page_anchor + "        if !pavo_native_builds::ENABLED {\n            return None;\n        }\n",
        1,
    )
    page = page.replace(
        'trf("Native build of {instance}", &name)',
        'trf("Pavo Native Builds · {instance}", &name)',
        1,
    )
    mods_page.write_text(page, encoding="utf-8")

    remaining = []
    for path in (source / "launcher").rglob("*.rs"):
        if "native_mods" in path.read_text(encoding="utf-8"):
            remaining.append(str(path))
    if remaining:
        raise SystemExit("Old native_mods identifiers remain in launcher sources: " + ", ".join(remaining))

    print("Pavo Native Builds: module renamed; UI hidden and build entry point blocked (ENABLED=false).", flush=True)


def patch_android_language(stage):
    import json

    catalog = stage / "crates/horizon-client/src/android/catalog.rs"
    source = catalog.read_text(encoding="utf-8")
    anchor = "    pub server: String,\n"
    if source.count(anchor) != 1:
        raise SystemExit("Could not locate Android launcher language config field.")
    source = source.replace(
        anchor,
        anchor
        + '    #[serde(default = "default_language")]\n    pub language: String,\n'
        + '    #[serde(default = "default_language_auto")]\n    pub language_auto: bool,\n',
        1,
    )
    anchor = "fn default_true() -> bool {\n"
    if source.count(anchor) != 1:
        raise SystemExit("Could not locate Android launcher config defaults.")
    language_defaults = """unsafe extern "C" {
    fn __system_property_get(name: *const std::ffi::c_char, value: *mut std::ffi::c_char) -> i32;
}

fn default_language_auto() -> bool { true }

fn android_system_property(name: &str) -> Option<String> {
    let key = std::ffi::CString::new(name).ok()?;
    let mut value = [0 as std::ffi::c_char; 92];
    let length = unsafe { __system_property_get(key.as_ptr(), value.as_mut_ptr()) };
    if length <= 0 {
        return None;
    }
    let bytes = unsafe {
        std::slice::from_raw_parts(value.as_ptr().cast::<u8>(), length as usize)
    };
    let result = String::from_utf8_lossy(bytes).trim().to_ascii_lowercase();
    if result.is_empty() { None } else { Some(result) }
}

fn default_language() -> String {
    for property in [
        "persist.sys.locale",
        "persist.sys.language",
        "ro.product.locale",
        "ro.product.locale.language",
    ] {
        let Some(locale) = android_system_property(property) else { continue };
        let language = locale.split(|c| c == '-' || c == '_').next().unwrap_or("");
        let code = match language {
            "es" => "es_es", "pt" => "pt_br", "fr" => "fr_fr",
            "it" => "it_it", "de" => "de_de", "ja" => "ja_jp",
            "zh" => "zh_cn", "en" => "en_us", _ => continue,
        };
        return code.into();
    }
    "en_us".into()
}

"""
    source = source.replace(anchor, language_defaults + anchor, 1)
    anchor = "        let mut config: Self = serde_json::from_value(value.clone()).unwrap_or_default();\n"
    if source.count(anchor) != 1:
        raise SystemExit("Could not locate Android launcher config loading.")
    source = source.replace(
        anchor,
        anchor
        + """        if config.language_auto {
            config.language = default_language();
        }
        let options_path = config.game_home(home).join("options.txt");
        let mut game_settings = horizon_client::settings::Settings::load(&options_path);
        if game_settings.language != config.language {
            game_settings.language = config.language.clone();
            let _ = game_settings.save(&options_path);
        }
""",
        1,
    )
    catalog.write_text(source, encoding="utf-8")

    core_lang = stage / "crates/horizon-client-core/src/lang.rs"
    core_source = core_lang.read_text(encoding="utf-8")
    bundled_anchor = '            _ => return None,\n'
    if core_source.count(bundled_anchor) != 1:
        raise SystemExit("Could not locate bundled Horizon language dispatch.")
    bundled_languages = ''.join(
        f'            "assets/horizon/lang/{code}.json" => include_str!("../../horizon-client/assets/horizon/lang/{code}.json"),\n'
        for code in ("pt_br", "fr_fr", "it_it", "de_de", "ja_jp")
    )
    core_source = core_source.replace(bundled_anchor, bundled_languages + bundled_anchor, 1)
    core_lang.write_text(core_source, encoding="utf-8")

    locale_translations = {
        "pt_br": {
            "horizon.pages.shader_packs": "Pacotes de shaders",
            "horizon.pages.shader_options": "Opções de shaders",
            "horizon.shader_options.profile": "Perfil",
            "Preset": "Predefinição", "LOD Distance": "Distância LOD",
            "Far Detail": "Detalhe distante", "Cave Culling": "Oclusão de cavernas",
            "Entity Culling": "Oclusão de entidades", "Chunk Updates": "Atualizações de chunks",
            "Upload Budget": "Limite de envio", "Sky": "Céu", "Fog": "Névoa",
            "Weather": "Clima", "Animated Textures": "Texturas animadas",
            "Present Mode": "Modo de apresentação", "Frame Pacing": "Ritmo de quadros",
            "Worker Threads": "Threads de trabalho", "Resolution Scale": "Escala de resolução",
            "Anti-aliasing": "Antisserrilhamento", "Upscaling": "Ampliação",
            "HDR Settings...": "Configurações HDR...", "Paper White": "Branco de referência",
            "Peak Brightness": "Brilho máximo", "Black Level": "Nível de preto",
            "UI Brightness": "Brilho da interface", "Battery Mode": "Modo de bateria",
            "Graphics API": "API gráfica", "Performance Overlay": "Sobreposição de desempenho",
            "Full Detail Distance": "Distância de detalhe completo", "On": "Ligado",
            "Off": "Desligado", "Unlimited": "Ilimitado", "Auto": "Automático",
            "Custom": "Personalizado", "Performance": "Desempenho", "Balanced": "Equilibrado",
            "Quality": "Qualidade", "Ultra Performance": "Desempenho ultra",
            "Native": "Nativo", "Full": "Completo", "Reduced": "Reduzido",
            "Efficient": "Eficiente", "Precise": "Preciso", "Auto (VSync)": "Automático (VSync)",
            "Immediate": "Imediato", "No cost": "Sem custo", "Low cost": "Custo baixo",
            "Medium cost": "Custo médio", "High cost": "Custo alto", "Saves time": "Economiza tempo",
            "Rendering": "Renderização", "Display": "Tela", "Preferences": "Preferências",
            "Overlay": "Sobreposição", "HDR": "HDR", "Graphics": "Gráficos",
            "Graphics: Advanced": "Gráficos: avançado", "Graphics: HDR": "Gráficos: HDR",
            "Advanced...": "Avançado...", "Simple...": "Simples...", "Done": "Concluído"
        },
        "fr_fr": {
            "horizon.pages.shader_packs": "Packs de shaders",
            "horizon.pages.shader_options": "Options des shaders",
            "horizon.shader_options.profile": "Profil",
            "Preset": "Préréglage", "LOD Distance": "Distance LOD",
            "Far Detail": "Détails lointains", "Cave Culling": "Occlusion des grottes",
            "Entity Culling": "Occlusion des entités", "Chunk Updates": "Mises à jour des chunks",
            "Upload Budget": "Budget de transfert", "Sky": "Ciel", "Fog": "Brouillard",
            "Weather": "Météo", "Animated Textures": "Textures animées",
            "Present Mode": "Mode de présentation", "Frame Pacing": "Rythme des images",
            "Worker Threads": "Threads de travail", "Resolution Scale": "Échelle de résolution",
            "Anti-aliasing": "Anticrénelage", "Upscaling": "Mise à l’échelle",
            "HDR Settings...": "Paramètres HDR...", "Paper White": "Blanc papier",
            "Peak Brightness": "Luminosité maximale", "Black Level": "Niveau de noir",
            "UI Brightness": "Luminosité de l’interface", "Battery Mode": "Mode batterie",
            "Graphics API": "API graphique", "Performance Overlay": "Superposition des performances",
            "Full Detail Distance": "Distance de détail complet", "On": "Activé",
            "Off": "Désactivé", "Unlimited": "Illimité", "Auto": "Automatique",
            "Custom": "Personnalisé", "Performance": "Performances", "Balanced": "Équilibré",
            "Quality": "Qualité", "Ultra Performance": "Performances ultra",
            "Native": "Natif", "Full": "Complet", "Reduced": "Réduit",
            "Efficient": "Efficace", "Precise": "Précis", "Auto (VSync)": "Automatique (VSync)",
            "Immediate": "Immédiat", "No cost": "Sans coût", "Low cost": "Coût faible",
            "Medium cost": "Coût moyen", "High cost": "Coût élevé", "Saves time": "Économise du temps",
            "Rendering": "Rendu", "Display": "Affichage", "Preferences": "Préférences",
            "Overlay": "Superposition", "HDR": "HDR", "Graphics": "Graphismes",
            "Graphics: Advanced": "Graphismes : avancés", "Graphics: HDR": "Graphismes : HDR",
            "Advanced...": "Avancé...", "Simple...": "Simple...", "Done": "Terminé"
        },
        "it_it": {
            "horizon.pages.shader_packs": "Pacchetti shader",
            "horizon.pages.shader_options": "Opzioni shader",
            "horizon.shader_options.profile": "Profilo",
            "Preset": "Preimpostazione", "LOD Distance": "Distanza LOD",
            "Far Detail": "Dettaglio distante", "Cave Culling": "Occlusione caverne",
            "Entity Culling": "Occlusione entità", "Chunk Updates": "Aggiornamenti chunk",
            "Upload Budget": "Limite di caricamento", "Sky": "Cielo", "Fog": "Nebbia",
            "Weather": "Meteo", "Animated Textures": "Texture animate",
            "Present Mode": "Modalità presentazione", "Frame Pacing": "Ritmo dei fotogrammi",
            "Worker Threads": "Thread di lavoro", "Resolution Scale": "Scala di risoluzione",
            "Anti-aliasing": "Antialiasing", "Upscaling": "Upscaling",
            "HDR Settings...": "Impostazioni HDR...", "Paper White": "Bianco carta",
            "Peak Brightness": "Luminosità massima", "Black Level": "Livello del nero",
            "UI Brightness": "Luminosità interfaccia", "Battery Mode": "Modalità batteria",
            "Graphics API": "API grafica", "Performance Overlay": "Overlay prestazioni",
            "Full Detail Distance": "Distanza dettaglio completo", "On": "Attivo",
            "Off": "Disattivato", "Unlimited": "Illimitato", "Auto": "Automatico",
            "Custom": "Personalizzato", "Performance": "Prestazioni", "Balanced": "Bilanciato",
            "Quality": "Qualità", "Ultra Performance": "Prestazioni ultra",
            "Native": "Nativo", "Full": "Completo", "Reduced": "Ridotto",
            "Efficient": "Efficiente", "Precise": "Preciso", "Auto (VSync)": "Automatico (VSync)",
            "Immediate": "Immediato", "No cost": "Nessun costo", "Low cost": "Costo basso",
            "Medium cost": "Costo medio", "High cost": "Costo elevato", "Saves time": "Risparmia tempo",
            "Rendering": "Rendering", "Display": "Schermo", "Preferences": "Preferenze",
            "Overlay": "Overlay", "HDR": "HDR", "Graphics": "Grafica",
            "Graphics: Advanced": "Grafica: avanzata", "Graphics: HDR": "Grafica: HDR",
            "Advanced...": "Avanzate...", "Simple...": "Semplici...", "Done": "Fatto"
        },
        "de_de": {
            "horizon.pages.shader_packs": "Shader-Pakete",
            "horizon.pages.shader_options": "Shader-Optionen",
            "horizon.shader_options.profile": "Profil",
            "Preset": "Voreinstellung", "LOD Distance": "LOD-Entfernung",
            "Far Detail": "Ferne Details", "Cave Culling": "Höhlen-Ausblendung",
            "Entity Culling": "Objekt-Ausblendung", "Chunk Updates": "Chunk-Aktualisierungen",
            "Upload Budget": "Übertragungsbudget", "Sky": "Himmel", "Fog": "Nebel",
            "Weather": "Wetter", "Animated Textures": "Animierte Texturen",
            "Present Mode": "Präsentationsmodus", "Frame Pacing": "Frame-Taktung",
            "Worker Threads": "Arbeitsthreads", "Resolution Scale": "Auflösungsskalierung",
            "Anti-aliasing": "Kantenglättung", "Upscaling": "Hochskalierung",
            "HDR Settings...": "HDR-Einstellungen...", "Paper White": "Papierweiß",
            "Peak Brightness": "Spitzenhelligkeit", "Black Level": "Schwarzwert",
            "UI Brightness": "Oberflächenhelligkeit", "Battery Mode": "Akkumodus",
            "Graphics API": "Grafik-API", "Performance Overlay": "Leistungsanzeige",
            "Full Detail Distance": "Volle Detailentfernung", "On": "Ein",
            "Off": "Aus", "Unlimited": "Unbegrenzt", "Auto": "Automatisch",
            "Custom": "Benutzerdefiniert", "Performance": "Leistung", "Balanced": "Ausgewogen",
            "Quality": "Qualität", "Ultra Performance": "Ultraleistung",
            "Native": "Nativ", "Full": "Voll", "Reduced": "Reduziert",
            "Efficient": "Effizient", "Precise": "Präzise", "Auto (VSync)": "Automatisch (VSync)",
            "Immediate": "Sofort", "No cost": "Kostenlos", "Low cost": "Geringe Kosten",
            "Medium cost": "Mittlere Kosten", "High cost": "Hohe Kosten", "Saves time": "Spart Zeit",
            "Rendering": "Rendering", "Display": "Anzeige", "Preferences": "Einstellungen",
            "Overlay": "Overlay", "HDR": "HDR", "Graphics": "Grafik",
            "Graphics: Advanced": "Grafik: erweitert", "Graphics: HDR": "Grafik: HDR",
            "Advanced...": "Erweitert...", "Simple...": "Einfach...", "Done": "Fertig"
        },
        "ja_jp": {
            "horizon.pages.shader_packs": "シェーダーパック",
            "horizon.pages.shader_options": "シェーダー設定",
            "horizon.shader_options.profile": "プロファイル",
            "Preset": "プリセット", "LOD Distance": "LOD 距離",
            "Far Detail": "遠景の詳細", "Cave Culling": "洞窟のカリング",
            "Entity Culling": "エンティティのカリング", "Chunk Updates": "チャンク更新",
            "Upload Budget": "アップロード量", "Sky": "空", "Fog": "霧",
            "Weather": "天候", "Animated Textures": "アニメーションテクスチャ",
            "Present Mode": "表示モード", "Frame Pacing": "フレームペーシング",
            "Worker Threads": "ワーカースレッド", "Resolution Scale": "解像度スケール",
            "Anti-aliasing": "アンチエイリアス", "Upscaling": "アップスケーリング",
            "HDR Settings...": "HDR 設定...", "Paper White": "ペーパーホワイト",
            "Peak Brightness": "ピーク輝度", "Black Level": "黒レベル",
            "UI Brightness": "UI の明るさ", "Battery Mode": "バッテリーモード",
            "Graphics API": "グラフィックス API", "Performance Overlay": "パフォーマンス表示",
            "Full Detail Distance": "詳細描画距離", "On": "オン", "Off": "オフ",
            "Unlimited": "無制限", "Auto": "自動", "Custom": "カスタム",
            "Performance": "パフォーマンス", "Balanced": "バランス",
            "Quality": "品質", "Ultra Performance": "ウルトラパフォーマンス",
            "Native": "ネイティブ", "Full": "最大", "Reduced": "低減",
            "Efficient": "効率的", "Precise": "高精度", "Auto (VSync)": "自動 (VSync)",
            "Immediate": "即時", "No cost": "負荷なし", "Low cost": "低負荷",
            "Medium cost": "中負荷", "High cost": "高負荷", "Saves time": "時間を節約",
            "Rendering": "レンダリング", "Display": "ディスプレイ", "Preferences": "設定",
            "Overlay": "オーバーレイ", "HDR": "HDR", "Graphics": "グラフィックス",
            "Graphics: Advanced": "グラフィックス：詳細", "Graphics: HDR": "グラフィックス：HDR",
            "Advanced...": "詳細...", "Simple...": "シンプル...", "Done": "完了"
        }
    }
    for locale, translations in locale_translations.items():
        locale_file = stage / f"crates/horizon-client/assets/horizon/lang/{locale}.json"
        locale_file.write_text(
            json.dumps(translations, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    spanish_file = stage / "crates/horizon-client/assets/horizon/lang/es_es.json"
    spanish = json.loads(spanish_file.read_text(encoding="utf-8"))
    spanish.update({
        "horizon.pages.shader_options": "Opciones de shaders",
        "horizon.shader_options.profile": "Perfil",
        "horizon.shader_options.loading": "Leyendo las opciones del paquete de shaders...",
        "horizon.shader_options.open": "Opciones de shaders...",
    })
    spanish_file.write_text(
        json.dumps(spanish, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    shader_options = stage / "crates/horizon-client-render/src/shader_options.rs"
    shader_source = shader_options.read_text(encoding="utf-8")
    named_anchor = '        Text::legacy(self.lang.get(key).unwrap_or(or))\n'
    if shader_source.count(named_anchor) != 1:
        raise SystemExit("Could not locate shader-pack label fallback.")
    shader_source = shader_source.replace(
        named_anchor,
        '        let translated = self.lang.get(key).map(str::to_owned).unwrap_or_else(|| crate::lang::get_or(or, or));\n        Text::legacy(&translated)\n',
        1,
    )
    value_anchor = '''            let text = self
                .lang
                .get(&format!("value.{name}.{value}"))
                .unwrap_or(value);
'''
    if shader_source.count(value_anchor) != 1:
        raise SystemExit("Could not locate shader-pack option value fallback.")
    shader_source = shader_source.replace(
        value_anchor,
        '''            let text = self
                .lang
                .get(&format!("value.{name}.{value}"))
                .map(str::to_owned)
                .unwrap_or_else(|| crate::lang::get_or(value, value));
''',
        1,
    )
    shader_options.write_text(shader_source, encoding="utf-8")

    pages = stage / "crates/horizon-client/src/android/launcher_pages.rs"
    source = pages.read_text(encoding="utf-8")
    action_variant_anchor = "    Page(Page),\n"
    if source.count(action_variant_anchor) != 1:
        raise SystemExit("Could not uniquely locate the Page variant in the Android launcher Action enum.")
    source = source.replace(
        action_variant_anchor,
        "    CycleLanguage,\n" + action_variant_anchor,
        1,
    )
    action_enum_start = source.find("pub(super) enum Action {")
    action_page_variant = source.find("    Page(Page),", action_enum_start)
    if action_enum_start < 0 or action_page_variant < 0:
        raise SystemExit("Could not validate the Android launcher Action enum.")
    if "CycleLanguage," not in source[action_enum_start:action_page_variant]:
        raise SystemExit("CycleLanguage was not inserted into the Android launcher Action enum.")
    patches = [
        (
            "                let count = 12;\n",
            "                let count = 13;\n",
            "Android launcher settings row count",
        ),
        (
            "                let rows: [(&str, &str, SettingControl<'_>); 12] = [\n",
            "                let rows: [(&str, &str, SettingControl<'_>); 13] = [\n",
            "Android launcher settings row array",
        ),
        (
            '''                    (
                        "Open game & world folders",
                        "Manage saves, resource packs and mods in Android Files",
                        SettingControl::Button("Open Files", Action::GameFiles, false),
                    ),
                ];''',
            '''                    (
                        "Open game & world folders",
                        "Manage saves, resource packs and mods in Android Files",
                        SettingControl::Button("Open Files", Action::GameFiles, false),
                    ),
                    (
                        match c.language.as_str() {
                            "es_es" => "Idioma",
                            "pt_br" => "Idioma",
                            "fr_fr" => "Langue",
                            "it_it" => "Lingua",
                            "de_de" => "Sprache",
                            "ja_jp" => "言語",
                            "zh_cn" => "语言",
                            _ => "Language",
                        },
                        match c.language.as_str() {
                            "es_es" => "Cambia el idioma del launcher y del juego",
                            "pt_br" => "Altera o idioma do launcher e do jogo",
                            "fr_fr" => "Change la langue du launcher et du jeu",
                            "it_it" => "Cambia la lingua del launcher e del gioco",
                            "de_de" => "Sprache von Launcher und Spiel ändern",
                            "ja_jp" => "ランチャーとゲームの言語を変更",
                            "zh_cn" => "更改启动器和游戏语言",
                            _ => "Change the launcher and in-game language",
                        },
                        SettingControl::Button(
                            match c.language.as_str() {
                                "es_es" => "Español v",
                                "pt_br" => "Português v",
                                "fr_fr" => "Français v",
                                "it_it" => "Italiano v",
                                "de_de" => "Deutsch v",
                                "ja_jp" => "日本語 v",
                                "zh_cn" => "简体中文 v",
                                _ => "English v",
                            },
                            Action::CycleLanguage,
                            false,
                        ),
                    ),
                ];''',
            "Android launcher language selector row",
        ),
        (
            "            Some(Action::UiScale) => self.open_chooser(chooser::Kind::UiScale),\n",
            '''            Some(Action::CycleLanguage) => {
                const LANGUAGES: [&str; 8] = [
                    "en_us", "es_es", "pt_br", "fr_fr",
                    "it_it", "de_de", "ja_jp", "zh_cn",
                ];
                let current = self.catalog.config.language.as_str();
                let index = LANGUAGES.iter().position(|code| *code == current).unwrap_or(0);
                let next = LANGUAGES[(index + 1) % LANGUAGES.len()];
                self.catalog.config.language = next.into();
                self.catalog.config.language_auto = false;
                self.modify_game_settings(|settings| settings.language = next.into());
                self.catalog.config.save(&self.home)?;
                self.catalog.status = match next {
                    "es_es" => "Idioma cambiado. Reinicia el juego para aplicar el cambio.".into(),
                    "pt_br" => "Idioma alterado. Reinicie o jogo para aplicar.".into(),
                    "fr_fr" => "Langue modifiée. Redémarrez le jeu pour appliquer le changement.".into(),
                    "it_it" => "Lingua modificata. Riavvia il gioco per applicare la modifica.".into(),
                    "de_de" => "Sprache geändert. Starte das Spiel neu, um die Änderung anzuwenden.".into(),
                    "ja_jp" => "言語を変更しました。適用するにはゲームを再起動してください。".into(),
                    "zh_cn" => "语言已更改。请重新启动游戏以应用更改。".into(),
                    _ => "Language changed. Restart the game to apply it.".into(),
                };
            }
            Some(Action::UiScale) => self.open_chooser(chooser::Kind::UiScale),
''',
            "Android language selector action",
        ),
    ]
    for old, new, label in patches:
        count = source.count(old)
        if count != 1:
            raise SystemExit(f"Could not uniquely locate {label} (found {count}).")
        source = source.replace(old, new, 1)

    # Put the language selector first so it is visible without scrolling.
    language_row_start = source.find("                        match c.language.as_str() {")
    if language_row_start < 0:
        raise SystemExit("Could not find the injected launcher language row.")
    language_row_start = source.rfind("                    (", 0, language_row_start)
    language_row_end_marker = """                            Action::CycleLanguage,
                            false,
                        ),
                    ),"""
    language_row_end = source.find(language_row_end_marker, language_row_start)
    if language_row_start < 0 or language_row_end < 0:
        raise SystemExit("Could not find the complete launcher language row.")
    language_row_end += len(language_row_end_marker)
    language_row = source[language_row_start:language_row_end] + "\n"
    source = source[:language_row_start] + source[language_row_end:]
    rows_anchor = "                let rows: [(&str, &str, SettingControl<'_>); 13] = [" + "\n"
    if source.count(rows_anchor) != 1:
        raise SystemExit("Could not locate the launcher settings row array to place the language selector first.")
    source = source.replace(rows_anchor, rows_anchor + language_row, 1)
    pages.write_text(source, encoding="utf-8")

    ui_theme = stage / "crates/horizon-client/src/ui_theme.rs"
    source = ui_theme.read_text(encoding="utf-8")
    import_anchor = "use std::{cell::RefCell, collections::HashMap, path::Path};\n"
    if source.count(import_anchor) != 1:
        raise SystemExit("Could not locate Horizon UI theme imports.")
    source = source.replace(
        import_anchor,
        "use std::{cell::{Cell, RefCell}, collections::HashMap, path::Path};\n",
        1,
    )
    ui_anchor = "pub struct Ui<'a> {\n"
    language_ui = r"""
thread_local! {
    static PAVO_LAUNCHER_LANGUAGE: Cell<u8> = const { Cell::new(0) };
}

pub fn set_launcher_language(language: &str) {
    let index = match language {
        "es_es" => 1, "pt_br" => 2, "fr_fr" => 3, "it_it" => 4,
        "de_de" => 5, "ja_jp" => 6, "zh_cn" => 7, _ => 0,
    };
    PAVO_LAUNCHER_LANGUAGE.with(|current| current.set(index));
}

fn pavo_content_library_text(lang: u8, text: &str) -> Option<String> {
    let exact = match (lang, text) {
            (1, "Content Library") => Some("Biblioteca de contenido"),
            (1, "Profile File Manager") => Some("Administrador de archivos del perfil"),
            (1, "Library") => Some("Biblioteca"),
            (1, "File Manager") => Some("Administrador de archivos"),
            (1, "Search installed mods, packs & shaders...") => Some("Buscar mods, paquetes y shaders instalados..."),
            (1, "+ Import") => Some("+ Importar"),
            (1, "Packs") => Some("Paquetes"),
            (1, "Sort: Name") => Some("Ordenar: nombre"),
            (1, "Sort: Type") => Some("Ordenar: tipo"),
            (1, "Sort: Size") => Some("Ordenar: tamaño"),
            (1, "Sort: Status") => Some("Ordenar: estado"),
            (1, "No local add-ons installed yet") => Some("Todavía no hay complementos locales instalados"),
            (1, "No installed add-ons match your filter") => Some("Ningún complemento instalado coincide con el filtro"),
            (1, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some("Importa mods .hmod/.jar, paquetes de recursos .zip o shaders; también puedes explorar los archivos."),
            (1, "Import Mod") => Some("Importar mod"),
            (1, "Import Pack") => Some("Importar paquete"),
            (1, "ACTIVE") => Some("ACTIVO"),
            (1, "OFF") => Some("DESACTIVADO"),
            (1, "Export") => Some("Exportar"),
            (1, "Delete") => Some("Eliminar"),
            (1, "Import") => Some("Importar"),
            (1, "+ Folder") => Some("+ Carpeta"),
            (1, "Paste") => Some("Pegar"),
            (1, "Clear") => Some("Limpiar"),
            (1, "Copy") => Some("Copiar"),
            (1, "Cut") => Some("Cortar"),
            (1, "Rename") => Some("Renombrar"),
            (1, "This folder is empty") => Some("Esta carpeta está vacía"),
            (1, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("Usa Importar para copiar archivos del almacenamiento, + Carpeta para crear una carpeta o Pegar para insertar desde el portapapeles."),
            (1, "Details") => Some("Detalles"),
            (1, "Root") => Some("Raíz"),
            (1, "Worlds") => Some("Mundos"),
            (1, "Controls") => Some("Controles"),
            (1, "Shots") => Some("Capturas"),
            (1, "Reports") => Some("Informes"),
            (1, "Select one or more files or folders first.") => Some("Primero selecciona uno o más archivos o carpetas."),
            (1, "Delete selected item?") => Some("¿Eliminar el elemento seleccionado?"),
            (1, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("Esto eliminará definitivamente los archivos seleccionados de este perfil. Esta acción no se puede deshacer."),
            (1, "Cancel") => Some("Cancelar"),
            (1, "Confirm Delete") => Some("Confirmar eliminación"),
            (1, "Rename File or Folder") => Some("Renombrar archivo o carpeta"),
            (1, "NEW NAME") => Some("NOMBRE NUEVO"),
            (1, "Enter new filename") => Some("Escribe el nombre del archivo"),
            (1, "Save Name") => Some("Guardar nombre"),
            (1, "Create New Folder") => Some("Crear carpeta nueva"),
            (1, "Skins & Capes Studio") => Some("Estudio de aspectos y capas"),
            (1, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("Previsualiza capas 3D, administra capas locales o activa las capas de tu cuenta en la nube."),
            (1, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("Examina tu personaje 3D, cambia el modelo de brazos y administra aspectos locales o en la nube."),
            (1, "Capes") => Some("Capas"),
            (1, "Cape Hidden") => Some("Capa oculta"),
            (1, "3D Cape View") => Some("Vista de capa 3D"),
            (1, "Slim (3px Arms)") => Some("Delgado (brazos de 3 px)"),
            (1, "Classic (4px Arms)") => Some("Clásico (brazos de 4 px)"),
            (1, "Loading default skin...") => Some("Cargando aspecto predeterminado..."),
            (1, "Default skin unavailable offline") => Some("Aspecto predeterminado no disponible sin conexión"),
            (1, "Cape Visibility: Hidden (Tap to Show)") => Some("Capa oculta (toca para mostrar)"),
            (1, "Cape Visibility: Shown (Tap to Hide)") => Some("Capa visible (toca para ocultar)"),
            (1, "Arm Model: Alex / Slim (3px)") => Some("Modelo de brazos: Alex / Delgado (3 px)"),
            (1, "Arm Model: Steve / Classic (4px)") => Some("Modelo de brazos: Steve / Clásico (4 px)"),
            (1, "Active Local Override") => Some("Modificación local activa"),
            (1, "Cape Hidden Locally") => Some("Capa oculta localmente"),
            (1, "Account Cloud Cape") => Some("Capa de la cuenta en la nube"),
            (1, "No Cape Equipped") => Some("Sin capa equipada"),
            (1, "Default Appearance (Steve / Classic)") => Some("Apariencia predeterminada (Steve / Clásico)"),
            (1, "Synchronizing with account service...") => Some("Sincronizando con el servicio de la cuenta..."),
            (1, "Waiting for Android file picker...") => Some("Esperando el selector de archivos de Android..."),
            (1, "Import a 64×32 PNG cape or select below") => Some("Importa una capa PNG de 64×32 o selecciona una de abajo"),
            (1, "Import a 64×64 PNG skin or select below") => Some("Importa un aspecto PNG de 64×64 o selecciona uno de abajo"),
            (1, "+ Import Cape PNG") => Some("+ Importar capa PNG"),
            (1, "+ Import Skin PNG") => Some("+ Importar aspecto PNG"),
            (1, "Active") => Some("Activo"),
            (1, "Apply") => Some("Aplicar"),
            (1, "Cloud Cape · Equipped on profile") => Some("Capa en la nube · Equipada en el perfil"),
            (1, "Cloud Cape · Tap to activate") => Some("Capa en la nube · Toca para activar"),
            (1, "Local Gallery · Currently applied") => Some("Galería local · Aplicado actualmente"),
            (1, "Local Cape PNG · Tap to equip") => Some("Capa PNG local · Toca para equipar"),
            (1, "Local Skin PNG · Tap to equip") => Some("Aspecto PNG local · Toca para equipar"),
            (1, "No custom capes in your gallery yet") => Some("Todavía no tienes capas personalizadas en la galería"),
            (1, "No custom skins in your gallery yet") => Some("Todavía no tienes aspectos personalizados en la galería"),
            (1, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("Toca «+ Importar capa PNG» para añadir una textura de capa 64×32 desde el dispositivo."),
            (1, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("Toca «+ Importar aspecto PNG» para añadir una textura de aspecto 64×64 desde el dispositivo."),
            (1, "Clear Local Cape Override") => Some("Quitar modificación de capa local"),
            (1, "Reset to Default Appearance Locally") => Some("Restablecer apariencia predeterminada local"),
            (1, "Use Cloud Appearance") => Some("Usar apariencia de la nube"),
            (1, "Syncing Cloud...") => Some("Sincronizando con la nube..."),
            (1, "Refresh Cloud Profile") => Some("Actualizar perfil de la nube"),
            (1, "Upload Selected Skin to Microsoft Profile") => Some("Subir el aspecto seleccionado al perfil de Microsoft"),
            (1, "Manage Ely.by Cloud Skin on Website") => Some("Administrar aspecto de Ely.by en el sitio web"),
            (1, "Applied locally. The account's cloud profile is unchanged.") => Some("Aplicado localmente. El perfil de la cuenta en la nube no se modificó."),
            (1, "Choose a PNG image in Android Files.") => Some("Selecciona una imagen PNG en Archivos de Android."),
            (1, "Using the account's cloud appearance. No cloud changes were made.") => Some("Usando la apariencia de la cuenta en la nube. No se realizaron cambios en la nube."),
            (1, "Local override cleared. Using the default appearance.") => Some("Se quitó la modificación local. Se usa la apariencia predeterminada."),
            (1, "Offline appearance is local to this installation.") => Some("La apariencia sin conexión es local de esta instalación."),
            (1, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Los aspectos en la nube de Ely.by se administran en ely.by. Las capas personalizadas pueden aplicarse localmente."),
            (1, "Select a local skin before uploading to your Microsoft profile.") => Some("Selecciona un aspecto local antes de subirlo a tu perfil de Microsoft."),
            (1, "Contacting account service...") => Some("Contactando con el servicio de la cuenta..."),
            (1, "Sign in before synchronizing your cloud appearance.") => Some("Inicia sesión antes de sincronizar tu apariencia en la nube."),
            (1, "The selected account changed. Open the gallery again.") => Some("La cuenta seleccionada cambió. Abre la galería de nuevo."),
            (1, "Select a skin first.") => Some("Primero selecciona un aspecto."),
            (2, "Content Library") => Some("Biblioteca de conteúdo"),
            (2, "Profile File Manager") => Some("Gerenciador de arquivos do perfil"),
            (2, "Library") => Some("Biblioteca"),
            (2, "File Manager") => Some("Gerenciador de arquivos"),
            (2, "Search installed mods, packs & shaders...") => Some("Pesquisar mods, pacotes e shaders instalados..."),
            (2, "+ Import") => Some("+ Importar"),
            (2, "Packs") => Some("Pacotes"),
            (2, "Sort: Name") => Some("Ordenar: nome"),
            (2, "Sort: Type") => Some("Ordenar: tipo"),
            (2, "Sort: Size") => Some("Ordenar: tamanho"),
            (2, "Sort: Status") => Some("Ordenar: status"),
            (2, "No local add-ons installed yet") => Some("Ainda não há complementos locais instalados"),
            (2, "No installed add-ons match your filter") => Some("Nenhum complemento instalado corresponde ao filtro"),
            (2, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some("Importe mods .hmod/.jar, pacotes de recursos .zip ou shaders; ou explore os arquivos."),
            (2, "Import Mod") => Some("Importar mod"),
            (2, "Import Pack") => Some("Importar pacote"),
            (2, "ACTIVE") => Some("ATIVO"),
            (2, "OFF") => Some("DESATIVADO"),
            (2, "Export") => Some("Exportar"),
            (2, "Delete") => Some("Excluir"),
            (2, "Import") => Some("Importar"),
            (2, "+ Folder") => Some("+ Pasta"),
            (2, "Paste") => Some("Colar"),
            (2, "Clear") => Some("Limpar"),
            (2, "Copy") => Some("Copiar"),
            (2, "Cut") => Some("Recortar"),
            (2, "Rename") => Some("Renomear"),
            (2, "This folder is empty") => Some("Esta pasta está vazia"),
            (2, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("Use Importar para copiar arquivos do armazenamento, + Pasta para criar uma pasta ou Colar para inserir da área de transferência."),
            (2, "Details") => Some("Detalhes"),
            (2, "Root") => Some("Raiz"),
            (2, "Worlds") => Some("Mundos"),
            (2, "Controls") => Some("Controles"),
            (2, "Shots") => Some("Capturas"),
            (2, "Reports") => Some("Relatórios"),
            (2, "Select one or more files or folders first.") => Some("Primeiro selecione um ou mais arquivos ou pastas."),
            (2, "Delete selected item?") => Some("Excluir o item selecionado?"),
            (2, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("Isso excluirá permanentemente os arquivos selecionados deste perfil. Esta ação não pode ser desfeita."),
            (2, "Cancel") => Some("Cancelar"),
            (2, "Confirm Delete") => Some("Confirmar exclusão"),
            (2, "Rename File or Folder") => Some("Renomear arquivo ou pasta"),
            (2, "NEW NAME") => Some("NOVO NOME"),
            (2, "Enter new filename") => Some("Digite o nome do arquivo"),
            (2, "Save Name") => Some("Salvar nome"),
            (2, "Create New Folder") => Some("Criar nova pasta"),
            (2, "Skins & Capes Studio") => Some("Estúdio de visuais e capas"),
            (2, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("Visualize capas 3D, gerencie substituições locais ou ative capas da conta na nuvem."),
            (2, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("Examine seu personagem 3D, altere o modelo dos braços e gerencie visuais locais ou na nuvem."),
            (2, "Capes") => Some("Capas"),
            (2, "Cape Hidden") => Some("Capa oculta"),
            (2, "3D Cape View") => Some("Visualização de capa 3D"),
            (2, "Slim (3px Arms)") => Some("Fino (braços de 3 px)"),
            (2, "Classic (4px Arms)") => Some("Clássico (braços de 4 px)"),
            (2, "Loading default skin...") => Some("Carregando visual padrão..."),
            (2, "Default skin unavailable offline") => Some("Visual padrão indisponível offline"),
            (2, "Cape Visibility: Hidden (Tap to Show)") => Some("Capa oculta (toque para mostrar)"),
            (2, "Cape Visibility: Shown (Tap to Hide)") => Some("Capa visível (toque para ocultar)"),
            (2, "Arm Model: Alex / Slim (3px)") => Some("Modelo dos braços: Alex / Fino (3 px)"),
            (2, "Arm Model: Steve / Classic (4px)") => Some("Modelo dos braços: Steve / Clássico (4 px)"),
            (2, "Active Local Override") => Some("Substituição local ativa"),
            (2, "Cape Hidden Locally") => Some("Capa oculta localmente"),
            (2, "Account Cloud Cape") => Some("Capa da conta na nuvem"),
            (2, "No Cape Equipped") => Some("Nenhuma capa equipada"),
            (2, "Default Appearance (Steve / Classic)") => Some("Aparência padrão (Steve / Clássico)"),
            (2, "Synchronizing with account service...") => Some("Sincronizando com o serviço da conta..."),
            (2, "Waiting for Android file picker...") => Some("Aguardando o seletor de arquivos do Android..."),
            (2, "Import a 64×32 PNG cape or select below") => Some("Importe uma capa PNG 64×32 ou selecione abaixo"),
            (2, "Import a 64×64 PNG skin or select below") => Some("Importe um visual PNG 64×64 ou selecione abaixo"),
            (2, "+ Import Cape PNG") => Some("+ Importar capa PNG"),
            (2, "+ Import Skin PNG") => Some("+ Importar visual PNG"),
            (2, "Active") => Some("Ativo"),
            (2, "Apply") => Some("Aplicar"),
            (2, "Cloud Cape · Equipped on profile") => Some("Capa na nuvem · Equipada no perfil"),
            (2, "Cloud Cape · Tap to activate") => Some("Capa na nuvem · Toque para ativar"),
            (2, "Local Gallery · Currently applied") => Some("Galeria local · Aplicado atualmente"),
            (2, "Local Cape PNG · Tap to equip") => Some("Capa PNG local · Toque para equipar"),
            (2, "Local Skin PNG · Tap to equip") => Some("Visual PNG local · Toque para equipar"),
            (2, "No custom capes in your gallery yet") => Some("Você ainda não tem capas personalizadas na galeria"),
            (2, "No custom skins in your gallery yet") => Some("Você ainda não tem visuais personalizados na galeria"),
            (2, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("Toque em «+ Importar capa PNG» para adicionar uma textura de capa 64×32."),
            (2, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("Toque em «+ Importar visual PNG» para adicionar uma textura 64×64."),
            (2, "Clear Local Cape Override") => Some("Remover substituição local da capa"),
            (2, "Reset to Default Appearance Locally") => Some("Restaurar aparência padrão localmente"),
            (2, "Use Cloud Appearance") => Some("Usar aparência da nuvem"),
            (2, "Syncing Cloud...") => Some("Sincronizando com a nuvem..."),
            (2, "Refresh Cloud Profile") => Some("Atualizar perfil da nuvem"),
            (2, "Upload Selected Skin to Microsoft Profile") => Some("Enviar visual selecionado para o perfil Microsoft"),
            (2, "Manage Ely.by Cloud Skin on Website") => Some("Gerenciar visual da nuvem Ely.by no site"),
            (2, "Applied locally. The account's cloud profile is unchanged.") => Some("Aplicado localmente. O perfil da conta na nuvem não foi alterado."),
            (2, "Choose a PNG image in Android Files.") => Some("Escolha uma imagem PNG nos Arquivos do Android."),
            (2, "Using the account's cloud appearance. No cloud changes were made.") => Some("Usando a aparência da conta na nuvem. Nenhuma alteração foi feita."),
            (2, "Local override cleared. Using the default appearance.") => Some("Substituição local removida. Usando a aparência padrão."),
            (2, "Offline appearance is local to this installation.") => Some("A aparência offline é local desta instalação."),
            (2, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Os visuais da nuvem Ely.by são gerenciados em ely.by. Capas personalizadas podem ser aplicadas localmente."),
            (2, "Select a local skin before uploading to your Microsoft profile.") => Some("Selecione um visual local antes de enviá-lo ao perfil Microsoft."),
            (2, "Contacting account service...") => Some("Contatando o serviço da conta..."),
            (2, "Sign in before synchronizing your cloud appearance.") => Some("Entre na conta antes de sincronizar sua aparência na nuvem."),
            (2, "The selected account changed. Open the gallery again.") => Some("A conta selecionada mudou. Abra a galeria novamente."),
            (2, "Select a skin first.") => Some("Primeiro selecione um visual."),
            (3, "Content Library") => Some("Bibliothèque de contenu"),
            (3, "Profile File Manager") => Some("Gestionnaire de fichiers du profil"),
            (3, "Library") => Some("Bibliothèque"),
            (3, "File Manager") => Some("Gestionnaire de fichiers"),
            (3, "Search installed mods, packs & shaders...") => Some("Rechercher les mods, packs et shaders installés..."),
            (3, "+ Import") => Some("+ Importer"),
            (3, "Packs") => Some("Packs"),
            (3, "Sort: Name") => Some("Trier : nom"),
            (3, "Sort: Type") => Some("Trier : type"),
            (3, "Sort: Size") => Some("Trier : taille"),
            (3, "Sort: Status") => Some("Trier : statut"),
            (3, "No local add-ons installed yet") => Some("Aucun module local installé pour le moment"),
            (3, "No installed add-ons match your filter") => Some("Aucun contenu installé ne correspond au filtre"),
            (3, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some("Importez des mods .hmod/.jar, des packs de ressources .zip ou des shaders, ou parcourez les fichiers."),
            (3, "Import Mod") => Some("Importer un mod"),
            (3, "Import Pack") => Some("Importer un pack"),
            (3, "ACTIVE") => Some("ACTIF"),
            (3, "OFF") => Some("DÉSACTIVÉ"),
            (3, "Export") => Some("Exporter"),
            (3, "Delete") => Some("Supprimer"),
            (3, "Import") => Some("Importer"),
            (3, "+ Folder") => Some("+ Dossier"),
            (3, "Paste") => Some("Coller"),
            (3, "Clear") => Some("Effacer"),
            (3, "Copy") => Some("Copier"),
            (3, "Cut") => Some("Couper"),
            (3, "Rename") => Some("Renommer"),
            (3, "This folder is empty") => Some("Ce dossier est vide"),
            (3, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("Utilisez Importer pour copier des fichiers, + Dossier pour créer un dossier ou Coller depuis le presse-papiers."),
            (3, "Details") => Some("Détails"),
            (3, "Root") => Some("Racine"),
            (3, "Worlds") => Some("Mondes"),
            (3, "Controls") => Some("Commandes"),
            (3, "Shots") => Some("Captures"),
            (3, "Reports") => Some("Rapports"),
            (3, "Select one or more files or folders first.") => Some("Sélectionnez d’abord un ou plusieurs fichiers ou dossiers."),
            (3, "Delete selected item?") => Some("Supprimer l’élément sélectionné ?"),
            (3, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("Cette action supprime définitivement les fichiers sélectionnés de ce profil et ne peut pas être annulée."),
            (3, "Cancel") => Some("Annuler"),
            (3, "Confirm Delete") => Some("Confirmer la suppression"),
            (3, "Rename File or Folder") => Some("Renommer le fichier ou le dossier"),
            (3, "NEW NAME") => Some("NOUVEAU NOM"),
            (3, "Enter new filename") => Some("Saisissez le nom du fichier"),
            (3, "Save Name") => Some("Enregistrer le nom"),
            (3, "Create New Folder") => Some("Créer un dossier"),
            (3, "Skins & Capes Studio") => Some("Atelier des skins et capes"),
            (3, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("Prévisualisez les capes 3D, gérez les remplacements locaux ou activez les capes du compte cloud."),
            (3, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("Examinez votre personnage 3D, changez le modèle des bras et gérez les skins locaux ou cloud."),
            (3, "Capes") => Some("Capes"),
            (3, "Cape Hidden") => Some("Cape masquée"),
            (3, "3D Cape View") => Some("Aperçu de cape 3D"),
            (3, "Slim (3px Arms)") => Some("Fin (bras de 3 px)"),
            (3, "Classic (4px Arms)") => Some("Classique (bras de 4 px)"),
            (3, "Loading default skin...") => Some("Chargement du skin par défaut..."),
            (3, "Default skin unavailable offline") => Some("Skin par défaut indisponible hors ligne"),
            (3, "Cape Visibility: Hidden (Tap to Show)") => Some("Cape masquée (touchez pour afficher)"),
            (3, "Cape Visibility: Shown (Tap to Hide)") => Some("Cape affichée (touchez pour masquer)"),
            (3, "Arm Model: Alex / Slim (3px)") => Some("Modèle des bras : Alex / Fin (3 px)"),
            (3, "Arm Model: Steve / Classic (4px)") => Some("Modèle des bras : Steve / Classique (4 px)"),
            (3, "Active Local Override") => Some("Remplacement local actif"),
            (3, "Cape Hidden Locally") => Some("Cape masquée localement"),
            (3, "Account Cloud Cape") => Some("Cape cloud du compte"),
            (3, "No Cape Equipped") => Some("Aucune cape équipée"),
            (3, "Default Appearance (Steve / Classic)") => Some("Apparence par défaut (Steve / Classique)"),
            (3, "Synchronizing with account service...") => Some("Synchronisation avec le service du compte..."),
            (3, "Waiting for Android file picker...") => Some("En attente du sélecteur de fichiers Android..."),
            (3, "Import a 64×32 PNG cape or select below") => Some("Importez une cape PNG 64×32 ou sélectionnez-en une ci-dessous"),
            (3, "Import a 64×64 PNG skin or select below") => Some("Importez un skin PNG 64×64 ou sélectionnez-en un ci-dessous"),
            (3, "+ Import Cape PNG") => Some("+ Importer une cape PNG"),
            (3, "+ Import Skin PNG") => Some("+ Importer un skin PNG"),
            (3, "Active") => Some("Actif"),
            (3, "Apply") => Some("Appliquer"),
            (3, "Cloud Cape · Equipped on profile") => Some("Cape cloud · Équipée sur le profil"),
            (3, "Cloud Cape · Tap to activate") => Some("Cape cloud · Touchez pour activer"),
            (3, "Local Gallery · Currently applied") => Some("Galerie locale · Actuellement appliqué"),
            (3, "Local Cape PNG · Tap to equip") => Some("Cape PNG locale · Touchez pour équiper"),
            (3, "Local Skin PNG · Tap to equip") => Some("Skin PNG local · Touchez pour équiper"),
            (3, "No custom capes in your gallery yet") => Some("Aucune cape personnalisée dans votre galerie"),
            (3, "No custom skins in your gallery yet") => Some("Aucun skin personnalisé dans votre galerie"),
            (3, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("Touchez « + Importer une cape PNG » pour ajouter une texture 64×32."),
            (3, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("Touchez « + Importer un skin PNG » pour ajouter une texture 64×64."),
            (3, "Clear Local Cape Override") => Some("Effacer le remplacement de cape local"),
            (3, "Reset to Default Appearance Locally") => Some("Rétablir l’apparence locale par défaut"),
            (3, "Use Cloud Appearance") => Some("Utiliser l’apparence cloud"),
            (3, "Syncing Cloud...") => Some("Synchronisation du cloud..."),
            (3, "Refresh Cloud Profile") => Some("Actualiser le profil cloud"),
            (3, "Upload Selected Skin to Microsoft Profile") => Some("Envoyer le skin sélectionné au profil Microsoft"),
            (3, "Manage Ely.by Cloud Skin on Website") => Some("Gérer le skin cloud Ely.by sur le site"),
            (3, "Applied locally. The account's cloud profile is unchanged.") => Some("Appliqué localement. Le profil cloud du compte n’a pas été modifié."),
            (3, "Choose a PNG image in Android Files.") => Some("Choisissez une image PNG dans les fichiers Android."),
            (3, "Using the account's cloud appearance. No cloud changes were made.") => Some("Utilisation de l’apparence cloud du compte. Aucun changement cloud effectué."),
            (3, "Local override cleared. Using the default appearance.") => Some("Remplacement local effacé. Utilisation de l’apparence par défaut."),
            (3, "Offline appearance is local to this installation.") => Some("L’apparence hors ligne est locale à cette installation."),
            (3, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Les skins cloud Ely.by se gèrent sur ely.by. Les capes personnalisées peuvent être appliquées localement."),
            (3, "Select a local skin before uploading to your Microsoft profile.") => Some("Sélectionnez un skin local avant de l’envoyer à votre profil Microsoft."),
            (3, "Contacting account service...") => Some("Connexion au service du compte..."),
            (3, "Sign in before synchronizing your cloud appearance.") => Some("Connectez-vous avant de synchroniser votre apparence cloud."),
            (3, "The selected account changed. Open the gallery again.") => Some("Le compte sélectionné a changé. Rouvrez la galerie."),
            (3, "Select a skin first.") => Some("Sélectionnez d’abord un skin."),
            (4, "Content Library") => Some("Libreria dei contenuti"),
            (4, "Profile File Manager") => Some("Gestore file del profilo"),
            (4, "Library") => Some("Libreria"),
            (4, "File Manager") => Some("Gestore file"),
            (4, "Search installed mods, packs & shaders...") => Some("Cerca mod, pacchetti e shader installati..."),
            (4, "+ Import") => Some("+ Importa"),
            (4, "Packs") => Some("Pacchetti"),
            (4, "Sort: Name") => Some("Ordina: nome"),
            (4, "Sort: Type") => Some("Ordina: tipo"),
            (4, "Sort: Size") => Some("Ordina: dimensione"),
            (4, "Sort: Status") => Some("Ordina: stato"),
            (4, "No local add-ons installed yet") => Some("Nessun componente locale installato"),
            (4, "No installed add-ons match your filter") => Some("Nessun contenuto installato corrisponde al filtro"),
            (4, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some("Importa mod .hmod/.jar, pacchetti risorse .zip o shader, oppure esplora i file."),
            (4, "Import Mod") => Some("Importa mod"),
            (4, "Import Pack") => Some("Importa pacchetto"),
            (4, "ACTIVE") => Some("ATTIVO"),
            (4, "OFF") => Some("DISATTIVATO"),
            (4, "Export") => Some("Esporta"),
            (4, "Delete") => Some("Elimina"),
            (4, "Import") => Some("Importa"),
            (4, "+ Folder") => Some("+ Cartella"),
            (4, "Paste") => Some("Incolla"),
            (4, "Clear") => Some("Pulisci"),
            (4, "Copy") => Some("Copia"),
            (4, "Cut") => Some("Taglia"),
            (4, "Rename") => Some("Rinomina"),
            (4, "This folder is empty") => Some("Questa cartella è vuota"),
            (4, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("Usa Importa per copiare file, + Cartella per creare una directory o Incolla dagli appunti."),
            (4, "Details") => Some("Dettagli"),
            (4, "Root") => Some("Radice"),
            (4, "Worlds") => Some("Mondi"),
            (4, "Controls") => Some("Controlli"),
            (4, "Shots") => Some("Screenshot"),
            (4, "Reports") => Some("Rapporti"),
            (4, "Select one or more files or folders first.") => Some("Seleziona prima uno o più file o cartelle."),
            (4, "Delete selected item?") => Some("Eliminare l'elemento selezionato?"),
            (4, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("I file selezionati verranno eliminati definitivamente da questo profilo. L'operazione non può essere annullata."),
            (4, "Cancel") => Some("Annulla"),
            (4, "Confirm Delete") => Some("Conferma eliminazione"),
            (4, "Rename File or Folder") => Some("Rinomina file o cartella"),
            (4, "NEW NAME") => Some("NUOVO NOME"),
            (4, "Enter new filename") => Some("Inserisci il nome del file"),
            (4, "Save Name") => Some("Salva nome"),
            (4, "Create New Folder") => Some("Crea nuova cartella"),
            (4, "Skins & Capes Studio") => Some("Studio skin e mantelli"),
            (4, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("Visualizza mantelli 3D, gestisci sostituzioni locali o attiva i mantelli cloud dell'account."),
            (4, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("Esamina il personaggio 3D, cambia il modello delle braccia e gestisci skin locali o cloud."),
            (4, "Capes") => Some("Mantelli"),
            (4, "Cape Hidden") => Some("Mantello nascosto"),
            (4, "3D Cape View") => Some("Vista mantello 3D"),
            (4, "Slim (3px Arms)") => Some("Snello (braccia da 3 px)"),
            (4, "Classic (4px Arms)") => Some("Classico (braccia da 4 px)"),
            (4, "Loading default skin...") => Some("Caricamento skin predefinita..."),
            (4, "Default skin unavailable offline") => Some("Skin predefinita non disponibile offline"),
            (4, "Cape Visibility: Hidden (Tap to Show)") => Some("Mantello nascosto (tocca per mostrare)"),
            (4, "Cape Visibility: Shown (Tap to Hide)") => Some("Mantello visibile (tocca per nascondere)"),
            (4, "Arm Model: Alex / Slim (3px)") => Some("Modello braccia: Alex / Snello (3 px)"),
            (4, "Arm Model: Steve / Classic (4px)") => Some("Modello braccia: Steve / Classico (4 px)"),
            (4, "Active Local Override") => Some("Sostituzione locale attiva"),
            (4, "Cape Hidden Locally") => Some("Mantello nascosto localmente"),
            (4, "Account Cloud Cape") => Some("Mantello cloud dell'account"),
            (4, "No Cape Equipped") => Some("Nessun mantello equipaggiato"),
            (4, "Default Appearance (Steve / Classic)") => Some("Aspetto predefinito (Steve / Classico)"),
            (4, "Synchronizing with account service...") => Some("Sincronizzazione con il servizio account..."),
            (4, "Waiting for Android file picker...") => Some("In attesa del selettore file Android..."),
            (4, "Import a 64×32 PNG cape or select below") => Some("Importa un mantello PNG 64×32 o selezionalo qui sotto"),
            (4, "Import a 64×64 PNG skin or select below") => Some("Importa una skin PNG 64×64 o selezionala qui sotto"),
            (4, "+ Import Cape PNG") => Some("+ Importa mantello PNG"),
            (4, "+ Import Skin PNG") => Some("+ Importa skin PNG"),
            (4, "Active") => Some("Attivo"),
            (4, "Apply") => Some("Applica"),
            (4, "Cloud Cape · Equipped on profile") => Some("Mantello cloud · Equipaggiato sul profilo"),
            (4, "Cloud Cape · Tap to activate") => Some("Mantello cloud · Tocca per attivare"),
            (4, "Local Gallery · Currently applied") => Some("Galleria locale · Attualmente applicato"),
            (4, "Local Cape PNG · Tap to equip") => Some("Mantello PNG locale · Tocca per equipaggiare"),
            (4, "Local Skin PNG · Tap to equip") => Some("Skin PNG locale · Tocca per equipaggiare"),
            (4, "No custom capes in your gallery yet") => Some("Non ci sono ancora mantelli personalizzati nella galleria"),
            (4, "No custom skins in your gallery yet") => Some("Non ci sono ancora skin personalizzate nella galleria"),
            (4, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("Tocca «+ Importa mantello PNG» per aggiungere una texture 64×32."),
            (4, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("Tocca «+ Importa skin PNG» per aggiungere una texture 64×64."),
            (4, "Clear Local Cape Override") => Some("Rimuovi sostituzione mantello locale"),
            (4, "Reset to Default Appearance Locally") => Some("Ripristina aspetto predefinito locale"),
            (4, "Use Cloud Appearance") => Some("Usa aspetto cloud"),
            (4, "Syncing Cloud...") => Some("Sincronizzazione cloud..."),
            (4, "Refresh Cloud Profile") => Some("Aggiorna profilo cloud"),
            (4, "Upload Selected Skin to Microsoft Profile") => Some("Carica la skin selezionata sul profilo Microsoft"),
            (4, "Manage Ely.by Cloud Skin on Website") => Some("Gestisci skin cloud Ely.by sul sito"),
            (4, "Applied locally. The account's cloud profile is unchanged.") => Some("Applicato localmente. Il profilo cloud dell'account non è stato modificato."),
            (4, "Choose a PNG image in Android Files.") => Some("Scegli un'immagine PNG nei File Android."),
            (4, "Using the account's cloud appearance. No cloud changes were made.") => Some("Uso dell'aspetto cloud dell'account. Nessuna modifica cloud effettuata."),
            (4, "Local override cleared. Using the default appearance.") => Some("Sostituzione locale rimossa. Uso dell'aspetto predefinito."),
            (4, "Offline appearance is local to this installation.") => Some("L'aspetto offline è locale a questa installazione."),
            (4, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Le skin cloud Ely.by si gestiscono su ely.by. I mantelli personalizzati possono essere applicati localmente."),
            (4, "Select a local skin before uploading to your Microsoft profile.") => Some("Seleziona una skin locale prima di caricarla sul profilo Microsoft."),
            (4, "Contacting account service...") => Some("Contatto del servizio account..."),
            (4, "Sign in before synchronizing your cloud appearance.") => Some("Accedi prima di sincronizzare il tuo aspetto cloud."),
            (4, "The selected account changed. Open the gallery again.") => Some("L'account selezionato è cambiato. Riapri la galleria."),
            (4, "Select a skin first.") => Some("Seleziona prima una skin."),
            (5, "Content Library") => Some("Inhaltsbibliothek"),
            (5, "Profile File Manager") => Some("Profil-Dateimanager"),
            (5, "Library") => Some("Bibliothek"),
            (5, "File Manager") => Some("Dateimanager"),
            (5, "Search installed mods, packs & shaders...") => Some("Installierte Mods, Pakete und Shader suchen..."),
            (5, "+ Import") => Some("+ Importieren"),
            (5, "Packs") => Some("Pakete"),
            (5, "Sort: Name") => Some("Sortieren: Name"),
            (5, "Sort: Type") => Some("Sortieren: Typ"),
            (5, "Sort: Size") => Some("Sortieren: Größe"),
            (5, "Sort: Status") => Some("Sortieren: Status"),
            (5, "No local add-ons installed yet") => Some("Noch keine lokalen Erweiterungen installiert"),
            (5, "No installed add-ons match your filter") => Some("Keine installierten Inhalte entsprechen dem Filter"),
            (5, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some("Importiere .hmod/.jar-Mods, .zip-Ressourcenpakete oder Shaderpakete oder durchsuche Dateien."),
            (5, "Import Mod") => Some("Mod importieren"),
            (5, "Import Pack") => Some("Paket importieren"),
            (5, "ACTIVE") => Some("AKTIV"),
            (5, "OFF") => Some("AUS"),
            (5, "Export") => Some("Exportieren"),
            (5, "Delete") => Some("Löschen"),
            (5, "Import") => Some("Importieren"),
            (5, "+ Folder") => Some("+ Ordner"),
            (5, "Paste") => Some("Einfügen"),
            (5, "Clear") => Some("Leeren"),
            (5, "Copy") => Some("Kopieren"),
            (5, "Cut") => Some("Ausschneiden"),
            (5, "Rename") => Some("Umbenennen"),
            (5, "This folder is empty") => Some("Dieser Ordner ist leer"),
            (5, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("Mit Importieren Dateien kopieren, mit + Ordner einen Ordner erstellen oder Inhalte aus der Zwischenablage einfügen."),
            (5, "Details") => Some("Details"),
            (5, "Root") => Some("Stamm"),
            (5, "Worlds") => Some("Welten"),
            (5, "Controls") => Some("Steuerung"),
            (5, "Shots") => Some("Screenshots"),
            (5, "Reports") => Some("Berichte"),
            (5, "Select one or more files or folders first.") => Some("Wähle zuerst eine oder mehrere Dateien oder Ordner aus."),
            (5, "Delete selected item?") => Some("Ausgewähltes Element löschen?"),
            (5, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("Die ausgewählten Dateien werden dauerhaft aus diesem Profil gelöscht. Dies kann nicht rückgängig gemacht werden."),
            (5, "Cancel") => Some("Abbrechen"),
            (5, "Confirm Delete") => Some("Löschen bestätigen"),
            (5, "Rename File or Folder") => Some("Datei oder Ordner umbenennen"),
            (5, "NEW NAME") => Some("NEUER NAME"),
            (5, "Enter new filename") => Some("Dateinamen eingeben"),
            (5, "Save Name") => Some("Namen speichern"),
            (5, "Create New Folder") => Some("Neuen Ordner erstellen"),
            (5, "Skins & Capes Studio") => Some("Skin- und Umhangstudio"),
            (5, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("Zeige 3D-Umhänge an, verwalte lokale Umhang-Überschreibungen oder aktiviere Cloud-Umhänge."),
            (5, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("Untersuche dein 3D-Charaktermodell, ändere die Armform und verwalte lokale oder Cloud-Skins."),
            (5, "Capes") => Some("Umhänge"),
            (5, "Cape Hidden") => Some("Umhang verborgen"),
            (5, "3D Cape View") => Some("3D-Umhangansicht"),
            (5, "Slim (3px Arms)") => Some("Schmal (3-px-Arme)"),
            (5, "Classic (4px Arms)") => Some("Klassisch (4-px-Arme)"),
            (5, "Loading default skin...") => Some("Standardskin wird geladen..."),
            (5, "Default skin unavailable offline") => Some("Standardskin offline nicht verfügbar"),
            (5, "Cape Visibility: Hidden (Tap to Show)") => Some("Umhang verborgen (tippen zum Anzeigen)"),
            (5, "Cape Visibility: Shown (Tap to Hide)") => Some("Umhang sichtbar (tippen zum Ausblenden)"),
            (5, "Arm Model: Alex / Slim (3px)") => Some("Arm-Modell: Alex / Schmal (3 px)"),
            (5, "Arm Model: Steve / Classic (4px)") => Some("Arm-Modell: Steve / Klassisch (4 px)"),
            (5, "Active Local Override") => Some("Lokale Überschreibung aktiv"),
            (5, "Cape Hidden Locally") => Some("Umhang lokal verborgen"),
            (5, "Account Cloud Cape") => Some("Cloud-Umhang des Kontos"),
            (5, "No Cape Equipped") => Some("Kein Umhang ausgerüstet"),
            (5, "Default Appearance (Steve / Classic)") => Some("Standardaussehen (Steve / Klassisch)"),
            (5, "Synchronizing with account service...") => Some("Kontodienst wird synchronisiert..."),
            (5, "Waiting for Android file picker...") => Some("Warten auf Android-Dateiauswahl..."),
            (5, "Import a 64×32 PNG cape or select below") => Some("64×32-PNG-Umhang importieren oder unten auswählen"),
            (5, "Import a 64×64 PNG skin or select below") => Some("64×64-PNG-Skin importieren oder unten auswählen"),
            (5, "+ Import Cape PNG") => Some("+ PNG-Umhang importieren"),
            (5, "+ Import Skin PNG") => Some("+ PNG-Skin importieren"),
            (5, "Active") => Some("Aktiv"),
            (5, "Apply") => Some("Anwenden"),
            (5, "Cloud Cape · Equipped on profile") => Some("Cloud-Umhang · Im Profil ausgerüstet"),
            (5, "Cloud Cape · Tap to activate") => Some("Cloud-Umhang · Zum Aktivieren tippen"),
            (5, "Local Gallery · Currently applied") => Some("Lokale Galerie · Derzeit angewendet"),
            (5, "Local Cape PNG · Tap to equip") => Some("Lokaler PNG-Umhang · Zum Ausrüsten tippen"),
            (5, "Local Skin PNG · Tap to equip") => Some("Lokaler PNG-Skin · Zum Ausrüsten tippen"),
            (5, "No custom capes in your gallery yet") => Some("Noch keine eigenen Umhänge in deiner Galerie"),
            (5, "No custom skins in your gallery yet") => Some("Noch keine eigenen Skins in deiner Galerie"),
            (5, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("Tippe auf „+ PNG-Umhang importieren“, um eine 64×32-Textur hinzuzufügen."),
            (5, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("Tippe auf „+ PNG-Skin importieren“, um eine 64×64-Textur hinzuzufügen."),
            (5, "Clear Local Cape Override") => Some("Lokale Umhang-Überschreibung entfernen"),
            (5, "Reset to Default Appearance Locally") => Some("Lokal auf Standardaussehen zurücksetzen"),
            (5, "Use Cloud Appearance") => Some("Cloud-Aussehen verwenden"),
            (5, "Syncing Cloud...") => Some("Cloud wird synchronisiert..."),
            (5, "Refresh Cloud Profile") => Some("Cloud-Profil aktualisieren"),
            (5, "Upload Selected Skin to Microsoft Profile") => Some("Ausgewählten Skin ins Microsoft-Profil hochladen"),
            (5, "Manage Ely.by Cloud Skin on Website") => Some("Ely.by-Cloud-Skin auf der Website verwalten"),
            (5, "Applied locally. The account's cloud profile is unchanged.") => Some("Lokal angewendet. Das Cloud-Profil des Kontos bleibt unverändert."),
            (5, "Choose a PNG image in Android Files.") => Some("Wähle eine PNG-Datei in Android-Dateien aus."),
            (5, "Using the account's cloud appearance. No cloud changes were made.") => Some("Das Cloud-Aussehen des Kontos wird verwendet. Es wurden keine Cloud-Änderungen vorgenommen."),
            (5, "Local override cleared. Using the default appearance.") => Some("Lokale Überschreibung entfernt. Standardsaussehen wird verwendet."),
            (5, "Offline appearance is local to this installation.") => Some("Das Offline-Aussehen gilt nur für diese Installation."),
            (5, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Ely.by-Cloud-Skins werden auf ely.by verwaltet. Eigene Umhänge können lokal angewendet werden."),
            (5, "Select a local skin before uploading to your Microsoft profile.") => Some("Wähle vor dem Hochladen in dein Microsoft-Profil einen lokalen Skin aus."),
            (5, "Contacting account service...") => Some("Kontodienst wird kontaktiert..."),
            (5, "Sign in before synchronizing your cloud appearance.") => Some("Melde dich an, bevor du dein Cloud-Aussehen synchronisierst."),
            (5, "The selected account changed. Open the gallery again.") => Some("Das ausgewählte Konto wurde geändert. Öffne die Galerie erneut."),
            (5, "Select a skin first.") => Some("Wähle zuerst einen Skin aus."),
            (6, "Content Library") => Some("コンテンツライブラリ"),
            (6, "Profile File Manager") => Some("プロファイルファイルマネージャー"),
            (6, "Library") => Some("ライブラリ"),
            (6, "File Manager") => Some("ファイルマネージャー"),
            (6, "Search installed mods, packs & shaders...") => Some("インストール済みのMOD・パック・シェーダーを検索..."),
            (6, "+ Import") => Some("+ インポート"),
            (6, "Packs") => Some("パック"),
            (6, "Sort: Name") => Some("並べ替え：名前"),
            (6, "Sort: Type") => Some("並べ替え：種類"),
            (6, "Sort: Size") => Some("並べ替え：サイズ"),
            (6, "Sort: Status") => Some("並べ替え：状態"),
            (6, "No local add-ons installed yet") => Some("ローカルの追加コンテンツはまだありません"),
            (6, "No installed add-ons match your filter") => Some("条件に一致するインストール済みコンテンツはありません"),
            (6, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some(".hmod/.jar MOD、.zip リソースパック、シェーダーパックをインポートするか、ファイルマネージャーで参照してください。"),
            (6, "Import Mod") => Some("MODをインポート"),
            (6, "Import Pack") => Some("パックをインポート"),
            (6, "ACTIVE") => Some("有効"),
            (6, "OFF") => Some("無効"),
            (6, "Export") => Some("エクスポート"),
            (6, "Delete") => Some("削除"),
            (6, "Import") => Some("インポート"),
            (6, "+ Folder") => Some("+ フォルダー"),
            (6, "Paste") => Some("貼り付け"),
            (6, "Clear") => Some("クリア"),
            (6, "Copy") => Some("コピー"),
            (6, "Cut") => Some("切り取り"),
            (6, "Rename") => Some("名前を変更"),
            (6, "This folder is empty") => Some("このフォルダーは空です"),
            (6, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("インポートでファイルをコピーし、+ フォルダーで作成、貼り付けでクリップボードから追加します。"),
            (6, "Details") => Some("詳細"),
            (6, "Root") => Some("ルート"),
            (6, "Worlds") => Some("ワールド"),
            (6, "Controls") => Some("コントロール"),
            (6, "Shots") => Some("スクリーンショット"),
            (6, "Reports") => Some("レポート"),
            (6, "Select one or more files or folders first.") => Some("先にファイルまたはフォルダーを選択してください。"),
            (6, "Delete selected item?") => Some("選択した項目を削除しますか？"),
            (6, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("選択したファイルはこのプロファイルから完全に削除され、元に戻せません。"),
            (6, "Cancel") => Some("キャンセル"),
            (6, "Confirm Delete") => Some("削除を確認"),
            (6, "Rename File or Folder") => Some("ファイルまたはフォルダー名を変更"),
            (6, "NEW NAME") => Some("新しい名前"),
            (6, "Enter new filename") => Some("ファイル名を入力"),
            (6, "Save Name") => Some("名前を保存"),
            (6, "Create New Folder") => Some("新しいフォルダーを作成"),
            (6, "Skins & Capes Studio") => Some("スキン＆マントスタジオ"),
            (6, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("3Dマントをプレビューし、ローカル設定を管理するか、アカウントのクラウドマントを有効にします。"),
            (6, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("3Dキャラクターを確認し、腕のモデルを切り替え、ローカルまたはクラウドのスキンを管理します。"),
            (6, "Capes") => Some("マント"),
            (6, "Cape Hidden") => Some("マント非表示"),
            (6, "3D Cape View") => Some("3Dマント表示"),
            (6, "Slim (3px Arms)") => Some("スリム（腕3px）"),
            (6, "Classic (4px Arms)") => Some("クラシック（腕4px）"),
            (6, "Loading default skin...") => Some("デフォルトスキンを読み込み中..."),
            (6, "Default skin unavailable offline") => Some("オフラインではデフォルトスキンを利用できません"),
            (6, "Cape Visibility: Hidden (Tap to Show)") => Some("マント非表示（タップで表示）"),
            (6, "Cape Visibility: Shown (Tap to Hide)") => Some("マント表示（タップで非表示）"),
            (6, "Arm Model: Alex / Slim (3px)") => Some("腕モデル：Alex / スリム（3px）"),
            (6, "Arm Model: Steve / Classic (4px)") => Some("腕モデル：Steve / クラシック（4px）"),
            (6, "Active Local Override") => Some("ローカル上書きが有効"),
            (6, "Cape Hidden Locally") => Some("ローカルでマントを非表示"),
            (6, "Account Cloud Cape") => Some("アカウントのクラウドマント"),
            (6, "No Cape Equipped") => Some("マント未装備"),
            (6, "Default Appearance (Steve / Classic)") => Some("デフォルト外観（Steve / クラシック）"),
            (6, "Synchronizing with account service...") => Some("アカウントサービスと同期中..."),
            (6, "Waiting for Android file picker...") => Some("Androidファイル選択を待機中..."),
            (6, "Import a 64×32 PNG cape or select below") => Some("64×32 PNGマントをインポートするか、下から選択"),
            (6, "Import a 64×64 PNG skin or select below") => Some("64×64 PNGスキンをインポートするか、下から選択"),
            (6, "+ Import Cape PNG") => Some("+ PNGマントをインポート"),
            (6, "+ Import Skin PNG") => Some("+ PNGスキンをインポート"),
            (6, "Active") => Some("有効"),
            (6, "Apply") => Some("適用"),
            (6, "Cloud Cape · Equipped on profile") => Some("クラウドマント · プロフィールに装備中"),
            (6, "Cloud Cape · Tap to activate") => Some("クラウドマント · タップして有効化"),
            (6, "Local Gallery · Currently applied") => Some("ローカルギャラリー · 適用中"),
            (6, "Local Cape PNG · Tap to equip") => Some("ローカルPNGマント · タップして装備"),
            (6, "Local Skin PNG · Tap to equip") => Some("ローカルPNGスキン · タップして適用"),
            (6, "No custom capes in your gallery yet") => Some("ギャラリーにカスタムマントはありません"),
            (6, "No custom skins in your gallery yet") => Some("ギャラリーにカスタムスキンはありません"),
            (6, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("上の「+ PNGマントをインポート」をタップして64×32テクスチャを追加します。"),
            (6, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("上の「+ PNGスキンをインポート」をタップして64×64テクスチャを追加します。"),
            (6, "Clear Local Cape Override") => Some("ローカルマント設定を解除"),
            (6, "Reset to Default Appearance Locally") => Some("ローカル表示をデフォルトに戻す"),
            (6, "Use Cloud Appearance") => Some("クラウドの外観を使用"),
            (6, "Syncing Cloud...") => Some("クラウドを同期中..."),
            (6, "Refresh Cloud Profile") => Some("クラウドプロフィールを更新"),
            (6, "Upload Selected Skin to Microsoft Profile") => Some("選択したスキンをMicrosoftプロフィールにアップロード"),
            (6, "Manage Ely.by Cloud Skin on Website") => Some("ウェブサイトでEly.byクラウドスキンを管理"),
            (6, "Applied locally. The account's cloud profile is unchanged.") => Some("ローカルに適用しました。アカウントのクラウドプロフィールは変更されていません。"),
            (6, "Choose a PNG image in Android Files.") => Some("AndroidのファイルからPNG画像を選択してください。"),
            (6, "Using the account's cloud appearance. No cloud changes were made.") => Some("アカウントのクラウド外観を使用しています。クラウドへの変更はありません。"),
            (6, "Local override cleared. Using the default appearance.") => Some("ローカル設定を解除しました。デフォルトの外観を使用します。"),
            (6, "Offline appearance is local to this installation.") => Some("オフラインの外観はこのインストール内のみで有効です。"),
            (6, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Ely.byのクラウドスキンはely.byで管理します。カスタムマントはローカルで適用できます。"),
            (6, "Select a local skin before uploading to your Microsoft profile.") => Some("Microsoftプロフィールにアップロードする前にローカルスキンを選択してください。"),
            (6, "Contacting account service...") => Some("アカウントサービスに接続中..."),
            (6, "Sign in before synchronizing your cloud appearance.") => Some("クラウド外観を同期するにはサインインしてください。"),
            (6, "The selected account changed. Open the gallery again.") => Some("選択したアカウントが変更されました。ギャラリーを再度開いてください。"),
            (6, "Select a skin first.") => Some("先にスキンを選択してください。"),
            (7, "Content Library") => Some("内容库"),
            (7, "Profile File Manager") => Some("配置文件管理器"),
            (7, "Library") => Some("库"),
            (7, "File Manager") => Some("文件管理器"),
            (7, "Search installed mods, packs & shaders...") => Some("搜索已安装的模组、资源包和光影..."),
            (7, "+ Import") => Some("+ 导入"),
            (7, "Packs") => Some("资源包"),
            (7, "Sort: Name") => Some("排序：名称"),
            (7, "Sort: Type") => Some("排序：类型"),
            (7, "Sort: Size") => Some("排序：大小"),
            (7, "Sort: Status") => Some("排序：状态"),
            (7, "No local add-ons installed yet") => Some("尚未安装本地附加内容"),
            (7, "No installed add-ons match your filter") => Some("没有符合筛选条件的已安装内容"),
            (7, "Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.") => Some("导入 .hmod/.jar 模组、.zip 资源包或光影包，也可以在文件管理器中浏览文件。"),
            (7, "Import Mod") => Some("导入模组"),
            (7, "Import Pack") => Some("导入资源包"),
            (7, "ACTIVE") => Some("已启用"),
            (7, "OFF") => Some("已关闭"),
            (7, "Export") => Some("导出"),
            (7, "Delete") => Some("删除"),
            (7, "Import") => Some("导入"),
            (7, "+ Folder") => Some("+ 文件夹"),
            (7, "Paste") => Some("粘贴"),
            (7, "Clear") => Some("清除"),
            (7, "Copy") => Some("复制"),
            (7, "Cut") => Some("剪切"),
            (7, "Rename") => Some("重命名"),
            (7, "This folder is empty") => Some("此文件夹为空"),
            (7, "Use Import to copy files from Android storage, + Folder to create a directory, or Paste from clipboard.") => Some("使用“导入”复制文件，使用“+ 文件夹”创建目录，或从剪贴板粘贴。"),
            (7, "Details") => Some("详情"),
            (7, "Root") => Some("根目录"),
            (7, "Worlds") => Some("世界"),
            (7, "Controls") => Some("控制"),
            (7, "Shots") => Some("截图"),
            (7, "Reports") => Some("报告"),
            (7, "Select one or more files or folders first.") => Some("请先选择一个或多个文件或文件夹。"),
            (7, "Delete selected item?") => Some("确定删除所选项目吗？"),
            (7, "This permanently deletes the selected file(s) from this profile and cannot be undone.") => Some("这将从此配置文件中永久删除所选文件，且无法撤销。"),
            (7, "Cancel") => Some("取消"),
            (7, "Confirm Delete") => Some("确认删除"),
            (7, "Rename File or Folder") => Some("重命名文件或文件夹"),
            (7, "NEW NAME") => Some("新名称"),
            (7, "Enter new filename") => Some("输入文件名"),
            (7, "Save Name") => Some("保存名称"),
            (7, "Create New Folder") => Some("新建文件夹"),
            (7, "Skins & Capes Studio") => Some("皮肤与披风工作室"),
            (7, "Preview 3D capes, manage local cape overrides, or activate cloud account capes.") => Some("预览3D披风、管理本地披风覆盖，或启用账户云端披风。"),
            (7, "Inspect your 3D character model, switch arm styles, and manage local or cloud skins.") => Some("查看3D角色模型、切换手臂样式，并管理本地或云端皮肤。"),
            (7, "Capes") => Some("披风"),
            (7, "Cape Hidden") => Some("披风已隐藏"),
            (7, "3D Cape View") => Some("3D披风视图"),
            (7, "Slim (3px Arms)") => Some("纤细型（3像素手臂）"),
            (7, "Classic (4px Arms)") => Some("经典型（4像素手臂）"),
            (7, "Loading default skin...") => Some("正在加载默认皮肤..."),
            (7, "Default skin unavailable offline") => Some("离线时无法使用默认皮肤"),
            (7, "Cape Visibility: Hidden (Tap to Show)") => Some("披风已隐藏（点击显示）"),
            (7, "Cape Visibility: Shown (Tap to Hide)") => Some("披风已显示（点击隐藏）"),
            (7, "Arm Model: Alex / Slim (3px)") => Some("手臂模型：Alex / 纤细型（3像素）"),
            (7, "Arm Model: Steve / Classic (4px)") => Some("手臂模型：Steve / 经典型（4像素）"),
            (7, "Active Local Override") => Some("本地覆盖已启用"),
            (7, "Cape Hidden Locally") => Some("本地已隐藏披风"),
            (7, "Account Cloud Cape") => Some("账户云端披风"),
            (7, "No Cape Equipped") => Some("未装备披风"),
            (7, "Default Appearance (Steve / Classic)") => Some("默认外观（Steve / 经典型）"),
            (7, "Synchronizing with account service...") => Some("正在与账户服务同步..."),
            (7, "Waiting for Android file picker...") => Some("正在等待 Android 文件选择器..."),
            (7, "Import a 64×32 PNG cape or select below") => Some("导入64×32 PNG披风，或在下方选择"),
            (7, "Import a 64×64 PNG skin or select below") => Some("导入64×64 PNG皮肤，或在下方选择"),
            (7, "+ Import Cape PNG") => Some("+ 导入PNG披风"),
            (7, "+ Import Skin PNG") => Some("+ 导入PNG皮肤"),
            (7, "Active") => Some("已启用"),
            (7, "Apply") => Some("应用"),
            (7, "Cloud Cape · Equipped on profile") => Some("云端披风 · 已装备到个人资料"),
            (7, "Cloud Cape · Tap to activate") => Some("云端披风 · 点击启用"),
            (7, "Local Gallery · Currently applied") => Some("本地图库 · 当前已应用"),
            (7, "Local Cape PNG · Tap to equip") => Some("本地PNG披风 · 点击装备"),
            (7, "Local Skin PNG · Tap to equip") => Some("本地PNG皮肤 · 点击应用"),
            (7, "No custom capes in your gallery yet") => Some("图库中尚无自定义披风"),
            (7, "No custom skins in your gallery yet") => Some("图库中尚无自定义皮肤"),
            (7, "Tap '+ Import Cape PNG' above to add a 64×32 cape texture from your device.") => Some("点击上方“+ 导入PNG披风”，从设备添加64×32纹理。"),
            (7, "Tap '+ Import Skin PNG' above to add a 64×64 skin texture from your device.") => Some("点击上方“+ 导入PNG皮肤”，从设备添加64×64纹理。"),
            (7, "Clear Local Cape Override") => Some("清除本地披风覆盖"),
            (7, "Reset to Default Appearance Locally") => Some("在本地恢复默认外观"),
            (7, "Use Cloud Appearance") => Some("使用云端外观"),
            (7, "Syncing Cloud...") => Some("正在同步云端..."),
            (7, "Refresh Cloud Profile") => Some("刷新云端个人资料"),
            (7, "Upload Selected Skin to Microsoft Profile") => Some("将所选皮肤上传到 Microsoft 个人资料"),
            (7, "Manage Ely.by Cloud Skin on Website") => Some("在网站管理 Ely.by 云端皮肤"),
            (7, "Applied locally. The account's cloud profile is unchanged.") => Some("已在本地应用。账户云端资料未更改。"),
            (7, "Choose a PNG image in Android Files.") => Some("请在 Android 文件中选择 PNG 图片。"),
            (7, "Using the account's cloud appearance. No cloud changes were made.") => Some("正在使用账户云端外观。未更改云端数据。"),
            (7, "Local override cleared. Using the default appearance.") => Some("已清除本地覆盖。正在使用默认外观。"),
            (7, "Offline appearance is local to this installation.") => Some("离线外观仅适用于此安装。"),
            (7, "Ely.by cloud skins are managed at ely.by. Custom capes can be applied locally.") => Some("Ely.by 云端皮肤在 ely.by 管理。自定义披风可在本地应用。"),
            (7, "Select a local skin before uploading to your Microsoft profile.") => Some("上传到 Microsoft 个人资料前，请先选择本地皮肤。"),
            (7, "Contacting account service...") => Some("正在联系账户服务..."),
            (7, "Sign in before synchronizing your cloud appearance.") => Some("请先登录，再同步云端外观。"),
            (7, "The selected account changed. Open the gallery again.") => Some("所选账户已更改。请重新打开图库。"),
            (7, "Select a skin first.") => Some("请先选择皮肤。"),
            (1, "All") => Some("Todos"),
            (1, "Mods") => Some("Mods"),
            (1, "Shaders") => Some("Shaders"),
            (1, "Resource Packs") => Some("Paquetes de recursos"),
            (1, "Skins") => Some("Aspectos"),
            (1, "Active Local") => Some("Local activo"),
            (1, "Active Cloud Cape") => Some("Capa de la nube activa"),
            (1, "Cloud Profile") => Some("Perfil de la nube"),
            (1, "Folder") => Some("Carpeta"),
            (1, "Tap to open") => Some("Toca para abrir"),
            (1, "Delete selected items") => Some("Eliminar elementos seleccionados"),
            (2, "All") => Some("Todos"),
            (2, "Mods") => Some("Mods"),
            (2, "Shaders") => Some("Shaders"),
            (2, "Resource Packs") => Some("Pacotes de recursos"),
            (2, "Skins") => Some("Visuais"),
            (2, "Active Local") => Some("Local ativo"),
            (2, "Active Cloud Cape") => Some("Capa da nuvem ativa"),
            (2, "Cloud Profile") => Some("Perfil da nuvem"),
            (2, "Folder") => Some("Pasta"),
            (2, "Tap to open") => Some("Toque para abrir"),
            (2, "Delete selected items") => Some("Excluir itens selecionados"),
            (3, "All") => Some("Tout"),
            (3, "Mods") => Some("Mods"),
            (3, "Shaders") => Some("Shaders"),
            (3, "Resource Packs") => Some("Packs de ressources"),
            (3, "Skins") => Some("Skins"),
            (3, "Active Local") => Some("Local actif"),
            (3, "Active Cloud Cape") => Some("Cape cloud active"),
            (3, "Cloud Profile") => Some("Profil cloud"),
            (3, "Folder") => Some("Dossier"),
            (3, "Tap to open") => Some("Touchez pour ouvrir"),
            (3, "Delete selected items") => Some("Supprimer les éléments sélectionnés"),
            (4, "All") => Some("Tutti"),
            (4, "Mods") => Some("Mod"),
            (4, "Shaders") => Some("Shader"),
            (4, "Resource Packs") => Some("Pacchetti risorse"),
            (4, "Skins") => Some("Skin"),
            (4, "Active Local") => Some("Locale attivo"),
            (4, "Active Cloud Cape") => Some("Mantello cloud attivo"),
            (4, "Cloud Profile") => Some("Profilo cloud"),
            (4, "Folder") => Some("Cartella"),
            (4, "Tap to open") => Some("Tocca per aprire"),
            (4, "Delete selected items") => Some("Elimina elementi selezionati"),
            (5, "All") => Some("Alle"),
            (5, "Mods") => Some("Mods"),
            (5, "Shaders") => Some("Shader"),
            (5, "Resource Packs") => Some("Ressourcenpakete"),
            (5, "Skins") => Some("Skins"),
            (5, "Active Local") => Some("Lokal aktiv"),
            (5, "Active Cloud Cape") => Some("Cloud-Umhang aktiv"),
            (5, "Cloud Profile") => Some("Cloud-Profil"),
            (5, "Folder") => Some("Ordner"),
            (5, "Tap to open") => Some("Zum Öffnen tippen"),
            (5, "Delete selected items") => Some("Ausgewählte Elemente löschen"),
            (6, "All") => Some("すべて"),
            (6, "Mods") => Some("MOD"),
            (6, "Shaders") => Some("シェーダー"),
            (6, "Resource Packs") => Some("リソースパック"),
            (6, "Skins") => Some("スキン"),
            (6, "Active Local") => Some("ローカル有効"),
            (6, "Active Cloud Cape") => Some("有効なクラウドマント"),
            (6, "Cloud Profile") => Some("クラウドプロフィール"),
            (6, "Folder") => Some("フォルダー"),
            (6, "Tap to open") => Some("タップして開く"),
            (6, "Delete selected items") => Some("選択した項目を削除"),
            (7, "All") => Some("全部"),
            (7, "Mods") => Some("模组"),
            (7, "Shaders") => Some("光影"),
            (7, "Resource Packs") => Some("资源包"),
            (7, "Skins") => Some("皮肤"),
            (7, "Active Local") => Some("本地已启用"),
            (7, "Active Cloud Cape") => Some("已启用云端披风"),
            (7, "Cloud Profile") => Some("云端个人资料"),
            (7, "Folder") => Some("文件夹"),
            (7, "Tap to open") => Some("点击打开"),
            (7, "Delete selected items") => Some("删除所选项目"),
            (1, "MOD") => Some("MOD"),
            (1, "PACK") => Some("PAQUETE"),
            (1, "SHADER") => Some("SHADER"),
            (1, "item(s)") => Some("elementos"),
            (2, "MOD") => Some("MOD"),
            (2, "PACK") => Some("PACOTE"),
            (2, "SHADER") => Some("SHADER"),
            (2, "item(s)") => Some("itens"),
            (3, "MOD") => Some("MOD"),
            (3, "PACK") => Some("PACK"),
            (3, "SHADER") => Some("SHADER"),
            (3, "item(s)") => Some("éléments"),
            (4, "MOD") => Some("MOD"),
            (4, "PACK") => Some("PACCHETTO"),
            (4, "SHADER") => Some("SHADER"),
            (4, "item(s)") => Some("elementi"),
            (5, "MOD") => Some("MOD"),
            (5, "PACK") => Some("PAKET"),
            (5, "SHADER") => Some("SHADER"),
            (5, "item(s)") => Some("Elemente"),
            (6, "MOD") => Some("MOD"),
            (6, "PACK") => Some("パック"),
            (6, "SHADER") => Some("シェーダー"),
            (6, "item(s)") => Some("項目"),
            (7, "MOD") => Some("模组"),
            (7, "PACK") => Some("资源包"),
            (7, "SHADER") => Some("光影"),
            (7, "item(s)") => Some("个项目"),
            (1, "FOLDER NAME") => Some("NOMBRE DE CARPETA"),
            (1, "Enter folder name") => Some("Escribe el nombre de la carpeta"),
            (1, "Create Folder") => Some("Crear carpeta"),
            (1, "Location") => Some("Ubicación"),
            (1, "Path") => Some("Ruta"),
            (1, "Kind") => Some("Tipo"),
            (1, "Size") => Some("Tamaño"),
            (1, "bytes") => Some("bytes"),
            (1, "Modified") => Some("Modificado"),
            (1, "Done") => Some("Listo"),
            (1, "Close") => Some("Cerrar"),
            (1, "Target") => Some("Destino"),
            (1, "Slashes and '..' are rejected") => Some("No se permiten barras ni '..'"),
            (2, "FOLDER NAME") => Some("NOME DA PASTA"),
            (2, "Enter folder name") => Some("Digite o nome da pasta"),
            (2, "Create Folder") => Some("Criar pasta"),
            (2, "Location") => Some("Localização"),
            (2, "Path") => Some("Caminho"),
            (2, "Kind") => Some("Tipo"),
            (2, "Size") => Some("Tamanho"),
            (2, "bytes") => Some("bytes"),
            (2, "Modified") => Some("Modificado"),
            (2, "Done") => Some("Concluído"),
            (2, "Close") => Some("Fechar"),
            (2, "Target") => Some("Destino"),
            (2, "Slashes and '..' are rejected") => Some("Barras e '..' não são permitidos"),
            (3, "FOLDER NAME") => Some("NOM DU DOSSIER"),
            (3, "Enter folder name") => Some("Saisissez le nom du dossier"),
            (3, "Create Folder") => Some("Créer le dossier"),
            (3, "Location") => Some("Emplacement"),
            (3, "Path") => Some("Chemin"),
            (3, "Kind") => Some("Type"),
            (3, "Size") => Some("Taille"),
            (3, "bytes") => Some("octets"),
            (3, "Modified") => Some("Modifié"),
            (3, "Done") => Some("Terminé"),
            (3, "Close") => Some("Fermer"),
            (3, "Target") => Some("Cible"),
            (3, "Slashes and '..' are rejected") => Some("Les barres et '..' sont interdits"),
            (4, "FOLDER NAME") => Some("NOME CARTELLA"),
            (4, "Enter folder name") => Some("Inserisci il nome della cartella"),
            (4, "Create Folder") => Some("Crea cartella"),
            (4, "Location") => Some("Posizione"),
            (4, "Path") => Some("Percorso"),
            (4, "Kind") => Some("Tipo"),
            (4, "Size") => Some("Dimensione"),
            (4, "bytes") => Some("byte"),
            (4, "Modified") => Some("Modificato"),
            (4, "Done") => Some("Fatto"),
            (4, "Close") => Some("Chiudi"),
            (4, "Target") => Some("Destinazione"),
            (4, "Slashes and '..' are rejected") => Some("Le barre e '..' non sono consentiti"),
            (5, "FOLDER NAME") => Some("ORDNERNAME"),
            (5, "Enter folder name") => Some("Ordnernamen eingeben"),
            (5, "Create Folder") => Some("Ordner erstellen"),
            (5, "Location") => Some("Speicherort"),
            (5, "Path") => Some("Pfad"),
            (5, "Kind") => Some("Typ"),
            (5, "Size") => Some("Größe"),
            (5, "bytes") => Some("Bytes"),
            (5, "Modified") => Some("Geändert"),
            (5, "Done") => Some("Fertig"),
            (5, "Close") => Some("Schließen"),
            (5, "Target") => Some("Ziel"),
            (5, "Slashes and '..' are rejected") => Some("Schrägstriche und '..' sind nicht erlaubt"),
            (6, "FOLDER NAME") => Some("フォルダー名"),
            (6, "Enter folder name") => Some("フォルダー名を入力"),
            (6, "Create Folder") => Some("フォルダーを作成"),
            (6, "Location") => Some("場所"),
            (6, "Path") => Some("パス"),
            (6, "Kind") => Some("種類"),
            (6, "Size") => Some("サイズ"),
            (6, "bytes") => Some("バイト"),
            (6, "Modified") => Some("更新日時"),
            (6, "Done") => Some("完了"),
            (6, "Close") => Some("閉じる"),
            (6, "Target") => Some("対象"),
            (6, "Slashes and '..' are rejected") => Some("スラッシュと '..' は使用できません"),
            (7, "FOLDER NAME") => Some("文件夹名称"),
            (7, "Enter folder name") => Some("输入文件夹名称"),
            (7, "Create Folder") => Some("创建文件夹"),
            (7, "Location") => Some("位置"),
            (7, "Path") => Some("路径"),
            (7, "Kind") => Some("类型"),
            (7, "Size") => Some("大小"),
            (7, "bytes") => Some("字节"),
            (7, "Modified") => Some("修改时间"),
            (7, "Done") => Some("完成"),
            (7, "Close") => Some("关闭"),
            (7, "Target") => Some("目标"),
            (7, "Slashes and '..' are rejected") => Some("不允许使用斜杠和 '..'"),
            (1, "Clipboard is empty.") => Some("El portapapeles está vacío."),
            (1, "Searching... You can switch tabs while results load.") => Some("Buscando... Puedes cambiar de pestaña mientras se cargan los resultados."),
            (1, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("No hay paquetes de shaders de Horizon que coincidan. Prueba otra búsqueda o importa un .hshader."),
            (1, "Download cancelled") => Some("Descarga cancelada"),
            (1, "Unsupported download source") => Some("Fuente de descarga no compatible"),
            (1, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("Los mods .hnmod nativos no son compatibles temporalmente con Android. Los mods integrados siguen disponibles."),
            (1, "Selection saved for the next game launch") => Some("Selección guardada para el próximo inicio del juego."),
            (1, "Refusing a redirected content path") => Some("Se rechazó una ruta de contenido redirigida."),
            (1, "Invalid content filename") => Some("El nombre del archivo de contenido no es válido."),
            (1, "Refusing a redirected content directory") => Some("Se rechazó un directorio de contenido redirigido."),
            (1, "Only an explicitly selected regular add-on file can be removed") => Some("Solo se puede eliminar un archivo de complemento normal seleccionado explícitamente."),
            (1, "Copied") => Some("Copiado"),
            (1, "Moved") => Some("Movido"),
            (1, "Pasted") => Some("Pegado"),
            (1, "Installed {count} item(s). Open Play to load this profile.") => Some("Se instalaron {count} elementos. Abre Jugar para cargar este perfil."),
            (1, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("Se instaló {}. Actívalo en Instalados después de revisar sus capacidades en Mods del juego."),
            (1, "bytes") => Some("bytes"),
            (2, "Clipboard is empty.") => Some("A área de transferência está vazia."),
            (2, "Searching... You can switch tabs while results load.") => Some("Pesquisando... Você pode trocar de aba enquanto os resultados carregam."),
            (2, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("Nenhum pacote de shaders Horizon corresponde à busca. Tente outra consulta ou importe um .hshader."),
            (2, "Download cancelled") => Some("Download cancelado"),
            (2, "Unsupported download source") => Some("Fonte de download não compatível"),
            (2, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("Mods .hnmod nativos estão temporariamente indisponíveis no Android. Os mods integrados continuam disponíveis."),
            (2, "Selection saved for the next game launch") => Some("Seleção salva para a próxima inicialização do jogo."),
            (2, "Refusing a redirected content path") => Some("Caminho de conteúdo redirecionado recusado."),
            (2, "Invalid content filename") => Some("Nome de arquivo de conteúdo inválido."),
            (2, "Refusing a redirected content directory") => Some("Diretório de conteúdo redirecionado recusado."),
            (2, "Only an explicitly selected regular add-on file can be removed") => Some("Somente um arquivo de complemento comum selecionado explicitamente pode ser removido."),
            (2, "Copied") => Some("Copiado"),
            (2, "Moved") => Some("Movido"),
            (2, "Pasted") => Some("Colado"),
            (2, "Installed {count} item(s). Open Play to load this profile.") => Some("{count} itens instalados. Abra Jogar para carregar este perfil."),
            (2, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("{} instalado. Ative em Instalados após revisar seus recursos nos Mods do jogo."),
            (2, "bytes") => Some("bytes"),
            (3, "Clipboard is empty.") => Some("Le presse-papiers est vide."),
            (3, "Searching... You can switch tabs while results load.") => Some("Recherche... Vous pouvez changer d’onglet pendant le chargement."),
            (3, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("Aucun pack de shaders Horizon ne correspond. Essayez une autre recherche ou importez un .hshader."),
            (3, "Download cancelled") => Some("Téléchargement annulé"),
            (3, "Unsupported download source") => Some("Source de téléchargement non prise en charge"),
            (3, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("Les mods .hnmod natifs sont temporairement indisponibles sur Android. Les mods intégrés restent disponibles."),
            (3, "Selection saved for the next game launch") => Some("Sélection enregistrée pour le prochain lancement du jeu."),
            (3, "Refusing a redirected content path") => Some("Chemin de contenu redirigé refusé."),
            (3, "Invalid content filename") => Some("Nom de fichier de contenu invalide."),
            (3, "Refusing a redirected content directory") => Some("Dossier de contenu redirigé refusé."),
            (3, "Only an explicitly selected regular add-on file can be removed") => Some("Seul un fichier d’extension standard explicitement sélectionné peut être supprimé."),
            (3, "Copied") => Some("Copié"),
            (3, "Moved") => Some("Déplacé"),
            (3, "Pasted") => Some("Collé"),
            (3, "Installed {count} item(s). Open Play to load this profile.") => Some("{count} éléments installés. Ouvrez Jouer pour charger ce profil."),
            (3, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("{} installé. Activez-le dans Installés après avoir vérifié ses capacités dans les mods du jeu."),
            (3, "bytes") => Some("octets"),
            (4, "Clipboard is empty.") => Some("Gli appunti sono vuoti."),
            (4, "Searching... You can switch tabs while results load.") => Some("Ricerca... Puoi cambiare scheda mentre i risultati vengono caricati."),
            (4, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("Nessun pacchetto shader Horizon corrisponde. Prova un'altra ricerca o importa un .hshader."),
            (4, "Download cancelled") => Some("Download annullato"),
            (4, "Unsupported download source") => Some("Fonte di download non supportata"),
            (4, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("I mod .hnmod nativi non sono temporaneamente supportati su Android. I mod integrati restano disponibili."),
            (4, "Selection saved for the next game launch") => Some("Selezione salvata per il prossimo avvio del gioco."),
            (4, "Refusing a redirected content path") => Some("Percorso del contenuto reindirizzato rifiutato."),
            (4, "Invalid content filename") => Some("Nome file del contenuto non valido."),
            (4, "Refusing a redirected content directory") => Some("Cartella del contenuto reindirizzata rifiutata."),
            (4, "Only an explicitly selected regular add-on file can be removed") => Some("È possibile rimuovere solo un file aggiuntivo normale selezionato esplicitamente."),
            (4, "Copied") => Some("Copiato"),
            (4, "Moved") => Some("Spostato"),
            (4, "Pasted") => Some("Incollato"),
            (4, "Installed {count} item(s). Open Play to load this profile.") => Some("Installati {count} elementi. Apri Gioca per caricare questo profilo."),
            (4, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("{} installato. Attivalo in Installati dopo averne verificato le funzionalità nei mod del gioco."),
            (4, "bytes") => Some("byte"),
            (5, "Clipboard is empty.") => Some("Die Zwischenablage ist leer."),
            (5, "Searching... You can switch tabs while results load.") => Some("Suche... Du kannst während des Ladens die Tabs wechseln."),
            (5, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("Keine Horizon-Shaderpakete entsprechen der Suche. Versuche eine andere Suche oder importiere ein .hshader."),
            (5, "Download cancelled") => Some("Download abgebrochen"),
            (5, "Unsupported download source") => Some("Nicht unterstützte Downloadquelle"),
            (5, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("Native .hnmod-Mods werden vorübergehend nicht auf Android unterstützt. Integrierte Mods bleiben verfügbar."),
            (5, "Selection saved for the next game launch") => Some("Auswahl für den nächsten Spielstart gespeichert."),
            (5, "Refusing a redirected content path") => Some("Umgeleiteter Inhaltspfad wurde abgelehnt."),
            (5, "Invalid content filename") => Some("Ungültiger Inhaltsdateiname."),
            (5, "Refusing a redirected content directory") => Some("Umgeleitetes Inhaltsverzeichnis wurde abgelehnt."),
            (5, "Only an explicitly selected regular add-on file can be removed") => Some("Nur eine ausdrücklich ausgewählte reguläre Erweiterungsdatei kann entfernt werden."),
            (5, "Copied") => Some("Kopiert"),
            (5, "Moved") => Some("Verschoben"),
            (5, "Pasted") => Some("Eingefügt"),
            (5, "Installed {count} item(s). Open Play to load this profile.") => Some("{count} Elemente installiert. Öffne Spielen, um dieses Profil zu laden."),
            (5, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("{} installiert. Aktiviere es unter Installiert, nachdem du die Funktionen in den Spiel-Mods geprüft hast."),
            (5, "bytes") => Some("Bytes"),
            (6, "Clipboard is empty.") => Some("クリップボードは空です。"),
            (6, "Searching... You can switch tabs while results load.") => Some("検索中... 読み込み中もタブを切り替えられます。"),
            (6, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("一致するHorizonシェーダーパックはありません。別の検索を試すか、.hshaderをインポートしてください。"),
            (6, "Download cancelled") => Some("ダウンロードをキャンセルしました"),
            (6, "Unsupported download source") => Some("サポートされていないダウンロード元"),
            (6, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("ネイティブ .hnmod MODは現在Androidで一時的にサポートされていません。内蔵MODは引き続き利用できます。"),
            (6, "Selection saved for the next game launch") => Some("選択内容を次回起動用に保存しました。"),
            (6, "Refusing a redirected content path") => Some("リダイレクトされたコンテンツパスを拒否しました。"),
            (6, "Invalid content filename") => Some("無効なコンテンツファイル名です。"),
            (6, "Refusing a redirected content directory") => Some("リダイレクトされたコンテンツディレクトリを拒否しました。"),
            (6, "Only an explicitly selected regular add-on file can be removed") => Some("明示的に選択された通常のアドオンファイルのみ削除できます。"),
            (6, "Copied") => Some("コピーしました"),
            (6, "Moved") => Some("移動しました"),
            (6, "Pasted") => Some("貼り付けました"),
            (6, "Installed {count} item(s). Open Play to load this profile.") => Some("{count}件をインストールしました。プロフィールを読み込むにはプレイを開いてください。"),
            (6, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("{}をインストールしました。ゲーム内MODで機能を確認してからインストール済みで有効にしてください。"),
            (6, "bytes") => Some("バイト"),
            (7, "Clipboard is empty.") => Some("剪贴板为空。"),
            (7, "Searching... You can switch tabs while results load.") => Some("正在搜索... 加载结果时可以切换标签页。"),
            (7, "No Horizon shader packs match this search. Try another query or import a .hshader.") => Some("没有匹配的 Horizon 光影包。请尝试其他搜索，或导入 .hshader。"),
            (7, "Download cancelled") => Some("下载已取消"),
            (7, "Unsupported download source") => Some("不支持的下载来源"),
            (7, "Native .hnmod mods are temporarily unsupported on Android. Built-in mods remain available.") => Some("Android 暂时不支持原生 .hnmod 模组。内置模组仍可使用。"),
            (7, "Selection saved for the next game launch") => Some("已保存选择，将在下次启动游戏时生效。"),
            (7, "Refusing a redirected content path") => Some("已拒绝重定向的内容路径。"),
            (7, "Invalid content filename") => Some("内容文件名无效。"),
            (7, "Refusing a redirected content directory") => Some("已拒绝重定向的内容目录。"),
            (7, "Only an explicitly selected regular add-on file can be removed") => Some("只能删除明确选中的普通附加内容文件。"),
            (7, "Copied") => Some("已复制"),
            (7, "Moved") => Some("已移动"),
            (7, "Pasted") => Some("已粘贴"),
            (7, "Installed {count} item(s). Open Play to load this profile.") => Some("已安装 {count} 个项目。打开“开始游戏”以加载此配置。"),
            (7, "Installed {}. Enable it in Installed after reviewing its capabilities in game Mods.") => Some("已安装 {}。请在游戏内模组中检查其功能后，在“已安装”中启用。"),
            (7, "bytes") => Some("字节"),
        _ => None,
    };
    if let Some(value) = exact {
        return Some(value.to_owned());
    }
    if let Some((prefix, suffix)) = text.split_once(" (") {
        if suffix.ends_with(')') && suffix[..suffix.len() - 1].chars().all(|c| c.is_ascii_digit()) {
            if let Some(label) = pavo_content_library_text(lang, prefix) {
                return Some(format!("{label} ({suffix}"));
            }
        }
    }
    if let Some(value) = text.strip_prefix("Active Local: ") {
        if let Some(label) = pavo_content_library_text(lang, "Active Local") {
            return Some(format!("{label}: {value}"));
        }
    }
    if let Some(value) = text.strip_prefix("Active Cloud Cape: ") {
        if let Some(label) = pavo_content_library_text(lang, "Active Cloud Cape") {
            return Some(format!("{label}: {value}"));
        }
    }
    if let Some(value) = text.strip_prefix("Cloud Profile (") {
        if let Some(label) = pavo_content_library_text(lang, "Cloud Profile") {
            return Some(format!("{label} ({value}"));
        }
    }
    if let Some(value) = text.strip_prefix("Paste (") {
        if let Some(label) = pavo_content_library_text(lang, "Paste") {
            return Some(format!("{label} ({value}"));
        }
    }
    if let Some(value) = text.strip_prefix("Delete (") {
        if let Some(label) = pavo_content_library_text(lang, "Delete") {
            return Some(format!("{label} ({value}"));
        }
    }
    if let Some(value) = text.strip_prefix("Delete ") {
        if let Some(count) = value.strip_suffix(" selected items?") {
            if count.chars().all(|c| c.is_ascii_digit()) {
                if let Some(label) = pavo_content_library_text(lang, "Delete selected items") {
                    return Some(format!("{label} ({count})?"));
                }
            }
        }
    }
    if let Some(value) = text.strip_prefix("Folder  •  ") {
        if let Some((count, _)) = value.split_once(" item(s)  •  Tap to open") {
            if let Some(label) = pavo_content_library_text(lang, "Folder") {
                if let Some(tap) = pavo_content_library_text(lang, "Tap to open") {
                    if let Some(unit) = pavo_content_library_text(lang, "item(s)") {
                        return Some(format!("{label}  •  {count} {unit}  •  {tap}"));
                    }
                }
            }
        }
    }
    let mut localized = text.to_owned();
    for label in ["Location", "Path", "Kind", "Size", "bytes", "Modified", "Target"] {
        if let Some(value) = pavo_content_library_text(lang, label) {
            localized = localized.replace(&format!("{label}:"), &format!("{value}:"));
        }
    }
    if let Some(value) = pavo_content_library_text(lang, "Slashes and '..' are rejected") {
        localized = localized.replace("(Slashes and '..' are rejected)", &format!("({value})"));
    }
    if let Some(value) = pavo_content_library_text(lang, "bytes") {
        localized = localized.replace(" bytes)", &format!(" {value})"));
    }
    if localized != text {
        return Some(localized);
    }
    None
}

fn pavo_launcher_text(text: &str) -> String {
    PAVO_LAUNCHER_LANGUAGE.with(|current| {
        let lang = current.get();
        if let Some(translated) = pavo_content_library_text(lang, text) {
            return translated;
        }
        let translated = match (lang, text) {
            (1, "Resource Pack") => "Paquetes de recursos",
            (1, "Shaders") => "Shaders",
            (1, "Theme Customization") => "Personalización del tema",
            (1, "Controls & HUD Layout") => "Controles y diseño del HUD",
            (1, "Diagnostics & Logs") => "Diagnóstico y registros",
            (1, "Getting your game ready") => "Preparando el juego",
            (1, "Ready to play") => "Listo para jugar",
            (1, "Your Java worlds") => "Tus mundos de Java",
            (1, "Your Java worlds, on Android") => "Tus mundos de Java en Android",
            (1, "Downloading add-on...") => "Descargando complemento...",
            (1, "Resolve") => "Resolver",
            (1, "Download") => "Descargar",
            (1, "Verify") => "Verificar",
            (1, "Install") => "Instalar",
            (1, "Settings") => "Ajustes",
            (1, "Video Settings") => "Ajustes de vídeo",
            (1, "Launcher Settings") => "Ajustes del launcher",
            (1, "Controls") => "Controles",
            (1, "Experimental Stuff") => "Funciones experimentales",
            (1, "Diagnostics") => "Diagnóstico",
            (1, "Account") => "Cuenta",
            (1, "Rendering, resolution and performance") => "Renderizado, resolución y rendimiento",
            (1, "Theme customization, profiles, DPI scale & preferences") => "Tema, perfiles, escala DPI y preferencias",
            (1, "HUD touch layout, buttons, sensitivity and profiles") => "Diseño táctil del HUD, botones, sensibilidad y perfiles",
            (1, "Logs, device info and troubleshooting") => "Registros, información del dispositivo y solución de problemas",
            (1, "Microsoft and Xbox sign-in") => "Inicio de sesión de Microsoft y Xbox",
            (1, "← Back") => "← Volver",
            (1, "Enabled") => "Activado",
            (1, "Off") => "Desactivado",
            (1, "Open Files") => "Abrir archivos",
            (1, "Open game & world folders") => "Abrir carpetas del juego y mundos",
            (1, "Manage saves, resource packs and mods in Android Files") => "Administra mundos, paquetes y mods desde Archivos",
            (1, "Reset Default") => "Restablecer valores",
            (1, "Light") => "Claro",
            (1, "Dark") => "Oscuro",
            (1, "Search") => "Buscar",
            (1, "Play") => "Jugar",
            (1, "Home") => "Inicio",
            (1, "Mods") => "Mods",
            (1, "Installed") => "Instalados",
            (1, "Discover") => "Descubrir",
            (1, "Downloads") => "Descargas",
            (1, "Skins") => "Aspectos",
            (1, "Servers") => "Servidores",
            (1, "Worlds") => "Mundos",
            (1, "Language") => "Idioma",
            (1, "Change the launcher and in-game language") => "Cambia el idioma del launcher y del juego",
            (1, "English v") => "Español v",
            (2, "Resource Pack") => "Pacote de recursos",
            (2, "Shaders") => "Shaders",
            (2, "Theme Customization") => "Personalização do tema",
            (2, "Controls & HUD Layout") => "Controles e layout do HUD",
            (2, "Diagnostics & Logs") => "Diagnóstico e registros",
            (2, "Ready to play") => "Pronto para jogar",
            (2, "Download") => "Baixar",
            (2, "Verify") => "Verificar",
            (2, "Install") => "Instalar",
            (2, "Settings") => "Configurações",
            (2, "Video Settings") => "Configurações de vídeo",
            (2, "Launcher Settings") => "Configurações do launcher",
            (2, "Controls") => "Controles",
            (2, "Experimental Stuff") => "Recursos experimentais",
            (2, "Diagnostics") => "Diagnóstico",
            (2, "Account") => "Conta",
            (2, "← Back") => "← Voltar",
            (2, "Enabled") => "Ativado",
            (2, "Off") => "Desativado",
            (2, "Search") => "Pesquisar",
            (2, "Play") => "Jogar",
            (2, "Home") => "Início",
            (2, "Installed") => "Instalados",
            (2, "Discover") => "Descobrir",
            (2, "Downloads") => "Downloads",
            (2, "Skins") => "Visuais",
            (2, "Servers") => "Servidores",
            (2, "Worlds") => "Mundos",
            (2, "Language") => "Idioma",
            (2, "English v") => "Português v",
            (3, "Resource Pack") => "Pack de ressources",
            (3, "Shaders") => "Shaders",
            (3, "Theme Customization") => "Personnalisation du thème",
            (3, "Controls & HUD Layout") => "Commandes et disposition du HUD",
            (3, "Diagnostics & Logs") => "Diagnostics et journaux",
            (3, "Ready to play") => "Prêt à jouer",
            (3, "Download") => "Télécharger",
            (3, "Verify") => "Vérifier",
            (3, "Install") => "Installer",
            (3, "Settings") => "Paramètres",
            (3, "Video Settings") => "Paramètres vidéo",
            (3, "Launcher Settings") => "Paramètres du launcher",
            (3, "Controls") => "Commandes",
            (3, "Experimental Stuff") => "Fonctionnalités expérimentales",
            (3, "Diagnostics") => "Diagnostics",
            (3, "Account") => "Compte",
            (3, "← Back") => "← Retour",
            (3, "Enabled") => "Activé",
            (3, "Off") => "Désactivé",
            (3, "Search") => "Rechercher",
            (3, "Play") => "Jouer",
            (3, "Home") => "Accueil",
            (3, "Installed") => "Installés",
            (3, "Discover") => "Découvrir",
            (3, "Downloads") => "Téléchargements",
            (3, "Skins") => "Skins",
            (3, "Servers") => "Serveurs",
            (3, "Worlds") => "Mondes",
            (3, "Language") => "Langue",
            (3, "English v") => "Français v",
            (4, "Resource Pack") => "Pacchetto risorse",
            (4, "Shaders") => "Shader",
            (4, "Theme Customization") => "Personalizzazione tema",
            (4, "Controls & HUD Layout") => "Controlli e layout HUD",
            (4, "Diagnostics & Logs") => "Diagnostica e registri",
            (4, "Ready to play") => "Pronto per giocare",
            (4, "Download") => "Scarica",
            (4, "Verify") => "Verifica",
            (4, "Install") => "Installa",
            (4, "Settings") => "Impostazioni",
            (4, "Video Settings") => "Impostazioni video",
            (4, "Launcher Settings") => "Impostazioni launcher",
            (4, "Controls") => "Controlli",
            (4, "Experimental Stuff") => "Funzioni sperimentali",
            (4, "Diagnostics") => "Diagnostica",
            (4, "Account") => "Account",
            (4, "← Back") => "← Indietro",
            (4, "Enabled") => "Attivato",
            (4, "Off") => "Disattivato",
            (4, "Search") => "Cerca",
            (4, "Play") => "Gioca",
            (4, "Home") => "Home",
            (4, "Installed") => "Installati",
            (4, "Discover") => "Esplora",
            (4, "Downloads") => "Download",
            (4, "Skins") => "Skin",
            (4, "Servers") => "Server",
            (4, "Worlds") => "Mondi",
            (4, "Language") => "Lingua",
            (4, "English v") => "Italiano v",
            (5, "Resource Pack") => "Ressourcenpaket",
            (5, "Shaders") => "Shader",
            (5, "Theme Customization") => "Design anpassen",
            (5, "Controls & HUD Layout") => "Steuerung und HUD-Layout",
            (5, "Diagnostics & Logs") => "Diagnose und Protokolle",
            (5, "Ready to play") => "Spielbereit",
            (5, "Download") => "Herunterladen",
            (5, "Verify") => "Überprüfen",
            (5, "Install") => "Installieren",
            (5, "Settings") => "Einstellungen",
            (5, "Video Settings") => "Videoeinstellungen",
            (5, "Launcher Settings") => "Launcher-Einstellungen",
            (5, "Controls") => "Steuerung",
            (5, "Experimental Stuff") => "Experimentelle Funktionen",
            (5, "Diagnostics") => "Diagnose",
            (5, "Account") => "Konto",
            (5, "← Back") => "← Zurück",
            (5, "Enabled") => "Aktiviert",
            (5, "Off") => "Aus",
            (5, "Search") => "Suchen",
            (5, "Play") => "Spielen",
            (5, "Home") => "Startseite",
            (5, "Installed") => "Installiert",
            (5, "Discover") => "Entdecken",
            (5, "Downloads") => "Downloads",
            (5, "Skins") => "Skins",
            (5, "Servers") => "Server",
            (5, "Worlds") => "Welten",
            (5, "Language") => "Sprache",
            (5, "English v") => "Deutsch v",
            (6, "Resource Pack") => "リソースパック",
            (6, "Shaders") => "シェーダー",
            (6, "Theme Customization") => "テーマのカスタマイズ",
            (6, "Controls & HUD Layout") => "操作とHUDレイアウト",
            (6, "Diagnostics & Logs") => "診断とログ",
            (6, "Ready to play") => "プレイ準備完了",
            (6, "Download") => "ダウンロード",
            (6, "Verify") => "確認",
            (6, "Install") => "インストール",
            (6, "Settings") => "設定",
            (6, "Video Settings") => "ビデオ設定",
            (6, "Launcher Settings") => "ランチャー設定",
            (6, "Controls") => "操作",
            (6, "Experimental Stuff") => "実験的な機能",
            (6, "Diagnostics") => "診断",
            (6, "Account") => "アカウント",
            (6, "← Back") => "← 戻る",
            (6, "Enabled") => "有効",
            (6, "Off") => "オフ",
            (6, "Search") => "検索",
            (6, "Play") => "プレイ",
            (6, "Home") => "ホーム",
            (6, "Installed") => "インストール済み",
            (6, "Discover") => "見つける",
            (6, "Downloads") => "ダウンロード",
            (6, "Skins") => "スキン",
            (6, "Servers") => "サーバー",
            (6, "Worlds") => "ワールド",
            (6, "Language") => "言語",
            (6, "English v") => "日本語 v",
            (7, "Resource Pack") => "资源包",
            (7, "Shaders") => "光影",
            (7, "Theme Customization") => "主题自定义",
            (7, "Controls & HUD Layout") => "控制与HUD布局",
            (7, "Diagnostics & Logs") => "诊断与日志",
            (7, "Ready to play") => "可以开始游戏",
            (7, "Download") => "下载",
            (7, "Verify") => "验证",
            (7, "Install") => "安装",
            (7, "Settings") => "设置",
            (7, "Video Settings") => "视频设置",
            (7, "Launcher Settings") => "启动器设置",
            (7, "Controls") => "控制",
            (7, "Experimental Stuff") => "实验功能",
            (7, "Diagnostics") => "诊断",
            (7, "Account") => "账户",
            (7, "← Back") => "← 返回",
            (7, "Enabled") => "已启用",
            (7, "Off") => "关闭",
            (7, "Search") => "搜索",
            (7, "Play") => "开始游戏",
            (7, "Home") => "主页",
            (7, "Installed") => "已安装",
            (7, "Discover") => "发现",
            (7, "Downloads") => "下载",
            (7, "Skins") => "皮肤",
            (7, "Servers") => "服务器",
            (7, "Worlds") => "世界",
            (7, "Language") => "语言",
            (7, "English v") => "简体中文 v",
            (1, "Shader Packs") => "Paquetes de shaders",
            (1, "Resource Packs") => "Paquetes de recursos",
            (1, "Available Versions") => "Versiones disponibles",
            (1, "Search catalog...") => "Buscar en el catálogo...",
            (1, "Search installed mods, packs & shaders...") => "Buscar mods, paquetes y shaders instalados...",
            (1, "Search mods or paste .hmod HTTPS link...") => "Buscar mods o pegar enlace HTTPS .hmod...",
            (1, "Import .hmod") => "Importar .hmod",
            (1, "Import Mod") => "Importar mod",
            (1, "Import Pack") => "Importar paquete",
            (1, "Import file") => "Importar archivo",
            (1, "No local add-ons installed yet") => "Todavía no hay complementos locales instalados",
            (1, "No installed add-ons match your filter") => "Ningún complemento instalado coincide con el filtro",
            (1, "Loading content catalog...") => "Cargando catálogo de contenido...",
            (1, "Syncing Modrinth details...") => "Sincronizando datos de Modrinth...",
            (1, "Downloading Java Edition 26.3 client.jar") => "Descargando client.jar de Java Edition 26.3",
            (1, "Retry installation") => "Reintentar instalación",
            (1, "Download Minecraft 26.3") => "Descargar Minecraft 26.3",
            (1, "Installed (Tap to Disable)") => "Instalado (toca para desactivar)",
            (1, "Import") => "Importar",
            (1, "Export") => "Exportar",
            (1, "Open folder") => "Abrir carpeta",
            (1, "World Files") => "Archivos de mundos",
            (1, "Profile File Manager") => "Administrador de archivos del perfil",
            (1, "File Manager") => "Administrador de archivos",
            (1, "Confirm Delete") => "Confirmar eliminación",
            (1, "Delete selected item?") => "¿Eliminar el elemento seleccionado?",
            (1, "Rename File or Folder") => "Renombrar archivo o carpeta",
            (1, "Select a file to export.") => "Selecciona un archivo para exportar.",
            (1, "This folder is empty") => "Esta carpeta está vacía",
            (2, "Shader Packs") => "Pacotes de shaders",
            (2, "Resource Packs") => "Pacotes de recursos",
            (2, "Available Versions") => "Versões disponíveis",
            (2, "Search catalog...") => "Pesquisar catálogo...",
            (2, "Search installed mods, packs & shaders...") => "Pesquisar mods, pacotes e shaders instalados...",
            (2, "Import .hmod") => "Importar .hmod",
            (2, "Import Mod") => "Importar mod",
            (2, "Import Pack") => "Importar pacote",
            (2, "Import file") => "Importar arquivo",
            (2, "No local add-ons installed yet") => "Nenhum complemento local instalado ainda",
            (2, "No installed add-ons match your filter") => "Nenhum complemento instalado corresponde ao filtro",
            (2, "Loading content catalog...") => "Carregando catálogo de conteúdo...",
            (2, "Syncing Modrinth details...") => "Sincronizando dados do Modrinth...",
            (2, "Retry installation") => "Tentar instalar novamente",
            (2, "Download Minecraft 26.3") => "Baixar Minecraft 26.3",
            (2, "Resolve") => "Resolver",
            (2, "Import") => "Importar",
            (2, "Export") => "Exportar",
            (2, "Open folder") => "Abrir pasta",
            (2, "Open Files") => "Abrir arquivos",
            (2, "World Files") => "Arquivos de mundos",
            (2, "File Manager") => "Gerenciador de arquivos",
            (2, "Confirm Delete") => "Confirmar exclusão",
            (2, "Delete selected item?") => "Excluir o item selecionado?",
            (3, "Shader Packs") => "Packs de shaders",
            (3, "Resource Packs") => "Packs de ressources",
            (3, "Available Versions") => "Versions disponibles",
            (3, "Search catalog...") => "Rechercher dans le catalogue...",
            (3, "Search installed mods, packs & shaders...") => "Rechercher les mods, packs et shaders installés...",
            (3, "Import .hmod") => "Importer .hmod",
            (3, "Import Mod") => "Importer un mod",
            (3, "Import Pack") => "Importer un pack",
            (3, "Import file") => "Importer un fichier",
            (3, "No local add-ons installed yet") => "Aucun module local installé pour le moment",
            (3, "No installed add-ons match your filter") => "Aucun module installé ne correspond au filtre",
            (3, "Loading content catalog...") => "Chargement du catalogue de contenu...",
            (3, "Syncing Modrinth details...") => "Synchronisation des informations Modrinth...",
            (3, "Retry installation") => "Réessayer l’installation",
            (3, "Download Minecraft 26.3") => "Télécharger Minecraft 26.3",
            (3, "Resolve") => "Résoudre",
            (3, "Import") => "Importer",
            (3, "Export") => "Exporter",
            (3, "Open folder") => "Ouvrir le dossier",
            (3, "Open Files") => "Ouvrir les fichiers",
            (3, "World Files") => "Fichiers des mondes",
            (3, "File Manager") => "Gestionnaire de fichiers",
            (3, "Confirm Delete") => "Confirmer la suppression",
            (3, "Delete selected item?") => "Supprimer l’élément sélectionné ?",
            (4, "Shader Packs") => "Pacchetti shader",
            (4, "Resource Packs") => "Pacchetti risorse",
            (4, "Available Versions") => "Versioni disponibili",
            (4, "Search catalog...") => "Cerca nel catalogo...",
            (4, "Search installed mods, packs & shaders...") => "Cerca mod, pacchetti e shader installati...",
            (4, "Import .hmod") => "Importa .hmod",
            (4, "Import Mod") => "Importa mod",
            (4, "Import Pack") => "Importa pacchetto",
            (4, "Import file") => "Importa file",
            (4, "No local add-ons installed yet") => "Nessun componente aggiuntivo locale installato",
            (4, "No installed add-ons match your filter") => "Nessun componente installato corrisponde al filtro",
            (4, "Loading content catalog...") => "Caricamento catalogo contenuti...",
            (4, "Syncing Modrinth details...") => "Sincronizzazione dettagli Modrinth...",
            (4, "Retry installation") => "Riprova installazione",
            (4, "Download Minecraft 26.3") => "Scarica Minecraft 26.3",
            (4, "Resolve") => "Risolvi",
            (4, "Import") => "Importa",
            (4, "Export") => "Esporta",
            (4, "Open folder") => "Apri cartella",
            (4, "Open Files") => "Apri file",
            (4, "World Files") => "File dei mondi",
            (4, "File Manager") => "Gestione file",
            (4, "Confirm Delete") => "Conferma eliminazione",
            (4, "Delete selected item?") => "Eliminare l'elemento selezionato?",
            (5, "Shader Packs") => "Shader-Pakete",
            (5, "Resource Packs") => "Ressourcenpakete",
            (5, "Available Versions") => "Verfügbare Versionen",
            (5, "Search catalog...") => "Katalog durchsuchen...",
            (5, "Search installed mods, packs & shaders...") => "Installierte Mods, Pakete und Shader suchen...",
            (5, "Import .hmod") => ".hmod importieren",
            (5, "Import Mod") => "Mod importieren",
            (5, "Import Pack") => "Paket importieren",
            (5, "Import file") => "Datei importieren",
            (5, "No local add-ons installed yet") => "Noch keine lokalen Erweiterungen installiert",
            (5, "No installed add-ons match your filter") => "Keine installierte Erweiterung entspricht dem Filter",
            (5, "Loading content catalog...") => "Inhaltskatalog wird geladen...",
            (5, "Syncing Modrinth details...") => "Modrinth-Details werden synchronisiert...",
            (5, "Retry installation") => "Installation erneut versuchen",
            (5, "Download Minecraft 26.3") => "Minecraft 26.3 herunterladen",
            (5, "Resolve") => "Beheben",
            (5, "Import") => "Importieren",
            (5, "Export") => "Exportieren",
            (5, "Open folder") => "Ordner öffnen",
            (5, "Open Files") => "Dateien öffnen",
            (5, "World Files") => "Welten-Dateien",
            (5, "File Manager") => "Dateimanager",
            (5, "Confirm Delete") => "Löschen bestätigen",
            (5, "Delete selected item?") => "Ausgewähltes Element löschen?",
            (6, "Shader Packs") => "シェーダーパック",
            (6, "Resource Packs") => "リソースパック",
            (6, "Available Versions") => "利用可能なバージョン",
            (6, "Search catalog...") => "カタログを検索...",
            (6, "Search installed mods, packs & shaders...") => "インストール済みの MOD・パック・シェーダーを検索...",
            (6, "Import .hmod") => ".hmod をインポート",
            (6, "Import Mod") => "MOD をインポート",
            (6, "Import Pack") => "パックをインポート",
            (6, "Import file") => "ファイルをインポート",
            (6, "No local add-ons installed yet") => "ローカルの追加コンテンツはまだありません",
            (6, "No installed add-ons match your filter") => "条件に一致するインストール済みコンテンツはありません",
            (6, "Loading content catalog...") => "コンテンツカタログを読み込み中...",
            (6, "Syncing Modrinth details...") => "Modrinth の情報を同期中...",
            (6, "Retry installation") => "インストールを再試行",
            (6, "Download Minecraft 26.3") => "Minecraft 26.3 をダウンロード",
            (6, "Resolve") => "解決",
            (6, "Import") => "インポート",
            (6, "Export") => "エクスポート",
            (6, "Open folder") => "フォルダーを開く",
            (6, "Open Files") => "ファイルを開く",
            (6, "World Files") => "ワールドファイル",
            (6, "File Manager") => "ファイルマネージャー",
            (6, "Confirm Delete") => "削除の確認",
            (6, "Delete selected item?") => "選択した項目を削除しますか？",
            (7, "Shader Packs") => "光影包",
            (7, "Resource Packs") => "资源包",
            (7, "Available Versions") => "可用版本",
            (7, "Search catalog...") => "搜索目录...",
            (7, "Search installed mods, packs & shaders...") => "搜索已安装的模组、资源包和光影...",
            (7, "Import .hmod") => "导入 .hmod",
            (7, "Import Mod") => "导入模组",
            (7, "Import Pack") => "导入资源包",
            (7, "Import file") => "导入文件",
            (7, "No local add-ons installed yet") => "尚未安装本地附加内容",
            (7, "No installed add-ons match your filter") => "没有符合筛选条件的已安装内容",
            (7, "Loading content catalog...") => "正在加载内容目录...",
            (7, "Syncing Modrinth details...") => "正在同步 Modrinth 信息...",
            (7, "Retry installation") => "重试安装",
            (7, "Download Minecraft 26.3") => "下载 Minecraft 26.3",
            (7, "Resolve") => "解决",
            (7, "Import") => "导入",
            (7, "Export") => "导出",
            (7, "Open folder") => "打开文件夹",
            (7, "Open Files") => "打开文件",
            (7, "World Files") => "世界文件",
            (7, "File Manager") => "文件管理器",
            (7, "Confirm Delete") => "确认删除",
            (7, "Delete selected item?") => "删除所选项目？",
            (0, "PavoHorizon para Android") => "PavoHorizon for Android",
            (0, "Exportar mundos (SAF)") => "Export worlds (SAF)",
            (0, "Play Minecraft 26.3") => "Play Minecraft 26.3",
            (0, "Retry installation") => "Retry installation",
            (0, "Download Minecraft 26.3") => "Download Minecraft 26.3",
            (0, "Launching...") => "Launching...",
            (1, "PavoHorizon para Android") => "PavoHorizon para Android",
            (1, "Exportar mundos (SAF)") => "Exportar mundos (SAF)",
            (1, "Play Minecraft 26.3") => "Jugar Minecraft 26.3",
            (1, "Launching...") => "Iniciando...",
            (2, "PavoHorizon para Android") => "PavoHorizon para Android",
            (2, "Exportar mundos (SAF)") => "Exportar mundos (SAF)",
            (2, "Play Minecraft 26.3") => "Jogar Minecraft 26.3",
            (2, "Launching...") => "Iniciando...",
            (3, "PavoHorizon para Android") => "PavoHorizon pour Android",
            (3, "Exportar mundos (SAF)") => "Exporter les mondes (SAF)",
            (3, "Play Minecraft 26.3") => "Jouer à Minecraft 26.3",
            (3, "Launching...") => "Démarrage...",
            (4, "PavoHorizon para Android") => "PavoHorizon per Android",
            (4, "Exportar mundos (SAF)") => "Esporta mondi (SAF)",
            (4, "Play Minecraft 26.3") => "Gioca a Minecraft 26.3",
            (4, "Launching...") => "Avvio...",
            (5, "PavoHorizon para Android") => "PavoHorizon für Android",
            (5, "Exportar mundos (SAF)") => "Welten exportieren (SAF)",
            (5, "Play Minecraft 26.3") => "Minecraft 26.3 spielen",
            (5, "Launching...") => "Wird gestartet...",
            (6, "PavoHorizon para Android") => "Android 版 PavoHorizon",
            (6, "Exportar mundos (SAF)") => "ワールドをエクスポート (SAF)",
            (6, "Play Minecraft 26.3") => "Minecraft 26.3 をプレイ",
            (6, "Launching...") => "起動中...",
            (7, "PavoHorizon para Android") => "Android 版 PavoHorizon",
            (7, "Exportar mundos (SAF)") => "导出世界 (SAF)",
            (7, "Play Minecraft 26.3") => "开始游戏 Minecraft 26.3",
            (7, "Launching...") => "正在启动...",
            _ => text,
        };
        translated.to_owned()
    })
}

"""
    if source.count(ui_anchor) != 1:
        raise SystemExit("Could not locate Horizon UI struct for language support.")
    source = source.replace(ui_anchor, language_ui + ui_anchor, 1)
    fit_anchor = "    pub fn fit_text(&self, face: Face, size: f32, text: &str, width: f32) -> String {\n"
    if source.count(fit_anchor) != 1:
        raise SystemExit("Could not locate Horizon translated text fitting.")
    source = source.replace(
        fit_anchor,
        fit_anchor + "        let translated = pavo_launcher_text(text);\n        let text = translated.as_str();\n",
        1,
    )
    measure_anchor = "    pub fn measure(&self, face: Face, size: f32, text: &str) -> f32 {\n"
    if source.count(measure_anchor) != 1:
        raise SystemExit("Could not locate Horizon translated text measurement.")
    source = source.replace(
        measure_anchor,
        measure_anchor + "        let translated = pavo_launcher_text(text);\n        let text = translated.as_str();\n",
        1,
    )
    text_anchor = "        self.text_line(face, size, (size * 1.45).round(), x, y, color, text)\n"
    if source.count(text_anchor) != 1:
        raise SystemExit("Could not locate Horizon UI text renderer.")
    source = source.replace(
        text_anchor,
        "        let translated = pavo_launcher_text(text);\n        self.text_line(face, size, (size * 1.45).round(), x, y, color, &translated)\n",
        1,
    )
    ui_theme.write_text(source, encoding="utf-8")

    source = pages.read_text(encoding="utf-8")
    draw_anchor = "        if r.suspended {\n            return Ok(());\n        }\n"
    if source.count(draw_anchor) != 1:
        raise SystemExit("Could not locate Android launcher frame entry point.")
    source = source.replace(
        draw_anchor,
        draw_anchor + "        horizon_client::ui_theme::set_launcher_language(&self.catalog.config.language);\n",
        1,
    )
    pages.write_text(source, encoding="utf-8")
    print("PavoHorizon language support: detects Android locale, persists manual override, and translates launcher UI plus Minecraft.", flush=True)

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
    patch_pavo_native_builds(source)
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
    patch_android_language(stage)

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

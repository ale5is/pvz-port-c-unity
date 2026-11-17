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

fn pavo_launcher_text(text: &str) -> String {
    PAVO_LAUNCHER_LANGUAGE.with(|current| {
        let lang = current.get();
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
            (1, "Installed") => "Instalados",
            (1, "Install") => "Instalar",
            (1, "Download") => "Descargar",
            (1, "Verify") => "Verificar",
            (1, "Resolve") => "Resolver",
            (1, "Import") => "Importar",
            (1, "Export") => "Exportar",
            (1, "Open folder") => "Abrir carpeta",
            (1, "Open Files") => "Abrir archivos",
            (1, "World Files") => "Archivos de mundos",
            (1, "Profile File Manager") => "Administrador de archivos del perfil",
            (1, "File Manager") => "Administrador de archivos",
            (1, "Confirm Delete") => "Confirmar eliminación",
            (1, "Delete selected item?") => "¿Eliminar el elemento seleccionado?",
            (1, "Rename File or Folder") => "Renombrar archivo o carpeta",
            (1, "Select a file to export.") => "Selecciona un archivo para exportar.",
            (1, "This folder is empty") => "Esta carpeta está vacía",
            (1, "← Back") => "← Volver",
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
            (2, "Installed") => "Instalados",
            (2, "Install") => "Instalar",
            (2, "Download") => "Baixar",
            (2, "Verify") => "Verificar",
            (2, "Resolve") => "Resolver",
            (2, "Import") => "Importar",
            (2, "Export") => "Exportar",
            (2, "Open folder") => "Abrir pasta",
            (2, "Open Files") => "Abrir arquivos",
            (2, "World Files") => "Arquivos de mundos",
            (2, "File Manager") => "Gerenciador de arquivos",
            (2, "Confirm Delete") => "Confirmar exclusão",
            (2, "Delete selected item?") => "Excluir o item selecionado?",
            (2, "← Back") => "← Voltar",
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
            (3, "Installed") => "Installés",
            (3, "Install") => "Installer",
            (3, "Download") => "Télécharger",
            (3, "Verify") => "Vérifier",
            (3, "Resolve") => "Résoudre",
            (3, "Import") => "Importer",
            (3, "Export") => "Exporter",
            (3, "Open folder") => "Ouvrir le dossier",
            (3, "Open Files") => "Ouvrir les fichiers",
            (3, "World Files") => "Fichiers des mondes",
            (3, "File Manager") => "Gestionnaire de fichiers",
            (3, "Confirm Delete") => "Confirmer la suppression",
            (3, "Delete selected item?") => "Supprimer l’élément sélectionné ?",
            (3, "← Back") => "← Retour",
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
            (4, "Installed") => "Installati",
            (4, "Install") => "Installa",
            (4, "Download") => "Scarica",
            (4, "Verify") => "Verifica",
            (4, "Resolve") => "Risolvi",
            (4, "Import") => "Importa",
            (4, "Export") => "Esporta",
            (4, "Open folder") => "Apri cartella",
            (4, "Open Files") => "Apri file",
            (4, "World Files") => "File dei mondi",
            (4, "File Manager") => "Gestione file",
            (4, "Confirm Delete") => "Conferma eliminazione",
            (4, "Delete selected item?") => "Eliminare l'elemento selezionato?",
            (4, "← Back") => "← Indietro",
            (5, "Shader Packs") => "Shader-Pakete",
            (5, "Resource Packs") => "R Ressourcenpakete",
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
            (5, "Installed") => "Installiert",
            (5, "Install") => "Installieren",
            (5, "Download") => "Herunterladen",
            (5, "Verify") => "Überprüfen",
            (5, "Resolve") => "Beheben",
            (5, "Import") => "Importieren",
            (5, "Export") => "Exportieren",
            (5, "Open folder") => "Ordner öffnen",
            (5, "Open Files") => "Dateien öffnen",
            (5, "World Files") => "Welten-Dateien",
            (5, "File Manager") => "Dateimanager",
            (5, "Confirm Delete") => "Löschen bestätigen",
            (5, "Delete selected item?") => "Ausgewähltes Element löschen?",
            (5, "← Back") => "← Zurück",
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
            (6, "Installed") => "インストール済み",
            (6, "Install") => "インストール",
            (6, "Download") => "ダウンロード",
            (6, "Verify") => "検証",
            (6, "Resolve") => "解決",
            (6, "Import") => "インポート",
            (6, "Export") => "エクスポート",
            (6, "Open folder") => "フォルダーを開く",
            (6, "Open Files") => "ファイルを開く",
            (6, "World Files") => "ワールドファイル",
            (6, "File Manager") => "ファイルマネージャー",
            (6, "Confirm Delete") => "削除の確認",
            (6, "Delete selected item?") => "選択した項目を削除しますか？",
            (6, "← Back") => "← 戻る",
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
            (7, "Installed") => "已安装",
            (7, "Install") => "安装",
            (7, "Download") => "下载",
            (7, "Verify") => "验证",
            (7, "Resolve") => "解决",
            (7, "Import") => "导入",
            (7, "Export") => "导出",
            (7, "Open folder") => "打开文件夹",
            (7, "Open Files") => "打开文件",
            (7, "World Files") => "世界文件",
            (7, "File Manager") => "文件管理器",
            (7, "Confirm Delete") => "确认删除",
            (7, "Delete selected item?") => "删除所选项目？",
            (7, "← Back") => "← 返回",
            (0, "PavoHorizon para Android") => "PavoHorizon for Android",
            (0, "Exportar mundos (SAF)") => "Export worlds (SAF)",
            (0, "Play Minecraft 26.3") => "Play Minecraft 26.3",
            (0, "Retry installation") => "Retry installation",
            (0, "Download Minecraft 26.3") => "Download Minecraft 26.3",
            (0, "Launching...") => "Launching...",
            (1, "PavoHorizon para Android") => "PavoHorizon para Android",
            (1, "Exportar mundos (SAF)") => "Exportar mundos (SAF)",
            (1, "Play Minecraft 26.3") => "Jugar Minecraft 26.3",
            (1, "Retry installation") => "Reintentar instalación",
            (1, "Download Minecraft 26.3") => "Descargar Minecraft 26.3",
            (1, "Launching...") => "Iniciando...",
            (2, "PavoHorizon para Android") => "PavoHorizon para Android",
            (2, "Exportar mundos (SAF)") => "Exportar mundos (SAF)",
            (2, "Play Minecraft 26.3") => "Jogar Minecraft 26.3",
            (2, "Retry installation") => "Tentar instalar novamente",
            (2, "Download Minecraft 26.3") => "Baixar Minecraft 26.3",
            (2, "Launching...") => "Iniciando...",
            (3, "PavoHorizon para Android") => "PavoHorizon pour Android",
            (3, "Exportar mundos (SAF)") => "Exporter les mondes (SAF)",
            (3, "Play Minecraft 26.3") => "Jouer à Minecraft 26.3",
            (3, "Retry installation") => "Réessayer l’installation",
            (3, "Download Minecraft 26.3") => "Télécharger Minecraft 26.3",
            (3, "Launching...") => "Démarrage...",
            (4, "PavoHorizon para Android") => "PavoHorizon per Android",
            (4, "Exportar mundos (SAF)") => "Esporta mondi (SAF)",
            (4, "Play Minecraft 26.3") => "Gioca a Minecraft 26.3",
            (4, "Retry installation") => "Riprova installazione",
            (4, "Download Minecraft 26.3") => "Scarica Minecraft 26.3",
            (4, "Launching...") => "Avvio...",
            (5, "PavoHorizon para Android") => "PavoHorizon für Android",
            (5, "Exportar mundos (SAF)") => "Welten exportieren (SAF)",
            (5, "Play Minecraft 26.3") => "Minecraft 26.3 spielen",
            (5, "Retry installation") => "Installation erneut versuchen",
            (5, "Download Minecraft 26.3") => "Minecraft 26.3 herunterladen",
            (5, "Launching...") => "Wird gestartet...",
            (6, "PavoHorizon para Android") => "Android 版 PavoHorizon",
            (6, "Exportar mundos (SAF)") => "ワールドをエクスポート (SAF)",
            (6, "Play Minecraft 26.3") => "Minecraft 26.3 をプレイ",
            (6, "Retry installation") => "インストールを再試行",
            (6, "Download Minecraft 26.3") => "Minecraft 26.3 をダウンロード",
            (6, "Launching...") => "起動中...",
            (7, "PavoHorizon para Android") => "Android 版 PavoHorizon",
            (7, "Exportar mundos (SAF)") => "导出世界 (SAF)",
            (7, "Play Minecraft 26.3") => "开始游戏 Minecraft 26.3",
            (7, "Retry installation") => "重试安装",
            (7, "Download Minecraft 26.3") => "下载 Minecraft 26.3",
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

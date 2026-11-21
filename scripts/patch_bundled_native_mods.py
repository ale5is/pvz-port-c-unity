#!/usr/bin/env python3
"""Patch the pinned Horizon Android launcher so Pavo's compiled native mods are toggles."""
from pathlib import Path
import sys

def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one exact match, found {count}")
    return source.replace(old, new, 1)

def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: patch_bundled_native_mods.py HORIZON_SOURCE")
    root = Path(sys.argv[1]).resolve()

    mod_list = root / "crates/horizon-client-render/src/mod_list.rs"
    source = mod_list.read_text(encoding="utf-8")
    source = replace_once(
        source,
        'pub const ANDROID_BUNDLED: [(&str, &str, &str); 4] = [',
        'pub const ANDROID_BUNDLED: [(&str, &str, &str); 6] = [',
        "Android bundled mod count",
    )
    old_end = '''    (
        "smartf3",
        "SmartF3",
        "Configurable modules for the F3 debug screen",
    ),
];'''
    new_end = '''    (
        "smartf3",
        "SmartF3",
        "Configurable modules for the F3 debug screen",
    ),
    (
        "jade",
        "Jade",
        "Information about the block or entity under the crosshair",
    ),
    (
        "simple-voice-chat",
        "Simple Voice Chat",
        "Proximity voice chat with push-to-talk and mute controls",
    ),
];'''
    source = replace_once(source, old_end, new_end, "Android bundled native mod entries")
    mod_list.write_text(source, encoding="utf-8")

    catalog = root / "crates/horizon-client/src/android/catalog.rs"
    source = catalog.read_text(encoding="utf-8")
    source = replace_once(
        source,
        'self.bundled_enabled = on.enabled.difference(&on.disabled).cloned().collect();',
        '''self.bundled_enabled = horizon_client::mod_list::ANDROID_BUNDLED
            .iter()
            .filter(|(id, _, _)| !on.disabled.contains(*id))
            .map(|(id, _, _)| (*id).to_owned())
            .collect();''',
        "Built-in native mod default state",
    )
    source = replace_once(
        source,
        'if on.enabled.contains(b_id) && !on.disabled.contains(b_id) {',
        'if !on.disabled.contains(b_id) {',
        "Installed list built-in native mod state",
    )
    catalog.write_text(source, encoding="utf-8")

    pages = root / "crates/horizon-client/src/android/launcher_pages.rs"
    source = pages.read_text(encoding="utf-8")
    helper_anchor = 'pub(super) fn button(\n'
    helper = '''fn pavo_bundled_action_label(enabled: bool, language: &str) -> &'static str {
    match (enabled, language) {
        (true, "es_es") => "Desactivar",
        (false, "es_es") => "Activar",
        (true, "pt_br") => "Desativar",
        (false, "pt_br") => "Ativar",
        (true, "fr_fr") => "Désactiver",
        (false, "fr_fr") => "Activer",
        (true, "it_it") => "Disattiva",
        (false, "it_it") => "Attiva",
        (true, "de_de") => "Deaktivieren",
        (false, "de_de") => "Aktivieren",
        (true, "ja_jp") => "無効化",
        (false, "ja_jp") => "有効化",
        (true, "zh_cn") => "禁用",
        (false, "zh_cn") => "启用",
        (true, _) => "Disable",
        (false, _) => "Enable",
    }
}

'''
    source = replace_once(source, helper_anchor, helper + helper_anchor, "Bundled native mod action label helper")
    source = replace_once(
        source,
        'if enabled { "Installed" } else { "Install" },',
        'pavo_bundled_action_label(enabled, &self.catalog.config.language),',
        "Bundled mod list button label",
    )
    source = replace_once(
        source,
        '''                if self.catalog.bundled_enabled.contains(bundled) {
                    "Installed (Tap to Disable)".to_owned()
                } else {
                    format!("Enable Built-in ({version})")
                }''',
        '''                pavo_bundled_action_label(
                    self.catalog.bundled_enabled.contains(bundled),
                    &self.catalog.config.language,
                )
                .to_owned()''',
        "Bundled mod detail button label",
    )
    source = replace_once(
        source,
        '''            if enabled {
                "disabled"
            } else {
                "installed & enabled"
            }''',
        '''            if enabled {
                "disabled"
            } else {
                "enabled"
            }''',
        "Bundled mod toggle status wording",
    )
    source = replace_once(
        source,
        'let enabled = list.enabled.contains(id) && !list.disabled.contains(id);',
        'let enabled = !list.disabled.contains(id);',
        "Native mod default-on toggle state",
    )
    pages.write_text(source, encoding="utf-8")
    print("Pavo Android catalog: Jade and Simple Voice Chat are listed as built-in native mods; their controls now activate/deactivate them without installing files.")

if __name__ == "__main__":
    main()

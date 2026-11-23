#!/usr/bin/env python3
from pathlib import Path
import sys


def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one exact match, found {count}")
    return source.replace(old, new, 1)


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: patch_starling_090_performance.py HORIZON_SOURCE")

    root = Path(sys.argv[1]).resolve()
    options = root / "crates/horizon-client/src/game/options.rs"
    source = options.read_text(encoding="utf-8")

    helper = '''/// Cap Android menus over a world at 60 FPS without changing world rendering.
fn menu_frame_cap(limit: i32, has_screen: bool) -> i32 {
    if cfg!(target_os = "android") && has_screen {
        limit.min(60)
    } else {
        limit
    }
}

'''
    source = replace_once(
        source,
        "impl Game<'_> {\n",
        helper + "impl Game<'_> {\n",
        "Android menu frame-cap helper",
    )

    source = replace_once(
        source,
        '''        let limit = framerate_limit(self.settings.max_fps as i32, context);
        self.settings
            .horizon
            .battery_limit(limit, self.title.is_none(), self.focused)
''',
        '''        let limit = framerate_limit(self.settings.max_fps as i32, context);
        let limit = self
            .settings
            .horizon
            .battery_limit(limit, self.title.is_none(), self.focused);
        menu_frame_cap(limit, context.has_screen)
''',
        "Android menu frame-cap application",
    )

    tests = '''
#[cfg(test)]
mod menu_frame_cap_tests {
    use super::menu_frame_cap;

    #[test]
    fn a_menu_caps_the_frame_rate_on_android_only() {
        let capped = if cfg!(target_os = "android") { 60 } else { 300 };
        assert_eq!(menu_frame_cap(300, true), capped);
        assert_eq!(menu_frame_cap(300, false), 300);
        assert_eq!(menu_frame_cap(30, true), 30);
    }
}
'''
    if "mod menu_frame_cap_tests" in source:
        raise SystemExit("Android menu frame-cap tests already exist; refusing to duplicate them.")
    source += tests
    options.write_text(source, encoding="utf-8")
    print("Starling 0.9.0: Android menus over a world are capped at 60 FPS; world FPS limits remain unchanged.")


if __name__ == "__main__":
    main()

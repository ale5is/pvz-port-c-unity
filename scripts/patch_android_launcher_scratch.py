#!/usr/bin/env python3
"""Backport Starling 0.9's isolated HudBatch scale to the Android launcher."""

import argparse
from pathlib import Path


def patch(source_root: Path) -> None:
    hud_path = source_root / "crates/horizon-client-render/src/hud.rs"
    pages_path = source_root / "crates/horizon-client/src/android/launcher_pages.rs"

    if not hud_path.is_file() or not pages_path.is_file():
        raise SystemExit("The selected folder is not a complete Horizon 0.7.8 source tree.")

    hud = hud_path.read_text(encoding="utf-8")
    pages = pages_path.read_text(encoding="utf-8")

    method = """    /// An offscreen or auxiliary UI batch, independent of the in-game GUI Scale option.
    ///
    /// Starling 0.9 uses this for launcher pages, editor drawing, and editor touch hit-testing.
    pub fn scratch(window: [u32; 2]) -> Self {
        let mut scale = 1;
        while window[0] / (scale + 1) >= 320 && window[1] / (scale + 1) >= 240 {
            scale += 1;
        }
        Self {
            vertices: Vec::new(),
            scale: scale as f32,
            width: (window[0] as f32 / scale as f32).ceil(),
            height: (window[1] as f32 / scale as f32).ceil(),
            patches: Vec::new(),
            blends: Vec::new(),
            end_portal: EndPortalFill::default(),
            glint_alpha: 0.75,
            game_time: 0.0,
            blur_at: 0,
            inline_profiles: Vec::new(),
        }
    }
"""

    anchor = """            inline_profiles: Vec::new(),
        }
    }
    /// Draw the vertices added since \"start\" with the inverting blend.
"""
    if "pub fn scratch(window: [u32; 2])" not in hud:
        if hud.count(anchor) != 1:
            raise SystemExit("Could not uniquely locate HudBatch::new() to add the Starling scratch constructor.")
        hud = hud.replace(anchor, """            inline_profiles: Vec::new(),
        }
    }
""" + method + """    /// Draw the vertices added since \"start\" with the inverting blend.
""", 1)
    elif hud.count("pub fn scratch(window: [u32; 2])") != 1:
        raise SystemExit("HudBatch::scratch exists more than once; refusing an ambiguous patch.")

    new_count = pages.count("HudBatch::new(")
    scratch_count = pages.count("HudBatch::scratch(")
    if new_count:
        if new_count != 5 or scratch_count:
            raise SystemExit(
                f"Unexpected launcher batch constructors: new={new_count}, scratch={scratch_count}; "
                "expected the five known Horizon 0.7.8 call sites."
            )
        pages = pages.replace("HudBatch::new(", "HudBatch::scratch(")
    elif scratch_count != 5:
        raise SystemExit(
            f"Unexpected launcher batch constructors after patch: new={new_count}, scratch={scratch_count}."
        )

    method_start = hud.index("pub fn scratch(window: [u32; 2])")
    method_end = hud.index("\n    }", method_start) + len("\n    }")
    scratch_method = hud[method_start:method_end]
    if "GUI_SCALE" in scratch_method:
        raise SystemExit("HudBatch::scratch must not read the in-game GUI_SCALE setting.")

    hud_path.write_text(hud, encoding="utf-8")
    pages_path.write_text(pages, encoding="utf-8")
    print(
        "Android launcher UI: backported Starling 0.9 HudBatch::scratch; "
        "all five scale/draw/touch call sites now use the independent auto scale.",
        flush=True,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("horizon_source", type=Path)
    args = parser.parse_args()
    patch(args.horizon_source.expanduser().resolve())


if __name__ == "__main__":
    main()

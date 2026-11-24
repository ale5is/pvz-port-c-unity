#!/usr/bin/env python3
"""Keep the full-screen Android control editor canvas opaque."""

import argparse
from pathlib import Path

OLD = "ui.fill(0.0, 0.0, w, h, rgba(9, 10, 16, 228));"
NEW = "ui.fill(0.0, 0.0, w, h, rgba(9, 10, 16, 255));"


def patch(source_root: Path) -> None:
    path = source_root / "crates/horizon-client/src/touch_ui.rs"
    if not path.is_file():
        raise SystemExit("The selected folder is not a complete Horizon 0.7.8 source tree.")

    source = path.read_text(encoding="utf-8")
    start = source.find("pub fn draw_hud_editor(")
    end = source.find("\n/// Handles touch input for the MCPE + GoyControl Editor", start)
    if start < 0 or end < 0:
        raise SystemExit("Could not uniquely locate the Android control editor drawing function.")

    body = source[start:end]
    old_count = body.count(OLD)
    new_count = body.count(NEW)
    if old_count == 1 and new_count == 0:
        body = body.replace(OLD, NEW, 1)
        source = source[:start] + body + source[end:]
        path.write_text(source, encoding="utf-8")
    elif old_count == 0 and new_count == 1:
        pass
    else:
        raise SystemExit(
            f"Unexpected control editor canvas alpha: old={old_count}, new={new_count}; "
            "expected exactly one known full-screen background fill."
        )

    print(
        "Android control editor: full-screen blueprint canvas alpha set to 255; "
        "control layout, grid accents, gameplay HUD and renderer are unchanged.",
        flush=True,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("horizon_source", type=Path)
    args = parser.parse_args()
    patch(args.horizon_source.expanduser().resolve())


if __name__ == "__main__":
    main()

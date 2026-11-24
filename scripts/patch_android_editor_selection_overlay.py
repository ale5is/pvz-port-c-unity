#!/usr/bin/env python3
"""Use solid pixel-aligned geometry for the Android editor selection overlay."""

import argparse
from pathlib import Path

START = "    // 5. Draw 8 Interactive Corner & Edge Resize Handles around Selected Component\n"
END = "    // 6. Larger Material You 3 Moveable Floating Menu Pod (`356x48dp`)\n"

REPLACEMENT = """    // 5. Draw the selection overlay with solid rectangles only.
    // Avoid the cached alpha-corner textures used by rounded/stroked Ui primitives.
    if let Some(([rx, ry, rw, rh], sel_name)) = selected_rect_and_title {
        let border = 1.5;
        let left = rx - 2.0;
        let top = ry - 2.0;
        let right = rx + rw + 2.0;
        let bottom = ry + rh + 2.0;
        ui.fill(left, top, right - left, border, accent);
        ui.fill(left, bottom - border, right - left, border, accent);
        ui.fill(left, top + border, border, bottom - top - border * 2.0, accent);
        ui.fill(right - border, top + border, border, bottom - top - border * 2.0, accent);

        let handles = [
            (rx - 4.5, ry - 4.5, 9.0, 9.0),
            (rx + rw - 4.5, ry - 4.5, 9.0, 9.0),
            (rx - 4.5, ry + rh - 4.5, 9.0, 9.0),
            (rx + rw - 4.5, ry + rh - 4.5, 9.0, 9.0),
            (rx + rw - 3.5, ry + rh * 0.5 - 7.0, 7.0, 14.0),
            (rx - 3.5, ry + rh * 0.5 - 7.0, 7.0, 14.0),
            (rx + rw * 0.5 - 7.0, ry + rh - 3.5, 14.0, 7.0),
            (rx + rw * 0.5 - 7.0, ry - 3.5, 14.0, 7.0),
        ];
        for (hx, hy, hw, hh) in handles {
            ui.fill(hx, hy, hw, hh, accent);
            let inset = 1.0;
            ui.fill(hx + inset, hy + inset, (hw - inset * 2.0).max(0.0), inset, accent_fg);
            ui.fill(hx + inset, hy + hh - inset * 2.0, (hw - inset * 2.0).max(0.0), inset, accent_fg);
            ui.fill(hx + inset, hy + inset, inset, (hh - inset * 2.0).max(0.0), accent_fg);
            ui.fill(hx + hw - inset * 2.0, hy + inset, inset, (hh - inset * 2.0).max(0.0), accent_fg);
        }
        let tag = format!("{sel_name} (Drag corners/edges to resize)");
        let tw = (ui.measure(Face::SemiBold, 9.5, &tag) + 12.0).min(w - 16.0);
        let tx = (rx + (rw - tw) * 0.5).clamp(8.0, (w - tw - 8.0).max(8.0));
        let ty = if ry > 24.0 {
            ry - 20.0
        } else {
            (ry + rh + 5.0).min(h - 20.0)
        };
        ui.fill(tx, ty, tw, 16.0, bg_card);
        ui.fill(tx, ty, tw, 1.0, accent);
        ui.fill(tx, ty + 15.0, tw, 1.0, accent);
        ui.fill(tx, ty + 1.0, 1.0, 14.0, accent);
        ui.fill(tx + tw - 1.0, ty + 1.0, 1.0, 14.0, accent);
        ui.centered_fit(
            Face::SemiBold,
            9.0,
            [tx + 3.0, ty, tw - 6.0, 16.0],
            accent,
            &tag,
        );
    }

"""

def patch(source_root: Path) -> None:
    path = source_root / "crates/horizon-client/src/touch_ui.rs"
    if not path.is_file():
        raise SystemExit("The selected folder is not a complete Horizon 0.7.8 source tree.")
    source = path.read_text(encoding="utf-8")
    start_count = source.count(START)
    end_count = source.count(END)
    if start_count != 1 or end_count != 1:
        raise SystemExit(
            f"Could not uniquely locate the editor selection overlay: start={start_count}, end={end_count}."
        )
    start = source.index(START)
    end = source.index(END, start)
    body = source[start:end]
    if "Avoid the cached alpha-corner textures" in body:
        print("Android editor selection overlay already uses solid rectangles.", flush=True)
        return
    if "ui.stroke_rounded(rx - 2.0" not in body or "ui.rounded(hx, hy, hw, hh" not in body:
        raise SystemExit("Unexpected selection overlay implementation; refusing to patch.")
    source = source[:start] + REPLACEMENT + source[end:]
    path.write_text(source, encoding="utf-8")
    print(
        "Android editor selection overlay: replaced rounded alpha-corner primitives with solid pixel-aligned rectangles; "
        "button rendering, touch handling, layout and gameplay HUD are unchanged.",
        flush=True,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("horizon_source", type=Path)
    args = parser.parse_args()
    patch(args.horizon_source.expanduser().resolve())


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Compare the 0.7.8 Android frontend's public API needs with Nightly exports."""
from __future__ import annotations

import json
import re
from pathlib import Path

BASE = Path("upstream/horizon-078/crates/horizon-client/src")
NIGHTLY = Path("horizon/crates/horizon-client/src")
RENDER = Path("horizon/crates/horizon-client-render/src")


def exported_modules(path: Path) -> set[str]:
    source = path.read_text(encoding="utf-8")
    found = set(re.findall(r"(?m)^\s*pub\s+mod\s+([A-Za-z_]\w*)\s*;", source))
    # Public grouped re-exports, including aliases, are part of the usable API.
    for body in re.findall(r"pub\s+use\s+horizon_client_render::\s*\{(.*?)\};", source, re.S):
        for item in body.split(","):
            item = item.strip()
            if not item:
                continue
            match = re.match(r"([A-Za-z_]\w*)(?:\s+as\s+([A-Za-z_]\w*))?$", item)
            if match:
                found.add(match.group(2) or match.group(1))
    return found


def main() -> None:
    old_files = [BASE / "android_main.rs", BASE / "official_install.rs"]
    old_files += sorted((BASE / "android").rglob("*.rs"))
    old_files = [path for path in old_files if path.is_file()]

    client_exports = exported_modules(NIGHTLY / "client_lib.rs")
    client_exports |= exported_modules(NIGHTLY / "lib.rs")
    render_exports = exported_modules(RENDER / "lib.rs")

    references: dict[str, set[str]] = {}
    for path in old_files:
        source = path.read_text(encoding="utf-8")
        for module in re.findall(r"\bhorizon_client::([A-Za-z_]\w*)", source):
            references.setdefault(module, set()).add(path.relative_to(BASE).as_posix())

    rows = []
    for module, files in sorted(references.items()):
        if module in client_exports:
            status = "available"
            replacement = "horizon_client"
        elif module in render_exports:
            status = "moved-to-render"
            replacement = "horizon_client_render"
        else:
            status = "missing"
            replacement = ""
        rows.append({
            "module": module,
            "status": status,
            "replacement_crate": replacement,
            "referenced_by": sorted(files),
        })

    result = {
        "base_revision": "68a2c4dd1b9a9e511ad3758a79ab772b6f9e5b29",
        "nightly_revision": "85b5bcca7c79bb470b90bdb5914b413aa66fe7ae",
        "android_source_files_scanned": len(old_files),
        "client_exports": len(client_exports),
        "render_exports": len(render_exports),
        "references": rows,
    }
    out = Path("dist/android-port-api-audit.json")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")

    print(f"Android source files scanned: {len(old_files)}")
    print("Legacy API references:")
    for row in rows:
        target = f" -> {row['replacement_crate']}" if row["replacement_crate"] else ""
        print(f"  {row['status']:16} {row['module']}{target}")
    print(f"Audit written to {out}")


if __name__ == "__main__":
    main()

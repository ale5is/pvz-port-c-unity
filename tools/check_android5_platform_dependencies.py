#!/usr/bin/env python3
"""Assert that the Android dependency closure does not enable desktop GPU APIs."""
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_platform_dependencies.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()

def read(relative: str) -> str:
    path = root / relative
    if not path.is_file():
        raise SystemExit(f"FAIL: missing manifest {relative}")
    return path.read_text(encoding="utf-8")

render = read("crates/horizon-client-render/Cargo.toml")
client = read("crates/horizon-client/Cargo.toml")
android = read("crates/horizon-android/Cargo.toml")

def section(source: str, header: str) -> str:
    start = source.find(header)
    if start < 0:
        raise SystemExit(f"FAIL: missing target dependency section {header}")
    end = source.find("\n[", start + len(header))
    return source[start:] if end < 0 else source[start:end]

for label, source in (("renderer", render), ("client", client)):
    base = source.split("\n[target.", 1)[0]
    if '"vulkan"' in base or '"metal"' in base or '"dx12"' in base:
        raise SystemExit(f"FAIL: {label} base dependencies still enable a desktop GPU backend")
    android_section = section(source, '[target.\'cfg(target_os = "android")\'.dependencies]')
    if '"gles"' not in android_section or any(x in android_section for x in ('"vulkan"', '"metal"', '"dx12"')):
        raise SystemExit(f"FAIL: {label} Android target must enable GLES only")
    print(f"PASS: {label} Android backend feature isolation")

for label, source in (("renderer", render), ("client", client), ("Android cdylib", android)):
    normal_dependencies = source.split("\\n[dev-dependencies]", 1)[0]
    if '"hlsl-out"' in normal_dependencies:
        raise SystemExit(f"FAIL: {label} normal dependencies still enable HLSL-only shader output")
    print(f"PASS: {label} excludes HLSL-only compiler output from normal builds")

android_base = android.split("\n[target.", 1)[0]
if any(x in android_base for x in ('"vulkan"', '"metal"', '"dx12"')):
    raise SystemExit("FAIL: Android cdylib directly enables a desktop GPU backend")
if '"gles"' not in android_base:
    raise SystemExit("FAIL: Android cdylib must retain wgpu GLES until native GLES2 is fully integrated")
print("PASS: Android cdylib excludes Vulkan, Metal and Direct3D")
print("PASS: wgpu GLES remains a transitional backend; native GLES2 is not yet the game renderer")

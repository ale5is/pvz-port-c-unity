#!/usr/bin/env python3
"""Remove desktop GPU backends from the Android dependency graph.

The shared Horizon renderer is still wgpu-based at this stage. This patch keeps
its GLES implementation available for the existing Android path, but prevents
Android target resolution from enabling Vulkan, Metal or Direct3D backends.
The native GLES2 renderer migration is a separate integration task.
"""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()

render_manifest = root / "crates/horizon-client-render/Cargo.toml"
client_manifest = root / "crates/horizon-client/Cargo.toml"
android_manifest = root / "crates/horizon-android/Cargo.toml"

naga_replacements = (
    (render_manifest, 'naga = { version = "30.0.1", features = ["wgsl-in", "glsl-in", "hlsl-out"] }\\n',
     'naga = { version = "30.0.1", features = ["wgsl-in", "glsl-in"] }\\n', "renderer HLSL compiler output"),
    (client_manifest, 'naga = { version = "30.0.1", features = ["wgsl-in", "hlsl-out"] }\\n',
     'naga = { version = "30.0.1", features = ["wgsl-in"] }\\n', "client HLSL compiler output"),
    (android_manifest, 'naga = { version = "30.0.1", features = ["wgsl-in", "hlsl-out"] }\\n',
     'naga = { version = "30.0.1", features = ["wgsl-in"] }\\n', "Android HLSL compiler output"),
)

def read(path: Path) -> str:
    if not path.is_file():
        raise SystemExit(f"Missing Cargo manifest: {path}")
    return path.read_text(encoding="utf-8")

def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"Expected exactly one {label}; found {count}")
    return source.replace(old, new)

render = read(render_manifest)
render = replace_once(
    render,
    'wgpu = { version = "30.0.1", default-features = false, features = ["std", "parking_lot", "vulkan", "metal", "wgsl", "naga-ir"] }\n',
    'wgpu = { version = "30.0.1", default-features = false, features = ["std", "parking_lot", "wgsl", "naga-ir"] }\n',
    "renderer base wgpu dependency",
)
render = replace_once(
    render,
    '[target.\'cfg(windows)\'.dependencies]\nwgpu = { version = "30.0.1", default-features = false, features = ["dx12"] }\n',
    '[target.\'cfg(windows)\'.dependencies]\nwgpu = { version = "30.0.1", default-features = false, features = ["dx12", "vulkan", "gles"] }\n',
    "renderer Windows backend dependency",
)
render = replace_once(
    render,
    '[target.\'cfg(not(target_os = "macos"))\'.dependencies]\nwgpu = { version = "30.0.1", default-features = false, features = ["gles"] }\n',
    '[target.\'cfg(target_os = "android")\'.dependencies]\nwgpu = { version = "30.0.1", default-features = false, features = ["gles"] }\n\n'
    '[target.\'cfg(target_os = "linux")\'.dependencies]\nwgpu = { version = "30.0.1", default-features = false, features = ["vulkan", "gles"] }\n\n'
    '[target.\'cfg(target_os = "macos")\'.dependencies]\nwgpu = { version = "30.0.1", default-features = false, features = ["metal"] }\n',
    "renderer non-macOS GLES dependency",
)
render_manifest.write_text(render, encoding="utf-8")

client = read(client_manifest)
client = replace_once(
    client,
    'wgpu = { version = "30.0.1", default-features = false, features = ["std", "parking_lot", "vulkan", "metal", "wgsl", "naga-ir"] }\n',
    'wgpu = { version = "30.0.1", default-features = false, features = ["std", "parking_lot", "wgsl", "naga-ir"] }\n',
    "client base wgpu dependency",
)
client = replace_once(
    client,
    '[target.\'cfg(windows)\'.dependencies]\n# Use the registered URI handler',
    '[target.\'cfg(windows)\'.dependencies]\n# Use the registered URI handler',
    "Windows dependency section",
)
client = replace_once(
    client,
    'wgpu = { version = "30.0.1", default-features = false, features = ["dx12"] }\n',
    'wgpu = { version = "30.0.1", default-features = false, features = ["dx12", "vulkan", "gles"] }\n',
    "client Windows backend dependency",
)
client = replace_once(
    client,
    '[target.\\'cfg(target_os = "android")\\'.dependencies]\\nrustix = { version = "1", default-features = false, features = ["std", "fs"] }\\n',
    '[target.\\'cfg(target_os = "android")\\'.dependencies]\\nrustix = { version = "1", default-features = false, features = ["std", "fs"] }\\n'
    'wgpu = { version = "30.0.1", default-features = false, features = ["gles"] }\\n',
    "client Android target dependency section",
)
client = replace_once(
    client,
    '[target.\\'cfg(not(target_os = "macos"))\\'.dependencies]\\n# OpenGL (WGL on Windows, EGL on Linux), the last fallback, for GPUs with no Vulkan or\\n# DirectX 12 driver (\`graphicsApi\\`, graphics.rs).\\nwgpu = { version = "30.0.1", default-features = false, features = ["gles"] }\\n',
    '[target.\\'cfg(target_os = "linux")\\'.dependencies]\\n# Vulkan and GLES remain available to the Linux desktop client.\\n'
    'wgpu = { version = "30.0.1", default-features = false, features = ["vulkan", "gles"] }\\n\\n'
    '[target.\\'cfg(target_os = "macos")\\'.dependencies]\\nwgpu = { version = "30.0.1", default-features = false, features = ["metal"] }\\n',
    "client non-Android GLES dependency",
)
client_manifest.write_text(client, encoding="utf-8")

android = read(android_manifest)
android = replace_once(
    android,
    'wgpu = { version = "30.0.1", default-features = false, features = ["std", "parking_lot", "vulkan", "metal", "wgsl", "naga-ir"] }\n',
    'wgpu = { version = "30.0.1", default-features = false, features = ["std", "parking_lot", "gles", "wgsl", "naga-ir"] }\n',
    "Android-only wgpu dependency",
)
android_manifest.write_text(android, encoding="utf-8")

for manifest_path, old, new, label in naga_replacements:
    source = read(manifest_path)
    source = replace_once(source, old, new, label)
    manifest_path.write_text(source, encoding="utf-8")
print("Android excludes desktop GPU backends and HLSL-only shader compiler output.")

#!/usr/bin/env python3
"""Install the reusable GLES2 C++ backend into Horizon's Android cdylib build."""
import argparse
from pathlib import Path
import shutil

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
repo = Path(__file__).resolve().parents[1]
cpp_source = repo / "tools/gles2-hud-prototype/app/src/main/cpp"
destination = root / "crates/horizon-android/gles2-native"
destination.mkdir(parents=True, exist_ok=True)

for name in (
    "gles2_terrain_renderer.h",
    "gles2_terrain_renderer.cpp",
    "horizon_terrain_adapter.h",
    "horizon_terrain_adapter.cpp",
    "horizon_gles2_bridge.h",
    "horizon_gles2_bridge.cpp",
):
    source_file = cpp_source / name
    if not source_file.is_file():
        raise SystemExit(f"Missing native GLES2 source: {source_file}")
    target = destination / name
    content = source_file.read_bytes()
    if target.exists() and target.read_bytes() != content:
        target.write_bytes(content)
    elif not target.exists():
        target.write_bytes(content)

cargo = root / "crates/horizon-android/Cargo.toml"
cargo_text = cargo.read_text(encoding="utf-8")
build_dep = '[build-dependencies]\ncc = "1.2"\n'
if "[build-dependencies]" not in cargo_text:
    cargo.write_text(cargo_text.rstrip() + "\n\n" + build_dep, encoding="utf-8")
elif 'cc = "1.2"' not in cargo_text:
    raise SystemExit("horizon-android already has build-dependencies without the expected cc pin")

build_rs = root / "crates/horizon-android/build.rs"
build_source = r'''use std::env;
use std::path::PathBuf;

fn main() {
    println!("cargo:rerun-if-changed=build.rs");
    let target = env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();
    if target != "android" {
        return;
    }

    let root = PathBuf::from(env::var_os("CARGO_MANIFEST_DIR").expect("manifest directory"));
    let native = root.join("gles2-native");
    for file in [
        "gles2_terrain_renderer.h",
        "gles2_terrain_renderer.cpp",
        "horizon_terrain_adapter.h",
        "horizon_terrain_adapter.cpp",
        "horizon_gles2_bridge.h",
        "horizon_gles2_bridge.cpp",
    ] {
        println!("cargo:rerun-if-changed={}", native.join(file).display());
    }

    cc::Build::new()
        .cpp(true)
        .std("c++17")
        .include(&native)
        .file(native.join("gles2_terrain_renderer.cpp"))
        .file(native.join("horizon_terrain_adapter.cpp"))
        .file(native.join("horizon_gles2_bridge.cpp"))
        .compile("pavo_horizon_gles2");

    println!("cargo:rustc-link-lib=EGL");
    println!("cargo:rustc-link-lib=GLESv2");
    println!("cargo:rustc-link-lib=android");
    println!("cargo:rustc-link-lib=log");
}
'''
if build_rs.exists() and build_rs.read_text(encoding="utf-8") != build_source:
    raise SystemExit("Existing horizon-android/build.rs differs; refusing to overwrite")
if not build_rs.exists():
    build_rs.write_text(build_source, encoding="utf-8")

print("Installed reusable GLES2 native sources and Android ARMv7 C++ build integration.")

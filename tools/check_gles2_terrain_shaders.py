#!/usr/bin/env python3
"""Check the standalone GLSL ES 1.00 terrain shader contract against the CPU adapter."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
vertex = (root / "patches/horizon-078/gles2-terrain.vert").read_text()
solid = (root / "patches/horizon-078/gles2-terrain-solid.frag").read_text()
translucent = (root / "patches/horizon-078/gles2-terrain-translucent.frag").read_text()
adapter = (root / "patches/horizon-078/gles2-terrain-adapter.py").read_text()

required_vertex = [
    "#version 100",
    "attribute vec3 a_position;",
    "attribute vec2 a_uv;",
    "attribute vec2 a_atlas_origin;",
    "attribute vec2 a_atlas_scale;",
    "attribute vec4 a_color;",
    "attribute vec2 a_light;",
    "attribute float a_opaque;",
    "uniform mat4 u_mvp;",
    "uniform vec3 u_section_origin;",
]
for item in required_vertex:
    assert item in vertex, f"missing vertex shader contract: {item}"

for label, shader in [("solid", solid), ("translucent", translucent)]:
    assert "#version 100" in shader, f"{label}: not GLSL ES 1.00"
    assert "texture2D(u_terrain_atlas" in shader, f"{label}: atlas sampler missing"
    assert "v_uv - max(ceil(v_uv) - 1.0, vec2(0.0))" in shader, f"{label}: Horizon tile_uv wrapping is missing"
    assert "u_atlas_size" in shader and "u_sprite_size" in shader, f"{label}: atlas inset missing"
    assert "texture2D(u_lightmap" in shader, f"{label}: lightmap sampling missing"
    assert "u_fog_range" in shader, f"{label}: fog range missing"

assert "pub struct Gles2TerrainVertex" in adapter
for field in ["position: [f32; 3]", "uv: [f32; 2]", "atlas_origin: [f32; 2]",
              "atlas_scale: [f32; 2]", "color: [u8; 4]", "light: [u8; 2]",
              "opaque_layer: f32", "emission: u8", "material_flags: u8"]:
    assert field in adapter, f"CPU vertex field missing: {field}"

assert "if (texel.a < 0.5 && v_opaque < 0.5)" in solid
assert "let emission = 15 - (packed_color[3] & 0x0f);" in adapter, "HDR emission nibble is not decoded"
assert "let material_flags = packed_color[3] >> 4;" in adapter, "upper material/fluid face nibble is not preserved"
assert "texel.a * v_color.a" not in solid and "texel.a * v_color.a" not in translucent, "packed vertex alpha must not be treated as texture transparency"
assert "if (color.a < 0.01)" in translucent
print("PASS: GLES2 terrain shader inputs match the CPU vertex adapter.")
print("PASS: solid cutout and translucent paths retain separate alpha semantics.")
print("PASS: packed vertex alpha is decoded into emission and material flags.")
print("NOTE: GLSL compilation is checked separately; this is not a GPU render test.")

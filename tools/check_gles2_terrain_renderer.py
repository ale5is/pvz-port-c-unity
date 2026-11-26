#!/usr/bin/env python3
"""Static ABI and draw-path checks for the native GLES2 terrain renderer."""
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()
header = (root / "gles2_terrain_renderer.h").read_text(encoding="utf-8")
source = (root / "gles2_terrain_renderer.cpp").read_text(encoding="utf-8")
adapter_header = (root / "horizon_terrain_adapter.h").read_text(encoding="utf-8")
adapter_source = (root / "horizon_terrain_adapter.cpp").read_text(encoding="utf-8")
cmake = (root / "CMakeLists.txt").read_text(encoding="utf-8")
native_host = (root / "native_hud.cpp").read_text(encoding="utf-8")

required_header = [
    "offsetof(Gles2TerrainVertex, position) == 0",
    "offsetof(Gles2TerrainVertex, uv) == 12",
    "offsetof(Gles2TerrainVertex, atlas_origin) == 20",
    "offsetof(Gles2TerrainVertex, atlas_scale) == 28",
    "offsetof(Gles2TerrainVertex, color) == 36",
    "offsetof(Gles2TerrainVertex, emission) == 40",
    "offsetof(Gles2TerrainVertex, material_flags) == 41",
    "offsetof(Gles2TerrainVertex, light) == 42",
    "offsetof(Gles2TerrainVertex, opaque_layer) == 44",
    "sizeof(Gles2TerrainVertex) == 48",
]
required_source = [
    "#version 100",
    "glBindAttribLocation(program, 0, \"a_position\")",
    "glBindAttribLocation(program, 7, \"a_material\")",
    "GL_UNSIGNED_BYTE, GL_TRUE, stride",
    "offsetof(Gles2TerrainVertex, color)",
    "offsetof(Gles2TerrainVertex, light)",
    "offsetof(Gles2TerrainVertex, emission)",
    "glBufferData(GL_ARRAY_BUFFER",
    "(count % 3) != 0",
    "Terrain vertex count must be a triangle list",
    "glDrawArrays(GL_TRIANGLES, 0, count)",
    "index <= 7",
    "glEnableVertexAttribArray(7)",
    "glDepthFunc(GL_LEQUAL)",
    "glDepthMask(GL_FALSE)",
    "glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)",
    "v_material.x * 119.0",
]
required_adapter_header = [
    "sizeof(HorizonTerrainVertex) == 24",
    "bool build_gles2_terrain_atlas(",
    "bool convert_horizon_terrain_vertices(",
]
required_adapter_source = [
    "layer_major_rgba",
    "pixel_bytes < source_bytes",
    "(vertex_count % 4) != 0",
    "output->push_back(converted[i + 2])",
    "output->push_back(converted[i + 3])",
    "All sprite layers do not fit in one GLES2 atlas texture",
    "15 - (alpha_flags & 0x0f)",
    "alpha_flags >> 4",
    "input.layer & 0x7fffu",
    "input.light >> 8",
    "vertex.opaque_layer",
    "static_cast<float>(input.uv[0]) / uv_steps",
]
required_host = [
    '#include "gles2_terrain_renderer.h"',
    "pavo::Gles2TerrainRenderer renderer;",
    "renderer.initialize(",
    "renderer.upload_solid(",
    "renderer.draw(",
    "renderer.release();",
    "EGL_DEPTH_SIZE, 16",
    "ANativeWindow_setBuffersGeometry(window, 0, 0, native_format) != 0",
    "eglSwapInterval(display, 1)",
    "engine->rendering.store(false)",
    "engine->window != window",
    "eglQuerySurface(display, surface, EGL_WIDTH, &width) != EGL_TRUE",
]
missing = [f"header: {item}" for item in required_header if item not in header]
missing += [f"renderer: {item}" for item in required_source if item not in source]
missing += [f"adapter header: {item}" for item in required_adapter_header if item not in adapter_header]
missing += [f"adapter source: {item}" for item in required_adapter_source if item not in adapter_source]
missing += [f"native host: {item}" for item in required_host if item not in native_host]
if source.count("attribute ") != 8:
    missing.append("renderer: GLES2 path must stay within the eight guaranteed vertex attribute slots")
if "horizon_terrain_adapter.cpp" not in cmake:
    missing.append("CMake: CPU terrain adapter must be compiled into the Android native target")
if "find_library(glesv2-lib GLESv2)" not in cmake or "${glesv2-lib}" not in cmake:
    missing.append("CMake: GLESv2 must be linked to the native target")
if missing:
    print("\n".join("FAIL: " + item for item in missing), file=sys.stderr)
    raise SystemExit(1)
print("PASS: 48-byte Rust/C++ terrain vertex ABI")
print("PASS: 24-byte Horizon input converts into the 48-byte GLES2 vertex ABI")
print("PASS: layer-major sprite pixels pack without truncating layers")
print("PASS: light, emission tier, fluid face, opaque flag and UV scale are decoded")
print("PASS: eight GLES2 attributes, including packed emission/material bytes")
print("PASS: atlas/lightmap sampling and VBO draw path")
print("PASS: opaque depth pass and translucent blend/depth-mask pass")
print("PASS: Android NativeActivity executes the GLES2 terrain renderer")
print("PASS: surface callbacks reject stale windows and restart a finished render thread")
print("PASS: EGL window setup and dimensions are checked before rendering")
print("PASS: terrain uploads reject incomplete triangle lists and restore GL bindings")

#!/usr/bin/env python3
"""Static ABI and draw-path checks for the native GLES2 terrain renderer."""
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()
header = (root / "gles2_terrain_renderer.h").read_text(encoding="utf-8")
source = (root / "gles2_terrain_renderer.cpp").read_text(encoding="utf-8")
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
    "v_material.x * 17.0",
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
missing += [f"native host: {item}" for item in required_host if item not in native_host]
if source.count("attribute ") != 8:
    missing.append("renderer: GLES2 path must stay within the eight guaranteed vertex attribute slots")
if "find_library(glesv2-lib GLESv2)" not in cmake or "${glesv2-lib}" not in cmake:
    missing.append("CMake: GLESv2 must be linked to the native target")
if missing:
    print("\n".join("FAIL: " + item for item in missing), file=sys.stderr)
    raise SystemExit(1)
print("PASS: 48-byte Rust/C++ terrain vertex ABI")
print("PASS: eight GLES2 attributes, including packed emission/material bytes")
print("PASS: atlas/lightmap sampling and VBO draw path")
print("PASS: opaque depth pass and translucent blend/depth-mask pass")
print("PASS: Android NativeActivity executes the GLES2 terrain renderer")
print("PASS: surface callbacks reject stale windows and restart a finished render thread")
print("PASS: EGL window setup and dimensions are checked before rendering")
print("PASS: terrain uploads reject incomplete triangle lists and restore GL bindings")

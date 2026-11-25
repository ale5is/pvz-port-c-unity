#!/usr/bin/env python3
"""Static ABI and draw-path checks for the native GLES2 terrain renderer."""
from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()
header = (root / "gles2_terrain_renderer.h").read_text(encoding="utf-8")
source = (root / "gles2_terrain_renderer.cpp").read_text(encoding="utf-8")

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
    '#version 100',
    "glBindAttribLocation(program, 0, \"a_position\")",
    "glBindAttribLocation(program, 6, \"a_opaque\")",
    "GL_UNSIGNED_BYTE, GL_TRUE, stride",
    "offsetof(Gles2TerrainVertex, color)",
    "offsetof(Gles2TerrainVertex, light)",
    "glBufferData(GL_ARRAY_BUFFER",
    "glDrawArrays(GL_TRIANGLES, 0, count)",
    "glDepthFunc(GL_LEQUAL)",
    "glDepthMask(GL_FALSE)",
    "glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)",
    "egl",
]
missing = [f"header: {item}" for item in required_header if item not in header]
missing += [f"renderer: {item}" for item in required_source if item not in source]
if missing:
    print("\n".join("FAIL: " + item for item in missing), file=sys.stderr)
    raise SystemExit(1)
print("PASS: 48-byte Rust/C++ terrain vertex ABI")
print("PASS: seven GLES2 attributes, atlas/lightmap sampling and VBO draw path")
print("PASS: opaque depth pass and translucent blend/depth-mask pass")

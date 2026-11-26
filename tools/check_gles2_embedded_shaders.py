#!/usr/bin/env python3
"""Extract and compile the exact GLSL ES shaders embedded in the native GLES2 renderer."""
from pathlib import Path
import re
import subprocess
import sys
import tempfile

if len(sys.argv) != 3:
    raise SystemExit("usage: check_gles2_embedded_shaders.py <renderer.cpp> <glslangValidator>")

source_path = Path(sys.argv[1]).resolve()
validator = sys.argv[2]
source = source_path.read_text(encoding="utf-8")
pattern = re.compile(r'const char\* (k\w+) = R"GLSL\((.*?)\)GLSL";', re.DOTALL)
shaders = dict(pattern.findall(source))
expected = {
    "kVertexShader": "vert",
    "kSolidFragmentShader": "frag",
    "kTranslucentFragmentShader": "frag",
}
missing = sorted(set(expected) - set(shaders))
if missing:
    raise SystemExit(f"Missing embedded GLES2 shaders: {missing}")

with tempfile.TemporaryDirectory(prefix="pavo-gles2-shaders-") as directory:
    root = Path(directory)
    for name, stage in expected.items():
        path = root / f"{name}.{stage}"
        path.write_text(shaders[name], encoding="utf-8")
        subprocess.run([validator, "-S", stage, str(path)], check=True)
        print(f"PASS: embedded {name} compiles as GLSL ES 1.00")

if source.count("#ifdef GL_FRAGMENT_PRECISION_HIGH") != 2:
    raise SystemExit("Both embedded fragment shaders must include the GLES2 precision fallback")
print("PASS: both embedded fragment shaders include the optional-highp fallback")

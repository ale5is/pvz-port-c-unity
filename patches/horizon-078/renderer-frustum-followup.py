#!/usr/bin/env python3
"""Finish adapting all renderer-local frustum call sites to neutral plane arrays."""
import argparse
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
gfx_path = root / "crates/horizon-client-render/src/gfx.rs"
resident_path = root / "crates/horizon-client-render/src/gfx/resident.rs"
gfx = gfx_path.read_text(encoding="utf-8")
resident = resident_path.read_text(encoding="utf-8")

def convert_literals(source, label):
    cursor = 0
    while True:
        match = re.search(r"\bFrustum\s*\{\s*planes\s*:", source[cursor:])
        if not match:
            return source
        start = cursor + match.start()
        brace = source.find("{", start)
        depth = 0
        end = None
        for i in range(brace, len(source)):
            if source[i] == "{":
                depth += 1
            elif source[i] == "}":
                depth -= 1
                if depth == 0:
                    end = i
                    break
        if end is None:
            raise SystemExit("Unclosed Frustum literal in " + label)
        body = source[brace + 1:end].strip()
        if not body.startswith("planes:"):
            raise SystemExit("Unexpected Frustum literal in " + label)
        expr = body[len("planes:"):].strip()
        if expr.endswith(","):
            expr = expr[:-1].strip()
        replacement = "Frustum::new((" + expr + ").map(|plane| plane.to_array()))"
        source = source[:start] + replacement + source[end + 1:]
        cursor = start + len(replacement)

gfx = convert_literals(gfx, "gfx.rs")
resident = convert_literals(resident, "resident.rs")

old = """impl Cull for Frustum {
    fn intersects(&self, min: Vec3, max: Vec3) -> bool {
        Frustum::intersects(self, min, max)
    }
    fn contains(&self, min: Vec3, max: Vec3) -> bool {
        Frustum::contains(self, min, max)
    }
}
impl Cull for Frusta {
    fn intersects(&self, min: Vec3, max: Vec3) -> bool {
        self.0.intersects(min, max) || self.1.is_some_and(|f| f.intersects(min, max))
    }
    /// Entirely inside one of them is entirely inside the union.
    fn contains(&self, min: Vec3, max: Vec3) -> bool {
        self.0.contains(min, max) || self.1.is_some_and(|f| f.contains(min, max))
    }
}"""
new = """impl Cull for Frustum {
    fn intersects(&self, min: Vec3, max: Vec3) -> bool {
        Frustum::intersects(self, min.to_array(), max.to_array())
    }
    fn contains(&self, min: Vec3, max: Vec3) -> bool {
        Frustum::contains(self, min.to_array(), max.to_array())
    }
}
impl Cull for Frusta {
    fn intersects(&self, min: Vec3, max: Vec3) -> bool {
        self.0.intersects(min.to_array(), max.to_array())
            || self.1.is_some_and(|f| f.intersects(min.to_array(), max.to_array()))
    }
    /// Entirely inside one of them is entirely inside the union.
    fn contains(&self, min: Vec3, max: Vec3) -> bool {
        self.0.contains(min.to_array(), max.to_array())
            || self.1.is_some_and(|f| f.contains(min.to_array(), max.to_array()))
    }
}"""
if old in gfx:
    gfx = gfx.replace(old, new, 1)
elif new not in gfx:
    raise SystemExit("Could not find Cull implementations to adapt")

old = "frustum.contains(min, top + Vec3::splat(16.0))"
new = "frustum.contains(min.to_array(), (top + Vec3::splat(16.0)).to_array())"
if old in resident:
    resident = resident.replace(old, new, 1)
elif new not in resident:
    raise SystemExit("Could not find resident direct Frustum containment call")

if "Frustum {" in gfx or "Frustum {" in resident:
    raise SystemExit("A renderer-local Frustum literal remains")
gfx_path.write_text(gfx, encoding="utf-8")
resident_path.write_text(resident, encoding="utf-8")
print("PASS: renderer and resident Frustum constructors use neutral plane arrays.")
print("PASS: Cull adapters convert glam coordinates at the backend boundary.")

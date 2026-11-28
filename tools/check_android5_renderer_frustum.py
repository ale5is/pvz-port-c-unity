#!/usr/bin/env python3
"""Verify camera frustum data and culling are independent of wgpu."""
import re
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: check_android5_renderer_frustum.py <horizon-source-root>")
root = Path(sys.argv[1]).resolve()
types = (root / "crates/horizon-render-types/src/lib.rs").read_text(encoding="utf-8")
api = (root / "crates/horizon-render-api/src/lib.rs").read_text(encoding="utf-8")
gfx = (root / "crates/horizon-client-render/src/gfx.rs").read_text(encoding="utf-8")
game = (root / "crates/horizon-client/src/game.rs").read_text(encoding="utf-8")

for required in ("pub struct Frustum", "pub fn intersects(&self, min: [f32; 3], max: [f32; 3])", "pub fn contains(&self, min: [f32; 3], max: [f32; 3])"):
    if types.count(required) != 1:
        raise SystemExit(f"FAIL: neutral render types missing or duplicate: {required}")
if "fn camera_frustum(" not in api:
    raise SystemExit("FAIL: RenderBackend does not expose camera_frustum")
if "Renderer::camera_frustum(" not in gfx or "Frustum::new(" not in gfx:
    raise SystemExit("FAIL: wgpu adapter does not convert the original frustum planes")
if "RenderBackend::camera_frustum(" not in game:
    raise SystemExit("FAIL: Game does not request the frustum through RenderBackend")
if "horizon_render_types::Frustum" not in game:
    raise SystemExit("FAIL: Game still stores a renderer-local frustum type")
if "v.intersects((o - glam::Vec3::ONE).to_array(), (o + glam::Vec3::splat(2.0)).to_array())" not in game:
    raise SystemExit("FAIL: Game's block-entity AABB margin changed")
if "pub struct Frustum" in gfx or "horizon_client::gfx::Frustum" in game:
    raise SystemExit("FAIL: concrete renderer still owns the shared Frustum type")
resident = (root / "crates/horizon-client-render/src/gfx/resident.rs").read_text(encoding="utf-8")
lod = (root / "crates/horizon-client-render/src/gfx/lod.rs").read_text(encoding="utf-8")
if re.search(r"(?<!struct )\bFrustum\s*\{\s*planes\s*:", gfx) or re.search(r"(?<!struct )\bFrustum\s*\{\s*planes\s*:", resident) or re.search(r"(?<!struct )\bFrustum\s*\{\s*planes\s*:", lod):
    raise SystemExit("FAIL: renderer or resident tests still construct local Frustum literals")
if "Frustum::intersects(self, min.to_array(), max.to_array())" not in gfx:
    raise SystemExit("FAIL: culling adapter does not convert coordinates at the neutral boundary")
if "frustum.contains(min.to_array(), (top + Vec3::splat(16.0)).to_array())" not in resident:
    raise SystemExit("FAIL: resident containment test does not use neutral coordinate arrays")
if "frustum.intersects(min.as_vec3().to_array(), max.as_vec3().to_array())" not in lod:
    raise SystemExit("FAIL: LOD frustum intersection does not use neutral coordinate arrays")
for forbidden in ("wgpu::", "winit::", "glam::", "EGLDisplay", "EGLContext", "GLuint"):
    if forbidden in api:
        raise SystemExit(f"FAIL: RenderBackend leaks implementation dependency: {forbidden}")
print("PASS: camera frustum planes and AABB culling are GPU-independent.")
print("PASS: Game obtains the camera frustum through RenderBackend without changing its culling margin.")

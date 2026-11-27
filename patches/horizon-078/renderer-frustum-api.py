#!/usr/bin/env python3
"""Move camera-frustum values and culling math into the GPU-free render-types crate."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
types = root / "crates/horizon-render-types/src/lib.rs"
types_manifest = root / "crates/horizon-render-types/Cargo.toml"
api_manifest = root / "crates/horizon-render-api/Cargo.toml"
game_manifest = root / "crates/horizon-client/Cargo.toml"
api = root / "crates/horizon-render-api/src/lib.rs"
gfx = root / "crates/horizon-client-render/src/gfx.rs"
mesher = root / "crates/horizon-client/src/game.rs"
for path in (types, types_manifest, api, gfx, mesher):
    if not path.is_file():
        raise SystemExit(f"Missing frustum migration source: {path}")

t = types.read_text(encoding="utf-8")
tm = types_manifest.read_text(encoding="utf-8")
am = api_manifest.read_text(encoding="utf-8")
gm = game_manifest.read_text(encoding="utf-8")
a = api.read_text(encoding="utf-8")
api_dependency = 'horizon-render-types = { path = "../horizon-render-types" }\n'
if api_dependency not in am:
    dependency_anchor = "[dependencies]\n"
    if am.count(dependency_anchor) != 1:
        raise SystemExit("Could not uniquely add horizon-render-types to horizon-render-api")
    am = am.replace(dependency_anchor, dependency_anchor + api_dependency, 1)
if api_dependency not in gm:
    dependency_anchor = "[dependencies]\n"
    if gm.count(dependency_anchor) != 1:
        raise SystemExit("Could not uniquely add horizon-render-types to horizon-client")
    gm = gm.replace(dependency_anchor, dependency_anchor + api_dependency, 1)
g = gfx.read_text(encoding="utf-8")
m = mesher.read_text(encoding="utf-8")

frustum_type = """/// Camera-relative clip planes shared by every rendering backend.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Frustum {
    planes: [[f32; 4]; 6],
}

impl Frustum {
    /// Construct from the six normalized clip planes in left/right/bottom/top/near/far order.
    pub const fn new(planes: [[f32; 4]; 6]) -> Self {
        Self { planes }
    }

    /// Whether the camera-relative box min..max is at least partly inside.
    pub fn intersects(&self, min: [f32; 3], max: [f32; 3]) -> bool {
        !self.planes.iter().any(|p| {
            let v = [
                if p[0] >= 0.0 { max[0] } else { min[0] },
                if p[1] >= 0.0 { max[1] } else { min[1] },
                if p[2] >= 0.0 { max[2] } else { min[2] },
            ];
            p[0] * v[0] + p[1] * v[1] + p[2] * v[2] + p[3] < 0.0
        })
    }

    /// Whether the camera-relative box min..max is entirely inside.
    pub fn contains(&self, min: [f32; 3], max: [f32; 3]) -> bool {
        self.planes.iter().all(|p| {
            let v = [
                if p[0] >= 0.0 { min[0] } else { max[0] },
                if p[1] >= 0.0 { min[1] } else { max[1] },
                if p[2] >= 0.0 { min[2] } else { max[2] },
            ];
            p[0] * v[0] + p[1] * v[1] + p[2] * v[2] + p[3] >= 0.0
        })
    }
}

"""
if "pub struct Frustum" not in t:
    anchor = "#[cfg(test)]\nmod tests {"
    if t.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate horizon-render-types tests")
    t = t.replace(anchor, frustum_type + anchor, 1)
    test_anchor = "    #[test]\n    fn vertex_layouts_match_the_upload_contract() {"
    tests = """    #[test]
    fn frustum_intersection_and_containment_preserve_clip_plane_signs() {
        use super::Frustum;

        let frustum = Frustum::new([
            [1.0, 0.0, 0.0, 1.0],
            [-1.0, 0.0, 0.0, 1.0],
            [0.0, 1.0, 0.0, 1.0],
            [0.0, -1.0, 0.0, 1.0],
            [0.0, 0.0, 1.0, 1.0],
            [0.0, 0.0, -1.0, 1.0],
        ]);
        assert!(frustum.intersects([-0.25; 3], [0.25; 3]));
        assert!(!frustum.intersects([2.0, 0.0, 0.0], [3.0, 0.5, 0.5]));
        assert!(frustum.contains([-0.5; 3], [0.5; 3]));
        assert!(!frustum.contains([-2.0; 3], [0.5; 3]));
    }

"""
    if t.count(test_anchor) != 1:
        raise SystemExit("Could not locate render-types layout test")
    t = t.replace(test_anchor, tests + test_anchor, 1)
elif "pub const fn new(planes: [[f32; 4]; 6])" not in t:
    raise SystemExit("Existing Frustum definition has an unexpected shape")

# Expose the neutral Frustum through the existing shared render-data crate.
if "pub use horizon_render_types::Frustum;" not in g:
    import_anchor = "use wgpu::util::DeviceExt;\n"
    if g.count(import_anchor) != 1:
        raise SystemExit("Could not locate gfx.rs imports for Frustum re-export")
    g = g.replace(import_anchor, "pub use horizon_render_types::Frustum;\n" + import_anchor, 1)

old_type = """/// Camera-relative clip planes (`Frustum`).
#[derive(Clone, Copy, Debug)]
pub struct Frustum {
    planes: [Vec4; 6],
}
impl Frustum {
    /// Whether the camera-relative box `min..max` is at least partly inside.
    pub fn intersects(&self, min: Vec3, max: Vec3) -> bool {
        !self.planes.iter().any(|p| {
            let v = Vec3::new(
                if p.x >= 0.0 { max.x } else { min.x },
                if p.y >= 0.0 { max.y } else { min.y },
                if p.z >= 0.0 { max.z } else { min.z },
            );
            p.truncate().dot(v) + p.w < 0.0
        })
    }
    /// Whether the camera-relative box `min..max` is entirely inside. Then
    /// `intersects` accepts every box within it: each plane's distance only
    /// grows from this box's nearest corner to the inner box's farthest, and
    /// rounding keeps that order.
    pub fn contains(&self, min: Vec3, max: Vec3) -> bool {
        self.planes.iter().all(|p| {
            let v = Vec3::new(
                if p.x >= 0.0 { min.x } else { max.x },
                if p.y >= 0.0 { min.y } else { max.y },
                if p.z >= 0.0 { min.z } else { max.z },
            );
            p.truncate().dot(v) + p.w >= 0.0
        })
    }
}
"""
if old_type in g:
    g = g.replace(old_type, "", 1)
elif "pub struct Frustum" in g:
    raise SystemExit("Renderer-local Frustum exists but does not match pinned definition")

old_construct = """        Frustum {
            planes: frustum(views.cull().view_proj(bob)),
        }"""
new_construct = """        Frustum::new(
            frustum(views.cull().view_proj(bob)).map(|plane| plane.to_array()),
        )"""
if old_construct in g:
    g = g.replace(old_construct, new_construct, 1)
elif "Frustum::new(" not in g:
    raise SystemExit("Could not migrate camera_frustum constructor")

# The backend contract receives a plain matrix value, never glam or a GPU type.
api_method = """    /// Build the frame's camera-relative culling planes.
    fn camera_frustum(
        &self,
        fov_degrees: f32,
        yaw: f32,
        pitch: f32,
        bob: [[f32; 4]; 4],
        depth_far: f32,
    ) -> horizon_render_types::Frustum;
"""
if "fn camera_frustum(" not in a:
    anchor = "    fn visible_block_entities<'a>("
    if a.count(anchor) != 1:
        raise SystemExit("Could not locate RenderBackend visibility method for frustum API")
    a = a.replace(anchor, api_method + anchor, 1)

adapter_method = """    fn camera_frustum(
        &self,
        fov_degrees: f32,
        yaw: f32,
        pitch: f32,
        bob: [[f32; 4]; 4],
        depth_far: f32,
    ) -> horizon_render_types::Frustum {
        Renderer::camera_frustum(
            self,
            fov_degrees,
            yaw,
            pitch,
            Mat4::from_cols_array_2d(&bob),
            depth_far,
        )
    }

"""
impl_anchor = "    fn visible_block_entities<'a>("
trait_start = g.find("impl<'frame> horizon_render_api::RenderBackend<Frame<'frame>> for Renderer {")
if trait_start < 0:
    raise SystemExit("Could not locate wgpu RenderBackend implementation")
trait_end = g.find("\nimpl ", trait_start + 10)
if trait_end < 0:
    trait_end = len(g)
body = g[trait_start:trait_end]
if "fn camera_frustum(" not in body:
    if body.count(impl_anchor) != 1:
        raise SystemExit("Could not uniquely locate visibility adapter method")
    g = g[:trait_start] + body.replace(impl_anchor, adapter_method + impl_anchor, 1) + g[trait_end:]

# Game uses only backend-neutral Frustum and fixed-size coordinate arrays.
old_field = "view_frustum: Option<horizon_client::gfx::Frustum>,"
new_field = "view_frustum: Option<horizon_render_types::Frustum>,"
if old_field in m:
    m = m.replace(old_field, new_field, 1)
elif new_field not in m:
    raise SystemExit("Could not locate Game view_frustum field")

old_call = """        self.view_frustum = self
            .renderer
            .as_ref()
            .map(|r| r.camera_frustum(fov_degrees, yaw, pitch, bob * spin, depth_far));"""
new_call = """        self.view_frustum = self.renderer.as_ref().map(|r| {
            horizon_render_api::RenderBackend::camera_frustum(
                r,
                fov_degrees,
                yaw,
                pitch,
                (bob * spin).to_cols_array_2d(),
                depth_far,
            )
        });"""
if old_call in m:
    m = m.replace(old_call, new_call, 1)
elif "RenderBackend::camera_frustum(" not in m:
    raise SystemExit("Could not locate Game camera_frustum call")

old_intersects = "v.intersects(o - glam::Vec3::ONE, o + glam::Vec3::splat(2.0))"
new_intersects = "v.intersects((o - glam::Vec3::ONE).to_array(), (o + glam::Vec3::splat(2.0)).to_array())"
if old_intersects in m:
    m = m.replace(old_intersects, new_intersects, 1)
elif new_intersects not in m:
    raise SystemExit("Could not locate Game frustum intersection call")

if "horizon_client::gfx::Frustum" in m or "r.camera_frustum(" in m:
    raise SystemExit("Game still depends on the renderer-local Frustum API")
if "pub struct Frustum" in g:
    raise SystemExit("Renderer-local Frustum definition remains after extraction")
if "wgpu::" in a or "winit::" in a or "glam::" in a:
    raise SystemExit("RenderBackend API leaks renderer implementation or math-library types")

types.write_text(t, encoding="utf-8")
api_manifest.write_text(am, encoding="utf-8")
game_manifest.write_text(gm, encoding="utf-8")
api.write_text(a, encoding="utf-8")
gfx.write_text(g, encoding="utf-8")
mesher.write_text(m, encoding="utf-8")
print("Moved Frustum planes and AABB culling into horizon-render-types; Game now requests frusta through RenderBackend.")

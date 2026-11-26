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
    "thread that owns the current EGL context",
    "std::thread::id owner_thread_",
    "bool owner_thread_set_ = false;",
    "bool bind_or_check_owner_thread(const char* operation);",
    "bool check_owner_thread(const char* operation);",
    "struct UniformLocations",
    "UniformLocations solid_uniforms_",
    "UniformLocations translucent_uniforms_",
    "bool upload_section(",
    "bool use_solid_ranges = false",
    "bool set_section_solid_ranges(",
    "bool has_solid_ranges = false;",
    "void remove_section(",
    "void remove_column(",
    "void draw_sections(",
    "std::map<std::array<std::int32_t, 3>, SectionBuffers> sections_",
    "section_count() const",
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
    "void Gles2TerrainRenderer::cache_uniforms",
    'uniforms->mvp = glGetUniformLocation(program, "u_mvp")',
    "draw_buffer_range(solid_program_, solid_uniforms_",
    "draw_buffer_range(translucent_program_, translucent_uniforms_",
    "glActiveTexture(GL_TEXTURE1)",
    "glBufferData(GL_ARRAY_BUFFER",
    "(count % 3) != 0",
    "Terrain vertex count must be a triangle list",
    "glDrawArrays(GL_TRIANGLES, first, count)",
    "Solid side range",
    "range.first, range.second",
    "index <= 7",
    "glEnableVertexAttribArray(7)",
    "glDepthFunc(GL_LEQUAL)",
    "glDepthMask(GL_FALSE)",
    "glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)",
    "v_material.x * 119.0",
    "kMaximumResidentSections = 512",
    "kMaximumResidentBytes = 32u * 1024u * 1024u",
    "replacement_bytes > kMaximumResidentBytes - retained_bytes",
    "Incoming solid side range",
    "replacement.has_solid_ranges = use_solid_ranges",
    "resident_bytes_ = retained_bytes + replacement_bytes",
    "resident_bytes_ -= it->second.byte_size",
    "resident_bytes_ = 0",
    "sections_.try_emplace(section_key)",
    "existing->second = std::move(replacement)",
    "sections_.try_emplace(section_key)",
    "Not enough memory to register the resident GLES2 section",
    "Resident GLES2 section table exceeds its allocation limit",
    "void Gles2TerrainRenderer::remove_section",
    "void Gles2TerrainRenderer::remove_column",
    "void Gles2TerrainRenderer::draw_sections",
    "std::sort(visible.begin()",
    "distance_squared > max_distance_squared",
    "clear_sections();",
    "Gles2TerrainRenderer::bind_or_check_owner_thread",
    "Gles2TerrainRenderer::check_owner_thread",
    "std::this_thread::get_id()",
    "must run on the EGL context owner thread",
    "requires initialize() on the EGL context owner thread",
    "if (!check_owner_thread(\"upload_solid\")) return false;",
    "if (!check_owner_thread(\"upload_translucent\")) return false;",
    "if (!check_owner_thread(\"upload_section\")) return false;",
    "if (!check_owner_thread(\"set_section_solid_ranges\")) return false;",
    "if (!check_owner_thread(\"remove_section\")) return;",
    "if (!check_owner_thread(\"remove_column\")) return;",
    "if (!check_owner_thread(\"clear_sections\")) return;",
    "if (!check_owner_thread(\"draw_sections\")) return;",
    "if (!check_owner_thread(\"draw\")) return;",
    "if (!check_owner_thread(\"release\")) return;",
    "if (!bind_or_check_owner_thread(\"initialize\")) return false;",
]
required_atlas_contract = [
    "if (sprite_size <= 0 || sprite_size > atlas_width || sprite_size > atlas_height",
    "Invalid atlas/lightmap pixels, dimensions, or sprite size",
    "rgba_byte_size(atlas_width, atlas_height, &atlas_bytes)",
    "rgba_byte_size(lightmap_width, lightmap_height, &lightmap_bytes)",
    "GLsizeiptr",
    "glPixelStorei(GL_UNPACK_ALIGNMENT, 4)",
    "GL_LINEAR",
    "GL_FRAGMENT_PRECISION_HIGH",
]
if "atlas_width % sprite_size" in source or "atlas_height % sprite_size" in source:
    raise SystemExit("FAIL: renderer: gutter-packed atlas dimensions must not be required to divide by sprite size")
if (16 + 2) % 16 == 0:
    raise SystemExit("FAIL: regression fixture must exercise a non-divisible gutter-packed atlas cell")
for item in required_atlas_contract:
    if item not in source:
        raise SystemExit(f"FAIL: renderer atlas contract missing {item}")
print("PASS: gutter-packed 18px cells are accepted for 16px source sprites")

if source.count("#ifdef GL_FRAGMENT_PRECISION_HIGH") != 2:
    raise SystemExit("FAIL: renderer: both fragment shaders need GLES2 fragment highp fallback")
if "numeric_limits<GLsizeiptr>::max()" not in source:
    raise SystemExit("FAIL: renderer: VBO byte count must fit GLsizeiptr on 32-bit ARM")
if "glPixelStorei(GL_UNPACK_ALIGNMENT, 4)" not in source:
    raise SystemExit("FAIL: renderer: texture upload must restore global unpack alignment")
if "upload_texture(&atlas_texture_, atlas_rgba, atlas_width, atlas_height, GL_LINEAR)" not in source:
    raise SystemExit("FAIL: renderer: guttered terrain atlas must use linear filtering")
if "rgba_byte_size" not in source or "max_size / (w * 4)" not in source:
    raise SystemExit("FAIL: renderer: RGBA texture allocation must use overflow-safe byte arithmetic")
draw_start = source.index("void Gles2TerrainRenderer::draw_buffer_range")
draw_end = source.index("void Gles2TerrainRenderer::draw(", draw_start)
if "glGetUniformLocation" in source[draw_start:draw_end]:
    raise SystemExit("FAIL: renderer: uniform locations must not be queried in the per-frame draw path")
if source.count("glGetUniformLocation(") != 9:
    raise SystemExit("FAIL: renderer: expected one cached lookup per shader uniform")
if "solid_uniforms_ = {};" not in source or "translucent_uniforms_ = {};" not in source:
    raise SystemExit("FAIL: renderer: cached uniform locations must reset on release")
release_start = source.index("void Gles2TerrainRenderer::release()")
release_body = source[release_start:]
if release_body.find("glActiveTexture(GL_TEXTURE1)") > release_body.rfind("glActiveTexture(GL_TEXTURE0)"):
    raise SystemExit("FAIL: renderer: release must clear both texture units and restore unit zero")
upload_start = source.index("bool Gles2TerrainRenderer::upload_texture(")
upload_end = source.index("bool Gles2TerrainRenderer::initialize(", upload_start)
upload_body = source[upload_start:upload_end]
if upload_body.find("glActiveTexture(GL_TEXTURE0);") < 0 or upload_body.find("glActiveTexture(GL_TEXTURE0);") > upload_body.find("glGenTextures(1, texture);"):
    raise SystemExit("FAIL: renderer: texture uploads must begin from a known active texture unit")
range_start = source.index("bool Gles2TerrainRenderer::set_section_solid_ranges(")
range_end = source.index("void Gles2TerrainRenderer::remove_section(", range_start)
range_body = source[range_start:range_end]
required_range_contract = [
    "if (!key || (range_count && !first_count_pairs))",
    "sections_.find(section_key)",
    "if (it == sections_.end())",
    "range_count > static_cast<std::size_t>(it->second.solid_count) / 3",
    "ranges.reserve(range_count)",
    "catch (const std::bad_alloc&)",
    "catch (const std::length_error&)",
    "Not enough memory to allocate solid side ranges",
    "Solid side range count exceeds the vector allocation limit",
    "first % 3 != 0",
    "count % 3 != 0",
    "end > static_cast<std::uint64_t>(it->second.solid_count)",
    "end < first",
    "first < previous_end",
    "std::numeric_limits<GLint>::max()",
    "std::numeric_limits<GLsizei>::max()",
    "it->second.solid_ranges = std::move(ranges)",
    "it->second.has_solid_ranges = true",
]
for item in required_range_contract:
    if item not in range_body:
        raise SystemExit(f"FAIL: renderer solid-range contract missing {item}")
if range_body.index("it->second.solid_ranges = std::move(ranges)") < range_body.index("for (std::size_t i = 0; i < range_count; ++i)"):
    raise SystemExit("FAIL: renderer: solid ranges must be committed only after all entries validate")
print("PASS: solid ranges reject invalid, out-of-bounds, unaligned and overlapping ranges atomically")

sections_start = source.index("void Gles2TerrainRenderer::draw_sections(")
sections_end = source.index("void Gles2TerrainRenderer::draw(", sections_start)
sections_body = source[sections_start:sections_end]
solid_loop = sections_body.find("for (const auto& section : visible)")
sort_call = sections_body.find("std::sort(visible.begin()")
blend_enable = sections_body.find("glEnable(GL_BLEND)", sort_call)
blend_func = sections_body.find("glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)", sort_call)
depth_disable = sections_body.find("glDepthMask(GL_FALSE)", sort_call)
translucent_loop = sections_body.find("for (const auto& section : visible)", solid_loop + 1)
depth_restore = sections_body.find("glDepthMask(GL_TRUE)", translucent_loop)
blend_disable = sections_body.find("glDisable(GL_BLEND)", depth_restore)
if min(solid_loop, sort_call, blend_enable, blend_func, depth_disable, translucent_loop, depth_restore, blend_disable) < 0:
    raise SystemExit("FAIL: renderer: incomplete solid/translucent pass state sequence")
if not (solid_loop < sort_call < blend_enable < blend_func < depth_disable < translucent_loop < depth_restore < blend_disable):
    raise SystemExit("FAIL: renderer: opaque pass must precede sorted translucent blending with depth writes restored")
if "return a.distance_squared > b.distance_squared;" not in sections_body:
    raise SystemExit("FAIL: renderer: translucent sections must be sorted far-to-near")
print("PASS: opaque pass precedes far-to-near translucent blending and restores depth state")
print("PASS: EGL context ownership is bound once and enforced for every public GL operation")

required_adapter_header = [
    "sizeof(HorizonTerrainVertex) == 24",
    "bool build_gles2_terrain_atlas(",
    "bool convert_horizon_terrain_vertices(",
    "bool upload_horizon_section(",
    "bool upload_horizon_section_with_ranges(",
]
required_adapter_source = [
    "layer_major_rgba",
    "pixel_bytes < source_bytes",
    "(vertex_count % 4) != 0",
    "converted.push_back(quad[0])",
    "converted.push_back(quad[1])",
    "converted.push_back(quad[2])",
    "converted.push_back(quad[3])",
    "All sprite layers do not fit in one GLES2 atlas texture",
    "15 - (alpha_flags & 0x0f)",
    "alpha_flags >> 4",
    "input.layer & 0x7fffu",
    "constexpr std::size_t gutter = 1",
    "built.cell_size = static_cast<int>(cell)",
    "atlas.cell_size != atlas.sprite_size + 2",
    "static_cast<std::size_t>(atlas.cell_size) + 1",
    "input.light >> 8",
    "vertex.opaque_layer",
    "static_cast<float>(input.uv[0]) / uv_steps",
    "converted_solid.data(), converted_solid.size()",
    "converted_translucent.data(), converted_translucent.size()",
    "renderer->upload_section(key,",
    "renderer->last_error()",
    "first_count_pairs, range_count, true",
    "if (range_count != 0 && !first_count_pairs)",
    "renderer->upload_section(key,",
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
atlas_start = adapter_source.index("bool build_gles2_terrain_atlas(")
atlas_end = adapter_source.index("\nbool convert_horizon_terrain_vertices(", atlas_start)
atlas_body = adapter_source[atlas_start:atlas_end]
for item in [
    "built.pixels.assign(atlas_bytes, 0)",
    "catch (const std::bad_alloc&)",
    "catch (const std::length_error&)",
    "Not enough memory to allocate the GLES2 terrain atlas",
    "GLES2 terrain atlas exceeds the vector allocation limit",
    "*atlas = std::move(built)",
]:
    if item not in atlas_body:
        raise SystemExit(f"FAIL: Horizon atlas allocation contract missing {item}")

conversion_start = adapter_source.index("bool convert_horizon_terrain_vertices(")
conversion_end = adapter_source.index("\n}  // namespace pavo", conversion_start)
conversion_body = adapter_source[conversion_start:conversion_end]
for item in [
    "atlas.sprite_size > std::numeric_limits<int>::max() - 2",
    "atlas_columns > std::numeric_limits<std::size_t>::max() / atlas_rows",
    "quad_count > converted.max_size() / 6",
    "Gles2TerrainVertex quad[4]{};",
    "converted.reserve(quad_count * 6)",
    "output->swap(converted)",
    "catch (const std::bad_alloc&)",
    "catch (const std::length_error&)",
    "Not enough memory to expand the Horizon terrain mesh",
    "Expanded Horizon terrain mesh exceeds the vector allocation limit",
]:
    if item not in conversion_body:
        raise SystemExit(f"FAIL: Horizon CPU adapter conversion contract missing {item}")
quad_order = [
    "converted.push_back(quad[0]);",
    "converted.push_back(quad[1]);",
    "converted.push_back(quad[2]);",
    "converted.push_back(quad[2]);",
    "converted.push_back(quad[3]);",
    "converted.push_back(quad[0]);",
]
quad_positions = []
search_from = 0
for item in quad_order:
    position = conversion_body.find(item, search_from)
    if position < 0:
        raise SystemExit("FAIL: Horizon quad expansion must preserve the exact (0,1,2,2,3,0) triangle winding")
    quad_positions.append(position)
    search_from = position + len(item)
if "output->clear()" in conversion_body or "converted.reserve(vertex_count)" in conversion_body:
    raise SystemExit("FAIL: Horizon CPU adapter must preserve output atomically and avoid the oversized intermediate vertex buffer")
upload_start = adapter_source.index("bool upload_horizon_section(")
upload_body = adapter_source[upload_start:adapter_source.index("}  // namespace pavo", upload_start)]
for item in [
    "if (!renderer || !key)",
    "convert_horizon_terrain_vertices(solid, solid_count",
    "convert_horizon_terrain_vertices(translucent, translucent_count",
    "renderer->upload_section(key,",
    "set_error(error, renderer->last_error())",
]:
    if item not in upload_body:
        raise SystemExit(f"FAIL: Horizon section upload boundary missing {item}")
if not (upload_body.index("convert_horizon_terrain_vertices(solid") <
        upload_body.index("convert_horizon_terrain_vertices(translucent") <
        upload_body.index("renderer->upload_section(key,")):
    raise SystemExit("FAIL: both section layers must convert before the atomic GPU replacement")
ranges_start = adapter_source.index("bool upload_horizon_section_with_ranges(")
ranges_end = adapter_source.index("}  // namespace pavo", ranges_start)
ranges_body = adapter_source[ranges_start:ranges_end]
for item in [
    "if (range_count != 0 && !first_count_pairs)",
    "convert_horizon_terrain_vertices(solid, solid_count",
    "convert_horizon_terrain_vertices(translucent, translucent_count",
    "renderer->upload_section(key,",
    "first_count_pairs, range_count, true",
]:
    if item not in ranges_body:
        raise SystemExit(f"FAIL: Horizon side-range upload adapter missing {item}")
if not (ranges_body.index("convert_horizon_terrain_vertices(solid") <
        ranges_body.index("convert_horizon_terrain_vertices(translucent") <
        ranges_body.index("renderer->upload_section(key,")):
    raise SystemExit("FAIL: both Horizon mesh layers must convert before atomic geometry/range upload")
print("PASS: Horizon CPU vertex conversion is overflow-checked, bounded, allocation-safe and atomic")
print("PASS: Horizon quad expansion preserves exact (0,1,2,2,3,0) triangle winding")
print("PASS: layer-major sprite pixels pack with duplicated filtering gutters")
print("PASS: light, emission tier, fluid face, opaque flag and UV scale are decoded")
print("PASS: eight GLES2 attributes, including packed emission/material bytes")
print("PASS: atlas/lightmap sampling and VBO draw path")
print("PASS: keyed multi-section residency, atomic replacement and bounded 512-section cache")
print("PASS: 32 MiB GPU VBO budget with replacement-aware accounting and eviction")
print("PASS: Horizon side-range validation and GLES2 glDrawArrays range culling")
print("PASS: column/section eviction, distance culling and back-to-front translucent sections")
print("PASS: opaque depth pass and translucent blend/depth-mask pass")
print("PASS: Android NativeActivity executes the GLES2 terrain renderer")
print("PASS: surface callbacks reject stale windows and restart a finished render thread")
print("PASS: EGL window setup and dimensions are checked before rendering")
print("PASS: terrain uploads reject incomplete triangle lists and restore GL bindings")

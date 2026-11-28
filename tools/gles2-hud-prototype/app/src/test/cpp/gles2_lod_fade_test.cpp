#include "gles2_lod_fade.h"
#include "gles2_mesh_bounds.h"

#include <cassert>
#include <chrono>
#include <cmath>

int main() {
    using Fade = pavo::Gles2LodFade;
    const auto start = Fade::TimePoint{};
    const auto at = [start](std::chrono::milliseconds ms) { return start + ms; };

    assert(Fade::fade_in(start, 3, at(std::chrono::milliseconds(0))) == 1u + 3u * 129u + 1u);
    assert(Fade::level(Fade::fade_in(start, 10, at(std::chrono::milliseconds(300)))) == 10u);
    assert(Fade::step(Fade::fade_in(start, 10, at(std::chrono::milliseconds(300)))) >= 32u);
    assert(Fade::fade_in(start, 10, at(std::chrono::milliseconds(599))) == 0u);
    assert(Fade::fade_in(start, 10, at(std::chrono::milliseconds(600))) == 0u);
    assert(Fade::fade_in(at(std::chrono::milliseconds(100)), 2, start) == 1u + 2u * 129u + 1u);

    assert(Fade::fade_out(start, 3, at(std::chrono::milliseconds(0))) == 1u + 3u * 129u + 65u);
    assert(Fade::level(Fade::fade_out(start, 3, at(std::chrono::milliseconds(300)))) == 3u);
    assert(Fade::step(Fade::fade_out(start, 3, at(std::chrono::milliseconds(300)))) >= 96u);
    assert(Fade::step(Fade::fade_out(start, 3, at(std::chrono::milliseconds(700)))) == 128u);
    assert(Fade::kHeld == 65u);

    struct TestVertex { float position[3]; };

    const float identity[16] = {
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    };
    const float zero_origin[3] = {0.0f, 0.0f, 0.0f};
    const float outside_origin[3] = {4.0f, 0.0f, 0.0f};
    TestVertex solid[2]{};
    TestVertex translucent[1]{};
    solid[0].position[1] = -64.0f;
    solid[1].position[1] = 100.0f;
    translucent[0].position[1] = 120.0f;
    assert(std::abs(pavo::gles2_terrain_mesh_center_y(
        solid, 2, translucent, 1, 8.0f) - 28.0f) < 0.001f);

    const auto bounds = pavo::gles2_terrain_mesh_bounds(solid, 2, translucent, 1);
    assert(bounds.valid);
    assert(bounds.minimum[1] == -64.0f);
    assert(bounds.maximum[1] == 120.0f);
    assert(pavo::gles2_aabb_intersects_clip(identity, zero_origin, bounds));
    assert(!pavo::gles2_aabb_intersects_clip(identity, outside_origin, bounds));
    const auto empty_bounds = pavo::gles2_terrain_mesh_bounds(
        static_cast<const TestVertex*>(nullptr), 0,
        static_cast<const TestVertex*>(nullptr), 0);
    assert(!empty_bounds.valid);
    assert(pavo::gles2_aabb_intersects_clip(identity, outside_origin, empty_bounds));
    assert(pavo::gles2_terrain_mesh_center_y(static_cast<const TestVertex*>(nullptr), 0, static_cast<const TestVertex*>(nullptr), 0, 8.0f) == 8.0f);
    return 0;
}

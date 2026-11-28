#pragma once

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <limits>

namespace pavo {

struct Gles2TerrainBounds {
    float minimum[3];
    float maximum[3];
    bool valid;
};

template <typename Vertex>
inline Gles2TerrainBounds gles2_terrain_mesh_bounds(
    const Vertex* solid, std::size_t solid_count,
    const Vertex* translucent, std::size_t translucent_count) noexcept {
    Gles2TerrainBounds bounds{{0.0f, 0.0f, 0.0f},
                              {0.0f, 0.0f, 0.0f}, false};
    for (int axis = 0; axis < 3; ++axis) {
        bounds.minimum[axis] = std::numeric_limits<float>::infinity();
        bounds.maximum[axis] = -std::numeric_limits<float>::infinity();
    }
    const auto include = [&bounds](const Vertex* vertices, std::size_t count) {
        if (!vertices) return;
        for (std::size_t i = 0; i < count; ++i) {
            const float x = vertices[i].position[0];
            const float y = vertices[i].position[1];
            const float z = vertices[i].position[2];
            if (!std::isfinite(x) || !std::isfinite(y) || !std::isfinite(z)) continue;
            const float position[3] = {x, y, z};
            for (int axis = 0; axis < 3; ++axis) {
                bounds.minimum[axis] = std::min(bounds.minimum[axis], position[axis]);
                bounds.maximum[axis] = std::max(bounds.maximum[axis], position[axis]);
            }
            bounds.valid = true;
        }
    };
    include(solid, solid_count);
    include(translucent, translucent_count);
    return bounds;
}

// Conservative clip-space AABB test for column-major OpenGL matrices.
// Invalid bounds fail open so malformed or empty meshes are never wrongly culled.
inline bool gles2_aabb_intersects_clip(const float* mvp, const float origin[3],
                                       const Gles2TerrainBounds& bounds) noexcept {
    if (!mvp || !origin || !bounds.valid) return true;
    for (int i = 0; i < 16; ++i) if (!std::isfinite(mvp[i])) return true;
    for (int i = 0; i < 3; ++i) {
        if (!std::isfinite(origin[i]) || !std::isfinite(bounds.minimum[i]) ||
            !std::isfinite(bounds.maximum[i]) || bounds.minimum[i] > bounds.maximum[i]) return true;
    }
    const float center[3] = {
        origin[0] + (bounds.minimum[0] + bounds.maximum[0]) * 0.5f,
        origin[1] + (bounds.minimum[1] + bounds.maximum[1]) * 0.5f,
        origin[2] + (bounds.minimum[2] + bounds.maximum[2]) * 0.5f
    };
    const float extent[3] = {
        (bounds.maximum[0] - bounds.minimum[0]) * 0.5f,
        (bounds.maximum[1] - bounds.minimum[1]) * 0.5f,
        (bounds.maximum[2] - bounds.minimum[2]) * 0.5f
    };
    for (int plane = 0; plane < 6; ++plane) {
        const int axis = plane / 2;
        const float sign = (plane % 2 == 0) ? 1.0f : -1.0f;
        const float a = mvp[3] + sign * mvp[axis];
        const float b = mvp[7] + sign * mvp[4 + axis];
        const float c = mvp[11] + sign * mvp[8 + axis];
        const float d = mvp[15] + sign * mvp[12 + axis];
        const float distance = a * center[0] + b * center[1] + c * center[2] + d;
        const float radius = std::abs(a) * extent[0] +
                             std::abs(b) * extent[1] +
                             std::abs(c) * extent[2];
        if (distance + radius < 0.0f) return false;
    }
    return true;
}

template <typename Vertex>
inline float gles2_terrain_mesh_center_y(
    const Vertex* solid, std::size_t solid_count,
    const Vertex* translucent, std::size_t translucent_count,
    float fallback) noexcept {
    float minimum = std::numeric_limits<float>::infinity();
    float maximum = -std::numeric_limits<float>::infinity();
    const auto include = [&minimum, &maximum](const Vertex* vertices, std::size_t count) {
        if (!vertices) return;
        for (std::size_t i = 0; i < count; ++i) {
            const float y = vertices[i].position[1];
            if (!std::isfinite(y)) continue;
            minimum = std::min(minimum, y);
            maximum = std::max(maximum, y);
        }
    };
    include(solid, solid_count);
    include(translucent, translucent_count);
    return std::isfinite(minimum) ? minimum + (maximum - minimum) * 0.5f : fallback;
}

}  // namespace pavo

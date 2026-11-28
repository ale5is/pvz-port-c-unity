#pragma once

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <limits>

namespace pavo {

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

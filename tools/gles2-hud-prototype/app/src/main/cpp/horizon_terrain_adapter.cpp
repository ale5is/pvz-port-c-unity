#include "horizon_terrain_adapter.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <utility>

namespace pavo {
namespace {

void set_error(std::string* error, const char* message) {
    if (error) *error = message;
}

bool checked_product(std::size_t a, std::size_t b, std::size_t* result) {
    if (a != 0 && b > std::numeric_limits<std::size_t>::max() / a) return false;
    *result = a * b;
    return true;
}

}  // namespace

bool build_gles2_terrain_atlas(const std::uint8_t* layer_major_rgba,
                               std::size_t layer_count, int sprite_size,
                               int max_texture_size, Gles2TerrainAtlas* atlas,
                               std::string* error) {
    if (!atlas || !layer_major_rgba || layer_count == 0 || sprite_size <= 0 ||
        max_texture_size <= 0) {
        set_error(error, "Invalid sprite atlas input");
        return false;
    }

    const std::size_t side = static_cast<std::size_t>(sprite_size);
    std::size_t layer_bytes = 0;
    std::size_t source_bytes = 0;
    if (!checked_product(side, side, &layer_bytes) ||
        !checked_product(layer_bytes, 4, &layer_bytes) ||
        !checked_product(layer_bytes, layer_count, &source_bytes) ||
        source_bytes > static_cast<std::size_t>(std::numeric_limits<std::ptrdiff_t>::max())) {
        set_error(error, "Sprite atlas source size overflows size_t");
        return false;
    }
    if (layer_count > 32768) {
        set_error(error, "Sprite layer count exceeds TerrainVertex's 15-bit layer index");
        return false;
    }

    const std::size_t max_columns = static_cast<std::size_t>(max_texture_size / sprite_size);
    if (max_columns == 0) {
        set_error(error, "A sprite layer exceeds GL_MAX_TEXTURE_SIZE");
        return false;
    }

    const std::size_t ideal_columns =
        static_cast<std::size_t>(std::ceil(std::sqrt(static_cast<double>(layer_count))));
    const std::size_t columns = std::min(max_columns, std::max<std::size_t>(1, ideal_columns));
    const std::size_t rows = (layer_count + columns - 1) / columns;
    std::size_t width = 0;
    std::size_t height = 0;
    std::size_t atlas_bytes = 0;
    if (!checked_product(columns, side, &width) ||
        !checked_product(rows, side, &height) ||
        width > static_cast<std::size_t>(max_texture_size) ||
        height > static_cast<std::size_t>(max_texture_size) ||
        !checked_product(width, height, &atlas_bytes) ||
        !checked_product(atlas_bytes, 4, &atlas_bytes)) {
        set_error(error, "All sprite layers do not fit in one GLES2 atlas texture");
        return false;
    }

    Gles2TerrainAtlas built;
    built.pixels.assign(atlas_bytes, 0);
    built.width = static_cast<int>(width);
    built.height = static_cast<int>(height);
    built.columns = static_cast<int>(columns);
    built.sprite_size = sprite_size;
    built.layer_count = layer_count;

    for (std::size_t layer = 0; layer < layer_count; ++layer) {
        const std::size_t x = (layer % columns) * side;
        const std::size_t y = (layer / columns) * side;
        for (std::size_t row = 0; row < side; ++row) {
            const std::size_t source_offset = layer * layer_bytes + row * side * 4;
            const std::size_t target_offset = ((y + row) * width + x) * 4;
            std::memcpy(built.pixels.data() + target_offset,
                        layer_major_rgba + source_offset, side * 4);
        }
    }

    *atlas = std::move(built);
    if (error) error->clear();
    return true;
}

bool convert_horizon_terrain_vertices(const HorizonTerrainVertex* source,
                                      std::size_t vertex_count,
                                      const Gles2TerrainAtlas& atlas,
                                      float uv_steps,
                                      std::vector<Gles2TerrainVertex>* output,
                                      std::string* error) {
    if (!output || (vertex_count != 0 && !source) || (vertex_count % 3) != 0 ||
        atlas.width <= 0 || atlas.height <= 0 || atlas.columns <= 0 ||
        atlas.sprite_size <= 0 || atlas.layer_count == 0 ||
        !std::isfinite(uv_steps) || uv_steps <= 0.0f) {
        set_error(error, "Invalid terrain mesh, atlas, or UV scale");
        return false;
    }

    output->clear();
    output->reserve(vertex_count);
    const float atlas_width = static_cast<float>(atlas.width);
    const float atlas_height = static_cast<float>(atlas.height);
    const float sprite_size = static_cast<float>(atlas.sprite_size);

    for (std::size_t i = 0; i < vertex_count; ++i) {
        const HorizonTerrainVertex& input = source[i];
        const std::uint16_t sprite_layer = input.layer & 0x7fffu;
        if (static_cast<std::size_t>(sprite_layer) >= atlas.layer_count) {
            output->clear();
            set_error(error, "Terrain vertex references a sprite layer absent from the atlas");
            return false;
        }

        Gles2TerrainVertex vertex{};
        std::copy(input.position, input.position + 3, vertex.position);
        vertex.uv[0] = static_cast<float>(input.uv[0]) / uv_steps;
        vertex.uv[1] = static_cast<float>(input.uv[1]) / uv_steps;

        const std::size_t layer = sprite_layer;
        const std::size_t x = (layer % static_cast<std::size_t>(atlas.columns)) *
                              static_cast<std::size_t>(atlas.sprite_size);
        const std::size_t y = (layer / static_cast<std::size_t>(atlas.columns)) *
                              static_cast<std::size_t>(atlas.sprite_size);
        vertex.atlas_origin[0] = static_cast<float>(x) / atlas_width;
        vertex.atlas_origin[1] = static_cast<float>(y) / atlas_height;
        vertex.atlas_scale[0] = sprite_size / atlas_width;
        vertex.atlas_scale[1] = sprite_size / atlas_height;

        const std::uint32_t color = input.color;
        const std::uint8_t alpha_flags = static_cast<std::uint8_t>(color >> 24);
        vertex.color[0] = static_cast<std::uint8_t>(color);
        vertex.color[1] = static_cast<std::uint8_t>(color >> 8);
        vertex.color[2] = static_cast<std::uint8_t>(color >> 16);
        vertex.color[3] = 255;

        vertex.emission = static_cast<std::uint8_t>(15 - (alpha_flags & 0x0f));
        const std::uint8_t high = alpha_flags >> 4;
        const bool fluid = high >= 8 && (high & 7) < 6;
        vertex.material_flags = fluid
            ? static_cast<std::uint8_t>(0x80 | (high & 7))
            : 0;

        vertex.light[0] = static_cast<std::uint8_t>(input.light);
        vertex.light[1] = static_cast<std::uint8_t>(input.light >> 8);
        vertex.opaque_layer = (input.layer & 0x8000u) != 0 ? 1.0f : 0.0f;
        output->push_back(vertex);
    }

    if (error) error->clear();
    return true;
}

}  // namespace pavo

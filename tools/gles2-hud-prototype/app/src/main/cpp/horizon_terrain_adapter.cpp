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
                               std::size_t pixel_bytes, std::size_t layer_count, int sprite_size,
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
    if (pixel_bytes < source_bytes) {
        set_error(error, "Sprite pixel buffer is shorter than the declared layer count");
        return false;
    }
    if (layer_count > 32768) {
        set_error(error, "Sprite layer count exceeds TerrainVertex's 15-bit layer index");
        return false;
    }

    constexpr std::size_t gutter = 1;
    const std::size_t cell = side + gutter * 2;
    if (cell > static_cast<std::size_t>(max_texture_size)) {
        set_error(error, "Sprite plus filtering gutter exceeds GL_MAX_TEXTURE_SIZE");
        return false;
    }
    const std::size_t max_columns = static_cast<std::size_t>(max_texture_size) / cell;
    const std::size_t ideal_columns =
        static_cast<std::size_t>(std::ceil(std::sqrt(static_cast<double>(layer_count))));
    const std::size_t columns = std::min(max_columns, std::max<std::size_t>(1, ideal_columns));
    const std::size_t rows = (layer_count + columns - 1) / columns;
    std::size_t width = 0;
    std::size_t height = 0;
    std::size_t atlas_bytes = 0;
    if (!checked_product(columns, cell, &width) ||
        !checked_product(rows, cell, &height) ||
        width > static_cast<std::size_t>(max_texture_size) ||
        height > static_cast<std::size_t>(max_texture_size) ||
        width > static_cast<std::size_t>(std::numeric_limits<int>::max()) ||
        height > static_cast<std::size_t>(std::numeric_limits<int>::max()) ||
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
    built.cell_size = static_cast<int>(cell);
    built.layer_count = layer_count;

    for (std::size_t layer = 0; layer < layer_count; ++layer) {
        const std::size_t cell_x = (layer % columns) * cell;
        const std::size_t cell_y = (layer / columns) * cell;
        for (std::size_t row = 0; row < side; ++row) {
            const std::size_t source_offset = layer * layer_bytes + row * side * 4;
            const std::size_t target_offset = ((cell_y + gutter + row) * width + cell_x + gutter) * 4;
            std::memcpy(built.pixels.data() + target_offset - 4,
                        layer_major_rgba + source_offset, 4);
            std::memcpy(built.pixels.data() + target_offset,
                        layer_major_rgba + source_offset, side * 4);
            std::memcpy(built.pixels.data() + target_offset + side * 4,
                        layer_major_rgba + source_offset + (side - 1) * 4, 4);
        }
        const std::size_t row_bytes = cell * 4;
        const std::size_t first_inner = ((cell_y + gutter) * width + cell_x) * 4;
        const std::size_t top = (cell_y * width + cell_x) * 4;
        const std::size_t last_inner = ((cell_y + gutter + side - 1) * width + cell_x) * 4;
        const std::size_t bottom = ((cell_y + cell - 1) * width + cell_x) * 4;
        std::memcpy(built.pixels.data() + top, built.pixels.data() + first_inner, row_bytes);
        std::memcpy(built.pixels.data() + bottom, built.pixels.data() + last_inner, row_bytes);
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
    if (!output || (vertex_count != 0 && !source) || (vertex_count % 4) != 0 ||
        atlas.width <= 0 || atlas.height <= 0 || atlas.columns <= 0 ||
        atlas.sprite_size <= 0 ||
        atlas.sprite_size > std::numeric_limits<int>::max() - 2 ||
        atlas.cell_size != atlas.sprite_size + 2 ||
        atlas.layer_count == 0 ||
        atlas.width % atlas.cell_size != 0 || atlas.height % atlas.cell_size != 0 ||
        atlas.columns > atlas.width / atlas.cell_size ||
        !std::isfinite(uv_steps) || uv_steps <= 0.0f) {
        set_error(error, "Invalid Horizon quad mesh, atlas, or UV scale");
        return false;
    }

    const std::size_t atlas_columns = static_cast<std::size_t>(atlas.columns);
    const std::size_t atlas_rows = static_cast<std::size_t>(atlas.height / atlas.cell_size);
    if (atlas_rows == 0 ||
        atlas_columns > std::numeric_limits<std::size_t>::max() / atlas_rows ||
        atlas.layer_count > atlas_columns * atlas_rows) {
        set_error(error, "Terrain atlas layer capacity is invalid");
        return false;
    }

    const std::size_t quad_count = vertex_count / 4;
    std::vector<Gles2TerrainVertex> converted;
    if (quad_count > converted.max_size() / 6 ||
        quad_count > output->max_size() / 6) {
        set_error(error, "Expanded Horizon terrain mesh exceeds vector capacity");
        return false;
    }
    converted.reserve(quad_count * 6);

    const float atlas_width = static_cast<float>(atlas.width);
    const float atlas_height = static_cast<float>(atlas.height);
    const float sprite_size = static_cast<float>(atlas.sprite_size);

    for (std::size_t quad_index = 0; quad_index < quad_count; ++quad_index) {
        Gles2TerrainVertex quad[4]{};
        for (std::size_t corner = 0; corner < 4; ++corner) {
            const HorizonTerrainVertex& input = source[quad_index * 4 + corner];
            const std::uint16_t sprite_layer = input.layer & 0x7fffu;
            if (static_cast<std::size_t>(sprite_layer) >= atlas.layer_count) {
                set_error(error, "Terrain vertex references a sprite layer absent from the atlas");
                return false;
            }

            Gles2TerrainVertex& vertex = quad[corner];
            std::copy(input.position, input.position + 3, vertex.position);
            vertex.uv[0] = static_cast<float>(input.uv[0]) / uv_steps;
            vertex.uv[1] = static_cast<float>(input.uv[1]) / uv_steps;

            const std::size_t layer = sprite_layer;
            const std::size_t x = (layer % atlas_columns) *
                                  static_cast<std::size_t>(atlas.cell_size) + 1;
            const std::size_t y = (layer / atlas_columns) *
                                  static_cast<std::size_t>(atlas.cell_size) + 1;
            vertex.atlas_origin[0] = static_cast<float>(x) / atlas_width;
            vertex.atlas_origin[1] = static_cast<float>(y) / atlas_height;
            vertex.atlas_scale[0] = sprite_size / atlas_width;
            vertex.atlas_scale[1] = sprite_size / atlas_height;

            const std::uint32_t color = input.color;
            const std::uint8_t alpha_flags = static_cast<std::uint8_t>(color >> 24);
            vertex.color[0] = static_cast<std::uint8_t>(color);
            vertex.color[1] = static_cast<std::uint8_t>(color >> 8);
            vertex.color[2] = static_cast<std::uint8_t>(color >> 16);
            vertex.color[3] = alpha_flags;

            vertex.emission = static_cast<std::uint8_t>(15 - (alpha_flags & 0x0f));
            vertex.material_flags = alpha_flags >> 4;
            vertex.light[0] = static_cast<std::uint8_t>(input.light);
            vertex.light[1] = static_cast<std::uint8_t>(input.light >> 8);
            vertex.opaque_layer = (input.layer & 0x8000u) != 0 ? 1.0f : 0.0f;
        }

        converted.push_back(quad[0]);
        converted.push_back(quad[1]);
        converted.push_back(quad[2]);
        converted.push_back(quad[2]);
        converted.push_back(quad[3]);
        converted.push_back(quad[0]);
    }

    output->swap(converted);
    if (error) error->clear();
    return true;
}

}  // namespace pavo

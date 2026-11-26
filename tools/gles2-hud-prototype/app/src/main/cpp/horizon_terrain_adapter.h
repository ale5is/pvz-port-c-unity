#pragma once

#include "gles2_terrain_renderer.h"

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace pavo {

struct HorizonTerrainVertex {
    float position[3];
    std::uint16_t uv[2];
    std::uint16_t layer;
    std::uint16_t light;
    std::uint32_t color;
};

static_assert(offsetof(HorizonTerrainVertex, position) == 0);
static_assert(offsetof(HorizonTerrainVertex, uv) == 12);
static_assert(offsetof(HorizonTerrainVertex, layer) == 16);
static_assert(offsetof(HorizonTerrainVertex, light) == 18);
static_assert(offsetof(HorizonTerrainVertex, color) == 20);
static_assert(sizeof(HorizonTerrainVertex) == 24);

struct Gles2TerrainAtlas {
    std::vector<std::uint8_t> pixels;
    int width = 0;
    int height = 0;
    int columns = 0;
    int sprite_size = 0;
    std::size_t layer_count = 0;
};

bool build_gles2_terrain_atlas(const std::uint8_t* layer_major_rgba,
                               std::size_t layer_count, int sprite_size,
                               int max_texture_size, Gles2TerrainAtlas* atlas,
                               std::string* error = nullptr);

bool convert_horizon_terrain_vertices(const HorizonTerrainVertex* source,
                                      std::size_t vertex_count,
                                      const Gles2TerrainAtlas& atlas,
                                      float uv_steps,
                                      std::vector<Gles2TerrainVertex>* output,
                                      std::string* error = nullptr);

}  // namespace pavo

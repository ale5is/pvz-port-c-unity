#pragma once

#include <cstddef>
#include <cstdint>

#if defined(__cplusplus)
extern "C" {
#endif

typedef struct PavoGles2Handle PavoGles2Handle;

typedef struct PavoHorizonTerrainVertex {
    float position[3];
    std::uint16_t uv[2];
    std::uint16_t layer;
    std::uint16_t light;
    std::uint32_t color;
} PavoHorizonTerrainVertex;

PavoGles2Handle* pavo_gles2_create();
void pavo_gles2_destroy(PavoGles2Handle* handle);

int pavo_gles2_initialize(
    PavoGles2Handle* handle,
    const std::uint8_t* sprite_layers_rgba,
    std::size_t sprite_pixel_bytes,
    std::size_t sprite_layer_count,
    int sprite_size,
    const std::uint8_t* lightmap_rgba,
    int lightmap_width,
    int lightmap_height,
    float uv_steps);

int pavo_gles2_upload_section(
    PavoGles2Handle* handle,
    const std::int32_t key[3],
    const PavoHorizonTerrainVertex* solid,
    std::size_t solid_count,
    const PavoHorizonTerrainVertex* translucent,
    std::size_t translucent_count,
    const std::uint32_t* solid_first_count_pairs,
    std::size_t solid_range_count);

void pavo_gles2_remove_column(PavoGles2Handle* handle, std::int32_t x, std::int32_t z);
void pavo_gles2_clear_sections(PavoGles2Handle* handle);
void pavo_gles2_draw_sections(
    PavoGles2Handle* handle,
    const float mvp[16],
    const float camera_position[3],
    const float fog_color[4],
    const float fog_range[2],
    float max_distance);
int pavo_gles2_update_lightmap(PavoGles2Handle* handle, const std::uint8_t* rgba, std::size_t byte_count);
int pavo_gles2_update_atlas_layer(
    PavoGles2Handle* handle,
    std::uint32_t layer,
    const std::uint8_t* rgba,
    std::size_t byte_count);
const char* pavo_gles2_last_error(const PavoGles2Handle* handle);

#if defined(__cplusplus)
}
#endif

#include "horizon_gles2_bridge.h"

#include "horizon_terrain_adapter.h"

#include <GLES2/gl2.h>
#include <algorithm>
#include <cmath>
#include <new>
#include <string>

struct PavoGles2Handle {
    pavo::Gles2TerrainRenderer renderer;
    pavo::Gles2TerrainAtlas atlas;
    std::string error;
    float uv_steps = 32768.0f;
    bool initialized = false;
};

namespace {
void set_error(PavoGles2Handle* handle, const char* message) {
    if (handle) handle->error = message ? message : "Unknown GLES2 bridge error";
}

bool valid_handle(PavoGles2Handle* handle) {
    if (handle) return true;
    return false;
}
}

extern "C" PavoGles2Handle* pavo_gles2_create() {
    try {
        return new PavoGles2Handle();
    } catch (...) {
        return nullptr;
    }
}

extern "C" void pavo_gles2_destroy(PavoGles2Handle* handle) {
    if (!handle) return;
    if (handle->initialized) handle->renderer.release();
    delete handle;
}

extern "C" int pavo_gles2_initialize(
    PavoGles2Handle* handle,
    const std::uint8_t* sprite_layers_rgba,
    std::size_t sprite_pixel_bytes,
    std::size_t sprite_layer_count,
    int sprite_size,
    const std::uint8_t* lightmap_rgba,
    int lightmap_width,
    int lightmap_height,
    float uv_steps) {
    if (!valid_handle(handle) || !sprite_layers_rgba || !lightmap_rgba ||
        lightmap_width <= 0 || lightmap_height <= 0 ||
        !std::isfinite(uv_steps) || uv_steps <= 0.0f) {
        set_error(handle, "Invalid GLES2 initialization arguments");
        return 0;
    }
    if (handle->initialized) {
        set_error(handle, "GLES2 bridge is already initialized; release and recreate after context loss");
        return 0;
    }
    GLint max_texture_size = 0;
    glGetIntegerv(GL_MAX_TEXTURE_SIZE, &max_texture_size);
    if (max_texture_size <= 0) {
        set_error(handle, "Could not query GL_MAX_TEXTURE_SIZE from the current GLES2 context");
        return 0;
    }
    if (!pavo::build_gles2_terrain_atlas(
            sprite_layers_rgba, sprite_pixel_bytes, sprite_layer_count, sprite_size,
            max_texture_size, &handle->atlas, &handle->error)) {
        return 0;
    }
    if (!handle->renderer.initialize(
            handle->atlas.pixels.data(), handle->atlas.width, handle->atlas.height,
            lightmap_rgba, lightmap_width, lightmap_height, sprite_size)) {
        set_error(handle, handle->renderer.last_error());
        handle->atlas = {};
        return 0;
    }
    handle->uv_steps = uv_steps;
    handle->initialized = true;
    handle->error.clear();
    return 1;
}

extern "C" int pavo_gles2_upload_section(
    PavoGles2Handle* handle,
    const std::int32_t key[3],
    const PavoHorizonTerrainVertex* solid,
    std::size_t solid_count,
    const PavoHorizonTerrainVertex* translucent,
    std::size_t translucent_count,
    const std::uint32_t* solid_first_count_pairs,
    std::size_t solid_range_count) {
    if (!valid_handle(handle) || !handle->initialized || !key ||
        (solid_count && !solid) || (translucent_count && !translucent) ||
        (solid_range_count && !solid_first_count_pairs)) {
        set_error(handle, "Invalid or uninitialized GLES2 section upload");
        return 0;
    }
    const bool ok = pavo::upload_horizon_section_with_ranges(
        &handle->renderer, key, solid, solid_count, translucent, translucent_count,
        solid_first_count_pairs, solid_range_count, handle->atlas, handle->uv_steps,
        &handle->error);
    return ok ? 1 : 0;
}

extern "C" void pavo_gles2_remove_column(PavoGles2Handle* handle, std::int32_t x, std::int32_t z) {
    if (!handle || !handle->initialized) return;
    handle->renderer.remove_column(x, z);
}

extern "C" void pavo_gles2_clear_sections(PavoGles2Handle* handle) {
    if (!handle || !handle->initialized) return;
    handle->renderer.clear_sections();
}

extern "C" void pavo_gles2_draw_sections(
    PavoGles2Handle* handle,
    const float mvp[16],
    const float camera_position[3],
    const float fog_color[4],
    const float fog_range[2],
    float max_distance) {
    if (!handle || !handle->initialized) return;
    handle->renderer.draw_sections(mvp, camera_position, fog_color, fog_range, max_distance);
}

extern "C" int pavo_gles2_update_lightmap(
    PavoGles2Handle* handle, const std::uint8_t* rgba, std::size_t byte_count) {
    if (!handle || !handle->initialized || !rgba) {
        set_error(handle, "Invalid or uninitialized GLES2 lightmap update");
        return 0;
    }
    if (!handle->renderer.update_lightmap(rgba, byte_count)) {
        set_error(handle, handle->renderer.last_error());
        return 0;
    }
    handle->error.clear();
    return 1;
}

extern "C" int pavo_gles2_update_atlas_layer(
    PavoGles2Handle* handle,
    std::uint32_t layer,
    const std::uint8_t* rgba,
    std::size_t byte_count) {
    if (!handle || !handle->initialized || !rgba) {
        set_error(handle, "Invalid or uninitialized GLES2 atlas update");
        return 0;
    }
    if (!handle->renderer.update_atlas_layer(layer, rgba, byte_count)) {
        set_error(handle, handle->renderer.last_error());
        return 0;
    }
    handle->error.clear();
    return 1;
}

extern "C" const char* pavo_gles2_last_error(const PavoGles2Handle* handle) {
    if (!handle) return "GLES2 bridge handle is null";
    if (!handle->error.empty()) return handle->error.c_str();
    return handle->renderer.last_error();
}

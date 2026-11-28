#pragma once

#include <GLES2/gl2.h>
#include "gles2_mesh_bounds.h"
#include <array>
#include <cstddef>
#include <cstdint>
#include <map>
#include <thread>
#include <utility>
#include <vector>

namespace pavo {

struct Gles2TerrainVertex {
    float position[3];
    float uv[2];
    float atlas_origin[2];
    float atlas_scale[2];
    std::uint8_t color[4];
    std::uint8_t emission;
    std::uint8_t material_flags;
    std::uint8_t light[2];
    float opaque_layer;
};

static_assert(offsetof(Gles2TerrainVertex, position) == 0);
static_assert(offsetof(Gles2TerrainVertex, uv) == 12);
static_assert(offsetof(Gles2TerrainVertex, atlas_origin) == 20);
static_assert(offsetof(Gles2TerrainVertex, atlas_scale) == 28);
static_assert(offsetof(Gles2TerrainVertex, color) == 36);
static_assert(offsetof(Gles2TerrainVertex, emission) == 40);
static_assert(offsetof(Gles2TerrainVertex, material_flags) == 41);
static_assert(offsetof(Gles2TerrainVertex, light) == 42);
static_assert(offsetof(Gles2TerrainVertex, opaque_layer) == 44);
static_assert(sizeof(Gles2TerrainVertex) == 48);

// Every GL-facing method, including destruction/release, must run on the
// thread that owns the current EGL context. Destroy this object before EGL teardown.
class Gles2TerrainRenderer {
public:
    Gles2TerrainRenderer() = default;
    ~Gles2TerrainRenderer();
    Gles2TerrainRenderer(const Gles2TerrainRenderer&) = delete;
    Gles2TerrainRenderer& operator=(const Gles2TerrainRenderer&) = delete;

    bool initialize(const std::uint8_t* atlas_rgba, int atlas_width, int atlas_height,
                    const std::uint8_t* lightmap_rgba, int lightmap_width, int lightmap_height,
                    int sprite_size);
    bool upload_solid(const Gles2TerrainVertex* vertices, std::size_t count);
    bool upload_translucent(const Gles2TerrainVertex* vertices, std::size_t count);
    bool update_atlas_layer(std::uint32_t layer, const std::uint8_t* rgba,
                            std::size_t byte_count);
    bool update_lightmap(const std::uint8_t* rgba, std::size_t byte_count);
    bool upload_section(const std::int32_t key[3],
                        const Gles2TerrainVertex* solid, std::size_t solid_count,
                        const Gles2TerrainVertex* translucent, std::size_t translucent_count,
                        const std::uint32_t* first_count_pairs = nullptr,
                        std::size_t range_count = 0, bool use_solid_ranges = false);
    bool upload_lod(const std::int32_t key[3],
                    const Gles2TerrainVertex* solid, std::size_t solid_count,
                    const Gles2TerrainVertex* translucent, std::size_t translucent_count);
    void remove_lod(const std::int32_t key[3]);
    void clear_lods();
    std::size_t lod_count() const { return lods_.size(); }
    bool set_section_solid_ranges(const std::int32_t key[3],
                                  const std::uint32_t* first_count_pairs,
                                  std::size_t range_count);
    void remove_section(const std::int32_t key[3]);
    void remove_column(std::int32_t section_x, std::int32_t section_z);
    void clear_sections();
    std::size_t section_count() const { return sections_.size(); }
    void draw_sections(const float* mvp, const float camera_position[3],
                       const float fog_color[4], const float fog_range[2],
                       float max_distance);
    void draw(const float* mvp, const float section_origin[3],
              const float camera_position[3], const float fog_color[4],
              const float fog_range[2]);
    void release();
    void abandon_context() noexcept;

    const char* last_error() const { return error_; }

private:
    struct SectionBuffers {
        GLuint solid_vbo = 0;
        GLuint translucent_vbo = 0;
        GLsizei solid_count = 0;
        GLsizei translucent_count = 0;
        std::size_t byte_size = 0;
        float center_y = 8.0f;
        Gles2TerrainBounds bounds{};
        bool has_solid_ranges = false;
        std::vector<std::pair<GLint, GLsizei>> solid_ranges;
    };

    struct UniformLocations {
        GLint mvp = -1;
        GLint section_origin = -1;
        GLint camera_position = -1;
        GLint fog_color = -1;
        GLint fog_range = -1;
        GLint atlas_size = -1;
        GLint sprite_size = -1;
        GLint terrain_atlas = -1;
        GLint lightmap = -1;
    };

    bool bind_or_check_owner_thread(const char* operation);
    bool check_owner_thread(const char* operation);
    bool create_programs();
    void cache_uniforms(GLuint program, UniformLocations* uniforms);
    bool upload_texture(GLuint* texture, const std::uint8_t* pixels,
                        int width, int height, GLint filter);
    bool upload_buffer(GLuint* buffer, GLsizei* vertex_count,
                        const Gles2TerrainVertex* vertices, std::size_t count);
    void draw_buffer_range(GLuint program, const UniformLocations& uniforms,
                     GLuint buffer, GLint first, GLsizei count, const float* mvp,
                     const float section_origin[3],
                     const float camera_position[3], const float fog_color[4],
                     const float fog_range[2]);

    GLuint solid_program_ = 0;
    GLuint translucent_program_ = 0;
    UniformLocations solid_uniforms_;
    UniformLocations translucent_uniforms_;
    GLuint atlas_texture_ = 0;
    GLuint lightmap_texture_ = 0;
    GLuint solid_vbo_ = 0;
    GLuint translucent_vbo_ = 0;
    GLsizei solid_count_ = 0;
    GLsizei translucent_count_ = 0;
    std::map<std::array<std::int32_t, 3>, SectionBuffers> sections_;
    std::map<std::array<std::int32_t, 3>, SectionBuffers> lods_;
    std::size_t resident_bytes_ = 0;
    int atlas_width_ = 0;
    int atlas_height_ = 0;
    int sprite_size_ = 0;
    std::thread::id owner_thread_;
    bool owner_thread_set_ = false;
    char error_[512] = {};
};

}  // namespace pavo

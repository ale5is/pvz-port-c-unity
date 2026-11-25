#pragma once

#include <GLES2/gl2.h>
#include <cstddef>
#include <cstdint>

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
    void draw(const float* mvp, const float section_origin[3],
              const float camera_position[3], const float fog_color[4],
              const float fog_range[2]);
    void release();

    const char* last_error() const { return error_; }

private:
    bool create_programs();
    bool upload_texture(GLuint* texture, const std::uint8_t* pixels,
                        int width, int height, GLint filter);
    bool upload_buffer(GLuint* buffer, GLsizei* vertex_count,
                        const Gles2TerrainVertex* vertices, std::size_t count);
    void draw_buffer(GLuint program, GLuint buffer, GLsizei count,
                     const float* mvp, const float section_origin[3],
                     const float camera_position[3], const float fog_color[4],
                     const float fog_range[2]);

    GLuint solid_program_ = 0;
    GLuint translucent_program_ = 0;
    GLuint atlas_texture_ = 0;
    GLuint lightmap_texture_ = 0;
    GLuint solid_vbo_ = 0;
    GLuint translucent_vbo_ = 0;
    GLsizei solid_count_ = 0;
    GLsizei translucent_count_ = 0;
    int atlas_width_ = 0;
    int atlas_height_ = 0;
    int sprite_size_ = 0;
    char error_[512] = {};
};

}  // namespace pavo

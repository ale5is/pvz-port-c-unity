#include "gles2_terrain_renderer.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <new>
#include <stdexcept>
#include <thread>
#include <vector>

namespace pavo {
namespace {

void clear_gl_errors() {
    while (glGetError() != GL_NO_ERROR) {}
}

bool rgba_byte_size(int width, int height, std::size_t* bytes) {
    if (!bytes || width <= 0 || height <= 0) return false;
    const std::size_t w = static_cast<std::size_t>(width);
    const std::size_t h = static_cast<std::size_t>(height);
    const std::size_t max_size = std::numeric_limits<std::size_t>::max();
    if (w > max_size / 4 || h > max_size / (w * 4)) return false;
    *bytes = w * h * 4;
    return *bytes <= static_cast<std::size_t>(std::numeric_limits<GLsizeiptr>::max());
}

const char* kVertexShader = R"GLSL(#version 100
precision highp float;
attribute vec3 a_position;
attribute vec2 a_uv;
attribute vec2 a_atlas_origin;
attribute vec2 a_atlas_scale;
attribute vec4 a_color;
attribute vec2 a_light;
attribute vec2 a_material;
attribute float a_opaque;
uniform mat4 u_mvp;
uniform vec3 u_section_origin;
uniform vec3 u_camera_position;
varying mediump vec2 v_uv;
varying mediump vec2 v_atlas_origin;
varying mediump vec2 v_atlas_scale;
varying lowp vec4 v_color;
varying mediump vec2 v_light;
varying mediump vec2 v_material;
varying mediump float v_opaque;
varying mediump float v_distance;
void main() {
    vec3 world_position = a_position + u_section_origin;
    gl_Position = u_mvp * vec4(world_position, 1.0);
    v_uv = a_uv;
    v_atlas_origin = a_atlas_origin;
    v_atlas_scale = a_atlas_scale;
    v_color = a_color;
    v_light = a_light * 255.0;
    v_material = a_material;
    v_opaque = a_opaque;
    v_distance = length(world_position - u_camera_position);
}
)GLSL";

const char* kSolidFragmentShader = R"GLSL(#version 100
#ifdef GL_FRAGMENT_PRECISION_HIGH
precision highp float;
#else
precision mediump float;
#endif
uniform sampler2D u_terrain_atlas;
uniform sampler2D u_lightmap;
uniform vec2 u_atlas_size;
uniform float u_sprite_size;
uniform vec4 u_fog_color;
uniform vec2 u_fog_range;
varying mediump vec2 v_uv;
varying mediump vec2 v_atlas_origin;
varying mediump vec2 v_atlas_scale;
varying lowp vec4 v_color;
varying mediump vec2 v_light;
varying mediump vec2 v_material;
varying mediump float v_opaque;
varying mediump float v_distance;
void main() {
    vec2 local_uv = v_uv - max(ceil(v_uv) - 1.0, vec2(0.0));
    vec2 half_texel = 0.5 / u_atlas_size;
    vec2 tile_uv = (local_uv * max(u_sprite_size - 1.0, 0.0) + 0.5) / u_atlas_size;
    vec2 atlas_uv = clamp(v_atlas_origin + tile_uv,
                          v_atlas_origin + half_texel,
                          v_atlas_origin + v_atlas_scale - half_texel);
    vec4 texel = texture2D(u_terrain_atlas, atlas_uv);
    if (texel.a < 0.5 && v_opaque < 0.5) discard;
    vec2 light_uv = clamp(vec2(v_light.y, v_light.x) / 256.0 + vec2(0.5 / 16.0),
                          vec2(0.5 / 16.0), vec2(15.5 / 16.0));
    vec3 light = texture2D(u_lightmap, light_uv).rgb;
    float emission = clamp(v_material.x * 119.0, 0.0, 1.0);
    light = mix(light, vec3(1.0), emission);
    vec4 color = vec4(texel.rgb * v_color.rgb * light, 1.0);
    float fog_span = max(u_fog_range.y - u_fog_range.x, 0.0001);
    float fog = clamp((v_distance - u_fog_range.x) / fog_span, 0.0, 1.0);
    color.rgb = mix(color.rgb, u_fog_color.rgb, fog * u_fog_color.a);
    gl_FragColor = color;
}
)GLSL";

const char* kTranslucentFragmentShader = R"GLSL(#version 100
#ifdef GL_FRAGMENT_PRECISION_HIGH
precision highp float;
#else
precision mediump float;
#endif
uniform sampler2D u_terrain_atlas;
uniform sampler2D u_lightmap;
uniform vec2 u_atlas_size;
uniform float u_sprite_size;
uniform vec4 u_fog_color;
uniform vec2 u_fog_range;
varying mediump vec2 v_uv;
varying mediump vec2 v_atlas_origin;
varying mediump vec2 v_atlas_scale;
varying lowp vec4 v_color;
varying mediump vec2 v_light;
varying mediump vec2 v_material;
varying mediump float v_opaque;
varying mediump float v_distance;
void main() {
    vec2 local_uv = v_uv - max(ceil(v_uv) - 1.0, vec2(0.0));
    vec2 half_texel = 0.5 / u_atlas_size;
    vec2 tile_uv = (local_uv * max(u_sprite_size - 1.0, 0.0) + 0.5) / u_atlas_size;
    vec2 atlas_uv = clamp(v_atlas_origin + tile_uv,
                          v_atlas_origin + half_texel,
                          v_atlas_origin + v_atlas_scale - half_texel);
    vec4 texel = texture2D(u_terrain_atlas, atlas_uv);
    vec2 light_uv = clamp(vec2(v_light.y, v_light.x) / 256.0 + vec2(0.5 / 16.0),
                          vec2(0.5 / 16.0), vec2(15.5 / 16.0));
    vec3 light = texture2D(u_lightmap, light_uv).rgb;
    float emission = clamp(v_material.x * 119.0, 0.0, 1.0);
    light = mix(light, vec3(1.0), emission);
    vec4 color = vec4(texel.rgb * v_color.rgb * light, texel.a);
    if (color.a < 0.01) discard;
    float fog_span = max(u_fog_range.y - u_fog_range.x, 0.0001);
    float fog = clamp((v_distance - u_fog_range.x) / fog_span, 0.0, 1.0);
    color.rgb = mix(color.rgb, u_fog_color.rgb, fog * u_fog_color.a);
    gl_FragColor = color;
}
)GLSL";

GLuint compile_shader(GLenum type, const char* source, char* error, std::size_t error_size) {
    GLuint shader = glCreateShader(type);
    if (!shader) {
        std::snprintf(error, error_size, "glCreateShader failed: 0x%x", glGetError());
        return 0;
    }
    glShaderSource(shader, 1, &source, nullptr);
    glCompileShader(shader);
    GLint ok = GL_FALSE;
    glGetShaderiv(shader, GL_COMPILE_STATUS, &ok);
    if (ok != GL_TRUE) {
        GLsizei written = 0;
        glGetShaderInfoLog(shader, static_cast<GLsizei>(error_size), &written, error);
        glDeleteShader(shader);
        return 0;
    }
    return shader;
}

GLuint link_program(const char* fragment, char* error, std::size_t error_size) {
    GLuint vertex_shader = compile_shader(GL_VERTEX_SHADER, kVertexShader, error, error_size);
    if (!vertex_shader) return 0;
    GLuint fragment_shader = compile_shader(GL_FRAGMENT_SHADER, fragment, error, error_size);
    if (!fragment_shader) {
        glDeleteShader(vertex_shader);
        return 0;
    }
    GLuint program = glCreateProgram();
    glAttachShader(program, vertex_shader);
    glAttachShader(program, fragment_shader);
    glBindAttribLocation(program, 0, "a_position");
    glBindAttribLocation(program, 1, "a_uv");
    glBindAttribLocation(program, 2, "a_atlas_origin");
    glBindAttribLocation(program, 3, "a_atlas_scale");
    glBindAttribLocation(program, 4, "a_color");
    glBindAttribLocation(program, 5, "a_light");
    glBindAttribLocation(program, 6, "a_opaque");
    glBindAttribLocation(program, 7, "a_material");
    glLinkProgram(program);
    GLint linked = GL_FALSE;
    glGetProgramiv(program, GL_LINK_STATUS, &linked);
    if (linked != GL_TRUE) {
        glGetProgramInfoLog(program, static_cast<GLsizei>(error_size), nullptr, error);
        glDeleteProgram(program);
        program = 0;
    }
    glDeleteShader(vertex_shader);
    glDeleteShader(fragment_shader);
    return program;
}

}  // namespace

Gles2TerrainRenderer::~Gles2TerrainRenderer() { release(); }

bool Gles2TerrainRenderer::bind_or_check_owner_thread(const char* operation) {
    const std::thread::id current = std::this_thread::get_id();
    if (!owner_thread_set_) {
        owner_thread_ = current;
        owner_thread_set_ = true;
        return true;
    }
    if (owner_thread_ == current) return true;
    std::snprintf(error_, sizeof(error_),
                  "%s must run on the EGL context owner thread", operation ? operation : "GLES2 operation");
    return false;
}

bool Gles2TerrainRenderer::check_owner_thread(const char* operation) {
    if (!owner_thread_set_) {
        std::snprintf(error_, sizeof(error_),
                      "%s requires initialize() on the EGL context owner thread",
                      operation ? operation : "GLES2 operation");
        return false;
    }
    return bind_or_check_owner_thread(operation);
}

bool Gles2TerrainRenderer::create_programs() {
    solid_program_ = link_program(kSolidFragmentShader, error_, sizeof(error_));
    if (!solid_program_) return false;
    translucent_program_ = link_program(kTranslucentFragmentShader, error_, sizeof(error_));
    if (!translucent_program_) return false;
    cache_uniforms(solid_program_, &solid_uniforms_);
    cache_uniforms(translucent_program_, &translucent_uniforms_);
    return true;
}

void Gles2TerrainRenderer::cache_uniforms(GLuint program, UniformLocations* uniforms) {
    if (!program || !uniforms) return;
    uniforms->mvp = glGetUniformLocation(program, "u_mvp");
    uniforms->section_origin = glGetUniformLocation(program, "u_section_origin");
    uniforms->camera_position = glGetUniformLocation(program, "u_camera_position");
    uniforms->fog_color = glGetUniformLocation(program, "u_fog_color");
    uniforms->fog_range = glGetUniformLocation(program, "u_fog_range");
    uniforms->atlas_size = glGetUniformLocation(program, "u_atlas_size");
    uniforms->sprite_size = glGetUniformLocation(program, "u_sprite_size");
    uniforms->terrain_atlas = glGetUniformLocation(program, "u_terrain_atlas");
    uniforms->lightmap = glGetUniformLocation(program, "u_lightmap");
}

bool Gles2TerrainRenderer::upload_texture(GLuint* texture, const std::uint8_t* pixels,
                                          int width, int height, GLint filter) {
    std::size_t byte_size = 0;
    if (!texture || !pixels || !rgba_byte_size(width, height, &byte_size)) {
        std::snprintf(error_, sizeof(error_), "Invalid texture data, dimensions, or byte size");
        return false;
    }
    clear_gl_errors();
    GLint max_size = 0;
    glGetIntegerv(GL_MAX_TEXTURE_SIZE, &max_size);
    const GLenum limit_error = glGetError();
    if (limit_error != GL_NO_ERROR || max_size <= 0) {
        std::snprintf(error_, sizeof(error_), "Could not query GL_MAX_TEXTURE_SIZE: 0x%x", limit_error);
        return false;
    }
    if (width > max_size || height > max_size) {
        std::snprintf(error_, sizeof(error_), "Texture %dx%d exceeds GL_MAX_TEXTURE_SIZE %d",
                      width, height, max_size);
        return false;
    }
    glActiveTexture(GL_TEXTURE0);
    glGenTextures(1, texture);
    if (!*texture) {
        std::snprintf(error_, sizeof(error_), "glGenTextures returned texture 0");
        return false;
    }
    glBindTexture(GL_TEXTURE_2D, *texture);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, filter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, filter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 4);
    const GLenum error = glGetError();
    if (error != GL_NO_ERROR) {
        std::snprintf(error_, sizeof(error_), "glTexImage2D failed: 0x%x", error);
        glDeleteTextures(1, texture);
        *texture = 0;
        return false;
    }
    glBindTexture(GL_TEXTURE_2D, 0);
    (void)byte_size;
    return true;
}

bool Gles2TerrainRenderer::initialize(const std::uint8_t* atlas_rgba, int atlas_width,
                                      int atlas_height, const std::uint8_t* lightmap_rgba,
                                      int lightmap_width, int lightmap_height, int sprite_size) {
    if (!bind_or_check_owner_thread("initialize")) return false;
    release();
    std::size_t atlas_bytes = 0;
    std::size_t lightmap_bytes = 0;
    if (sprite_size <= 0 || sprite_size > atlas_width || sprite_size > atlas_height ||
        !rgba_byte_size(atlas_width, atlas_height, &atlas_bytes) ||
        !rgba_byte_size(lightmap_width, lightmap_height, &lightmap_bytes) ||
        !atlas_rgba || !lightmap_rgba) {
        std::snprintf(error_, sizeof(error_), "Invalid atlas/lightmap pixels, dimensions, or sprite size");
        return false;
    }
    atlas_width_ = atlas_width;
    atlas_height_ = atlas_height;
    sprite_size_ = sprite_size;
    if (!create_programs()) {
        release();
        return false;
    }
    if (!upload_texture(&atlas_texture_, atlas_rgba, atlas_width, atlas_height, GL_LINEAR) ||
        !upload_texture(&lightmap_texture_, lightmap_rgba, lightmap_width, lightmap_height, GL_LINEAR)) {
        release();
        return false;
    }
    error_[0] = '\0';
    (void)atlas_bytes;
    (void)lightmap_bytes;
    return true;
}

bool Gles2TerrainRenderer::update_atlas_layer(std::uint32_t layer,
                                               const std::uint8_t* rgba,
                                               std::size_t byte_count) {
    if (!check_owner_thread("update_atlas_layer")) return false;
    if (!atlas_texture_ || !rgba || sprite_size_ <= 0 || atlas_width_ <= 0 ||
        atlas_height_ <= 0) {
        std::snprintf(error_, sizeof(error_), "Atlas layer update requires an initialized atlas and RGBA pixels");
        return false;
    }

    const std::size_t sprite = static_cast<std::size_t>(sprite_size_);
    if (sprite > std::numeric_limits<std::size_t>::max() / 4 ||
        sprite > std::numeric_limits<std::size_t>::max() / (sprite * 4)) {
        std::snprintf(error_, sizeof(error_), "Animated terrain sprite byte size overflow");
        return false;
    }
    const std::size_t row_bytes = sprite * 4;
    const std::size_t expected_bytes = row_bytes * sprite;
    if (byte_count != expected_bytes) {
        std::snprintf(error_, sizeof(error_), "Animated terrain layer must contain exactly one RGBA sprite");
        return false;
    }

    const std::uint32_t cell_size = static_cast<std::uint32_t>(sprite_size_) + 2u;
    if (cell_size < static_cast<std::uint32_t>(sprite_size_) ||
        atlas_width_ % static_cast<int>(cell_size) != 0 ||
        atlas_height_ % static_cast<int>(cell_size) != 0) {
        std::snprintf(error_, sizeof(error_), "Packed terrain atlas dimensions do not match guttered sprite cells");
        return false;
    }
    const std::uint32_t columns = static_cast<std::uint32_t>(atlas_width_) / cell_size;
    const std::uint32_t rows = static_cast<std::uint32_t>(atlas_height_) / cell_size;
    const std::uint64_t cell_count = static_cast<std::uint64_t>(columns) * rows;
    if (columns == 0 || rows == 0 || layer >= cell_count) {
        std::snprintf(error_, sizeof(error_), "Animated terrain sprite layer is outside the packed atlas");
        return false;
    }

    const std::size_t padded_row_bytes = static_cast<std::size_t>(cell_size) * 4;
    if (cell_size > std::numeric_limits<std::size_t>::max() / padded_row_bytes) {
        std::snprintf(error_, sizeof(error_), "Animated terrain padded cell byte size overflow");
        return false;
    }
    std::vector<std::uint8_t> padded_cell;
    try {
        padded_cell.resize(padded_row_bytes * static_cast<std::size_t>(cell_size));
    } catch (const std::bad_alloc&) {
        std::snprintf(error_, sizeof(error_), "Not enough memory to stage animated terrain atlas cell");
        return false;
    } catch (const std::length_error&) {
        std::snprintf(error_, sizeof(error_), "Animated terrain atlas cell exceeds vector limits");
        return false;
    }

    for (std::size_t row = 0; row < sprite; ++row) {
        const std::uint8_t* source = rgba + row * row_bytes;
        std::uint8_t* destination = padded_cell.data() + (row + 1) * padded_row_bytes;
        std::memcpy(destination, source, 4);
        std::memcpy(destination + 4, source, row_bytes);
        std::memcpy(destination + 4 + row_bytes, source + row_bytes - 4, 4);
    }
    std::memcpy(padded_cell.data(), padded_cell.data() + padded_row_bytes,
                padded_row_bytes);
    std::memcpy(padded_cell.data() + (static_cast<std::size_t>(cell_size) - 1) * padded_row_bytes,
                padded_cell.data() + (static_cast<std::size_t>(cell_size) - 2) * padded_row_bytes,
                padded_row_bytes);

    const GLint x = static_cast<GLint>((layer % columns) * cell_size);
    const GLint y = static_cast<GLint>((layer / columns) * cell_size);
    clear_gl_errors();
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, atlas_texture_);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexSubImage2D(GL_TEXTURE_2D, 0, x, y, static_cast<GLsizei>(cell_size),
                    static_cast<GLsizei>(cell_size), GL_RGBA, GL_UNSIGNED_BYTE,
                    padded_cell.data());
    glPixelStorei(GL_UNPACK_ALIGNMENT, 4);
    glBindTexture(GL_TEXTURE_2D, 0);
    const GLenum error = glGetError();
    if (error != GL_NO_ERROR) {
        std::snprintf(error_, sizeof(error_), "Animated terrain atlas sub-image upload failed: 0x%x", error);
        return false;
    }
    error_[0] = '\\0';
    return true;
}

bool Gles2TerrainRenderer::upload_buffer(GLuint* buffer, GLsizei* vertex_count,
                                         const Gles2TerrainVertex* vertices, std::size_t count) {
    if (!buffer || !vertex_count || (count && !vertices) || (count % 3) != 0 ||
        count > static_cast<std::size_t>(std::numeric_limits<GLsizei>::max()) ||
        count > std::numeric_limits<std::size_t>::max() / sizeof(Gles2TerrainVertex) ||
        count > static_cast<std::size_t>(std::numeric_limits<GLsizeiptr>::max()) / sizeof(Gles2TerrainVertex)) {
        std::snprintf(error_, sizeof(error_), "Terrain vertex count must be a triangle list with a valid buffer size");
        return false;
    }
    if (count == 0) {
        if (*buffer) glDeleteBuffers(1, buffer);
        *buffer = 0;
        *vertex_count = 0;
        error_[0] = '\0';
        return true;
    }

    clear_gl_errors();
    GLuint replacement_buffer = 0;
    glGenBuffers(1, &replacement_buffer);
    if (!replacement_buffer) {
        std::snprintf(error_, sizeof(error_), "glGenBuffers returned buffer 0");
        return false;
    }
    glBindBuffer(GL_ARRAY_BUFFER, replacement_buffer);
    glBufferData(GL_ARRAY_BUFFER, static_cast<GLsizeiptr>(count * sizeof(Gles2TerrainVertex)),
                 vertices, GL_STATIC_DRAW);
    glBindBuffer(GL_ARRAY_BUFFER, 0);
    const GLenum error = glGetError();
    if (error != GL_NO_ERROR) {
        std::snprintf(error_, sizeof(error_), "Terrain VBO upload failed: 0x%x", error);
        glDeleteBuffers(1, &replacement_buffer);
        return false;
    }

    if (*buffer) glDeleteBuffers(1, buffer);
    *buffer = replacement_buffer;
    *vertex_count = static_cast<GLsizei>(count);
    error_[0] = '\0';
    return true;
}

bool Gles2TerrainRenderer::upload_solid(const Gles2TerrainVertex* vertices, std::size_t count) {
    if (!check_owner_thread("upload_solid")) return false;
    return upload_buffer(&solid_vbo_, &solid_count_, vertices, count);
}

bool Gles2TerrainRenderer::upload_translucent(const Gles2TerrainVertex* vertices, std::size_t count) {
    if (!check_owner_thread("upload_translucent")) return false;
    return upload_buffer(&translucent_vbo_, &translucent_count_, vertices, count);
}

bool Gles2TerrainRenderer::upload_section(const std::int32_t key[3],
                                          const Gles2TerrainVertex* solid,
                                          std::size_t solid_count,
                                          const Gles2TerrainVertex* translucent,
                                          std::size_t translucent_count,
                                          const std::uint32_t* first_count_pairs,
                                          std::size_t range_count,
                                          bool use_solid_ranges) {
    if (!check_owner_thread("upload_section")) return false;
    if (!key || (range_count != 0 && !first_count_pairs) ||
        (!use_solid_ranges && range_count != 0)) {
        std::snprintf(error_, sizeof(error_), "Section key and optional solid range data are required");
        return false;
    }
    std::vector<std::pair<GLint, GLsizei>> solid_ranges;
    if (use_solid_ranges) {
        if (range_count > std::numeric_limits<std::size_t>::max() / 2 ||
            solid_count % 3 != 0 || range_count > solid_count / 3) {
            std::snprintf(error_, sizeof(error_), "Solid side range count exceeds the incoming triangle list or packed-index limit");
            return false;
        }
        try {
            solid_ranges.reserve(range_count);
        } catch (const std::bad_alloc&) {
            std::snprintf(error_, sizeof(error_), "Not enough memory to allocate solid side ranges");
            return false;
        } catch (const std::length_error&) {
            std::snprintf(error_, sizeof(error_), "Solid side range count exceeds the vector allocation limit");
            return false;
        }
        std::uint64_t previous_end = 0;
        for (std::size_t i = 0; i < range_count; ++i) {
            const std::uint64_t first = first_count_pairs[i * 2];
            const std::uint64_t count = first_count_pairs[i * 2 + 1];
            const std::uint64_t end = first + count;
            if (count == 0 || first % 3 != 0 || count % 3 != 0 ||
                end > static_cast<std::uint64_t>(solid_count) || end < first ||
                (i != 0 && first < previous_end) ||
                first > static_cast<std::uint64_t>(std::numeric_limits<GLint>::max()) ||
                count > static_cast<std::uint64_t>(std::numeric_limits<GLsizei>::max())) {
                std::snprintf(error_, sizeof(error_), "Incoming solid side range %zu is invalid or overlaps", i);
                return false;
            }
            solid_ranges.emplace_back(static_cast<GLint>(first), static_cast<GLsizei>(count));
            previous_end = end;
        }
    }
    const std::array<std::int32_t, 3> section_key{{key[0], key[1], key[2]}};
    if (solid_count == 0 && translucent_count == 0) {
        remove_section(key);
        error_[0] = '\0';
        return true;
    }
    const auto existing = sections_.find(section_key);
    constexpr std::size_t kMaximumResidentSections = 512;
    constexpr std::size_t kMaximumResidentBytes = 32u * 1024u * 1024u;
    if (existing == sections_.end() && sections_.size() >= kMaximumResidentSections) {
        std::snprintf(error_, sizeof(error_), "GLES2 resident section limit (%zu) reached",
                      kMaximumResidentSections);
        return false;
    }
    const std::size_t vertex_size = sizeof(Gles2TerrainVertex);
    if (solid_count > std::numeric_limits<std::size_t>::max() / vertex_size ||
        translucent_count > std::numeric_limits<std::size_t>::max() / vertex_size) {
        std::snprintf(error_, sizeof(error_), "Section VBO byte count overflow");
        return false;
    }
    const std::size_t solid_bytes = solid_count * vertex_size;
    const std::size_t translucent_bytes = translucent_count * vertex_size;
    if (translucent_bytes > std::numeric_limits<std::size_t>::max() - solid_bytes) {
        std::snprintf(error_, sizeof(error_), "Combined section VBO byte count overflow");
        return false;
    }
    const std::size_t replacement_bytes = solid_bytes + translucent_bytes;
    const std::size_t previous_bytes = existing == sections_.end() ? 0 : existing->second.byte_size;
    if (previous_bytes > resident_bytes_) {
        std::snprintf(error_, sizeof(error_), "GLES2 resident VBO accounting is inconsistent");
        return false;
    }
    const std::size_t retained_bytes = resident_bytes_ - previous_bytes;
    if (resident_bytes_ > kMaximumResidentBytes ||
        replacement_bytes > kMaximumResidentBytes - resident_bytes_) {
        std::snprintf(error_, sizeof(error_),
                      "GLES2 temporary plus resident VBO budget exceeded: requested %zu bytes, available %zu",
                      replacement_bytes, resident_bytes_ > kMaximumResidentBytes
                          ? 0 : kMaximumResidentBytes - resident_bytes_);
        return false;
    }

    auto insertion = sections_.end();
    if (existing == sections_.end()) {
        try {
            insertion = sections_.try_emplace(section_key).first;
        } catch (const std::bad_alloc&) {
            std::snprintf(error_, sizeof(error_), "Not enough memory to register the resident GLES2 section");
            return false;
        } catch (const std::length_error&) {
            std::snprintf(error_, sizeof(error_), "Resident GLES2 section table exceeds its allocation limit");
            return false;
        }
    }

    SectionBuffers replacement;
    replacement.byte_size = replacement_bytes;
    replacement.has_solid_ranges = use_solid_ranges;
    replacement.solid_ranges = std::move(solid_ranges);
    if (!upload_buffer(&replacement.solid_vbo, &replacement.solid_count, solid, solid_count) ||
        !upload_buffer(&replacement.translucent_vbo, &replacement.translucent_count,
                       translucent, translucent_count)) {
        if (replacement.solid_vbo) glDeleteBuffers(1, &replacement.solid_vbo);
        if (replacement.translucent_vbo) glDeleteBuffers(1, &replacement.translucent_vbo);
        if (insertion != sections_.end()) sections_.erase(insertion);
        return false;
    }

    if (existing != sections_.end()) {
        if (existing->second.solid_vbo) glDeleteBuffers(1, &existing->second.solid_vbo);
        if (existing->second.translucent_vbo) glDeleteBuffers(1, &existing->second.translucent_vbo);
        existing->second = std::move(replacement);
    } else {
        insertion->second = std::move(replacement);
    }
    resident_bytes_ = retained_bytes + replacement_bytes;
    error_[0] = '\0';
    return true;
}

bool Gles2TerrainRenderer::set_section_solid_ranges(
    const std::int32_t key[3], const std::uint32_t* first_count_pairs,
    std::size_t range_count) {
    if (!check_owner_thread("set_section_solid_ranges")) return false;
    if (!key || (range_count && !first_count_pairs)) {
        std::snprintf(error_, sizeof(error_), "Section key and solid range data are required");
        return false;
    }
    const std::array<std::int32_t, 3> section_key{{key[0], key[1], key[2]}};
    const auto it = sections_.find(section_key);
    if (it == sections_.end()) {
        std::snprintf(error_, sizeof(error_), "Cannot set solid ranges for a nonresident section");
        return false;
    }
    if (range_count > std::numeric_limits<std::size_t>::max() / 2 ||
        range_count > static_cast<std::size_t>(it->second.solid_count) / 3) {
        std::snprintf(error_, sizeof(error_), "Solid side range count exceeds the resident triangle list or packed-index limit");
        return false;
    }
    std::vector<std::pair<GLint, GLsizei>> ranges;
    try {
        ranges.reserve(range_count);
    } catch (const std::bad_alloc&) {
        std::snprintf(error_, sizeof(error_), "Not enough memory to allocate solid side ranges");
        return false;
    } catch (const std::length_error&) {
        std::snprintf(error_, sizeof(error_), "Solid side range count exceeds the vector allocation limit");
        return false;
    }
    std::uint64_t previous_end = 0;
    for (std::size_t i = 0; i < range_count; ++i) {
        const std::uint64_t first = first_count_pairs[i * 2];
        const std::uint64_t count = first_count_pairs[i * 2 + 1];
        const std::uint64_t end = first + count;
        if (count == 0 || first % 3 != 0 || count % 3 != 0 ||
            end > static_cast<std::uint64_t>(it->second.solid_count) ||
            end < first || (i != 0 && first < previous_end) ||
            first > static_cast<std::uint64_t>(std::numeric_limits<GLint>::max()) ||
            count > static_cast<std::uint64_t>(std::numeric_limits<GLsizei>::max())) {
            std::snprintf(error_, sizeof(error_), "Solid side range %zu is invalid or overlaps", i);
            return false;
        }
        ranges.emplace_back(static_cast<GLint>(first), static_cast<GLsizei>(count));
        previous_end = end;
    }
    it->second.solid_ranges = std::move(ranges);
    it->second.has_solid_ranges = true;
    error_[0] = '\0';
    return true;
}

void Gles2TerrainRenderer::remove_section(const std::int32_t key[3]) {
    if (!check_owner_thread("remove_section")) return;
    if (!key) return;
    const std::array<std::int32_t, 3> section_key{{key[0], key[1], key[2]}};
    const auto it = sections_.find(section_key);
    if (it == sections_.end()) return;
    if (it->second.solid_vbo) glDeleteBuffers(1, &it->second.solid_vbo);
    if (it->second.translucent_vbo) glDeleteBuffers(1, &it->second.translucent_vbo);
    resident_bytes_ -= it->second.byte_size;
    sections_.erase(it);
}

void Gles2TerrainRenderer::remove_column(std::int32_t section_x, std::int32_t section_z) {
    if (!check_owner_thread("remove_column")) return;
    for (auto it = sections_.begin(); it != sections_.end();) {
        if (it->first[0] == section_x && it->first[2] == section_z) {
            if (it->second.solid_vbo) glDeleteBuffers(1, &it->second.solid_vbo);
            if (it->second.translucent_vbo) glDeleteBuffers(1, &it->second.translucent_vbo);
            resident_bytes_ -= it->second.byte_size;
            it = sections_.erase(it);
        } else {
            ++it;
        }
    }
}

void Gles2TerrainRenderer::clear_sections() {
    if (!check_owner_thread("clear_sections")) return;
    for (auto& entry : sections_) {
        if (entry.second.solid_vbo) glDeleteBuffers(1, &entry.second.solid_vbo);
        if (entry.second.translucent_vbo) glDeleteBuffers(1, &entry.second.translucent_vbo);
    }
    sections_.clear();
    resident_bytes_ = 0;
}

void Gles2TerrainRenderer::draw_sections(const float* mvp, const float camera_position[3],
                                         const float fog_color[4], const float fog_range[2],
                                         float max_distance) {
    if (!check_owner_thread("draw_sections")) return;
    if (!solid_program_ || !translucent_program_ || !mvp || !camera_position ||
        !fog_color || !fog_range) return;
    const auto finite_values = [](const float* values, std::size_t count) {
        return std::all_of(values, values + count,
                           [](float value) { return std::isfinite(value); });
    };
    if (!finite_values(mvp, 16) || !finite_values(camera_position, 3) ||
        !finite_values(fog_color, 4) || !finite_values(fog_range, 2) ||
        !std::isfinite(max_distance)) {
        std::snprintf(error_, sizeof(error_), "Terrain draw parameters must contain only finite values");
        return;
    }
    struct VisibleSection {
        float distance_squared;
        const SectionBuffers* buffers;
        float origin[3];
        std::array<std::int32_t, 3> key;
    };
    std::vector<VisibleSection> visible;
    try {
        visible.reserve(sections_.size());
    } catch (const std::bad_alloc&) {
        std::snprintf(error_, sizeof(error_), "Not enough memory to build the visible GLES2 section list");
        return;
    } catch (const std::length_error&) {
        std::snprintf(error_, sizeof(error_), "Visible GLES2 section list exceeds the vector allocation limit");
        return;
    }
    constexpr float kSectionCullRadius = 13.85640646f;
    const float max_cull_distance = max_distance > 0.0f ? max_distance + kSectionCullRadius : 0.0f;
    const float max_distance_squared = max_cull_distance * max_cull_distance;
    constexpr float kSectionSize = 16.0f;
    for (const auto& entry : sections_) {
        const float origin[3] = {
            static_cast<float>(entry.first[0]) * kSectionSize,
            static_cast<float>(entry.first[1]) * kSectionSize,
            static_cast<float>(entry.first[2]) * kSectionSize
        };
        const float dx = origin[0] + 8.0f - camera_position[0];
        const float dy = origin[1] + 8.0f - camera_position[1];
        const float dz = origin[2] + 8.0f - camera_position[2];
        const float distance_squared = dx * dx + dy * dy + dz * dz;
        if (max_distance_squared > 0.0f &&
            distance_squared > max_distance_squared) continue;
        visible.push_back({distance_squared, &entry.second, {origin[0], origin[1], origin[2]}, entry.first});
    }

    glEnable(GL_DEPTH_TEST);
    glDepthFunc(GL_LEQUAL);
    glDepthMask(GL_TRUE);
    glDisable(GL_BLEND);
    for (const auto& section : visible) {
        if (section.buffers->has_solid_ranges) {
            for (const auto& range : section.buffers->solid_ranges) {
                draw_buffer_range(solid_program_, solid_uniforms_, section.buffers->solid_vbo,
                                  range.first, range.second, mvp, section.origin,
                                  camera_position, fog_color, fog_range);
            }
        } else {
            draw_buffer_range(solid_program_, solid_uniforms_, section.buffers->solid_vbo,
                              0, section.buffers->solid_count, mvp, section.origin,
                              camera_position, fog_color, fog_range);
        }
    }

    std::sort(visible.begin(), visible.end(),
              [](const VisibleSection& a, const VisibleSection& b) {
                  if (a.distance_squared != b.distance_squared) {
                      return a.distance_squared > b.distance_squared;
                  }
                  return a.key < b.key;
              });
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    glDepthMask(GL_FALSE);
    for (const auto& section : visible) {
        draw_buffer_range(translucent_program_, translucent_uniforms_,
                          section.buffers->translucent_vbo, 0,
                          section.buffers->translucent_count, mvp, section.origin,
                          camera_position, fog_color, fog_range);
    }
    glDepthMask(GL_TRUE);
    glDisable(GL_BLEND);
    glActiveTexture(GL_TEXTURE1);
    glBindTexture(GL_TEXTURE_2D, 0);
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, 0);
    glUseProgram(0);
    error_[0] = '\0';
}

void Gles2TerrainRenderer::draw_buffer_range(GLuint program, const UniformLocations& uniforms,
                                             GLuint buffer, GLint first, GLsizei count, const float* mvp,
                                       const float section_origin[3], const float camera_position[3],
                                       const float fog_color[4], const float fog_range[2]) {
    if (!program || !buffer || count <= 0) return;
    glUseProgram(program);
    if (uniforms.mvp >= 0) glUniformMatrix4fv(uniforms.mvp, 1, GL_FALSE, mvp);
    if (uniforms.section_origin >= 0) glUniform3fv(uniforms.section_origin, 1, section_origin);
    if (uniforms.camera_position >= 0) glUniform3fv(uniforms.camera_position, 1, camera_position);
    if (uniforms.fog_color >= 0) glUniform4fv(uniforms.fog_color, 1, fog_color);
    if (uniforms.fog_range >= 0) glUniform2fv(uniforms.fog_range, 1, fog_range);
    if (uniforms.atlas_size >= 0) glUniform2f(uniforms.atlas_size,
                static_cast<float>(atlas_width_), static_cast<float>(atlas_height_));
    if (uniforms.sprite_size >= 0) glUniform1f(uniforms.sprite_size, static_cast<float>(sprite_size_));
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, atlas_texture_);
    if (uniforms.terrain_atlas >= 0) glUniform1i(uniforms.terrain_atlas, 0);
    glActiveTexture(GL_TEXTURE1);
    glBindTexture(GL_TEXTURE_2D, lightmap_texture_);
    if (uniforms.lightmap >= 0) glUniform1i(uniforms.lightmap, 1);

    glBindBuffer(GL_ARRAY_BUFFER, buffer);
    const GLsizei stride = sizeof(Gles2TerrainVertex);
    glEnableVertexAttribArray(0);
    glEnableVertexAttribArray(1);
    glEnableVertexAttribArray(2);
    glEnableVertexAttribArray(3);
    glEnableVertexAttribArray(4);
    glEnableVertexAttribArray(5);
    glEnableVertexAttribArray(6);
    glEnableVertexAttribArray(7);
    glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, position)));
    glVertexAttribPointer(1, 2, GL_FLOAT, GL_FALSE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, uv)));
    glVertexAttribPointer(2, 2, GL_FLOAT, GL_FALSE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, atlas_origin)));
    glVertexAttribPointer(3, 2, GL_FLOAT, GL_FALSE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, atlas_scale)));
    glVertexAttribPointer(4, 4, GL_UNSIGNED_BYTE, GL_TRUE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, color)));
    glVertexAttribPointer(5, 2, GL_UNSIGNED_BYTE, GL_TRUE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, light)));
    glVertexAttribPointer(6, 1, GL_FLOAT, GL_FALSE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, opaque_layer)));
    glVertexAttribPointer(7, 2, GL_UNSIGNED_BYTE, GL_TRUE, stride,
                          reinterpret_cast<const void*>(offsetof(Gles2TerrainVertex, emission)));
    glDrawArrays(GL_TRIANGLES, first, count);
    for (GLuint index = 0; index <= 7; ++index) glDisableVertexAttribArray(index);
    glBindBuffer(GL_ARRAY_BUFFER, 0);
}

void Gles2TerrainRenderer::draw(const float* mvp, const float section_origin[3],
                                const float camera_position[3], const float fog_color[4],
                                const float fog_range[2]) {
    if (!check_owner_thread("draw")) return;
    if (!solid_program_ || !translucent_program_ || !mvp || !section_origin ||
        !camera_position || !fog_color || !fog_range) return;
    glEnable(GL_DEPTH_TEST);
    glDepthFunc(GL_LEQUAL);
    glDepthMask(GL_TRUE);
    glDisable(GL_BLEND);
    draw_buffer_range(solid_program_, solid_uniforms_, solid_vbo_, 0, solid_count_, mvp,
                      section_origin, camera_position, fog_color, fog_range);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    glDepthMask(GL_FALSE);
    draw_buffer_range(translucent_program_, translucent_uniforms_, translucent_vbo_, 0,
                      translucent_count_, mvp, section_origin, camera_position, fog_color, fog_range);
    glDepthMask(GL_TRUE);
    glDisable(GL_BLEND);
    glActiveTexture(GL_TEXTURE1);
    glBindTexture(GL_TEXTURE_2D, 0);
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, 0);
    glUseProgram(0);
}

void Gles2TerrainRenderer::release() {
    if (!owner_thread_set_) return;
    if (!check_owner_thread("release")) return;
    clear_sections();
    glActiveTexture(GL_TEXTURE0);
    if (solid_vbo_) glDeleteBuffers(1, &solid_vbo_);
    if (translucent_vbo_) glDeleteBuffers(1, &translucent_vbo_);
    if (atlas_texture_) glDeleteTextures(1, &atlas_texture_);
    if (lightmap_texture_) glDeleteTextures(1, &lightmap_texture_);
    if (solid_program_) glDeleteProgram(solid_program_);
    if (translucent_program_) glDeleteProgram(translucent_program_);
    glBindBuffer(GL_ARRAY_BUFFER, 0);
    glActiveTexture(GL_TEXTURE1);
    glBindTexture(GL_TEXTURE_2D, 0);
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, 0);
    glUseProgram(0);
    solid_vbo_ = translucent_vbo_ = atlas_texture_ = lightmap_texture_ = 0;
    solid_program_ = translucent_program_ = 0;
    solid_uniforms_ = {};
    translucent_uniforms_ = {};
    solid_count_ = translucent_count_ = 0;
    atlas_width_ = atlas_height_ = sprite_size_ = 0;
}

}  // namespace pavo

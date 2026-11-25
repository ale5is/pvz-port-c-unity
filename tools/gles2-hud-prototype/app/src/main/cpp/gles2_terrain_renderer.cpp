#include "gles2_terrain_renderer.h"

#include <algorithm>
#include <cstdio>
#include <cstring>
#include <limits>

namespace pavo {
namespace {

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
varying highp float v_distance;
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
precision highp float;
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
varying highp float v_distance;
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
    float emission = clamp(v_material.x * 17.0, 0.0, 1.0);
    light = mix(light, vec3(1.0), emission);
    vec4 color = vec4(texel.rgb * v_color.rgb * light, 1.0);
    float fog_span = max(u_fog_range.y - u_fog_range.x, 0.0001);
    float fog = clamp((v_distance - u_fog_range.x) / fog_span, 0.0, 1.0);
    color.rgb = mix(color.rgb, u_fog_color.rgb, fog * u_fog_color.a);
    gl_FragColor = color;
}
)GLSL";

const char* kTranslucentFragmentShader = R"GLSL(#version 100
precision highp float;
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
varying highp float v_distance;
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
    float emission = clamp(v_material.x * 17.0, 0.0, 1.0);
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

bool Gles2TerrainRenderer::create_programs() {
    solid_program_ = link_program(kSolidFragmentShader, error_, sizeof(error_));
    if (!solid_program_) return false;
    translucent_program_ = link_program(kTranslucentFragmentShader, error_, sizeof(error_));
    return translucent_program_ != 0;
}

bool Gles2TerrainRenderer::upload_texture(GLuint* texture, const std::uint8_t* pixels,
                                          int width, int height, GLint filter) {
    if (!pixels || width <= 0 || height <= 0) {
        std::snprintf(error_, sizeof(error_), "Invalid texture data or dimensions");
        return false;
    }
    GLint max_size = 0;
    glGetIntegerv(GL_MAX_TEXTURE_SIZE, &max_size);
    if (width > max_size || height > max_size) {
        std::snprintf(error_, sizeof(error_), "Texture %dx%d exceeds GL_MAX_TEXTURE_SIZE %d",
                      width, height, max_size);
        return false;
    }
    glGenTextures(1, texture);
    glBindTexture(GL_TEXTURE_2D, *texture);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, filter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, filter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels);
    const GLenum error = glGetError();
    if (error != GL_NO_ERROR) {
        std::snprintf(error_, sizeof(error_), "glTexImage2D failed: 0x%x", error);
        glDeleteTextures(1, texture);
        *texture = 0;
        return false;
    }
    return true;
}

bool Gles2TerrainRenderer::initialize(const std::uint8_t* atlas_rgba, int atlas_width,
                                      int atlas_height, const std::uint8_t* lightmap_rgba,
                                      int lightmap_width, int lightmap_height, int sprite_size) {
    release();
    if (sprite_size <= 0 || atlas_width <= 0 || atlas_height <= 0 ||
        atlas_width % sprite_size != 0 || atlas_height % sprite_size != 0) {
        std::snprintf(error_, sizeof(error_), "Atlas dimensions must be positive multiples of sprite size");
        return false;
    }
    atlas_width_ = atlas_width;
    atlas_height_ = atlas_height;
    sprite_size_ = sprite_size;
    if (!create_programs()) {
        release();
        return false;
    }
    if (!upload_texture(&atlas_texture_, atlas_rgba, atlas_width, atlas_height, GL_NEAREST) ||
        !upload_texture(&lightmap_texture_, lightmap_rgba, lightmap_width, lightmap_height, GL_LINEAR)) {
        release();
        return false;
    }
    return true;
}

bool Gles2TerrainRenderer::upload_buffer(GLuint* buffer, GLsizei* vertex_count,
                                         const Gles2TerrainVertex* vertices, std::size_t count) {
    if ((count && !vertices) || count > static_cast<std::size_t>(std::numeric_limits<GLsizei>::max()) ||
        count > std::numeric_limits<std::size_t>::max() / sizeof(Gles2TerrainVertex)) {
        std::snprintf(error_, sizeof(error_), "Invalid terrain vertex buffer size");
        return false;
    }
    if (*buffer) glDeleteBuffers(1, buffer);
    *buffer = 0;
    *vertex_count = 0;
    if (count == 0) return true;
    glGenBuffers(1, buffer);
    glBindBuffer(GL_ARRAY_BUFFER, *buffer);
    glBufferData(GL_ARRAY_BUFFER, static_cast<GLsizeiptr>(count * sizeof(Gles2TerrainVertex)),
                 vertices, GL_STATIC_DRAW);
    glBindBuffer(GL_ARRAY_BUFFER, 0);
    const GLenum error = glGetError();
    if (error != GL_NO_ERROR) {
        std::snprintf(error_, sizeof(error_), "Terrain VBO upload failed: 0x%x", error);
        glDeleteBuffers(1, buffer);
        *buffer = 0;
        return false;
    }
    *vertex_count = static_cast<GLsizei>(count);
    return true;
}

bool Gles2TerrainRenderer::upload_solid(const Gles2TerrainVertex* vertices, std::size_t count) {
    return upload_buffer(&solid_vbo_, &solid_count_, vertices, count);
}

bool Gles2TerrainRenderer::upload_translucent(const Gles2TerrainVertex* vertices, std::size_t count) {
    return upload_buffer(&translucent_vbo_, &translucent_count_, vertices, count);
}

void Gles2TerrainRenderer::draw_buffer(GLuint program, GLuint buffer, GLsizei count,
                                       const float* mvp, const float section_origin[3],
                                       const float camera_position[3], const float fog_color[4],
                                       const float fog_range[2]) {
    if (!program || !buffer || count <= 0) return;
    glUseProgram(program);
    glUniformMatrix4fv(glGetUniformLocation(program, "u_mvp"), 1, GL_FALSE, mvp);
    glUniform3fv(glGetUniformLocation(program, "u_section_origin"), 1, section_origin);
    glUniform3fv(glGetUniformLocation(program, "u_camera_position"), 1, camera_position);
    glUniform4fv(glGetUniformLocation(program, "u_fog_color"), 1, fog_color);
    glUniform2fv(glGetUniformLocation(program, "u_fog_range"), 1, fog_range);
    glUniform2f(glGetUniformLocation(program, "u_atlas_size"),
                static_cast<float>(atlas_width_), static_cast<float>(atlas_height_));
    glUniform1f(glGetUniformLocation(program, "u_sprite_size"), static_cast<float>(sprite_size_));
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, atlas_texture_);
    glUniform1i(glGetUniformLocation(program, "u_terrain_atlas"), 0);
    glActiveTexture(GL_TEXTURE1);
    glBindTexture(GL_TEXTURE_2D, lightmap_texture_);
    glUniform1i(glGetUniformLocation(program, "u_lightmap"), 1);

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
    glDrawArrays(GL_TRIANGLES, 0, count);
    for (GLuint index = 0; index <= 7; ++index) glDisableVertexAttribArray(index);
    glBindBuffer(GL_ARRAY_BUFFER, 0);
}

void Gles2TerrainRenderer::draw(const float* mvp, const float section_origin[3],
                                const float camera_position[3], const float fog_color[4],
                                const float fog_range[2]) {
    if (!solid_program_ || !translucent_program_) return;
    glEnable(GL_DEPTH_TEST);
    glDepthFunc(GL_LEQUAL);
    glDepthMask(GL_TRUE);
    glDisable(GL_BLEND);
    draw_buffer(solid_program_, solid_vbo_, solid_count_, mvp, section_origin,
                camera_position, fog_color, fog_range);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    glDepthMask(GL_FALSE);
    draw_buffer(translucent_program_, translucent_vbo_, translucent_count_, mvp,
                section_origin, camera_position, fog_color, fog_range);
    glDepthMask(GL_TRUE);
    glDisable(GL_BLEND);
}

void Gles2TerrainRenderer::release() {
    if (solid_vbo_) glDeleteBuffers(1, &solid_vbo_);
    if (translucent_vbo_) glDeleteBuffers(1, &translucent_vbo_);
    if (atlas_texture_) glDeleteTextures(1, &atlas_texture_);
    if (lightmap_texture_) glDeleteTextures(1, &lightmap_texture_);
    if (solid_program_) glDeleteProgram(solid_program_);
    if (translucent_program_) glDeleteProgram(translucent_program_);
    solid_vbo_ = translucent_vbo_ = atlas_texture_ = lightmap_texture_ = 0;
    solid_program_ = translucent_program_ = 0;
    solid_count_ = translucent_count_ = 0;
    atlas_width_ = atlas_height_ = sprite_size_ = 0;
}

}  // namespace pavo

#version 100
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
varying mediump float v_opaque;
varying highp float v_distance;

void main() {
    vec2 local_uv = v_uv - max(ceil(v_uv) - 1.0, vec2(0.0));
    vec2 half_texel = 0.5 / u_atlas_size;
    vec2 tile_uv = (local_uv * max(u_sprite_size - 1.0, 0.0) + 0.5) / u_atlas_size;
    vec2 atlas_uv = v_atlas_origin + tile_uv;
    atlas_uv = clamp(
        atlas_uv,
        v_atlas_origin + half_texel,
        v_atlas_origin + v_atlas_scale - half_texel
    );

    vec4 texel = texture2D(u_terrain_atlas, atlas_uv);
    vec2 light_uv = clamp(
        vec2(v_light.y, v_light.x) / 256.0 + vec2(0.5 / 16.0),
        vec2(0.5 / 16.0),
        vec2(15.5 / 16.0)
    );
    vec3 light = texture2D(u_lightmap, light_uv).rgb;
    vec4 color = vec4(texel.rgb * v_color.rgb * light, texel.a * v_color.a);
    if (color.a < 0.01) {
        discard;
    }

    float fog_span = max(u_fog_range.y - u_fog_range.x, 0.0001);
    float fog = clamp((v_distance - u_fog_range.x) / fog_span, 0.0, 1.0);
    color.rgb = mix(color.rgb, u_fog_color.rgb, fog * u_fog_color.a);
    gl_FragColor = color;
}

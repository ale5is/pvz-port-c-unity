#version 100
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

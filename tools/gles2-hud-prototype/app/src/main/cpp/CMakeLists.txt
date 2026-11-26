cmake_minimum_required(VERSION 3.22.1)
project(pavo_gles2_native LANGUAGES C CXX)

add_library(
    pavo_gles2_native SHARED
    native_hud.cpp
    gles2_terrain_renderer.cpp
    horizon_terrain_adapter.cpp
    horizon_gles2_bridge.cpp
    horizon_egl_owner.cpp
)

find_library(android-lib android)
find_library(log-lib log)
find_library(egl-lib EGL)
find_library(glesv2-lib GLESv2)

target_link_libraries(
    pavo_gles2_native
    ${android-lib}
    ${log-lib}
    ${egl-lib}
    ${glesv2-lib}
)

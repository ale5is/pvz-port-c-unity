#include <android/native_activity.h>
#include <android/native_window.h>
#include <android/log.h>
#include <EGL/egl.h>
#include <GLES2/gl2.h>
#include "gles2_terrain_renderer.h"

#include <array>
#include <atomic>
#include <cstdint>
#include <chrono>
#include <cstddef>
#include <thread>

#define LOG_TAG "PavoGLES2Native"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

struct Engine {
    ANativeWindow* window = nullptr;
    std::thread render_thread;
    std::atomic<bool> stop{false};
    std::atomic<bool> paused{true};
    std::atomic<bool> rendering{false};
};

static std::array<pavo::Gles2TerrainVertex, 24> create_terrain_vertices() {
    std::array<pavo::Gles2TerrainVertex, 24> vertices{};
    const float quads[4][6][3] = {
        {{-0.88f, 0.82f, 0.0f}, {-0.06f, 0.82f, 0.0f}, {-0.88f, 0.06f, 0.0f},
         {-0.06f, 0.82f, 0.0f}, {-0.06f, 0.06f, 0.0f}, {-0.88f, 0.06f, 0.0f}},
        {{0.06f, 0.82f, 0.0f}, {0.88f, 0.82f, 0.0f}, {0.06f, 0.06f, 0.0f},
         {0.88f, 0.82f, 0.0f}, {0.88f, 0.06f, 0.0f}, {0.06f, 0.06f, 0.0f}},
        {{-0.88f, -0.06f, 0.0f}, {-0.06f, -0.06f, 0.0f}, {-0.88f, -0.82f, 0.0f},
         {-0.06f, -0.06f, 0.0f}, {-0.06f, -0.82f, 0.0f}, {-0.88f, -0.82f, 0.0f}},
        {{0.06f, -0.06f, 0.0f}, {0.88f, -0.06f, 0.0f}, {0.06f, -0.82f, 0.0f},
         {0.88f, -0.06f, 0.0f}, {0.88f, -0.82f, 0.0f}, {0.06f, -0.82f, 0.0f}}
    };
    const float atlas_origins[4][2] = {
        {0.0f, 0.0f}, {0.5f, 0.0f}, {0.0f, 0.5f}, {0.5f, 0.5f}
    };
    for (std::size_t quad = 0; quad < 4; ++quad) {
        for (std::size_t vertex = 0; vertex < 6; ++vertex) {
            auto& out = vertices[quad * 6 + vertex];
            for (int axis = 0; axis < 3; ++axis) out.position[axis] = quads[quad][vertex][axis];
            out.uv[0] = vertex == 1 || vertex == 3 || vertex == 4 ? 1.0f : 0.0f;
            out.uv[1] = vertex == 2 || vertex == 4 || vertex == 5 ? 1.0f : 0.0f;
            out.atlas_origin[0] = atlas_origins[quad][0];
            out.atlas_origin[1] = atlas_origins[quad][1];
            out.atlas_scale[0] = 0.5f;
            out.atlas_scale[1] = 0.5f;
            out.color[0] = 255;
            out.color[1] = 255;
            out.color[2] = 255;
            out.color[3] = 255;
            out.light[0] = 0;
            out.light[1] = 0;
            out.opaque_layer = 1.0f;
        }
    }
    return vertices;
}

static std::array<std::uint8_t, 16> create_test_atlas() {
    return {{
        255, 0, 0, 255, 0, 255, 0, 255,
        0, 0, 255, 255, 255, 255, 255, 255
    }};
}

static std::array<std::uint8_t, 16 * 16 * 4> create_test_lightmap() {
    std::array<std::uint8_t, 16 * 16 * 4> pixels{};
    pixels.fill(255);
    return pixels;
}

static bool create_egl(ANativeWindow* window, EGLDisplay* out_display,
                       EGLSurface* out_surface, EGLContext* out_context) {
    EGLDisplay display = eglGetDisplay(EGL_DEFAULT_DISPLAY);
    if (display == EGL_NO_DISPLAY) {
        LOGE("eglGetDisplay failed: 0x%x", eglGetError());
        return false;
    }

    EGLint major = 0;
    EGLint minor = 0;
    if (eglInitialize(display, &major, &minor) != EGL_TRUE) {
        LOGE("eglInitialize failed: 0x%x", eglGetError());
        return false;
    }

    const EGLint config_attributes[] = {
        EGL_SURFACE_TYPE, EGL_WINDOW_BIT,
        EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT,
        EGL_RED_SIZE, 8,
        EGL_GREEN_SIZE, 8,
        EGL_BLUE_SIZE, 8,
        EGL_ALPHA_SIZE, 8,
        EGL_DEPTH_SIZE, 16,
        EGL_STENCIL_SIZE, 0,
        EGL_NONE
    };

    EGLConfig config = nullptr;
    EGLint config_count = 0;
    if (eglChooseConfig(display, config_attributes, &config, 1, &config_count) != EGL_TRUE
        || config_count < 1) {
        LOGE("eglChooseConfig failed: 0x%x", eglGetError());
        eglTerminate(display);
        return false;
    }

    EGLint native_format = 0;
    eglGetConfigAttrib(display, config, EGL_NATIVE_VISUAL_ID, &native_format);
    if (ANativeWindow_setBuffersGeometry(window, 0, 0, native_format) != 0) {
        LOGE("ANativeWindow_setBuffersGeometry failed");
        eglTerminate(display);
        return false;
    }

    EGLSurface surface = eglCreateWindowSurface(display, config, window, nullptr);
    if (surface == EGL_NO_SURFACE) {
        LOGE("eglCreateWindowSurface failed: 0x%x", eglGetError());
        eglTerminate(display);
        return false;
    }

    const EGLint context_attributes[] = {
        EGL_CONTEXT_CLIENT_VERSION, 2,
        EGL_NONE
    };
    EGLContext context = eglCreateContext(display, config, EGL_NO_CONTEXT, context_attributes);
    if (context == EGL_NO_CONTEXT) {
        LOGE("eglCreateContext GLES2 failed: 0x%x", eglGetError());
        eglDestroySurface(display, surface);
        eglTerminate(display);
        return false;
    }

    if (eglMakeCurrent(display, surface, surface, context) != EGL_TRUE) {
        LOGE("eglMakeCurrent failed: 0x%x", eglGetError());
        eglDestroyContext(display, context);
        eglDestroySurface(display, surface);
        eglTerminate(display);
        return false;
    }

    if (eglSwapInterval(display, 1) != EGL_TRUE) {
        LOGE("eglSwapInterval failed: 0x%x", eglGetError());
    }

    *out_display = display;
    *out_surface = surface;
    *out_context = context;
    LOGI("EGL %d.%d, GLES2 context created, surface %dx%d",
         major, minor, ANativeWindow_getWidth(window), ANativeWindow_getHeight(window));
    return true;
}

static void destroy_egl(EGLDisplay display, EGLSurface surface, EGLContext context) {
    if (display != EGL_NO_DISPLAY) {
        eglMakeCurrent(display, EGL_NO_SURFACE, EGL_NO_SURFACE, EGL_NO_CONTEXT);
        if (surface != EGL_NO_SURFACE) eglDestroySurface(display, surface);
        if (context != EGL_NO_CONTEXT) eglDestroyContext(display, context);
        eglTerminate(display);
    }
}

static void render_loop(Engine* engine, ANativeWindow* window) {
    EGLDisplay display = EGL_NO_DISPLAY;
    EGLSurface surface = EGL_NO_SURFACE;
    EGLContext context = EGL_NO_CONTEXT;

    if (!create_egl(window, &display, &surface, &context)) return;

    const char* renderer_name = reinterpret_cast<const char*>(glGetString(GL_RENDERER));
    const char* version = reinterpret_cast<const char*>(glGetString(GL_VERSION));
    LOGI("GL_RENDERER=%s GL_VERSION=%s", renderer_name ? renderer_name : "unknown",
         version ? version : "unknown");

    pavo::Gles2TerrainRenderer renderer;
    const auto atlas = create_test_atlas();
    const auto lightmap = create_test_lightmap();
    const auto vertices = create_terrain_vertices();
    const bool initialized = renderer.initialize(atlas.data(), 2, 2, lightmap.data(), 16, 16, 1);
    const bool uploaded = initialized && renderer.upload_solid(vertices.data(), vertices.size());
    if (!uploaded) {
        LOGE("GLES2 terrain renderer setup failed: %s", renderer.last_error());
        renderer.release();
        destroy_egl(display, surface, context);
        return;
    }

    const float mvp[16] = {
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    };
    const float section_origin[3] = {0.0f, 0.0f, 0.0f};
    const float camera_position[3] = {0.0f, 0.0f, 2.0f};
    const float fog_color[4] = {0.055f, 0.075f, 0.11f, 0.0f};
    const float fog_range[2] = {1.0f, 2.0f};

    glDisable(GL_CULL_FACE);
    int width = 0;
    int height = 0;
    if (eglQuerySurface(display, surface, EGL_WIDTH, &width) != EGL_TRUE ||
        eglQuerySurface(display, surface, EGL_HEIGHT, &height) != EGL_TRUE ||
        width <= 0 || height <= 0) {
        LOGE("Could not query a valid EGL window size: 0x%x", eglGetError());
        renderer.release();
        destroy_egl(display, surface, context);
        return;
    }
    glClearDepthf(1.0f);
    unsigned long long frames = 0;

    while (!engine->stop.load()) {
        glViewport(0, 0, width, height);
        glClearColor(0.055f, 0.075f, 0.11f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        renderer.draw(mvp, section_origin, camera_position, fog_color, fog_range);

        const GLenum error = glGetError();
        if (error != GL_NO_ERROR) {
            LOGE("GLES error 0x%x after frame %llu", error, frames);
            break;
        }
        if (eglSwapBuffers(display, surface) != EGL_TRUE) {
            LOGE("eglSwapBuffers failed: 0x%x", eglGetError());
            break;
        }

        ++frames;
        if (frames == 1 || frames % 300 == 0) {
            LOGI("Presented %llu terrain-renderer frames at %dx%d", frames, width, height);
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(16));
    }

    LOGI("Stopping GLES2 terrain render loop after %llu frames", frames);
    renderer.release();
    destroy_egl(display, surface, context);
}

static void stop_renderer(Engine* engine) {
    engine->stop.store(true);
    if (engine->render_thread.joinable()) engine->render_thread.join();
    engine->rendering.store(false);
}

static void start_renderer(Engine* engine) {
    if (engine->paused.load() || engine->window == nullptr) return;
    if (engine->render_thread.joinable()) {
        if (engine->rendering.load()) return;
        engine->render_thread.join();
    }
    engine->stop.store(false);
    engine->rendering.store(true);
    ANativeWindow_acquire(engine->window);
    ANativeWindow* window = engine->window;
    engine->render_thread = std::thread([engine, window]() {
        render_loop(engine, window);
        ANativeWindow_release(window);
        engine->rendering.store(false);
    });
}

static void on_window_created(ANativeActivity* activity, ANativeWindow* window) {
    auto* engine = static_cast<Engine*>(activity->instance);
    if (!engine) return;
    stop_renderer(engine);
    if (engine->window) ANativeWindow_release(engine->window);
    ANativeWindow_acquire(window);
    engine->window = window;
    LOGI("Native window created: %dx%d", ANativeWindow_getWidth(window),
         ANativeWindow_getHeight(window));
    start_renderer(engine);
}

static void on_window_destroyed(ANativeActivity* activity, ANativeWindow* window) {
    auto* engine = static_cast<Engine*>(activity->instance);
    if (!engine || engine->window != window) return;
    stop_renderer(engine);
    if (engine->window) {
        ANativeWindow_release(engine->window);
        engine->window = nullptr;
    }
    LOGI("Native window destroyed; EGL thread stopped and joined");
}

static void on_pause(ANativeActivity* activity) {
    auto* engine = static_cast<Engine*>(activity->instance);
    if (!engine) return;
    engine->paused.store(true);
    stop_renderer(engine);
    LOGI("Activity paused; EGL resources released");
}

static void on_resume(ANativeActivity* activity) {
    auto* engine = static_cast<Engine*>(activity->instance);
    if (!engine) return;
    engine->paused.store(false);
    start_renderer(engine);
    LOGI("Activity resumed");
}

static void on_destroy(ANativeActivity* activity) {
    auto* engine = static_cast<Engine*>(activity->instance);
    if (!engine) return;
    engine->paused.store(true);
    stop_renderer(engine);
    if (engine->window) {
        ANativeWindow_release(engine->window);
        engine->window = nullptr;
    }
    delete engine;
    activity->instance = nullptr;
    LOGI("NativeActivity destroyed");
}

extern "C" void ANativeActivity_onCreate(ANativeActivity* activity, void*, size_t) {
    auto* engine = new Engine();
    activity->instance = engine;
    activity->callbacks->onNativeWindowCreated = on_window_created;
    activity->callbacks->onNativeWindowDestroyed = on_window_destroyed;
    activity->callbacks->onPause = on_pause;
    activity->callbacks->onResume = on_resume;
    activity->callbacks->onDestroy = on_destroy;
    LOGI("Pavo GLES2 NativeActivity initialized");
}

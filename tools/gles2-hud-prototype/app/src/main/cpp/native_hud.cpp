#include <android/native_activity.h>
#include <android/native_window.h>
#include <android/log.h>
#include <EGL/egl.h>
#include <GLES2/gl2.h>

#include <atomic>
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
};

static GLuint compile_shader(GLenum type, const char* source) {
    GLuint shader = glCreateShader(type);
    glShaderSource(shader, 1, &source, nullptr);
    glCompileShader(shader);
    GLint compiled = GL_FALSE;
    glGetShaderiv(shader, GL_COMPILE_STATUS, &compiled);
    if (compiled != GL_TRUE) {
        GLint length = 0;
        glGetShaderiv(shader, GL_INFO_LOG_LENGTH, &length);
        char log[2048] = {};
        glGetShaderInfoLog(shader, sizeof(log), nullptr, log);
        LOGE("Shader compile failed (%u): %s", type, log);
        glDeleteShader(shader);
        return 0;
    }
    return shader;
}

static GLuint create_program() {
    static const char* vertex_source =
        "attribute vec2 aPosition;\n"
        "attribute vec2 aUv;\n"
        "attribute vec4 aColor;\n"
        "varying vec2 vUv;\n"
        "varying vec4 vColor;\n"
        "void main(){gl_Position=vec4(aPosition,0.0,1.0);vUv=aUv;vColor=aColor;}";

    static const char* fragment_source =
        "precision mediump float;\n"
        "uniform sampler2D uAtlas;\n"
        "varying vec2 vUv;\n"
        "varying vec4 vColor;\n"
        "void main(){gl_FragColor=texture2D(uAtlas,vUv)*vColor;}";

    GLuint vertex = compile_shader(GL_VERTEX_SHADER, vertex_source);
    GLuint fragment = compile_shader(GL_FRAGMENT_SHADER, fragment_source);
    if (!vertex || !fragment) {
        if (vertex) glDeleteShader(vertex);
        if (fragment) glDeleteShader(fragment);
        return 0;
    }

    GLuint program = glCreateProgram();
    glAttachShader(program, vertex);
    glAttachShader(program, fragment);
    glLinkProgram(program);

    GLint linked = GL_FALSE;
    glGetProgramiv(program, GL_LINK_STATUS, &linked);
    if (linked != GL_TRUE) {
        char log[2048] = {};
        glGetProgramInfoLog(program, sizeof(log), nullptr, log);
        LOGE("Program link failed: %s", log);
        glDeleteProgram(program);
        program = 0;
    }

    glDeleteShader(vertex);
    glDeleteShader(fragment);
    return program;
}

static GLuint create_atlas() {
    const GLubyte pixels[] = {
        255, 0, 0, 255,       0, 255, 0, 255,
        0, 0, 255, 255,       255, 255, 255, 255
    };

    GLuint texture = 0;
    glGenTextures(1, &texture);
    glBindTexture(GL_TEXTURE_2D, texture);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, 2, 2, 0, GL_RGBA, GL_UNSIGNED_BYTE, pixels);
    return texture;
}

static GLuint create_vbo() {
    static const GLfloat vertices[] = {
        -0.88f,  0.82f, 0.25f, 0.25f, 1, 1, 1, 1,
        -0.06f,  0.82f, 0.25f, 0.25f, 1, 1, 1, 1,
        -0.88f,  0.06f, 0.25f, 0.25f, 1, 1, 1, 1,
        -0.06f,  0.82f, 0.25f, 0.25f, 1, 1, 1, 1,
        -0.06f,  0.06f, 0.25f, 0.25f, 1, 1, 1, 1,
        -0.88f,  0.06f, 0.25f, 0.25f, 1, 1, 1, 1,

         0.06f,  0.82f, 0.75f, 0.25f, 1, 1, 1, 1,
         0.88f,  0.82f, 0.75f, 0.25f, 1, 1, 1, 1,
         0.06f,  0.06f, 0.75f, 0.25f, 1, 1, 1, 1,
         0.88f,  0.82f, 0.75f, 0.25f, 1, 1, 1, 1,
         0.88f,  0.06f, 0.75f, 0.25f, 1, 1, 1, 1,
         0.06f,  0.06f, 0.75f, 0.25f, 1, 1, 1, 1,

        -0.88f, -0.06f, 0.25f, 0.75f, 1, 1, 1, 1,
        -0.06f, -0.06f, 0.25f, 0.75f, 1, 1, 1, 1,
        -0.88f, -0.82f, 0.25f, 0.75f, 1, 1, 1, 1,
        -0.06f, -0.06f, 0.25f, 0.75f, 1, 1, 1, 1,
        -0.06f, -0.82f, 0.25f, 0.75f, 1, 1, 1, 1,
        -0.88f, -0.82f, 0.25f, 0.75f, 1, 1, 1, 1,

         0.06f, -0.06f, 0.75f, 0.75f, 1, 1, 1, 0.72f,
         0.88f, -0.06f, 0.75f, 0.75f, 1, 1, 1, 0.72f,
         0.06f, -0.82f, 0.75f, 0.75f, 1, 1, 1, 0.72f,
         0.88f, -0.06f, 0.75f, 0.75f, 1, 1, 1, 0.72f,
         0.88f, -0.82f, 0.75f, 0.75f, 1, 1, 1, 0.72f,
         0.06f, -0.82f, 0.75f, 0.75f, 1, 1, 1, 0.72f
    };

    GLuint buffer = 0;
    glGenBuffers(1, &buffer);
    glBindBuffer(GL_ARRAY_BUFFER, buffer);
    glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);
    glBindBuffer(GL_ARRAY_BUFFER, 0);
    return buffer;
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
        EGL_DEPTH_SIZE, 0,
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
    ANativeWindow_setBuffersGeometry(window, 0, 0, native_format);

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

    const char* renderer = reinterpret_cast<const char*>(glGetString(GL_RENDERER));
    const char* version = reinterpret_cast<const char*>(glGetString(GL_VERSION));
    LOGI("GL_RENDERER=%s GL_VERSION=%s", renderer ? renderer : "unknown",
         version ? version : "unknown");

    GLuint program = create_program();
    GLuint texture = create_atlas();
    GLuint vbo = create_vbo();

    if (!program || !texture || !vbo) {
        LOGE("Failed to create HUD resources");
        if (vbo) glDeleteBuffers(1, &vbo);
        if (texture) glDeleteTextures(1, &texture);
        if (program) glDeleteProgram(program);
        destroy_egl(display, surface, context);
        return;
    }

    GLint position = glGetAttribLocation(program, "aPosition");
    GLint uv = glGetAttribLocation(program, "aUv");
    GLint color = glGetAttribLocation(program, "aColor");
    GLint atlas = glGetUniformLocation(program, "uAtlas");

    glDisable(GL_DEPTH_TEST);
    glDisable(GL_CULL_FACE);
    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

    int width = 0;
    int height = 0;
    eglQuerySurface(display, surface, EGL_WIDTH, &width);
    eglQuerySurface(display, surface, EGL_HEIGHT, &height);
    unsigned long long frames = 0;

    while (!engine->stop.load()) {
        glViewport(0, 0, width, height);
        glClearColor(0.055f, 0.075f, 0.11f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);

        glUseProgram(program);
        glActiveTexture(GL_TEXTURE0);
        glBindTexture(GL_TEXTURE_2D, texture);
        glUniform1i(atlas, 0);
        glBindBuffer(GL_ARRAY_BUFFER, vbo);

        const GLsizei stride = 8 * static_cast<GLsizei>(sizeof(GLfloat));
        glEnableVertexAttribArray(static_cast<GLuint>(position));
        glEnableVertexAttribArray(static_cast<GLuint>(uv));
        glEnableVertexAttribArray(static_cast<GLuint>(color));
        glVertexAttribPointer(static_cast<GLuint>(position), 2, GL_FLOAT, GL_FALSE, stride, nullptr);
        glVertexAttribPointer(static_cast<GLuint>(uv), 2, GL_FLOAT, GL_FALSE, stride,
                              reinterpret_cast<const void*>(2 * sizeof(GLfloat)));
        glVertexAttribPointer(static_cast<GLuint>(color), 4, GL_FLOAT, GL_FALSE, stride,
                              reinterpret_cast<const void*>(4 * sizeof(GLfloat)));

        glEnable(GL_SCISSOR_TEST);
        glScissor(0, 0, width, height);
        glDrawArrays(GL_TRIANGLES, 0, 24);
        glDisable(GL_SCISSOR_TEST);

        glDisableVertexAttribArray(static_cast<GLuint>(position));
        glDisableVertexAttribArray(static_cast<GLuint>(uv));
        glDisableVertexAttribArray(static_cast<GLuint>(color));
        glBindBuffer(GL_ARRAY_BUFFER, 0);

        GLenum error = glGetError();
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
            LOGI("Presented %llu frames at %dx%d", frames, width, height);
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(16));
    }

    LOGI("Stopping GLES2 render loop after %llu frames", frames);
    glDeleteBuffers(1, &vbo);
    glDeleteTextures(1, &texture);
    glDeleteProgram(program);
    destroy_egl(display, surface, context);
}

static void stop_renderer(Engine* engine) {
    engine->stop.store(true);
    if (engine->render_thread.joinable()) engine->render_thread.join();
}

static void start_renderer(Engine* engine) {
    if (engine->paused.load() || engine->window == nullptr || engine->render_thread.joinable()) {
        return;
    }
    engine->stop.store(false);
    ANativeWindow_acquire(engine->window);
    ANativeWindow* window = engine->window;
    engine->render_thread = std::thread([engine, window]() {
        render_loop(engine, window);
        ANativeWindow_release(window);
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

static void on_window_destroyed(ANativeActivity* activity, ANativeWindow*) {
    auto* engine = static_cast<Engine*>(activity->instance);
    if (!engine) return;
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

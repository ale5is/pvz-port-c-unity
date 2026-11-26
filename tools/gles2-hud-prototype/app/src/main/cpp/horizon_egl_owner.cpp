#include "horizon_egl_owner.h"

#include <android/native_window.h>
#include <EGL/egl.h>
#include <GLES2/gl2.h>

#include <new>
#include <string>
#include <thread>

struct PavoEglOwner {
    EGLDisplay display = EGL_NO_DISPLAY;
    EGLConfig config = nullptr;
    EGLSurface surface = EGL_NO_SURFACE;
    EGLContext context = EGL_NO_CONTEXT;
    ANativeWindow* window = nullptr;
    std::thread::id owner_thread;
    std::uint64_t context_generation = 1;
    std::string error;
};

namespace {
void fail(PavoEglOwner* owner, const char* operation) {
    if (owner) owner->error = std::string(operation) + " failed (EGL 0x" +
        [] (EGLint value) {
            constexpr char digits[] = "0123456789abcdef";
            std::string out(4, '0');
            for (int i = 3; i >= 0; --i) {
                out[static_cast<std::size_t>(i)] = digits[value & 15];
                value >>= 4;
            }
            return out;
        }(eglGetError()) + ")";
}

bool on_owner_thread(PavoEglOwner* owner) {
    if (!owner) return false;
    if (owner->owner_thread == std::this_thread::get_id()) return true;
    owner->error = "EGL operation called from a non-owner thread";
    return false;
}

void teardown(PavoEglOwner* owner) {
    if (!owner) return;
    if (owner->display != EGL_NO_DISPLAY) {
        eglMakeCurrent(owner->display, EGL_NO_SURFACE, EGL_NO_SURFACE, EGL_NO_CONTEXT);
        if (owner->surface != EGL_NO_SURFACE) eglDestroySurface(owner->display, owner->surface);
        if (owner->context != EGL_NO_CONTEXT) eglDestroyContext(owner->display, owner->context);
        eglTerminate(owner->display);
    }
    owner->surface = EGL_NO_SURFACE;
    owner->context = EGL_NO_CONTEXT;
    owner->display = EGL_NO_DISPLAY;
    if (owner->window) ANativeWindow_release(owner->window);
    owner->window = nullptr;
}
}

extern "C" PavoEglOwner* pavo_egl_create(void* native_window) {
    if (!native_window) return nullptr;
    PavoEglOwner* owner = nullptr;
    try {
        owner = new PavoEglOwner();
        owner->owner_thread = std::this_thread::get_id();
        owner->window = static_cast<ANativeWindow*>(native_window);
        ANativeWindow_acquire(owner->window);
        owner->display = eglGetDisplay(EGL_DEFAULT_DISPLAY);
        if (owner->display == EGL_NO_DISPLAY) { fail(owner, "eglGetDisplay"); teardown(owner); delete owner; return nullptr; }

        EGLint major = 0, minor = 0;
        if (eglInitialize(owner->display, &major, &minor) != EGL_TRUE) {
            fail(owner, "eglInitialize"); teardown(owner); delete owner; return nullptr;
        }
        const EGLint attributes[] = {
            EGL_SURFACE_TYPE, EGL_WINDOW_BIT,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES2_BIT,
            EGL_RED_SIZE, 8, EGL_GREEN_SIZE, 8, EGL_BLUE_SIZE, 8,
            EGL_ALPHA_SIZE, 8, EGL_DEPTH_SIZE, 16, EGL_NONE
        };
        EGLint count = 0;
        if (eglChooseConfig(owner->display, attributes, &owner->config, 1, &count) != EGL_TRUE || count != 1) {
            fail(owner, "eglChooseConfig"); teardown(owner); delete owner; return nullptr;
        }
        EGLint format = 0;
        if (eglGetConfigAttrib(owner->display, owner->config, EGL_NATIVE_VISUAL_ID, &format) != EGL_TRUE ||
            ANativeWindow_setBuffersGeometry(owner->window, 0, 0, format) != 0) {
            fail(owner, "ANativeWindow_setBuffersGeometry"); teardown(owner); delete owner; return nullptr;
        }
        owner->surface = eglCreateWindowSurface(owner->display, owner->config, owner->window, nullptr);
        if (owner->surface == EGL_NO_SURFACE) { fail(owner, "eglCreateWindowSurface"); teardown(owner); delete owner; return nullptr; }
        const EGLint context_attributes[] = {EGL_CONTEXT_CLIENT_VERSION, 2, EGL_NONE};
        owner->context = eglCreateContext(owner->display, owner->config, EGL_NO_CONTEXT, context_attributes);
        if (owner->context == EGL_NO_CONTEXT) { fail(owner, "eglCreateContext"); teardown(owner); delete owner; return nullptr; }
        if (eglMakeCurrent(owner->display, owner->surface, owner->surface, owner->context) != EGL_TRUE) {
            fail(owner, "eglMakeCurrent"); teardown(owner); delete owner; return nullptr;
        }
        eglSwapInterval(owner->display, 1);
        EGLint width = 0, height = 0;
        if (eglQuerySurface(owner->display, owner->surface, EGL_WIDTH, &width) != EGL_TRUE ||
            eglQuerySurface(owner->display, owner->surface, EGL_HEIGHT, &height) != EGL_TRUE ||
            width <= 0 || height <= 0) {
            fail(owner, "eglQuerySurface"); teardown(owner); delete owner; return nullptr;
        }
        glViewport(0, 0, width, height);
        owner->error.clear();
        return owner;
    } catch (...) {
        if (owner) { teardown(owner); delete owner; }
        return nullptr;
    }
}

extern "C" int pavo_egl_make_current(PavoEglOwner* owner) {
    if (!on_owner_thread(owner) || owner->display == EGL_NO_DISPLAY ||
        owner->surface == EGL_NO_SURFACE || owner->context == EGL_NO_CONTEXT) return 0;
    if (eglMakeCurrent(owner->display, owner->surface, owner->surface, owner->context) != EGL_TRUE) {
        fail(owner, "eglMakeCurrent"); return 0;
    }
    return 1;
}

extern "C" int pavo_egl_suspend(PavoEglOwner* owner) {
    if (!on_owner_thread(owner) || owner->display == EGL_NO_DISPLAY) return 0;
    eglMakeCurrent(owner->display, EGL_NO_SURFACE, EGL_NO_SURFACE, EGL_NO_CONTEXT);
    if (owner->surface != EGL_NO_SURFACE) {
        eglDestroySurface(owner->display, owner->surface);
        owner->surface = EGL_NO_SURFACE;
    }
    if (owner->window) {
        ANativeWindow_release(owner->window);
        owner->window = nullptr;
    }
    return 1;
}

extern "C" int pavo_egl_resume(PavoEglOwner* owner, void* native_window) {
    if (!on_owner_thread(owner) || !native_window || owner->display == EGL_NO_DISPLAY) return 0;
    if (owner->surface != EGL_NO_SURFACE || owner->window) {
        if (!pavo_egl_suspend(owner)) return 0;
    }
    owner->window = static_cast<ANativeWindow*>(native_window);
    ANativeWindow_acquire(owner->window);
    EGLint format = 0;
    if (eglGetConfigAttrib(owner->display, owner->config, EGL_NATIVE_VISUAL_ID, &format) != EGL_TRUE ||
        ANativeWindow_setBuffersGeometry(owner->window, 0, 0, format) != 0) {
        fail(owner, "ANativeWindow_setBuffersGeometry");
        ANativeWindow_release(owner->window); owner->window = nullptr; return 0;
    }
    owner->surface = eglCreateWindowSurface(owner->display, owner->config, owner->window, nullptr);
    if (owner->surface == EGL_NO_SURFACE) {
        fail(owner, "eglCreateWindowSurface");
        ANativeWindow_release(owner->window); owner->window = nullptr; return 0;
    }
    if (eglMakeCurrent(owner->display, owner->surface, owner->surface, owner->context) != EGL_TRUE) {
        const EGLint error = eglGetError();
        if (error == EGL_CONTEXT_LOST) {
            if (owner->context != EGL_NO_CONTEXT) eglDestroyContext(owner->display, owner->context);
            const EGLint context_attributes[] = {EGL_CONTEXT_CLIENT_VERSION, 2, EGL_NONE};
            owner->context = eglCreateContext(owner->display, owner->config, EGL_NO_CONTEXT, context_attributes);
            if (owner->context != EGL_NO_CONTEXT) ++owner->context_generation;
            if (owner->context == EGL_NO_CONTEXT ||
                eglMakeCurrent(owner->display, owner->surface, owner->surface, owner->context) != EGL_TRUE) {
                fail(owner, "recreate GLES2 context");
                if (owner->context != EGL_NO_CONTEXT) eglDestroyContext(owner->display, owner->context);
                owner->context = EGL_NO_CONTEXT;
                eglDestroySurface(owner->display, owner->surface); owner->surface = EGL_NO_SURFACE;
                ANativeWindow_release(owner->window); owner->window = nullptr;
                return 0;
            }
        } else {
            owner->error = "eglMakeCurrent during resume failed";
            if (owner->surface != EGL_NO_SURFACE) eglDestroySurface(owner->display, owner->surface);
            owner->surface = EGL_NO_SURFACE;
            ANativeWindow_release(owner->window); owner->window = nullptr;
            return 0;
        }
    }
    EGLint width = 0, height = 0;
    if (eglQuerySurface(owner->display, owner->surface, EGL_WIDTH, &width) != EGL_TRUE ||
        eglQuerySurface(owner->display, owner->surface, EGL_HEIGHT, &height) != EGL_TRUE ||
        width <= 0 || height <= 0) {
        fail(owner, "eglQuerySurface after resume"); return 0;
    }
    glViewport(0, 0, width, height);
    owner->error.clear();
    return 1;
}

extern "C" int pavo_egl_resize(PavoEglOwner* owner, int* width, int* height) {
    if (!on_owner_thread(owner) || !width || !height || !pavo_egl_make_current(owner)) return 0;
    EGLint w = 0, h = 0;
    if (eglQuerySurface(owner->display, owner->surface, EGL_WIDTH, &w) != EGL_TRUE ||
        eglQuerySurface(owner->display, owner->surface, EGL_HEIGHT, &h) != EGL_TRUE || w <= 0 || h <= 0) {
        fail(owner, "eglQuerySurface"); return 0;
    }
    *width = w; *height = h;
    glViewport(0, 0, w, h);
    return 1;
}

extern "C" int pavo_egl_begin_frame(PavoEglOwner* owner, float red, float green, float blue, float alpha) {
    if (!on_owner_thread(owner) || !pavo_egl_make_current(owner)) return 0;
    EGLint width = 0, height = 0;
    if (eglQuerySurface(owner->display, owner->surface, EGL_WIDTH, &width) != EGL_TRUE ||
        eglQuerySurface(owner->display, owner->surface, EGL_HEIGHT, &height) != EGL_TRUE ||
        width <= 0 || height <= 0) {
        fail(owner, "eglQuerySurface"); return 0;
    }
    glViewport(0, 0, width, height);
    glClearColor(red, green, blue, alpha);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
    return 1;
}

extern "C" int pavo_egl_swap(PavoEglOwner* owner) {
    if (!on_owner_thread(owner) || !pavo_egl_make_current(owner)) return 0;
    if (eglSwapBuffers(owner->display, owner->surface) != EGL_TRUE) {
        fail(owner, "eglSwapBuffers"); return 0;
    }
    return 1;
}

extern "C" void pavo_egl_destroy(PavoEglOwner* owner) {
    if (!owner || !on_owner_thread(owner)) return;
    teardown(owner);
    delete owner;
}

extern "C" const char* pavo_egl_last_error(const PavoEglOwner* owner) {
    return owner ? owner->error.c_str() : "EGL owner is null";
}

package dev.pavo.glesprobe;

import android.app.Activity;
import android.opengl.EGL14;
import android.opengl.EGLConfig;
import android.opengl.EGLContext;
import android.opengl.EGLDisplay;
import android.opengl.EGLSurface;
import android.opengl.GLES20;
import android.os.Bundle;
import android.util.Log;
import android.view.Gravity;
import android.widget.ScrollView;
import android.widget.TextView;

import java.util.Locale;

public final class MainActivity extends Activity {
    private static final String TAG = "PavoGlesProbe";
    private TextView output;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        output = new TextView(this);
        output.setTextSize(14);
        output.setTextIsSelectable(true);
        output.setGravity(Gravity.START);
        output.setPadding(18, 18, 18, 18);
        output.setText("Pavo GLES capability probe\nConsultando EGL y OpenGL ES...");
        ScrollView scroll = new ScrollView(this);
        scroll.addView(output);
        setContentView(scroll);
        Thread probe = new Thread(() -> {
            String report = runProbe();
            Log.i(TAG, report);
            runOnUiThread(() -> output.setText(report));
        }, "PavoGlesProbe");
        probe.start();
    }

    private String runProbe() {
        StringBuilder report = new StringBuilder();
        report.append("PAVO GLES CAPABILITY PROBE\n");
        report.append("Android API: ").append(android.os.Build.VERSION.SDK_INT).append("\n");
        report.append("Device: ").append(android.os.Build.MANUFACTURER).append(" ")
                .append(android.os.Build.MODEL).append("\n\n");
        EGLDisplay display = EGL14.eglGetDisplay(EGL14.EGL_DEFAULT_DISPLAY);
        if (display == EGL14.EGL_NO_DISPLAY) {
            report.append("ERROR: eglGetDisplay: ").append(error()).append("\n");
            return report.toString();
        }
        int[] eglVersion = new int[2];
        if (!EGL14.eglInitialize(display, eglVersion, 0, eglVersion, 1)) {
            report.append("ERROR: eglInitialize: ").append(error()).append("\n");
            return report.toString();
        }
        report.append("EGL_VERSION: ").append(EGL14.eglQueryString(display, EGL14.EGL_VERSION)).append("\n");
        report.append("EGL_VENDOR: ").append(EGL14.eglQueryString(display, EGL14.EGL_VENDOR)).append("\n");
        report.append("EGL client version: ").append(eglVersion[0]).append(".").append(eglVersion[1]).append("\n");
        report.append("EGL_EXTENSIONS: ").append(EGL14.eglQueryString(display, EGL14.EGL_EXTENSIONS)).append("\n\n");
        probeContext(display, 3, report);
        report.append("\n");
        probeContext(display, 2, report);
        EGL14.eglTerminate(display);
        return report.toString();
    }

    private void probeContext(EGLDisplay display, int requestedVersion, StringBuilder report) {
        report.append("===== REQUESTED OPENGL ES ").append(requestedVersion).append(".x =====\n");
        int renderableBit = requestedVersion >= 3 ? 0x0040 : EGL14.EGL_OPENGL_ES2_BIT;
        int[] configAttributes = {
                EGL14.EGL_SURFACE_TYPE, EGL14.EGL_PBUFFER_BIT,
                EGL14.EGL_RENDERABLE_TYPE, renderableBit,
                EGL14.EGL_RED_SIZE, 8, EGL14.EGL_GREEN_SIZE, 8,
                EGL14.EGL_BLUE_SIZE, 8, EGL14.EGL_ALPHA_SIZE, 8, EGL14.EGL_NONE
        };
        EGLConfig[] configs = new EGLConfig[1];
        int[] count = new int[1];
        if (!EGL14.eglChooseConfig(display, configAttributes, 0, configs, 0, configs.length, count, 0)
                || count[0] == 0) {
            report.append("UNAVAILABLE: eglChooseConfig failed: ").append(error()).append("\n");
            return;
        }
        int[] surfaceAttributes = {EGL14.EGL_WIDTH, 16, EGL14.EGL_HEIGHT, 16, EGL14.EGL_NONE};
        EGLSurface surface = EGL14.eglCreatePbufferSurface(display, configs[0], surfaceAttributes, 0);
        if (surface == EGL14.EGL_NO_SURFACE) {
            report.append("UNAVAILABLE: eglCreatePbufferSurface failed: ").append(error()).append("\n");
            return;
        }
        int[] contextAttributes = {
                EGL14.EGL_CONTEXT_CLIENT_VERSION, requestedVersion, EGL14.EGL_NONE
        };
        EGLContext context = EGL14.eglCreateContext(display, configs[0], EGL14.EGL_NO_CONTEXT,
                contextAttributes, 0);
        if (context == EGL14.EGL_NO_CONTEXT) {
            report.append("UNAVAILABLE: eglCreateContext failed: ").append(error()).append("\n");
            EGL14.eglDestroySurface(display, surface);
            return;
        }
        if (!EGL14.eglMakeCurrent(display, surface, surface, context)) {
            report.append("UNAVAILABLE: eglMakeCurrent failed: ").append(error()).append("\n");
            EGL14.eglDestroyContext(display, context);
            EGL14.eglDestroySurface(display, surface);
            return;
        }
        report.append("GL_VERSION: ").append(glString(GLES20.GL_VERSION)).append("\n");
        report.append("GL_VENDOR: ").append(glString(GLES20.GL_VENDOR)).append("\n");
        report.append("GL_RENDERER: ").append(glString(GLES20.GL_RENDERER)).append("\n");
        report.append("GLSL_VERSION: ").append(glString(0x8B8C)).append("\n");
        report.append("GL_EXTENSIONS: ").append(glString(GLES20.GL_EXTENSIONS)).append("\n");
        appendLimit(report, "GL_MAX_TEXTURE_SIZE", GLES20.GL_MAX_TEXTURE_SIZE);
        appendLimit(report, "GL_MAX_CUBE_MAP_TEXTURE_SIZE", GLES20.GL_MAX_CUBE_MAP_TEXTURE_SIZE);
        appendLimit(report, "GL_MAX_TEXTURE_IMAGE_UNITS", GLES20.GL_MAX_TEXTURE_IMAGE_UNITS);
        appendLimit(report, "GL_MAX_VERTEX_TEXTURE_IMAGE_UNITS", GLES20.GL_MAX_VERTEX_TEXTURE_IMAGE_UNITS);
        appendLimit(report, "GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS", GLES20.GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS);
        appendLimit(report, "GL_MAX_VERTEX_ATTRIBS", GLES20.GL_MAX_VERTEX_ATTRIBS);
        appendLimit(report, "GL_MAX_VARYING_VECTORS", GLES20.GL_MAX_VARYING_VECTORS);
        report.append("RGBA8 framebuffer: ").append(testRgbaFramebuffer() ? "complete" : "not complete").append("\n");
        report.append("Depth16 framebuffer attachment: ").append(testRenderbuffer(GLES20.GL_DEPTH_ATTACHMENT,
                GLES20.GL_DEPTH_COMPONENT16) ? "complete" : "not complete").append("\n");
        report.append("Stencil8 framebuffer attachment: ").append(testRenderbuffer(GLES20.GL_STENCIL_ATTACHMENT,
                GLES20.GL_STENCIL_INDEX8) ? "complete" : "not complete").append("\n");
        report.append("GLSL ES 1.00 basic shader link: ").append(testBasicShader() ? "success" : "failed").append("\n");
        report.append("RGBA 2D texture upload: ").append(testTextureUpload(GLES20.GL_RGBA) ? "success" : "failed").append("\n");
        report.append("RGB 2D texture upload: ").append(testTextureUpload(GLES20.GL_RGB) ? "success" : "failed").append("\n");
        EGL14.eglMakeCurrent(display, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_CONTEXT);
        EGL14.eglDestroyContext(display, context);
        EGL14.eglDestroySurface(display, surface);
    }

    private void appendLimit(StringBuilder report, String name, int parameter) {
        int[] value = new int[1];
        GLES20.glGetIntegerv(parameter, value, 0);
        report.append(name).append(": ").append(value[0]).append("\n");
    }

    private boolean testRgbaFramebuffer() {
        int[] texture = new int[1];
        int[] framebuffer = new int[1];
        GLES20.glGenTextures(1, texture, 0);
        GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture[0]);
        setNearestTextureParameters();
        GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 16, 16, 0,
                GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, null);
        GLES20.glGenFramebuffers(1, framebuffer, 0);
        GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, framebuffer[0]);
        GLES20.glFramebufferTexture2D(GLES20.GL_FRAMEBUFFER, GLES20.GL_COLOR_ATTACHMENT0,
                GLES20.GL_TEXTURE_2D, texture[0], 0);
        boolean complete = GLES20.glCheckFramebufferStatus(GLES20.GL_FRAMEBUFFER)
                == GLES20.GL_FRAMEBUFFER_COMPLETE && GLES20.glGetError() == GLES20.GL_NO_ERROR;
        GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, 0);
        GLES20.glDeleteFramebuffers(1, framebuffer, 0);
        GLES20.glDeleteTextures(1, texture, 0);
        return complete;
    }

    private boolean testRenderbuffer(int attachment, int format) {
        int[] texture = new int[1];
        int[] framebuffer = new int[1];
        int[] renderbuffer = new int[1];
        GLES20.glGenTextures(1, texture, 0);
        GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture[0]);
        setNearestTextureParameters();
        GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 16, 16, 0,
                GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, null);
        GLES20.glGenFramebuffers(1, framebuffer, 0);
        GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, framebuffer[0]);
        GLES20.glFramebufferTexture2D(GLES20.GL_FRAMEBUFFER, GLES20.GL_COLOR_ATTACHMENT0,
                GLES20.GL_TEXTURE_2D, texture[0], 0);
        GLES20.glGenRenderbuffers(1, renderbuffer, 0);
        GLES20.glBindRenderbuffer(GLES20.GL_RENDERBUFFER, renderbuffer[0]);
        GLES20.glRenderbufferStorage(GLES20.GL_RENDERBUFFER, format, 16, 16);
        GLES20.glFramebufferRenderbuffer(GLES20.GL_FRAMEBUFFER, attachment,
                GLES20.GL_RENDERBUFFER, renderbuffer[0]);
        boolean complete = GLES20.glCheckFramebufferStatus(GLES20.GL_FRAMEBUFFER)
                == GLES20.GL_FRAMEBUFFER_COMPLETE && GLES20.glGetError() == GLES20.GL_NO_ERROR;
        GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, 0);
        GLES20.glBindRenderbuffer(GLES20.GL_RENDERBUFFER, 0);
        GLES20.glDeleteRenderbuffers(1, renderbuffer, 0);
        GLES20.glDeleteFramebuffers(1, framebuffer, 0);
        GLES20.glDeleteTextures(1, texture, 0);
        return complete;
    }

    private boolean testTextureUpload(int format) {
        int[] texture = new int[1];
        byte[] pixels = new byte[16 * 16 * (format == GLES20.GL_RGBA ? 4 : 3)];
        GLES20.glGenTextures(1, texture, 0);
        GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture[0]);
        setNearestTextureParameters();
        java.nio.ByteBuffer data = java.nio.ByteBuffer.allocateDirect(pixels.length);
        data.put(pixels).position(0);
        GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, format, 16, 16, 0,
                format, GLES20.GL_UNSIGNED_BYTE, data);
        boolean success = GLES20.glGetError() == GLES20.GL_NO_ERROR;
        GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, 0);
        GLES20.glDeleteTextures(1, texture, 0);
        return success;
    }

    private void setNearestTextureParameters() {
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_NEAREST);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_NEAREST);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_S, GLES20.GL_CLAMP_TO_EDGE);
        GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_T, GLES20.GL_CLAMP_TO_EDGE);
    }

    private boolean testBasicShader() {
        String vertex = "attribute vec4 aPosition; attribute vec2 aUv; varying vec2 vUv; "
                + "void main(){ gl_Position=aPosition; vUv=aUv; }";
        String fragment = "precision mediump float; varying vec2 vUv; uniform sampler2D uTexture; "
                + "void main(){ gl_FragColor=texture2D(uTexture,vUv); }";
        int vs = compileShader(GLES20.GL_VERTEX_SHADER, vertex);
        int fs = compileShader(GLES20.GL_FRAGMENT_SHADER, fragment);
        if (vs == 0 || fs == 0) {
            if (vs != 0) GLES20.glDeleteShader(vs);
            if (fs != 0) GLES20.glDeleteShader(fs);
            return false;
        }
        int program = GLES20.glCreateProgram();
        GLES20.glAttachShader(program, vs);
        GLES20.glAttachShader(program, fs);
        GLES20.glBindAttribLocation(program, 0, "aPosition");
        GLES20.glBindAttribLocation(program, 1, "aUv");
        GLES20.glLinkProgram(program);
        int[] status = new int[1];
        GLES20.glGetProgramiv(program, GLES20.GL_LINK_STATUS, status, 0);
        GLES20.glDeleteProgram(program);
        GLES20.glDeleteShader(vs);
        GLES20.glDeleteShader(fs);
        return status[0] == GLES20.GL_TRUE && GLES20.glGetError() == GLES20.GL_NO_ERROR;
    }

    private int compileShader(int type, String source) {
        int shader = GLES20.glCreateShader(type);
        GLES20.glShaderSource(shader, source);
        GLES20.glCompileShader(shader);
        int[] status = new int[1];
        GLES20.glGetShaderiv(shader, GLES20.GL_COMPILE_STATUS, status, 0);
        if (status[0] != GLES20.GL_TRUE) {
            GLES20.glDeleteShader(shader);
            return 0;
        }
        return shader;
    }

    private String glString(int name) {
        String value = GLES20.glGetString(name);
        return value == null ? "(null)" : value;
    }

    private String error() {
        return String.format(Locale.ROOT, "0x%04X", EGL14.eglGetError());
    }
}

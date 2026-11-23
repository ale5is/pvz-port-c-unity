package dev.pavo.glesprobe;

import android.app.Activity;
import android.app.ActivityManager;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.os.Build;
import android.os.Bundle;
import android.os.Debug;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.view.Gravity;
import android.view.ViewGroup;
import android.graphics.Color;
import android.util.Log;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.nio.IntBuffer;
import java.util.Locale;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

public final class MainActivity extends Activity {
    private static final String TAG = "PavoGlesProbe";
    private SceneView glView;
    private TextView report;
    private String lastReport = "Esperando contexto OpenGL ES 2.0...";

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Color.rgb(16, 20, 28));

        glView = new SceneView(this);
        root.addView(glView, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(LinearLayout.HORIZONTAL);
        controls.setGravity(Gravity.CENTER_VERTICAL);
        controls.setPadding(8, 6, 8, 6);

        Button run = new Button(this);
        run.setText("Repetir pruebas");
        run.setOnClickListener(v -> glView.runTests());
        controls.addView(run, new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        Button copy = new Button(this);
        copy.setText("Copiar informe");
        copy.setOnClickListener(v -> {
            ClipboardManager clipboard = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            clipboard.setPrimaryClip(ClipData.newPlainText("Pavo GLES report", lastReport));
            report.setText(lastReport + "\n\nInforme copiado.");
        });
        controls.addView(copy, new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        root.addView(controls);

        ScrollView scroll = new ScrollView(this);
        report = new TextView(this);
        report.setTextColor(Color.WHITE);
        report.setTextSize(12);
        report.setPadding(12, 8, 12, 12);
        report.setText(lastReport);
        scroll.addView(report);
        root.addView(scroll, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 0.8f));

        setContentView(root);
    }

    private void publish(String value) {
        lastReport = value;
        runOnUiThread(() -> report.setText(value));
        Log.i(TAG, value);
    }

    @Override
    protected void onPause() {
        super.onPause();
        if (glView != null) glView.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (glView != null) glView.onResume();
    }

    private final class SceneView extends GLSurfaceView {
        private final ProbeRenderer renderer;

        SceneView(Context context) {
            super(context);
            setEGLContextClientVersion(2);
            setEGLConfigChooser(8, 8, 8, 8, 16, 8);
            renderer = new ProbeRenderer();
            setRenderer(renderer);
            setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        }

        void runTests() {
            queueEvent(renderer::runSuite);
        }
    }

    private final class ProbeRenderer implements GLSurfaceView.Renderer {
        private int program;
        private int positionLocation;
        private int colorLocation;
        private int width;
        private int height;
        private long frames;
        private long lastFrameLog;

        private static final String VERTEX =
                "attribute vec3 aPosition; void main(){ gl_Position=vec4(aPosition,1.0); }";
        private static final String FRAGMENT =
                "precision mediump float; uniform vec4 uColor; void main(){ gl_FragColor=uColor; }";

        @Override
        public void onSurfaceCreated(GL10 gl, EGLConfig config) {
            GLES20.glDisable(GLES20.GL_CULL_FACE);
            GLES20.glEnable(GLES20.GL_DEPTH_TEST);
            GLES20.glDepthFunc(GLES20.GL_LEQUAL);
            GLES20.glDepthMask(true);
            GLES20.glClearDepthf(1f);
            GLES20.glClearColor(0.035f, 0.055f, 0.09f, 1f);
            program = buildProgram(VERTEX, FRAGMENT);
            if (program != 0) {
                positionLocation = GLES20.glGetAttribLocation(program, "aPosition");
                colorLocation = GLES20.glGetUniformLocation(program, "uColor");
            }
            runSuite();
        }

        @Override
        public void onSurfaceChanged(GL10 gl, int w, int h) {
            width = w;
            height = h;
            GLES20.glViewport(0, 0, w, h);
        }

        @Override
        public void onDrawFrame(GL10 gl) {
            GLES20.glViewport(0, 0, width, height);
            GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);
            if (program == 0) return;

            GLES20.glUseProgram(program);
            // Blue background quad is farther away.
            drawQuad(-0.92f, -0.88f, 0.92f, 0.88f, 0.45f, 0.08f, 0.28f, 0.95f, 1f);
            // Green center is closer and must remain visible.
            drawQuad(-0.38f, -0.36f, 0.38f, 0.36f, -0.45f, 0.08f, 0.88f, 0.20f, 1f);

            frames++;
            long now = System.nanoTime();
            if (now - lastFrameLog > 5000000000L) {
                Log.i(TAG, "Scene active; frames=" + frames + " viewport=" + width + "x" + height);
                lastFrameLog = now;
            }
        }

        private void drawQuad(float left, float bottom, float right, float top, float z,
                              float r, float g, float b, float a) {
            float[] data = {
                    left, bottom, z, right, bottom, z,
                    left, top, z, right, top, z
            };
            FloatBuffer buffer = floats(data);
            GLES20.glUniform4f(colorLocation, r, g, b, a);
            GLES20.glEnableVertexAttribArray(positionLocation);
            GLES20.glVertexAttribPointer(positionLocation, 3, GLES20.GL_FLOAT, false, 0, buffer);
            GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP, 0, 4);
            GLES20.glDisableVertexAttribArray(positionLocation);
        }

        void runSuite() {
            StringBuilder out = new StringBuilder();
            out.append("PAVO GLES2 — INFORME DE DIAGNÓSTICO\n");
            out.append("Dispositivo: ").append(Build.MANUFACTURER).append(" ").append(Build.MODEL).append("\n");
            out.append("Android: ").append(Build.VERSION.RELEASE).append(" / API ").append(Build.VERSION.SDK_INT).append("\n");
            out.append("ABI: ").append(Build.CPU_ABI).append("\n");
            ActivityManager am = (ActivityManager) getSystemService(ACTIVITY_SERVICE);
            out.append("Memoria app (memoryClass): ").append(am.getMemoryClass()).append(" MB");
            if (Build.VERSION.SDK_INT >= 11) out.append(" / largeMemoryClass: ").append(am.getLargeMemoryClass()).append(" MB");
            out.append("\nHeap max: ").append(Runtime.getRuntime().maxMemory() / (1024 * 1024)).append(" MB");
            out.append(" / nativa PSS: ").append(Debug.getNativeHeapAllocatedSize() / (1024 * 1024)).append(" MB\n");

            String vendor = GLES20.glGetString(GLES20.GL_VENDOR);
            String renderer = GLES20.glGetString(GLES20.GL_RENDERER);
            String version = GLES20.glGetString(GLES20.GL_VERSION);
            String shading = GLES20.glGetString(GLES20.GL_SHADING_LANGUAGE_VERSION);
            out.append("GL_VENDOR: ").append(vendor).append("\n");
            out.append("GL_RENDERER: ").append(renderer).append("\n");
            out.append("GL_VERSION: ").append(version).append("\n");
            out.append("GLSL: ").append(shading).append("\n");

            int[] value = new int[1];
            GLES20.glGetIntegerv(GLES20.GL_MAX_TEXTURE_SIZE, value, 0);
            out.append("MAX_TEXTURE_SIZE: ").append(value[0]).append("\n");
            GLES20.glGetIntegerv(GLES20.GL_MAX_TEXTURE_IMAGE_UNITS, value, 0);
            out.append("Fragment texture units: ").append(value[0]).append("\n");
            GLES20.glGetIntegerv(GLES20.GL_MAX_VERTEX_ATTRIBS, value, 0);
            out.append("Vertex attributes: ").append(value[0]).append("\n");
            GLES20.glGetIntegerv(GLES20.GL_MAX_VARYING_VECTORS, value, 0);
            out.append("Varying vectors: ").append(value[0]).append("\n");
            GLES20.glGetIntegerv(GLES20.GL_MAX_VERTEX_TEXTURE_IMAGE_UNITS, value, 0);
            out.append("Vertex texture units: ").append(value[0]).append("\n");
            GLES20.glGetIntegerv(GLES20.GL_DEPTH_BITS, value, 0);
            int defaultDepthBits = value[0];
            out.append("Default framebuffer depth bits: ").append(defaultDepthBits).append("\n");

            String extensions = GLES20.glGetString(GLES20.GL_EXTENSIONS);
            out.append("Extensions: ").append(extensions == null ? "(no disponibles)" : extensions).append("\n\n");

            out.append(result("Shader GLSL ES 1.00 compile/link", testShader()));
            out.append(result("Textura RGBA 2x2 + upload", testTexture()));
            out.append(result("VBO + glBufferData", testVbo()));
            out.append(result("Framebuffer RGBA8 + Depth16", testFramebuffer()));
            DepthResult depth = testDepth();
            out.append(result("Depth test real (draw order inverso + readPixels)", depth.passed));
            out.append("Pixel central RGBA: ").append(depth.pixel).append("\n");
            out.append("Profundidad default: ").append(defaultDepthBits).append(" bits\n");
            out.append("\nESCENA EN PANTALLA: cuadrado azul exterior + centro verde uniforme.\n");
            out.append("El centro verde debe tapar el azul. No hay textura de cuadros en esta prueba.\n");
            out.append("Nota: estas pruebas validan capacidades GLES2; no prueban todavía el renderer completo de Horizon.\n");

            clearErrors();
            publish(out.toString());
        }

        private String result(String name, boolean pass) {
            return (pass ? "[PASS] " : "[FAIL] ") + name + "\n";
        }

        private boolean testShader() {
            int p = buildProgram(VERTEX, FRAGMENT);
            boolean ok = p != 0;
            if (p != 0) GLES20.glDeleteProgram(p);
            return ok && noError("shader");
        }

        private boolean testTexture() {
            clearErrors();
            int[] ids = new int[1];
            GLES20.glGenTextures(1, ids, 0);
            if (ids[0] == 0) return false;
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, ids[0]);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_NEAREST);
            ByteBuffer pixels = ByteBuffer.allocateDirect(16).order(ByteOrder.nativeOrder());
            pixels.put(new byte[] {(byte)255,0,0,(byte)255, 0,(byte)255,0,(byte)255,
                    0,0,(byte)255,(byte)255, (byte)255,(byte)255,(byte)255,(byte)255});
            pixels.position(0);
            GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 2, 2, 0,
                    GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, pixels);
            boolean ok = noError("texture");
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, 0);
            GLES20.glDeleteTextures(1, ids, 0);
            return ok;
        }

        private boolean testVbo() {
            clearErrors();
            int[] ids = new int[1];
            GLES20.glGenBuffers(1, ids, 0);
            if (ids[0] == 0) return false;
            GLES20.glBindBuffer(GLES20.GL_ARRAY_BUFFER, ids[0]);
            FloatBuffer data = floats(new float[] {0f,0f,0f, 1f,0f,0f, 0f,1f,0f});
            GLES20.glBufferData(GLES20.GL_ARRAY_BUFFER, data.remaining() * 4, data, GLES20.GL_STATIC_DRAW);
            boolean ok = noError("vbo");
            GLES20.glBindBuffer(GLES20.GL_ARRAY_BUFFER, 0);
            GLES20.glDeleteBuffers(1, ids, 0);
            return ok;
        }

        private boolean testFramebuffer() {
            clearErrors();
            int[] texture = new int[1];
            int[] framebuffer = new int[1];
            int[] depth = new int[1];
            GLES20.glGenTextures(1, texture, 0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture[0]);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 16, 16, 0,
                    GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, null);
            GLES20.glGenFramebuffers(1, framebuffer, 0);
            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, framebuffer[0]);
            GLES20.glFramebufferTexture2D(GLES20.GL_FRAMEBUFFER, GLES20.GL_COLOR_ATTACHMENT0,
                    GLES20.GL_TEXTURE_2D, texture[0], 0);
            GLES20.glGenRenderbuffers(1, depth, 0);
            GLES20.glBindRenderbuffer(GLES20.GL_RENDERBUFFER, depth[0]);
            GLES20.glRenderbufferStorage(GLES20.GL_RENDERBUFFER, GLES20.GL_DEPTH_COMPONENT16, 16, 16);
            GLES20.glFramebufferRenderbuffer(GLES20.GL_FRAMEBUFFER, GLES20.GL_DEPTH_ATTACHMENT,
                    GLES20.GL_RENDERBUFFER, depth[0]);
            int status = GLES20.glCheckFramebufferStatus(GLES20.GL_FRAMEBUFFER);
            boolean ok = status == GLES20.GL_FRAMEBUFFER_COMPLETE && noError("framebuffer");
            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, 0);
            GLES20.glBindRenderbuffer(GLES20.GL_RENDERBUFFER, 0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, 0);
            GLES20.glDeleteRenderbuffers(1, depth, 0);
            GLES20.glDeleteFramebuffers(1, framebuffer, 0);
            GLES20.glDeleteTextures(1, texture, 0);
            return ok;
        }

        private static final class DepthResult {
            final boolean passed;
            final String pixel;
            DepthResult(boolean passed, String pixel) { this.passed = passed; this.pixel = pixel; }
        }

        private DepthResult testDepth() {
            clearErrors();
            if (program == 0) return new DepthResult(false, "sin shader");
            int[] texture = new int[1];
            int[] framebuffer = new int[1];
            int[] depth = new int[1];
            GLES20.glGenTextures(1, texture, 0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture[0]);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 16, 16, 0,
                    GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, null);
            GLES20.glGenFramebuffers(1, framebuffer, 0);
            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, framebuffer[0]);
            GLES20.glFramebufferTexture2D(GLES20.GL_FRAMEBUFFER, GLES20.GL_COLOR_ATTACHMENT0,
                    GLES20.GL_TEXTURE_2D, texture[0], 0);
            GLES20.glGenRenderbuffers(1, depth, 0);
            GLES20.glBindRenderbuffer(GLES20.GL_RENDERBUFFER, depth[0]);
            GLES20.glRenderbufferStorage(GLES20.GL_RENDERBUFFER, GLES20.GL_DEPTH_COMPONENT16, 16, 16);
            GLES20.glFramebufferRenderbuffer(GLES20.GL_FRAMEBUFFER, GLES20.GL_DEPTH_ATTACHMENT,
                    GLES20.GL_RENDERBUFFER, depth[0]);
            int status = GLES20.glCheckFramebufferStatus(GLES20.GL_FRAMEBUFFER);
            if (status != GLES20.GL_FRAMEBUFFER_COMPLETE) {
                cleanupFbo(texture, framebuffer, depth);
                return new DepthResult(false, "FBO incomplete 0x" + Integer.toHexString(status));
            }

            GLES20.glViewport(0, 0, 16, 16);
            GLES20.glEnable(GLES20.GL_DEPTH_TEST);
            GLES20.glDepthMask(true);
            GLES20.glDepthFunc(GLES20.GL_LEQUAL);
            GLES20.glClearDepthf(1f);
            GLES20.glClearColor(0f, 0f, 0f, 1f);
            GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);
            GLES20.glUseProgram(program);
            // First draw the nearer green square, then the farther blue square.
            // With depth testing enabled, the later blue draw must fail at the center.
            drawTestQuad(-0.65f, -0.65f, 0.65f, 0.65f, -0.5f, 0f, 1f, 0f, 1f);
            drawTestQuad(-0.95f, -0.95f, 0.95f, 0.95f, 0.5f, 0f, 0f, 1f, 1f);
            ByteBuffer pixel = ByteBuffer.allocateDirect(4).order(ByteOrder.nativeOrder());
            GLES20.glReadPixels(8, 8, 1, 1, GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, pixel);
            int r = pixel.get(0) & 255;
            int g = pixel.get(1) & 255;
            int b = pixel.get(2) & 255;
            boolean ok = g > 180 && r < 80 && b < 80 && noError("depth readback");
            String rgba = String.format(Locale.US, "%d,%d,%d,%d", r, g, b, pixel.get(3) & 255);
            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, 0);
            GLES20.glViewport(0, 0, width, height);
            cleanupFbo(texture, framebuffer, depth);
            return new DepthResult(ok, rgba);
        }

        private void drawTestQuad(float left, float bottom, float right, float top, float z,
                                  float r, float g, float b, float a) {
            GLES20.glUniform4f(colorLocation, r, g, b, a);
            float[] data = {left,bottom,z, right,bottom,z, left,top,z, right,top,z};
            FloatBuffer buffer = floats(data);
            GLES20.glEnableVertexAttribArray(positionLocation);
            GLES20.glVertexAttribPointer(positionLocation, 3, GLES20.GL_FLOAT, false, 0, buffer);
            GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP, 0, 4);
            GLES20.glDisableVertexAttribArray(positionLocation);
        }

        private void cleanupFbo(int[] texture, int[] framebuffer, int[] depth) {
            GLES20.glBindFramebuffer(GLES20.GL_FRAMEBUFFER, 0);
            GLES20.glBindRenderbuffer(GLES20.GL_RENDERBUFFER, 0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, 0);
            if (depth[0] != 0) GLES20.glDeleteRenderbuffers(1, depth, 0);
            if (framebuffer[0] != 0) GLES20.glDeleteFramebuffers(1, framebuffer, 0);
            if (texture[0] != 0) GLES20.glDeleteTextures(1, texture, 0);
        }

        private int buildProgram(String vertexSource, String fragmentSource) {
            int vs = compile(GLES20.GL_VERTEX_SHADER, vertexSource);
            int fs = compile(GLES20.GL_FRAGMENT_SHADER, fragmentSource);
            if (vs == 0 || fs == 0) return 0;
            int p = GLES20.glCreateProgram();
            GLES20.glAttachShader(p, vs);
            GLES20.glAttachShader(p, fs);
            GLES20.glBindAttribLocation(p, 0, "aPosition");
            GLES20.glBindAttribLocation(p, 1, "uColor");
            GLES20.glLinkProgram(p);
            int[] status = new int[1];
            GLES20.glGetProgramiv(p, GLES20.GL_LINK_STATUS, status, 0);
            if (status[0] != GLES20.GL_TRUE) {
                Log.e(TAG, "Program link failed: " + GLES20.glGetProgramInfoLog(p));
                GLES20.glDeleteProgram(p);
                p = 0;
            }
            GLES20.glDeleteShader(vs);
            GLES20.glDeleteShader(fs);
            return p;
        }

        private int compile(int type, String source) {
            int shader = GLES20.glCreateShader(type);
            GLES20.glShaderSource(shader, source);
            GLES20.glCompileShader(shader);
            int[] status = new int[1];
            GLES20.glGetShaderiv(shader, GLES20.GL_COMPILE_STATUS, status, 0);
            if (status[0] != GLES20.GL_TRUE) {
                Log.e(TAG, "Shader compile failed: " + GLES20.glGetShaderInfoLog(shader));
                GLES20.glDeleteShader(shader);
                return 0;
            }
            return shader;
        }

        private boolean noError(String label) {
            int error = GLES20.glGetError();
            if (error != GLES20.GL_NO_ERROR) {
                Log.e(TAG, label + " GL error=0x" + Integer.toHexString(error));
                return false;
            }
            return true;
        }

        private void clearErrors() {
            while (GLES20.glGetError() != GLES20.GL_NO_ERROR) { }
        }

        private FloatBuffer floats(float[] values) {
            FloatBuffer buffer = ByteBuffer.allocateDirect(values.length * 4)
                    .order(ByteOrder.nativeOrder()).asFloatBuffer();
            buffer.put(values).position(0);
            return buffer;
        }
    }
}

package dev.pavo.gles2hud;

import android.app.Activity;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.util.Log;
import android.view.Gravity;
import android.widget.FrameLayout;
import android.widget.TextView;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.nio.ShortBuffer;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

public final class MainActivity extends Activity {
    private static final String TAG = "PavoGLES2HUD";
    private HudSurface surface;
    private TextView status;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        FrameLayout root = new FrameLayout(this);
        surface = new HudSurface(this);
        root.addView(surface, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));
        status = new TextView(this);
        status.setTextColor(0xffffffff);
        status.setTextSize(13);
        status.setPadding(18, 12, 18, 12);
        status.setBackgroundColor(0xb0000000);
        status.setGravity(Gravity.TOP | Gravity.START);
        status.setText("GLES2 surface starting…");
        FrameLayout.LayoutParams overlay = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                FrameLayout.LayoutParams.WRAP_CONTENT,
                Gravity.TOP | Gravity.START);
        root.addView(status, overlay);
        setContentView(root);
        surface.setStatusListener(message -> runOnUiThread(() -> status.setText(message)));
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (surface != null) surface.onResume();
    }

    @Override
    protected void onPause() {
        if (surface != null) surface.onPause();
        super.onPause();
    }

    private interface StatusListener {
        void onStatus(String message);
    }

    private static final class HudSurface extends GLSurfaceView {
        private StatusListener listener;

        HudSurface(Activity activity) {
            super(activity);
            setEGLContextClientVersion(2);
            setPreserveEGLContextOnPause(false);
            setRenderer(new HudRenderer(message -> {
                StatusListener current = listener;
                if (current != null) current.onStatus(message);
            }));
            setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        }

        void setStatusListener(StatusListener value) {
            listener = value;
        }
    }

    private static final class HudRenderer implements GLSurfaceView.Renderer {
        private static final String VERTEX =
                "attribute vec2 aPosition;\n" +
                "attribute vec2 aUv;\n" +
                "attribute vec4 aColor;\n" +
                "varying vec2 vUv;\n" +
                "varying vec4 vColor;\n" +
                "void main(){ gl_Position=vec4(aPosition,0.0,1.0); vUv=aUv; vColor=aColor; }";
        private static final String FRAGMENT =
                "precision mediump float;\n" +
                "uniform sampler2D uAtlas;\n" +
                "varying vec2 vUv;\n" +
                "varying vec4 vColor;\n" +
                "void main(){ gl_FragColor=texture2D(uAtlas,vUv)*vColor; }";

        private final StatusListener listener;
        private int program;
        private int texture;
        private int vertexBuffer;
        private int positionLocation;
        private int uvLocation;
        private int colorLocation;
        private int atlasLocation;
        private int width;
        private int height;
        private int frames;
        private long lastStatusTime;
        private String glInfo = "GLES2";

        HudRenderer(StatusListener listener) {
            this.listener = listener;
        }

        @Override
        public void onSurfaceCreated(GL10 ignored, EGLConfig config) {
            program = createProgram(VERTEX, FRAGMENT);
            positionLocation = GLES20.glGetAttribLocation(program, "aPosition");
            uvLocation = GLES20.glGetAttribLocation(program, "aUv");
            colorLocation = GLES20.glGetAttribLocation(program, "aColor");
            atlasLocation = GLES20.glGetUniformLocation(program, "uAtlas");
            texture = createAtlas();
            vertexBuffer = createVertexBuffer();
            GLES20.glDisable(GLES20.GL_DEPTH_TEST);
            GLES20.glDisable(GLES20.GL_CULL_FACE);
            GLES20.glEnable(GLES20.GL_BLEND);
            GLES20.glBlendFunc(GLES20.GL_SRC_ALPHA, GLES20.GL_ONE_MINUS_SRC_ALPHA);
            glInfo = "GL " + GLES20.glGetString(GLES20.GL_VERSION)
                    + "\n" + GLES20.glGetString(GLES20.GL_RENDERER)
                    + "\nContext/resources recreated";
            Log.i(TAG, glInfo);
            report("GLES2 context created\n" + glInfo);
        }

        @Override
        public void onSurfaceChanged(GL10 ignored, int w, int h) {
            width = Math.max(1, w);
            height = Math.max(1, h);
            GLES20.glViewport(0, 0, width, height);
            Log.i(TAG, "Surface changed: " + width + "x" + height);
            report(glInfo + "\nSurface: " + width + " x " + height);
        }

        @Override
        public void onDrawFrame(GL10 ignored) {
            GLES20.glViewport(0, 0, width, height);
            GLES20.glClearColor(0.055f, 0.075f, 0.11f, 1.0f);
            GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT);

            GLES20.glUseProgram(program);
            GLES20.glActiveTexture(GLES20.GL_TEXTURE0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture);
            GLES20.glUniform1i(atlasLocation, 0);
            GLES20.glBindBuffer(GLES20.GL_ARRAY_BUFFER, vertexBuffer);

            int stride = 8 * 4;
            GLES20.glEnableVertexAttribArray(positionLocation);
            GLES20.glEnableVertexAttribArray(uvLocation);
            GLES20.glEnableVertexAttribArray(colorLocation);
            GLES20.glVertexAttribPointer(positionLocation, 2, GLES20.GL_FLOAT, false, stride, 0);
            GLES20.glVertexAttribPointer(uvLocation, 2, GLES20.GL_FLOAT, false, stride, 8);
            GLES20.glVertexAttribPointer(colorLocation, 4, GLES20.GL_FLOAT, false, stride, 16);

            GLES20.glScissor(0, 0, width, height);
            GLES20.glEnable(GLES20.GL_SCISSOR_TEST);
            GLES20.glDrawArrays(GLES20.GL_TRIANGLES, 0, 18);
            GLES20.glDisable(GLES20.GL_SCISSOR_TEST);

            GLES20.glDisableVertexAttribArray(positionLocation);
            GLES20.glDisableVertexAttribArray(uvLocation);
            GLES20.glDisableVertexAttribArray(colorLocation);
            GLES20.glBindBuffer(GLES20.GL_ARRAY_BUFFER, 0);

            int error = GLES20.glGetError();
            if (error != GLES20.GL_NO_ERROR) {
                Log.e(TAG, "GLES error 0x" + Integer.toHexString(error));
                report(glInfo + "\nGLES ERROR: 0x" + Integer.toHexString(error));
            } else {
                frames++;
                long now = System.currentTimeMillis();
                if (now - lastStatusTime > 3000) {
                    lastStatusTime = now;
                    report(glInfo + "\nSurface: " + width + " x " + height
                            + "\nFrames rendered: " + frames
                            + "\nVBO: 18 vertices / 1 draw call"
                            + "\nBlend + scissor: enabled");
                }
            }
        }

        private int createAtlas() {
            byte[] pixels = new byte[] {
                    (byte)255, 0, 0, (byte)255,       0, (byte)255, 0, (byte)255,
                    0, 0, (byte)255, (byte)255,       (byte)255, (byte)255, (byte)255, (byte)255
            };
            ByteBuffer data = ByteBuffer.allocateDirect(pixels.length).order(ByteOrder.nativeOrder());
            data.put(pixels).position(0);
            int[] ids = new int[1];
            GLES20.glGenTextures(1, ids, 0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, ids[0]);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_S, GLES20.GL_CLAMP_TO_EDGE);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_T, GLES20.GL_CLAMP_TO_EDGE);
            GLES20.glPixelStorei(GLES20.GL_UNPACK_ALIGNMENT, 1);
            GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 2, 2, 0,
                    GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, data);
            return ids[0];
        }

        private int createVertexBuffer() {
            float[] vertices = new float[] {
                    -0.92f, 0.48f, 0.05f, 0.05f, 1, 1, 1, 0.88f,
                    -0.18f, 0.48f, 0.45f, 0.05f, 1, 1, 1, 0.88f,
                    -0.92f, -0.46f, 0.05f, 0.45f, 1, 1, 1, 0.88f,
                    -0.18f, 0.48f, 0.45f, 0.05f, 1, 1, 1, 0.88f,
                    -0.18f, -0.46f, 0.45f, 0.45f, 1, 1, 1, 0.88f,
                    -0.92f, -0.46f, 0.05f, 0.45f, 1, 1, 1, 0.88f,

                    -0.05f, 0.48f, 0.55f, 0.05f, 1, 1, 1, 1,
                    0.92f, 0.48f, 0.95f, 0.05f, 1, 1, 1, 1,
                    -0.05f, -0.46f, 0.55f, 0.45f, 1, 1, 1, 1,
                    0.92f, 0.48f, 0.95f, 0.05f, 1, 1, 1, 1,
                    0.92f, -0.46f, 0.95f, 0.45f, 1, 1, 1, 1,
                    -0.05f, -0.46f, 0.55f, 0.45f, 1, 1, 1, 1,

                    -0.35f, -0.62f, 0.55f, 0.55f, 1, 1, 1, 0.72f,
                    0.35f, -0.62f, 0.95f, 0.95f, 1, 1, 1, 0.72f,
                    -0.35f, -0.92f, 0.55f, 0.95f, 1, 1, 1, 0.72f,
                    0.35f, -0.62f, 0.95f, 0.95f, 1, 1, 1, 0.72f,
                    0.35f, -0.92f, 0.95f, 0.95f, 1, 1, 1, 0.72f,
                    -0.35f, -0.92f, 0.55f, 0.95f, 1, 1, 1, 0.72f
            };
            FloatBuffer buffer = ByteBuffer.allocateDirect(vertices.length * 4)
                    .order(ByteOrder.nativeOrder()).asFloatBuffer();
            buffer.put(vertices).position(0);
            int[] ids = new int[1];
            GLES20.glGenBuffers(1, ids, 0);
            GLES20.glBindBuffer(GLES20.GL_ARRAY_BUFFER, ids[0]);
            GLES20.glBufferData(GLES20.GL_ARRAY_BUFFER, vertices.length * 4,
                    buffer, GLES20.GL_STATIC_DRAW);
            GLES20.glBindBuffer(GLES20.GL_ARRAY_BUFFER, 0);
            return ids[0];
        }

        private int createProgram(String vertexSource, String fragmentSource) {
            int vertex = compile(GLES20.GL_VERTEX_SHADER, vertexSource);
            int fragment = compile(GLES20.GL_FRAGMENT_SHADER, fragmentSource);
            int result = GLES20.glCreateProgram();
            GLES20.glAttachShader(result, vertex);
            GLES20.glAttachShader(result, fragment);
            GLES20.glLinkProgram(result);
            int[] status = new int[1];
            GLES20.glGetProgramiv(result, GLES20.GL_LINK_STATUS, status, 0);
            if (status[0] == 0) {
                String log = GLES20.glGetProgramInfoLog(result);
                GLES20.glDeleteProgram(result);
                throw new IllegalStateException("Program link failed: " + log);
            }
            GLES20.glDeleteShader(vertex);
            GLES20.glDeleteShader(fragment);
            return result;
        }

        private int compile(int type, String source) {
            int shader = GLES20.glCreateShader(type);
            GLES20.glShaderSource(shader, source);
            GLES20.glCompileShader(shader);
            int[] status = new int[1];
            GLES20.glGetShaderiv(shader, GLES20.GL_COMPILE_STATUS, status, 0);
            if (status[0] == 0) {
                String log = GLES20.glGetShaderInfoLog(shader);
                GLES20.glDeleteShader(shader);
                throw new IllegalStateException("Shader compile failed: " + log);
            }
            return shader;
        }

        private void report(String message) {
            if (listener != null) listener.onStatus(message);
        }
    }
}

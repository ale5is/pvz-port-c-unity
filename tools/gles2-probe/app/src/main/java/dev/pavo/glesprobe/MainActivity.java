package dev.pavo.glesprobe;

import android.app.Activity;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.opengl.GLES20;
import android.util.Log;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.FloatBuffer;
import java.nio.ShortBuffer;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

public final class MainActivity extends Activity {
    private static final String TAG = "PavoGlesScene";
    private SceneView view;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        view = new SceneView();
        setContentView(view);
    }

    @Override
    protected void onPause() {
        super.onPause();
        if (view != null) view.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (view != null) view.onResume();
    }

    private final class SceneView extends GLSurfaceView {
        SceneView() {
            super(MainActivity.this);
            setEGLContextClientVersion(2);
            setEGLConfigChooser(8, 8, 8, 8, 16, 8);
            setRenderer(new SceneRenderer());
            setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
        }
    }

    private static final class SceneRenderer implements GLSurfaceView.Renderer {
        private static final String VERTEX =
                "attribute vec3 aPosition; attribute vec2 aUv; attribute vec4 aColor; " +
                "uniform mat4 uMvp; varying vec2 vUv; varying vec4 vColor; " +
                "void main(){ gl_Position=uMvp*vec4(aPosition,1.0); vUv=aUv; vColor=aColor; }";
        private static final String FRAGMENT =
                "precision mediump float; varying vec2 vUv; varying vec4 vColor; " +
                "uniform sampler2D uTexture; void main(){ gl_FragColor=texture2D(uTexture,vUv)*vColor; }";
        private int program;
        private int texture;
        private int position;
        private int uv;
        private int color;
        private int mvp;
        private int sampler;
        private FloatBuffer vertices;
        private ShortBuffer indices;
        private final float[] matrix = new float[16];
        private int width;
        private int height;
        private long frames;
        private long lastLog;

        @Override
        public void onSurfaceCreated(GL10 gl, EGLConfig config) {
            GLES20.glClearColor(0.035f, 0.055f, 0.09f, 1f);
            GLES20.glEnable(GLES20.GL_DEPTH_TEST);
            GLES20.glDepthMask(true);
            GLES20.glDepthFunc(GLES20.GL_LEQUAL);
            GLES20.glClearDepthf(1.0f);
            int[] depthBits = new int[1];
            GLES20.glGetIntegerv(GLES20.GL_DEPTH_BITS, depthBits, 0);
            Log.i(TAG, "Depth test enabled; depthBits=" + depthBits[0]);
            GLES20.glDisable(GLES20.GL_CULL_FACE);
            program = link(VERTEX, FRAGMENT);
            position = GLES20.glGetAttribLocation(program, "aPosition");
            uv = GLES20.glGetAttribLocation(program, "aUv");
            color = GLES20.glGetAttribLocation(program, "aColor");
            mvp = GLES20.glGetUniformLocation(program, "uMvp");
            sampler = GLES20.glGetUniformLocation(program, "uTexture");
            createTexture();
            createGeometry();
            Log.i(TAG, "GLES2 depth overlap scene initialized; expected: blue outer square with green center; program=" + program + " texture=" + texture);
        }

        @Override
        public void onSurfaceChanged(GL10 gl, int w, int h) {
            width = w;
            height = h;
            GLES20.glViewport(0, 0, w, h);
        }

        @Override
        public void onDrawFrame(GL10 gl) {
            GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT | GLES20.GL_DEPTH_BUFFER_BIT);
            if (program == 0 || texture == 0) return;
            float aspect = height == 0 ? 1f : (float) width / (float) height;
            android.opengl.Matrix.setIdentityM(matrix, 0);
            android.opengl.Matrix.orthoM(matrix, 0, -aspect, aspect, -1f, 1f, -4f, 4f);
            GLES20.glUseProgram(program);
            GLES20.glUniformMatrix4fv(mvp, 1, false, matrix, 0);
            GLES20.glActiveTexture(GLES20.GL_TEXTURE0);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture);
            GLES20.glUniform1i(sampler, 0);
            vertices.position(0);
            GLES20.glEnableVertexAttribArray(position);
            GLES20.glVertexAttribPointer(position, 3, GLES20.GL_FLOAT, false, 9 * 4, vertices);
            vertices.position(3);
            GLES20.glEnableVertexAttribArray(uv);
            GLES20.glVertexAttribPointer(uv, 2, GLES20.GL_FLOAT, false, 9 * 4, vertices);
            vertices.position(5);
            GLES20.glEnableVertexAttribArray(color);
            GLES20.glVertexAttribPointer(color, 4, GLES20.GL_FLOAT, false, 9 * 4, vertices);
            indices.position(0);
            GLES20.glDrawElements(GLES20.GL_TRIANGLES, 12, GLES20.GL_UNSIGNED_SHORT, indices);
            GLES20.glDisableVertexAttribArray(position);
            GLES20.glDisableVertexAttribArray(uv);
            GLES20.glDisableVertexAttribArray(color);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, 0);
            int error = GLES20.glGetError();
            if (error != GLES20.GL_NO_ERROR) Log.e(TAG, "GL error: 0x" + Integer.toHexString(error));
            frames++;
            long now = System.nanoTime();
            if (now - lastLog >= 5000000000L) {
                Log.i(TAG, "Scene running; frames=" + frames + " viewport=" + width + "x" + height);
                lastLog = now;
            }
        }

        private void createGeometry() {
            float[] data = {
                -0.85f,-0.65f,-0.35f, 0f,1f, 0.65f,0.75f,1f,1f,
                 0.85f,-0.65f,-0.35f, 1f,1f, 0.65f,0.75f,1f,1f,
                 0.85f, 0.65f,-0.35f, 1f,0f, 0.65f,0.75f,1f,1f,
                -0.85f, 0.65f,-0.35f, 0f,0f, 0.65f,0.75f,1f,1f,
                -0.38f,-0.30f, 0.35f, 0f,1f, 0.55f,1f,0.55f,1f,
                 0.38f,-0.30f, 0.35f, 1f,1f, 0.55f,1f,0.55f,1f,
                 0.38f, 0.30f, 0.35f, 1f,0f, 0.55f,1f,0.55f,1f,
                -0.38f, 0.30f, 0.35f, 0f,0f, 0.55f,1f,0.55f,1f
            };
            short[] order = {0,1,2,0,2,3,4,5,6,4,6,7};
            vertices = ByteBuffer.allocateDirect(data.length * 4).order(ByteOrder.nativeOrder()).asFloatBuffer();
            vertices.put(data).position(0);
            indices = ByteBuffer.allocateDirect(order.length * 2).order(ByteOrder.nativeOrder()).asShortBuffer();
            indices.put(order).position(0);
        }

        private void createTexture() {
            int[] ids = new int[1];
            GLES20.glGenTextures(1, ids, 0);
            texture = ids[0];
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, texture);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_NEAREST);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_S, GLES20.GL_CLAMP_TO_EDGE);
            GLES20.glTexParameteri(GLES20.GL_TEXTURE_2D, GLES20.GL_TEXTURE_WRAP_T, GLES20.GL_CLAMP_TO_EDGE);
            ByteBuffer pixels = ByteBuffer.allocateDirect(64 * 64 * 4).order(ByteOrder.nativeOrder());
            for (int y = 0; y < 64; y++) {
                for (int x = 0; x < 64; x++) {
                    boolean tile = ((x / 8) + (y / 8)) % 2 == 0;
                    pixels.put((byte) (tile ? 45 : 18));
                    pixels.put((byte) (tile ? 190 : 90));
                    pixels.put((byte) (tile ? 105 : 220));
                    pixels.put((byte) 255);
                }
            }
            pixels.position(0);
            GLES20.glTexImage2D(GLES20.GL_TEXTURE_2D, 0, GLES20.GL_RGBA, 64, 64, 0,
                    GLES20.GL_RGBA, GLES20.GL_UNSIGNED_BYTE, pixels);
            GLES20.glBindTexture(GLES20.GL_TEXTURE_2D, 0);
        }

        private int link(String vertexSource, String fragmentSource) {
            int vs = compile(GLES20.GL_VERTEX_SHADER, vertexSource);
            int fs = compile(GLES20.GL_FRAGMENT_SHADER, fragmentSource);
            if (vs == 0 || fs == 0) return 0;
            int p = GLES20.glCreateProgram();
            GLES20.glAttachShader(p, vs);
            GLES20.glAttachShader(p, fs);
            GLES20.glBindAttribLocation(p, 0, "aPosition");
            GLES20.glBindAttribLocation(p, 1, "aUv");
            GLES20.glBindAttribLocation(p, 2, "aColor");
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
    }
}

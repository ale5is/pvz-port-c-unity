package dev.pavo.horizonnightly;

public final class HorizonActivity extends android.app.NativeActivity {
    static {
        System.loadLibrary("horizon_android");
    }
}

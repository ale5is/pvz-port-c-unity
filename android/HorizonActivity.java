package dev.horizon.android;

public final class HorizonActivity extends android.app.NativeActivity {
    private static final int REQUEST_RECORD_AUDIO = 4102;

    static { System.loadLibrary("horizon_android"); }

    private native void nativeDocumentResult(int request, int result, String uri);

    @Override protected void onCreate(android.os.Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (android.os.Build.VERSION.SDK_INT >= 23
                && checkSelfPermission(android.Manifest.permission.RECORD_AUDIO)
                != android.content.pm.PackageManager.PERMISSION_GRANTED) {
            requestPermissions(
                    new String[] { android.Manifest.permission.RECORD_AUDIO },
                    REQUEST_RECORD_AUDIO);
        }
    }

    @Override protected void onActivityResult(int request, int result, android.content.Intent data) {
        super.onActivityResult(request, result, data);
        if (request != 4101) return;
        nativeDocumentResult(request, result,
                data == null || data.getData() == null ? null : data.getData().toString());
    }
}

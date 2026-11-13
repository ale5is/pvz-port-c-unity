#!/usr/bin/env python3
"""Apply the small Android-side native-library handoff patch to pinned Horizon 0.7.8."""
from __future__ import annotations
import pathlib
import sys

ACTIVITY = r'''package dev.horizon.android;

import android.app.AlertDialog;
import android.os.Bundle;
import android.system.ErrnoException;
import android.system.Os;
import android.text.InputType;
import android.view.inputmethod.EditorInfo;
import android.widget.EditText;
import android.widget.Toast;

/** SAF callback and native-library selection. The game and build client stay Rust. */
public final class HorizonActivity extends android.app.NativeActivity {
    private static final String GAME_LIBRARY = "HORIZON_GAME_LIBRARY";
    private static final String APK_STAMP = "HORIZON_APK";

    private void loadGame() {
        String stamp = NativeMods.stamp(this);
        if (stamp != null) {
            try {
                Os.setenv(APK_STAMP, stamp, true);
            } catch (ErrnoException ignored) {
                // A build without a matching install stamp is not loaded.
            }
        }
        java.io.File build = NativeMods.chosen(this, versionName());
        if (build != null) {
            try {
                Os.setenv(GAME_LIBRARY, build.getPath(), true);
                System.load(build.getPath());
                return;
            } catch (UnsatisfiedLinkError | ErrnoException error) {
                try {
                    Os.unsetenv(GAME_LIBRARY);
                } catch (ErrnoException ignored) {
                    // Nothing to clear.
                }
                NativeMods.failed(this, error.getMessage());
            }
        }
        System.loadLibrary("horizon_android");
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        loadGame();
        super.onCreate(savedInstanceState);
    }

    private native void nativeDocumentResult(int request, int result, String uri);

    @Override
    protected void onActivityResult(int request, int result, android.content.Intent data) {
        super.onActivityResult(request, result, data);
        nativeDocumentResult(request, result,
                data == null || data.getData() == null ? null : data.getData().toString());
    }

    public String versionName() {
        try {
            return getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
        } catch (android.content.pm.PackageManager.NameNotFoundException error) {
            return null;
        }
    }

    public String nativeBuildServer() {
        return NativeMods.buildServer(this);
    }

    public void installNativeBuild(String key) {
        NativeMods.install(this, key, versionName());
    }

    /** Called by the in-game Mods page; the URL is entered by the player. */
    public void askBuildServer(final String current) {
        runOnUiThread(() -> {
            EditText address = new EditText(this);
            address.setSingleLine(true);
            address.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
            address.setImeOptions(EditorInfo.IME_ACTION_DONE);
            address.setHint("https://your-build-server.example");
            address.setText(current == null ? "" : current);
            new AlertDialog.Builder(this)
                    .setTitle("Horizon native-mod build server")
                    .setMessage("Only use a server you operate or fully trust. It receives selected native mod source and returns a library that runs inside Horizon with the game's permissions.")
                    .setView(address)
                    .setPositiveButton("Use server", (dialog, which) -> {
                        String value = address.getText().toString().trim();
                        if (NativeMods.isBuildServer(value)) {
                            NativeMods.chooseBuildServer(this, value);
                        } else {
                            Toast.makeText(this, "Enter an HTTPS host URL without a path.", Toast.LENGTH_LONG).show();
                        }
                    })
                    .setNeutralButton("Clear", (dialog, which) -> NativeMods.chooseBuildServer(this, null))
                    .setNegativeButton("Cancel", null)
                    .show();
        });
    }
}
'''

NATIVE_MODS = r'''package dev.horizon.android;

import android.content.Context;
import android.content.pm.PackageManager;
import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.regex.Pattern;

/** Selects a compiled native library only for the exact APK install that built it. */
final class NativeMods {
    static final String LIBRARY = "libhorizon_android.so";
    private static final Pattern SERVER = Pattern.compile(
            "https://[a-z0-9]([a-z0-9.-]{0,251}[a-z0-9])?(:[0-9]{1,5})?/?",
            Pattern.CASE_INSENSITIVE);

    private NativeMods() {}

    private static File dir(Context context) {
        return new File(context.getFilesDir(), "native");
    }

    static File chosen(Context context, String version) {
        File root = dir(context);
        String key = read(new File(root, "current"));
        if (key == null || key.isEmpty() || key.contains("/") || key.startsWith(".")) {
            return null;
        }
        File build = new File(root, key);
        File library = new File(build, LIBRARY);
        if (!library.isFile() || version == null || !version.equals(read(new File(build, "for")))) {
            return null;
        }
        String stamp = stamp(context);
        if (stamp == null || !stamp.equals(read(new File(build, "apk")))) {
            return null;
        }
        return library;
    }

    static String stamp(Context context) {
        try {
            return Long.toString(context.getPackageManager()
                    .getPackageInfo(context.getPackageName(), 0).lastUpdateTime);
        } catch (PackageManager.NameNotFoundException error) {
            return null;
        }
    }

    static void install(Context context, String key, String version) {
        if (key == null || !key.matches("[A-Za-z0-9_-]{8,80}") || version == null) {
            return;
        }
        File root = dir(context);
        File build = new File(root, key);
        if (!new File(build, LIBRARY).isFile()) {
            return;
        }
        String stamp = stamp(context);
        if (stamp == null) {
            return;
        }
        write(new File(build, "for"), version);
        write(new File(build, "apk"), stamp);
        write(new File(root, "current"), key);
        new File(root, "notice").delete();
    }

    static void failed(Context context, String reason) {
        File root = dir(context);
        new File(root, "current").delete();
        write(new File(root, "notice"), "The selected native-mod build could not be loaded: "
                + (reason == null ? "unknown linker error" : reason)
                + ". Horizon started with the APK's original library.");
    }

    static String buildServer(Context context) {
        return read(new File(dir(context), "build-server"));
    }

    static boolean isBuildServer(String value) {
        return value != null && SERVER.matcher(value.trim()).matches();
    }

    static void chooseBuildServer(Context context, String value) {
        File file = new File(dir(context), "build-server");
        if (value == null) {
            file.delete();
        } else if (isBuildServer(value)) {
            write(file, value.trim().replaceAll("/+$", ""));
        }
    }

    private static String read(File file) {
        try (BufferedReader reader = new BufferedReader(
                new InputStreamReader(new FileInputStream(file), StandardCharsets.UTF_8))) {
            return reader.readLine();
        } catch (IOException error) {
            return null;
        }
    }

    private static void write(File file, String text) {
        try {
            File parent = file.getParentFile();
            if (parent != null) {
                parent.mkdirs();
            }
            try (FileOutputStream output = new FileOutputStream(file)) {
                output.write(text.getBytes(StandardCharsets.UTF_8));
            }
        } catch (IOException ignored) {
            // Best effort; the APK's original library remains available.
        }
    }
}
'''

WRAPPER = r'''// NativeActivity enters the APK's library first. If Java preloaded a compiled build,
// hand the same activity to that library's android-activity entry point.
unsafe extern "C" {
    fn ANativeActivity_onCreate(
        activity: *mut std::ffi::c_void,
        saved: *mut std::ffi::c_void,
        saved_size: usize,
    );
}

#[link(name = "dl")]
unsafe extern "C" {
    fn dlopen(filename: *const std::ffi::c_char, flags: std::ffi::c_int) -> *mut std::ffi::c_void;
    fn dlsym(handle: *mut std::ffi::c_void, symbol: *const std::ffi::c_char) -> *mut std::ffi::c_void;
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_on_create(
    activity: *mut std::ffi::c_void,
    saved: *mut std::ffi::c_void,
    saved_size: usize,
) {
    use std::os::unix::ffi::OsStrExt;
    type OnCreate = unsafe extern "C" fn(
        *mut std::ffi::c_void,
        *mut std::ffi::c_void,
        usize,
    );
    let own: OnCreate = ANativeActivity_onCreate;
    if let Some(path) = std::env::var_os("HORIZON_GAME_LIBRARY") {
        if let Ok(path) = std::ffi::CString::new(path.as_os_str().as_bytes()) {
            let entry = unsafe {
                let handle = dlopen(path.as_ptr(), 2);
                if handle.is_null() {
                    std::ptr::null_mut()
                } else {
                    dlsym(handle, c"ANativeActivity_onCreate".as_ptr())
                }
            };
            if !entry.is_null() && entry != own as *mut std::ffi::c_void {
                let entry = unsafe {
                    std::mem::transmute::<*mut std::ffi::c_void, OnCreate>(entry)
                };
                return unsafe { entry(activity, saved, saved_size) };
            }
        }
        log::error!("The selected native-mod library has no Android entry point; using APK library.");
    }
    unsafe { own(activity, saved, saved_size) }
}

'''

def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count == 0 and new in text:
        return text
    if count != 1:
        raise SystemExit(f"{label}: expected one insertion point, found {count}")
    return text.replace(old, new, 1)


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: apply_loader_patch.py HORIZON_SOURCE")
    root = pathlib.Path(sys.argv[1])
    activity_path = root / "packaging/android/java/dev/horizon/android/HorizonActivity.java"
    manifest_path = root / "packaging/android/AndroidManifest.xml"
    rust_path = root / "crates/horizon-client/src/android_main.rs"
    for path in (activity_path, manifest_path, rust_path):
        if not path.is_file():
            raise SystemExit(f"missing pinned Horizon source file: {path}")

    activity = activity_path.read_text()
    if 'static { System.loadLibrary("horizon_android"); }' in activity:
        activity_path.write_text(ACTIVITY)
    elif "private void loadGame()" not in activity:
        raise SystemExit("HorizonActivity is neither the expected 0.7.8 source nor already patched")

    native_mods_path = activity_path.with_name("NativeMods.java")
    native_mods_path.write_text(NATIVE_MODS)

    manifest = manifest_path.read_text()
    old = '<meta-data android:name="android.app.lib_name" android:value="horizon_android" />'
    new = old + '\n            <meta-data android:name="android.app.func_name" android:value="horizon_on_create" />'
    count = manifest.count(old)
    if count == 2:
        manifest = manifest.replace(old, new)
        manifest_path.write_text(manifest)
    elif count == 0 and manifest.count('android.app.func_name" android:value="horizon_on_create"') == 2:
        pass
    else:
        raise SystemExit(f"AndroidManifest.xml: expected two native library declarations, found {count}")

    rust = rust_path.read_text()
    marker = '#[unsafe(no_mangle)]\nfn android_on_create('
    if "pub unsafe extern \"C\" fn horizon_on_create(" not in rust:
        rust = replace_once(rust, marker, WRAPPER + marker, "android_main.rs")
        rust_path.write_text(rust)

    print("Horizon native library loader patch applied")
    

if __name__ == "__main__":
    main()

#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(sys.argv[1]).resolve()

def replace(path, old, new, expected=1):
    file = ROOT / path
    source = file.read_text(encoding="utf-8")
    count = source.count(old)
    if count != expected:
        raise SystemExit(f"{path}: expected {expected} exact match(es), found {count}")
    file.write_text(source.replace(old, new), encoding="utf-8")

def write_new(path, content):
    file = ROOT / path
    if file.exists():
        raise SystemExit(f"Refusing to overwrite existing {path}")
    file.parent.mkdir(parents=True, exist_ok=True)
    file.write_text(content, encoding="utf-8")

android_wrapper = r'''
#[unsafe(no_mangle)]
pub unsafe extern "C" fn horizon_on_create(
    activity: *mut std::ffi::c_void,
    saved: *mut std::ffi::c_void,
    saved_size: usize,
) {
    unsafe extern "C" {
        fn ANativeActivity_onCreate(
            activity: *mut std::ffi::c_void,
            saved: *mut std::ffi::c_void,
            saved_size: usize,
        );
    }
    type OnCreate = unsafe extern "C" fn(
        *mut std::ffi::c_void,
        *mut std::ffi::c_void,
        usize,
    );
    let own: OnCreate = ANativeActivity_onCreate;
    if let Some(path) = std::env::var_os("HORIZON_GAME_LIBRARY") {
        use std::os::unix::ffi::OsStrExt;
        let path = std::ffi::CString::new(path.as_os_str().as_bytes()).unwrap_or_default();
        let entry = unsafe {
            let build = dlopen(path.as_ptr(), 2);
            if build.is_null() {
                std::ptr::null_mut()
            } else {
                dlsym(build, c"ANativeActivity_onCreate".as_ptr())
            }
        };
        if !entry.is_null() && entry != own as *mut std::ffi::c_void {
            let entry = unsafe { std::mem::transmute::<*mut std::ffi::c_void, OnCreate>(entry) };
            return unsafe { entry(activity, saved, saved_size) };
        }
        eprintln!("Horizon: compiled native library has no NativeActivity entry point; using APK library");
    }
    unsafe { own(activity, saved, saved_size) }
}

#[link(name = "dl")]
unsafe extern "C" {
    fn dlopen(filename: *const std::os::raw::c_char, flags: std::os::raw::c_int) -> *mut std::ffi::c_void;
    fn dlsym(handle: *mut std::ffi::c_void, symbol: *const std::os::raw::c_char) -> *mut std::ffi::c_void;
}
'''

replace(
    "crates/horizon-client/src/android_main.rs",
    "use std::cell::RefCell;\n",
    "use std::cell::RefCell;\n" + android_wrapper,
)

manifest = "packaging/android/AndroidManifest.xml"
meta = '<meta-data android:name="android.app.lib_name" android:value="horizon_android" />'
replacement = meta + '\n            <meta-data android:name="android.app.func_name" android:value="horizon_on_create" />'
replace(manifest, meta, replacement, expected=2)

activity = r'''package dev.horizon.android;

import android.os.Bundle;
import android.system.Os;
import java.io.File;

/** NativeActivity bootstrap and SAF result callback. */
public final class HorizonActivity extends android.app.NativeActivity {
    private File nativeBuild;
    private native void nativeDocumentResult(int request, int result, String uri);

    @Override protected void onCreate(Bundle state) {
        loadGameLibrary();
        super.onCreate(state);
    }

    private void loadGameLibrary() {
        try {
            Os.unsetenv("HORIZON_GAME_LIBRARY");
        } catch (Exception ignored) {
        }
        try {
            String stamp = NativeMods.stamp(this);
            if (stamp != null) Os.setenv("HORIZON_APK", stamp, true);
            File candidate = NativeMods.chosen(this);
            if (candidate != null) {
                Os.setenv("HORIZON_GAME_LIBRARY", candidate.getAbsolutePath(), true);
                System.load(candidate.getAbsolutePath());
                nativeBuild = candidate;
            }
        } catch (Exception | UnsatisfiedLinkError error) {
            NativeMods.failed(this, error.toString());
            try {
                Os.unsetenv("HORIZON_GAME_LIBRARY");
            } catch (Exception ignored) {
            }
            nativeBuild = null;
        }
        System.loadLibrary("horizon_android");
    }

    @Override protected void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus && nativeBuild != null) NativeMods.started(this);
    }

    @Override protected void onActivityResult(int request, int result, android.content.Intent data) {
        super.onActivityResult(request, result, data);
        if (request != 4101) return;
        nativeDocumentResult(request, result,
                data == null || data.getData() == null ? null : data.getData().toString());
    }
}
'''
write_new("packaging/android/java/dev/horizon/android/NativeMods.java", r'''package dev.horizon.android;

import android.content.Context;
import android.content.pm.PackageManager;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

/** Selects the verified native build for this exact APK installation. */
final class NativeMods {
    static final String LIBRARY = "libhorizon_android.so";
    private static final int MAX_STARTS = 2;

    private NativeMods() {}

    private static File dir(Context context) {
        return new File(context.getFilesDir(), "native");
    }

    static File chosen(Context context) {
        File root = dir(context);
        String key = read(new File(root, "current"));
        if (key == null || key.isEmpty() || key.contains("/") || key.startsWith(".")) return null;
        File folder = new File(root, key);
        File library = new File(folder, LIBRARY);
        String stamp = stamp(context);
        if (!library.isFile() || stamp == null || !stamp.equals(read(new File(folder, "apk")))) {
            return null;
        }
        File starts = new File(root, "starts");
        int count = 0;
        try {
            count = Integer.parseInt(String.valueOf(read(starts)));
        } catch (NumberFormatException ignored) {
        }
        if (count >= MAX_STARTS) {
            setAside(root, "The native build failed to reach the game window twice. Horizon started with the APK library.");
            return null;
        }
        write(starts, Integer.toString(count + 1));
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

    static void started(Context context) {
        new File(dir(context), "starts").delete();
    }

    static void failed(Context context, String why) {
        setAside(dir(context), "The native build could not be loaded (" + why + "). Horizon started with the APK library.");
    }

    private static void setAside(File root, String notice) {
        new File(root, "current").delete();
        new File(root, "starts").delete();
        write(new File(root, "notice"), notice);
    }

    private static String read(File file) {
        try {
            return new String(Files.readAllBytes(file.toPath()), StandardCharsets.UTF_8).trim();
        } catch (IOException error) {
            return null;
        }
    }

    private static void write(File file, String value) {
        try {
            File parent = file.getParentFile();
            if (parent != null) parent.mkdirs();
            Files.write(file.toPath(), value.getBytes(StandardCharsets.UTF_8));
        } catch (IOException ignored) {
        }
    }
}
''')
write_new("packaging/android/java/dev/horizon/android/HorizonActivity.java", activity)

replace(
    "crates/horizon-client/src/content_files.rs",
    'Self::Mods => lower.ends_with(".hmod"),',
    'Self::Mods => lower.ends_with(".hmod") || lower.ends_with(".hnmod"),',
)
needle = '''    if file.metadata()?.len() > MAX_INPUT {
        return Err("Import exceeds 256 MiB".into());
    }
    let mut zip = zip::ZipArchive::new(file)?;'''
replacement = '''    if file.metadata()?.len() > MAX_INPUT {
        return Err("Import exceeds 256 MiB".into());
    }
    if kind == Kind::Mods && name.to_ascii_lowercase().ends_with(".hnmod") {
        horizon_mods::index::native::Bundle::read(path)
            .map_err(|e| format!("Invalid native Horizon mod: {e}"))?;
        return Ok(false);
    }
    let mut zip = zip::ZipArchive::new(file)?;'''
replace("crates/horizon-client/src/content_files.rs", needle, replacement)

replace(
    "crates/horizon-client/src/android/documents.rs",
    "    let dir = request.home.join(request.kind.folder());",
    '''    let native_mod = request.kind == Kind::Mods && name.to_ascii_lowercase().ends_with(".hnmod");
    let dir = if native_mod {
        request.home.join("horizon-native-mods")
    } else {
        request.home.join(request.kind.folder())
    };''',
)
replace(
    "crates/horizon-client/src/android/documents.rs",
    '''        let name = if request.kind == Kind::Mods {
            format!("{}.hmod", horizon_mods::Package::read(&staged)?.manifest.id)
        } else if native_shader || name.to_ascii_lowercase().ends_with(".hshader") {''',
    '''        let name = if native_mod {
            format!(
                "{}.hnmod",
                horizon_mods::index::native::Bundle::read(&staged)?.manifest.id
            )
        } else if request.kind == Kind::Mods {
            format!("{}.hmod", horizon_mods::Package::read(&staged)?.manifest.id)
        } else if native_shader || name.to_ascii_lowercase().ends_with(".hshader") {''',
)
replace(
    "crates/horizon-client/src/android/documents.rs",
    '''            imported(
                format!(
                    "Imported {} into {}. Enable it in game settings.",
                    target.file_name().unwrap_or_default().to_string_lossy(),
                    request.kind.label()
                ),
                request.kind,
            );''',
    '''            if native_mod {
                imported(
                    format!(
                        "Imported native mod '{}'. Compile it in Options > Mods.",
                        target.file_name().unwrap_or_default().to_string_lossy()
                    ),
                    request.kind,
                );
            } else {
                imported(
                    format!(
                        "Imported {} into {}. Enable it in game settings.",
                        target.file_name().unwrap_or_default().to_string_lossy(),
                        request.kind.label()
                    ),
                    request.kind,
                );
            }''',
)
replace(
    "crates/horizon-client/src/android/documents.rs",
    '''    let result = (|| -> Result<()> {
        if matches!(choice, Choice::Replace)''',
    '''    let native_mod = c.target.extension().is_some_and(|ext| ext == "hnmod");
    let result = (|| -> Result<()> {
        if matches!(choice, Choice::Replace)''',
)
replace(
    "crates/horizon-client/src/android/documents.rs",
    '''        Ok(()) => imported(
            "Import completed. Enable it in Installed or game settings.",
            c.kind,
        ),''',
    '''        Ok(()) => imported(
            if native_mod {
                "Native mod imported. Compile it in Options > Mods."
            } else {
                "Import completed. Enable it in Installed or game settings."
            },
            c.kind,
        ),''',
)

replace(
    "crates/horizon-client/src/android/document_store.rs",
    '''            "mods",
            "packs",''',
    '''            "mods",
            "native-mods",
            "packs",''',
)
replace(
    "crates/horizon-client/src/android/document_store.rs",
    '''            Some("mods") => ("horizon-mods", "Mods"),
            Some("packs") => ("resourcepacks", "Resource Packs"),''',
    '''            Some("mods") => ("horizon-mods", "Mods"),
            Some("native-mods") => ("horizon-native-mods", "Native Mods"),
            Some("packs") => ("resourcepacks", "Resource Packs"),''',
)
replace(
    "crates/horizon-client/src/android/document_store.rs",
    'Some("zip" | "hmod" | "hshader") => "application/zip",',
    'Some("zip" | "hmod" | "hnmod" | "hshader") => "application/zip",',
)
replace(
    "crates/horizon-client/src/android/document_store.rs",
    '''    ("horizon-mods", "Mods"),
    ("resourcepacks", "Packs"),''',
    '''    ("horizon-mods", "Mods"),
    ("horizon-native-mods", "Native Mods"),
    ("resourcepacks", "Packs"),''',
)

replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    'Import .hmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.',
    'Import .hmod/.hnmod/.jar mods, .zip resource packs, or shader packs — or browse files in File Manager.',
)
replace(
    "crates/horizon-client/src/android/launcher_pages.rs",
    'entry.name.ends_with(".hmod") || entry.name.ends_with(".jar")',
    'entry.name.ends_with(".hmod") || entry.name.ends_with(".hnmod") || entry.name.ends_with(".jar")',
)

print("Applied Horizon native-mod import and dynamic-library loader patches.")

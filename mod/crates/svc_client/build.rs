use std::path::PathBuf;

fn main() {
    let manifest_dir = PathBuf::from(std::env::var("CARGO_MANIFEST_DIR").unwrap());
    let local_vendor = manifest_dir.join("vendor");
    let staged_vendor = manifest_dir.join("../../../native-data/simple-voice-chat/vendor");
    let root = if local_vendor.is_dir() {
        local_vendor
    } else if staged_vendor.is_dir() {
        staged_vendor
    } else {
        panic!("libopus vendor sources were not found in the crate or Horizon native-data staging");
    };
    let mut build = cc::Build::new();
    build
        .define("HAVE_CONFIG_H", None)
        .include(&root)
        .include(root.join("include"))
        .include(root.join("celt"))
        .include(root.join("silk"))
        .include(root.join("silk/float"))
        .include(root.join("src"))
        .flag_if_supported("-O2");
    let mut n = 0;
    for dir in ["celt", "silk", "silk/float", "src"] {
        for entry in walk(&root.join(dir)) {
            let ps = entry.to_string_lossy().replace('\\', "/");
            let name = entry.file_name().unwrap().to_string_lossy().to_string();
            let skip = ps.contains("test")
                || ps.contains("demo")
                || ps.contains("compare")
                || ps.contains("/arm/")
                || ps.contains("/x86/")
                || ps.contains("/mips/")
                || ps.contains("/fixed/");
            if name.ends_with(".c") && !skip {
                build.file(entry);
                n += 1;
            }
        }
    }
    println!("cargo:warning=svc_client: compiling {} libopus C sources", n);
    build.compile("opus");
}

fn walk(dir: &std::path::Path) -> Vec<PathBuf> {
    let mut out = Vec::new();
    if let Ok(rd) = std::fs::read_dir(dir) {
        for e in rd.flatten() {
            let p = e.path();
            if p.is_dir() {
                out.extend(walk(&p));
            } else {
                out.push(p);
            }
        }
    }
    out
}

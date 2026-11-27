#!/usr/bin/env python3
"""Move Android GLES renderer construction back to the window/event thread."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
root = parser.parse_args().source.resolve()
game = root / "crates/horizon-client/src/game.rs"
source = game.read_text(encoding="utf-8")

old_field = """    renderer_job: Option<JoinHandle<std::result::Result<Renderer, String>>>,
    /// GLES preparation exclusively owns the context; keep pumping native events without drawing.
    renderer_job_gl: bool,"""
new_field = """    renderer_job: Option<JoinHandle<std::result::Result<Renderer, String>>>,
    /// CPU-only GLES texture preparation; this worker never owns a GPU context or window.
    gles2_cpu_job: Option<JoinHandle<horizon_client::gfx::PreparedTextures>>,
    /// GLES preparation exclusively owns the context; keep pumping native events without drawing.
    renderer_job_gl: bool,"""
if new_field not in source:
    if source.count(old_field) != 1:
        raise SystemExit("Could not uniquely locate Loading renderer-job fields")
    source = source.replace(old_field, new_field)

completion = """        if let Some(job) = &self.gles2_cpu_job {
            if !job.is_finished() {
                return Ok(());
            }
            let prepared = self.gles2_cpu_job.take().unwrap()
                .join()
                .map_err(|_| "GLES2 CPU texture preparation worker panicked")?;
            let Some(Ok(loaded)) = &mut self.loaded else {
                return Err("GLES2 texture preparation completed without loaded resources".into());
            };
            loaded.renderer_cpu = Some(prepared);
            log::info!("Horizon: Android GLES CPU texture preparation completed");
            return Ok(());
        }
"""
anchor = """        if let Some(job) = &self.renderer_job {
            if !job.is_finished() {"""
if completion not in source:
    if source.count(anchor) != 1:
        raise SystemExit("Could not uniquely locate renderer worker completion branch")
    source = source.replace(anchor, completion + anchor)

branch = """        if gl {
            // Keep the loading renderer alive while CPU mip/PBR preparation runs.
            // Only owned CPU buffers cross this worker boundary; it never creates
            // a wgpu renderer, transfers a wgpu context, or touches EGL/GLES.
            let assets = loaded.pack.render.clone();
            let forcefield = loaded.pack.forcefield.clone();
            let prepared = loaded.renderer_cpu.take();
            self.gles2_cpu_job = Some(
                std::thread::Builder::new()
                    .name("GLES2 CPU texture preparation".into())
                    .spawn(move || {
                        let started = Instant::now();
                        let prepared = prepared.unwrap_or_else(|| {
                            horizon_client::gfx::PreparedTextures::new(
                                &assets.pixels,
                                assets.sprite_size,
                                assets.texture_layers,
                                &assets.sprite_mips,
                                (&forcefield.2, forcefield.0),
                            )
                            .with_pbr(
                                &assets.pbr_normals,
                                &assets.pbr_specular,
                                assets.pbr_size,
                                assets.texture_layers,
                            )
                        });
                        log::info!(
                            "Horizon: GLES2 CPU texture preparation took {} ms",
                            started.elapsed().as_millis()
                        );
                        prepared
                    })?,
            );
            return Ok(());
        }
"""
gl_anchor = """        if gl && (!self.menu.needs_world_renderer() || !self.menu.entry_presented) {
            return Ok(());
        }
        let _ = previous.device.poll(wgpu::PollType::wait_indefinitely());"""
if branch not in source:
    if source.count(gl_anchor) != 1:
        raise SystemExit("Could not uniquely locate Android GLES world-renderer preparation boundary")
    source = source.replace(gl_anchor, gl_anchor.replace(
        "        let _ = previous.device.poll(wgpu::PollType::wait_indefinitely());",
        branch + "        let _ = previous.device.poll(wgpu::PollType::wait_indefinitely());"
    ))

init_old = """        renderer_job: None,
        renderer_job_gl: false,"""
init_new = """        renderer_job: None,
        gles2_cpu_job: None,
        renderer_job_gl: false,"""
if init_new not in source:
    if source.count(init_old) != 1:
        raise SystemExit("Could not uniquely locate Loading initialization")
    source = source.replace(init_old, init_new)

cancel_old = """                if loading.cancel_renderer && loading.renderer_job.is_none() {"""
cancel_new = """                if loading.cancel_renderer
                    && loading.renderer_job.is_none()
                    && loading.gles2_cpu_job.is_none()
                {"""
if cancel_new not in source:
    if source.count(cancel_old) != 1:
        raise SystemExit("Could not uniquely locate renderer cancellation guard")
    source = source.replace(cancel_old, cancel_new)

defer_old = """                    || loading.renderer_job.is_some()
                    || defer_gles_world"""
defer_new = """                    || loading.renderer_job.is_some()
                    || loading.gles2_cpu_job.is_some()
                    || defer_gles_world"""
if defer_new not in source:
    if source.count(defer_old) != 1:
        raise SystemExit("Could not uniquely locate loading-to-game transition guard")
    source = source.replace(defer_old, defer_new)

if source.count(branch) != 1 or source.count(completion) != 1:
    raise SystemExit("Android GLES CPU worker integration is incomplete or duplicated")
game.write_text(source, encoding="utf-8")
print("Android GLES CPU preparation now runs on a worker; wgpu renderer creation remains on the event thread.")

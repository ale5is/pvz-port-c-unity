#!/usr/bin/env python3
from pathlib import Path
import sys


def replace_once(source: str, old: str, new: str, label: str) -> str:
    count = source.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one exact match, found {count}")
    return source.replace(old, new, 1)


def patch_frame_pacing(root: Path) -> None:
    path = root / "crates/horizon-client-input/src/window.rs"
    source = path.read_text(encoding="utf-8")

    source = replace_once(
        source,
        '''    fn park(&mut self, nanos: i64) {
        std::thread::sleep(std::time::Duration::from_nanos(nanos.max(0) as u64));
    }
''',
        '''    fn park(&mut self, nanos: i64) {
        let end = self.nanos().saturating_add(nanos);
        park_in_steps(
            end,
            || self.nanos(),
            |step| {
                std::thread::sleep(std::time::Duration::from_nanos(step as u64));
            },
        );
    }
''',
        "short-step frame pacing sleep",
    )

    source = replace_once(
        source,
        "/// The real clock without busy waiting: where vanilla spins on\n",
        '''/// The longest single sleep of SystemClock::park.
const PARK_STEP_NS: i64 = 1_000_000;

fn park_in_steps(end: i64, mut now: impl FnMut() -> i64, mut sleep: impl FnMut(i64)) {
    loop {
        let left = end - now();
        if left <= 0 {
            return;
        }
        sleep(left.min(PARK_STEP_NS));
    }
}

/// The real clock without busy waiting: where vanilla spins on
''',
        "short-step sleep helper",
    )

    source = replace_once(
        source,
        '''pub fn end_frame(
    limiter: &mut FramerateLimiter,
    framerate_limit: i32,
    clock: &mut impl LimiterClock,
) {
    if framerate_limit < 260 {
        limiter.limit_display_fps(framerate_limit, clock);
    }
}

''',
        '''pub fn end_frame(
    limiter: &mut FramerateLimiter,
    framerate_limit: i32,
    clock: &mut impl LimiterClock,
) {
    if framerate_limit < 260 {
        limiter.limit_display_fps(framerate_limit, clock);
    }
}

pub fn end_frame_on_grid(
    limiter: &mut FramerateLimiter,
    framerate_limit: i32,
    clock: &mut impl LimiterClock,
) {
    if framerate_limit < 260 {
        limiter.limit_on_grid(framerate_limit, clock);
    }
}

''',
        "fixed-grid frame-pacing entry point",
    )

    source = replace_once(
        source,
        '''        loop {
            let remaining = target_time_ns - clock.nanos();
            if remaining <= 0 {
                break;
            }
            if remaining > self.average_overshoot_ns + 500_000 {
                let sleep_start = clock.nanos();
                let expected = remaining - self.average_overshoot_ns - 500_000;
                if !clock.interrupted() {
                    clock.park(expected);
                    let duration = clock.nanos() - sleep_start;
                    let overshoot = duration - expected;
                    if overshoot > 0 && overshoot < 25_000_000 {
                        self.average_overshoot_ns = (0.1 * overshoot as f64
                            + 0.9 * self.average_overshoot_ns as f64)
                            as i64;
                        self.average_overshoot_ns = self.average_overshoot_ns.min(2_000_000);
                    }
                }
            } else {
                clock.spin();
            }
        }
        self.last_frame_time = clock.nanos();
    }
    pub fn last_frame_time(&self) -> i64 {
''',
        '''        self.wait_until(target_time_ns, clock);
        self.last_frame_time = clock.nanos();
    }
    pub fn limit_on_grid(&mut self, framerate_limit: i32, clock: &mut impl LimiterClock) {
        let period = 1_000_000_000 / i64::from(framerate_limit);
        if framerate_limit != self.last_framerate_limit {
            self.average_overshoot_ns = 0;
            self.last_framerate_limit = framerate_limit;
        }
        let deadline = self.last_frame_time + period;
        if clock.nanos() >= deadline {
            self.last_frame_time = clock.nanos();
            return;
        }
        self.wait_until(deadline, clock);
        let woke = clock.nanos();
        self.last_frame_time = if woke - deadline > period / 2 {
            woke
        } else {
            deadline
        };
    }
    fn wait_until(&mut self, target_time_ns: i64, clock: &mut impl LimiterClock) {
        loop {
            let remaining = target_time_ns - clock.nanos();
            if remaining <= 0 {
                break;
            }
            if remaining > self.average_overshoot_ns + 500_000 {
                let sleep_start = clock.nanos();
                let expected = remaining - self.average_overshoot_ns - 500_000;
                if !clock.interrupted() {
                    clock.park(expected);
                    let duration = clock.nanos() - sleep_start;
                    let overshoot = duration - expected;
                    if overshoot > 0 && overshoot < 25_000_000 {
                        self.average_overshoot_ns = (0.1 * overshoot as f64
                            + 0.9 * self.average_overshoot_ns as f64)
                            as i64;
                        self.average_overshoot_ns = self.average_overshoot_ns.min(2_000_000);
                    }
                }
            } else {
                clock.spin();
            }
        }
    }
    pub fn last_frame_time(&self) -> i64 {
''',
        "fixed-grid limiter implementation",
    )

    source = replace_once(
        source,
        '''    #[test]
    fn vsync_only_uses_fifo() {
''',
        '''    #[test]
    fn efficient_pacing_keeps_frame_slots_on_a_fixed_grid() {
        let mut limiter = FramerateLimiter::new(0);
        let mut clock = Virtual {
            now: 0,
            oversleep: 200_000,
        };
        for _ in 0..100 {
            limiter.limit_on_grid(100, &mut clock);
        }
        assert!(clock.now >= 1_000_000_000);
        assert!(clock.now < 1_001_000_000);
    }

    #[test]
    fn a_long_frame_restarts_the_grid_without_a_burst() {
        let mut limiter = FramerateLimiter::new(0);
        let mut clock = Virtual {
            now: 0,
            oversleep: 0,
        };
        limiter.limit_on_grid(100, &mut clock);
        assert_eq!(clock.now, 10_000_000);
        clock.now += 25_000_000;
        limiter.limit_on_grid(100, &mut clock);
        assert_eq!(clock.now, 35_000_000);
        limiter.limit_on_grid(100, &mut clock);
        assert_eq!(clock.now, 45_000_000);
    }

    #[test]
    fn long_sleeps_are_split_into_short_steps() {
        let now = std::cell::Cell::new(0);
        let mut steps = Vec::new();
        park_in_steps(
            3_500_000,
            || now.get(),
            |step| {
                steps.push(step);
                now.set(now.get() + step);
            },
        );
        assert_eq!(steps, vec![1_000_000, 1_000_000, 1_000_000, 500_000]);
    }

    #[test]
    fn vsync_only_uses_fifo() {
''',
        "fixed-grid pacing regression tests",
    )

    path.write_text(source, encoding="utf-8")


def patch_android_menu_cap(root: Path) -> None:
    path = root / "crates/horizon-client/src/game/options.rs"
    source = path.read_text(encoding="utf-8")

    helper = '''/// Cap Android menus over a world at 60 FPS without changing world rendering.
fn menu_frame_cap(limit: i32, has_screen: bool, android: bool) -> i32 {
    if android && has_screen {
        limit.min(60)
    } else {
        limit
    }
}

'''
    source = replace_once(
        source,
        "impl Game<'_> {\n",
        helper + "impl Game<'_> {\n",
        "Android menu frame-cap helper",
    )

    source = replace_once(
        source,
        '''        let limit = framerate_limit(self.settings.max_fps as i32, context);
        self.settings
            .horizon
            .battery_limit(limit, self.title.is_none(), self.focused)
''',
        '''        let limit = framerate_limit(self.settings.max_fps as i32, context);
        let limit = self
            .settings
            .horizon
            .battery_limit(limit, self.title.is_none(), self.focused);
        menu_frame_cap(limit, context.has_screen, cfg!(target_os = "android"))
''',
        "Android menu frame-cap application",
    )

    source = replace_once(
        source,
        """            horizon_client::graphics::Pacing::Efficient => {
                let mut clock = horizon_client::window::SleepingClock(self.frame_clock);
                horizon_client::window::end_frame(&mut self.limiter, limit, &mut clock);
            }
""",
        """            horizon_client::graphics::Pacing::Efficient => {
                let mut clock = horizon_client::window::SleepingClock(self.frame_clock);
                horizon_client::window::end_frame_on_grid(&mut self.limiter, limit, &mut clock);
            }
""",
        "fixed-grid efficient pacing selection",
    )

    tests = '''
#[cfg(test)]
mod menu_frame_cap_tests {
    use super::menu_frame_cap;

    #[test]
    fn a_menu_caps_the_frame_rate_on_android_only() {
        assert_eq!(menu_frame_cap(300, true, true), 60);
        assert_eq!(menu_frame_cap(300, false, true), 300);
        assert_eq!(menu_frame_cap(30, true, true), 30);
        assert_eq!(menu_frame_cap(300, true, false), 300);
    }
}
'''
    if "mod menu_frame_cap_tests" in source:
        raise SystemExit("Android menu frame-cap tests already exist; refusing to duplicate them.")
    source += tests
    path.write_text(source, encoding="utf-8")



def patch_mobile_far_detail(root: Path) -> None:
    graphics_path = root / "crates/horizon-client-render/src/graphics.rs"
    graphics = graphics_path.read_text(encoding="utf-8")
    graphics = replace_once(
        graphics,
        """pub enum FarDetail {
    /// Each tile level starts at eight times its size from the camera.
    #[default]
    Full,
    /// Coarser levels start at half that distance: about a quarter of the
    /// far quads, for GPUs that far terrain slows down. Changes the picture.
    Reduced,
}
impl FarDetail {
    pub fn name(self) -> &'static str {
        match self {
            FarDetail::Full => "full",
            FarDetail::Reduced => "reduced",
        }
    }
    pub fn label(self) -> &'static str {
        match self {
            FarDetail::Full => "Full",
            FarDetail::Reduced => "Reduced",
        }
    }
    fn parse(s: &str) -> Self {
        if s == "reduced" {
            FarDetail::Reduced
        } else {
            FarDetail::Full
        }
    }
}
""",
        """pub enum FarDetail {
    /// Each tile level starts at eight times its size from the camera.
    #[default]
    Full,
    /// Coarser levels start at half that distance: about a quarter of the
    /// far quads, for GPUs that far terrain slows down. Changes the picture.
    Reduced,
    /// Coarser levels start at a quarter of Full's distance: about one-sixteenth
    /// of Full's far quads, intended for phones whose GPUs are geometry-bound.
    Mobile,
}
impl FarDetail {
    pub fn name(self) -> &'static str {
        match self {
            FarDetail::Full => "full",
            FarDetail::Reduced => "reduced",
            FarDetail::Mobile => "mobile",
        }
    }
    pub fn label(self) -> &'static str {
        match self {
            FarDetail::Full => "Full",
            FarDetail::Reduced => "Reduced",
            FarDetail::Mobile => "Mobile",
        }
    }
    fn parse(s: &str) -> Self {
        match s {
            "reduced" => FarDetail::Reduced,
            "mobile" => FarDetail::Mobile,
            _ => FarDetail::Full,
        }
    }
    /// Number of half-distance coarsening steps applied by the far-terrain planner.
    pub fn coarseness(self) -> u8 {
        match self {
            FarDetail::Full => 0,
            FarDetail::Reduced => 1,
            FarDetail::Mobile => 2,
        }
    }
}
""",
        "Mobile far-detail graphics option",
    )

    graphics = replace_once(
        graphics,
        """pub fn lod_distance_capped(distance: u32, info: &wgpu::AdapterInfo) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if lod_capped(info) {
        distance.min(INTEGRATED_LOD_DISTANCE)
    } else {
        distance
    }
}
""",
        """/// Mobile far terrain can use a longer integrated-GPU range because its
/// quadtree emits substantially fewer distant terrain cells.
pub const INTEGRATED_MOBILE_LOD_DISTANCE: u32 = 256;

pub fn lod_distance_capped(
    distance: u32,
    info: &wgpu::AdapterInfo,
    coarseness: u8,
) -> u32 {
    let distance = distance.min(crate::lod::MAX_CHUNKS);
    if !lod_capped(info) {
        distance
    } else if coarseness >= 2 {
        distance.min(INTEGRATED_MOBILE_LOD_DISTANCE)
    } else {
        distance.min(INTEGRATED_LOD_DISTANCE)
    }
}
""",
        "Mobile integrated-GPU LOD distance cap",
    )
    graphics = replace_once(
        graphics,
        "lod_distance_capped(self.lod_distance, info)",
        "lod_distance_capped(self.lod_distance, info, self.far_detail.coarseness())",
        "far-detail-aware LOD distance getter",
    )
    graphics = replace_once(
        graphics,
        """            assert_eq!(g.lod_distance_on(&hd620), capped);
            assert_eq!(g.lod_distance_on(&m4), distance);
            assert_eq!(g.lod_distance_on(&discrete), distance);
""",
        """            assert_eq!(g.lod_distance_on(&hd620), capped);
            assert_eq!(g.lod_distance_on(&m4), distance);
            assert_eq!(g.lod_distance_on(&discrete), distance);
        }
        let mobile = Graphics {
            lod_distance: 1024,
            far_detail: FarDetail::Mobile,
            ..Graphics::default()
        };
        assert_eq!(mobile.lod_distance_on(&hd620), INTEGRATED_MOBILE_LOD_DISTANCE);
        assert_eq!(mobile.lod_distance_on(&m4), 1024);
        assert_eq!(mobile.lod_distance_on(&discrete), 1024);
""",
        "Mobile integrated-GPU distance regression tests",
    )
    graphics = replace_once(
        graphics,
        """        assert_eq!(
            Graphics::parse("farDetail:reduced\\n").far_detail,
            FarDetail::Reduced
        );
        for text in ["farDetail:full\\n", "farDetail:other\\n", ""] {
""",
        """        assert_eq!(
            Graphics::parse("farDetail:reduced\\n").far_detail,
            FarDetail::Reduced
        );
        assert_eq!(
            Graphics::parse("farDetail:mobile\\n").far_detail,
            FarDetail::Mobile
        );
        for detail in [FarDetail::Full, FarDetail::Reduced, FarDetail::Mobile] {
            let parsed = Graphics::parse(&format!("farDetail:{}\\n", detail.name()));
            assert_eq!(parsed.far_detail, detail);
            assert_eq!(Graphics::parse(&parsed.serialize()).far_detail, detail);
        }
        for text in ["farDetail:full\\n", "farDetail:other\\n", ""] {
""",
        "Mobile far-detail serialization tests",
    )
    graphics_path.write_text(graphics, encoding="utf-8")

    ui_path = root / "crates/horizon-client-render/src/graphics_ui.rs"
    ui = ui_path.read_text(encoding="utf-8")
    ui = replace_once(
        ui,
        "g.far_detail = step(&[FarDetail::Full, FarDetail::Reduced], g.far_detail, back)",
        "g.far_detail = step(&[FarDetail::Full, FarDetail::Reduced, FarDetail::Mobile], g.far_detail, back)",
        "Mobile far-detail settings cycle",
    )
    ui_path.write_text(ui, encoding="utf-8")

    lod_path = root / "crates/horizon-client-render/src/lod.rs"
    lod = lod_path.read_text(encoding="utf-8")
    lod = replace_once(
        lod,
        "area: Option<([i32; 2], i32, u32, i32, bool)>",
        "area: Option<([i32; 2], i32, u32, i32, u8)>",
        "LOD plan cache key type",
    )
    lod = replace_once(
        lod,
        """    /// Reduced far detail (set_reduced_detail).
    reduced_detail: bool,
""",
        """    /// Far-terrain coarseness: 0 Full, 1 Reduced, 2 Mobile.
    far_coarseness: u8,
""",
        "LOD far-detail state",
    )
    lod = replace_once(
        lod,
        "            reduced_detail: false,",
        "            far_coarseness: 0,",
        "LOD far-detail default",
    )
    lod = replace_once(
        lod,
        """    /// Reduced far detail (graphics::FarDetail::Reduced): the next update
    /// replans when it changes, and tiles sampled from then on approximate
    /// wide cells (GeneratorWorker::lod_grid).
    pub fn set_reduced_detail(&mut self, reduced: bool) {
        self.reduced_detail = reduced;
        self.shared.approximate.store(reduced, Ordering::Relaxed);
    }
""",
        """    /// Selects Full (0), Reduced (1), or Mobile (2) far-terrain planning.
    /// The next update replans when it changes; coarser modes also sample
    /// wide cells (GeneratorWorker::lod_grid) rather than full-detail columns.
    pub fn set_far_coarseness(&mut self, coarseness: u8) {
        self.far_coarseness = coarseness.min(4);
        self.shared
            .approximate
            .store(self.far_coarseness > 0, Ordering::Relaxed);
    }
""",
        "LOD far-detail setter",
    )
    lod = replace_once(lod, "            self.reduced_detail,", "            self.far_coarseness,", "LOD area cache coarseness")
    lod = replace_once(lod, "            self.reduced_detail,", "            self.far_coarseness,", "LOD planner coarseness")
    lod = replace_once(
        lod,
        """    fn new(camera: [i32; 2], detail: i32, distance: u32) -> Self {
        Self::with_previous(camera, detail, distance, &Self::default(), 0, false)
    }
""",
        """    fn new(camera: [i32; 2], detail: i32, distance: u32) -> Self {
        Self::with_previous(camera, detail, distance, &Self::default(), 0, 0)
    }
""",
        "LOD full-detail plan constructor",
    )
    lod = replace_once(
        lod,
        """        previous: &Self,
        elevation: i32,
        reduced: bool,
    ) -> Self {
""",
        """        previous: &Self,
        elevation: i32,
        coarseness: u8,
    ) -> Self {
""",
        "LOD plan coarseness parameter",
    )
    lod = replace_once(
        lod,
        "result.visit(key, camera, detail, range, previous, reduced);",
        "result.visit(key, camera, detail, range, previous, coarseness);",
        "LOD root visit coarseness",
    )
    lod = replace_once(
        lod,
        """        previous: &Self,
        reduced: bool,
    ) {
""",
        """        previous: &Self,
        coarseness: u8,
    ) {
""",
        "LOD recursive visit coarseness",
    )
    lod = replace_once(
        lod,
        """        // Keep distant cells small on screen; finest tiles retain individual
        // blocks. Reduced keeps them twice as large (a quarter of the cells).
        let split = if previous.split.contains(&key) { 10 } else { 8 };
        // Beyond the old 1024-chunk limit the cells are as large as reduced's:
        // the whole world is 22 levels, not all of them worth full cells.
        let threshold = key.size()
            * if key.distance(camera) >= WORLD_START {
                WORLD_SPLIT
            } else if reduced {
                split / 2
            } else {
                split
            };
        if key.level > 0
            && (key.distance(camera) < detail * 16 + 16
                || key.distance(camera).max(self.elevation) < threshold)
""",
        """        // Each coarseness step halves the distance at which far tiles split:
        // Full keeps 8/10 tile widths, Reduced 4/5, Mobile 2/2.
        let split = if previous.split.contains(&key) { 10 } else { 8 };
        let split = key.size() * split / (2 << coarseness.min(4));
        // The world-wide planner is at least as coarse as this selected mode.
        let threshold = if key.distance(camera) >= WORLD_START {
            split.min(key.size() * WORLD_SPLIT)
        } else {
            split
        };
        if key.level > 0
            && (key.distance(camera) < detail * 16 + 16
                || key.distance(camera).max(self.elevation) < threshold)
""",
        "Mobile far-terrain quadtree split thresholds",
    )
    lod = replace_once(
        lod,
        "self.visit(child, camera, detail, range, previous, reduced);",
        "self.visit(child, camera, detail, range, previous, coarseness);",
        "LOD child visit coarseness",
    )
    call_replacements = [
        ("Plan::with_previous(camera, 12, distance, &Plan::default(), 0, true)",
         "Plan::with_previous(camera, 12, distance, &Plan::default(), 0, 1)"),
        ("Plan::with_previous(camera, 12, MAX_CHUNKS, &Plan::default(), elevation, false)",
         "Plan::with_previous(camera, 12, MAX_CHUNKS, &Plan::default(), elevation, 0)"),
        ("Plan::with_previous(camera, 12, MAX_CHUNKS, &Plan::default(), elevation, true)",
         "Plan::with_previous(camera, 12, MAX_CHUNKS, &Plan::default(), elevation, 1)"),
        ("Plan::with_previous([0, 0], 12, 1024, &Plan::default(), 1024, false)",
         "Plan::with_previous([0, 0], 12, 1024, &Plan::default(), 1024, 0)"),
        ("Plan::with_previous(camera, 8, 64, &before, 0, false)",
         "Plan::with_previous(camera, 8, 64, &before, 0, 0)"),
        ("Plan::with_previous([16, 0], 2, 256, &before, 0, false)",
         "Plan::with_previous([16, 0], 2, 256, &before, 0, 0)"),
    ]
    for old, new in call_replacements:
        lod = replace_once(lod, old, new, "LOD test coarseness call")
    lod = replace_once(
        lod,
        """    #[test]
    fn far_plan_is_bounded_and_near_tiles_align_with_chunks() {
""",
        """    #[test]
    fn mobile_far_detail_is_coarser_than_reduced_without_coarsening_near_terrain() {
        let leaves = |plan: &Plan| {
            plan.nodes
                .iter()
                .filter(|key| !plan.split.contains(key))
                .count()
        };
        for camera in [[0, 0], [100, -40]] {
            for distance in [64, 128, 512, 1024] {
                let full = Plan::new(camera, 12, distance);
                let reduced =
                    Plan::with_previous(camera, 12, distance, &Plan::default(), 0, 1);
                let mobile =
                    Plan::with_previous(camera, 12, distance, &Plan::default(), 0, 2);
                assert!(reduced.split.is_subset(&full.split));
                assert!(mobile.split.is_subset(&reduced.split));
                assert_eq!(mobile.roots, full.roots);
                for key in mobile.nodes.iter().filter(|key| !mobile.split.contains(key)) {
                    if key.distance(camera) < 12 * 16 + 16 {
                        assert_eq!(key.level, 0, "{key:?}");
                    }
                }
                let (reduced, mobile) = (leaves(&reduced), leaves(&mobile));
                assert!(
                    mobile <= reduced && (distance < 128 || mobile < reduced),
                    "{mobile} of {reduced} tiles at {distance}"
                );
            }
        }
    }

    #[test]
    fn far_plan_is_bounded_and_near_tiles_align_with_chunks() {
""",
        "Mobile far-detail quadtree regression tests",
    )
    lod_path.write_text(lod, encoding="utf-8")

    game_path = root / "crates/horizon-client/src/game.rs"
    game = game_path.read_text(encoding="utf-8")
    game = replace_once(
        game,
        """    /// LOD Distance and Far Detail Reduced as chosen: the player's LOD
    /// Distance when it is on, else a mod's far terrain
    /// (render.set-far-terrain).
    fn far_terrain_choice(&self) -> (u32, bool) {
        let g = &self.settings.horizon;
        let reduced = g.far_detail == horizon_client::graphics::FarDetail::Reduced;
        if g.lod_distance > 0 {
            return (g.lod_distance, reduced);
        }
        match self.mod_far_terrain() {
            Some(f) => (f.distance, reduced || f.reduced),
            None => (0, reduced),
        }
    }
""",
        """    /// LOD Distance and Far Detail as chosen: the player's LOD Distance
    /// when it is on, else a mod's far terrain (render.set-far-terrain).
    fn far_terrain_choice(&self) -> (u32, u8) {
        let g = &self.settings.horizon;
        let coarseness = g.far_detail.coarseness();
        if g.lod_distance > 0 {
            return (g.lod_distance, coarseness);
        }
        match self.mod_far_terrain() {
            Some(f) => (f.distance, coarseness.max(u8::from(f.reduced))),
            None => (0, coarseness),
        }
    }
""",
        "Game far-terrain coarseness selection",
    )
    game = replace_once(
        game,
        "horizon_client::graphics::lod_distance_capped(distance, &renderer.info)",
        "horizon_client::graphics::lod_distance_capped(distance, &renderer.info, self.far_terrain_choice().1)",
        "Game LOD distance uses selected far detail",
    )
    game = replace_once(
        game,
        "let (far_distance, far_reduced) = self.far_terrain_choice();",
        "let (far_distance, far_coarseness) = self.far_terrain_choice();",
        "Game frame far-terrain coarseness",
    )
    game = replace_once(
        game,
        "horizon_client::graphics::lod_distance_capped(far_distance, &renderer.info);",
        "horizon_client::graphics::lod_distance_capped(far_distance, &renderer.info, far_coarseness);",
        "Game frame LOD distance cap",
    )
    game = replace_once(
        game,
        "lod.set_reduced_detail(far_reduced);",
        "lod.set_far_coarseness(far_coarseness);",
        "Game frame LOD coarseness setter",
    )
    game_path.write_text(game, encoding="utf-8")

def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: patch_starling_090_performance.py HORIZON_SOURCE")

    root = Path(sys.argv[1]).resolve()
    patch_frame_pacing(root)
    patch_android_menu_cap(root)
    patch_mobile_far_detail(root)
    print("Starling 0.9.0: frame pacing, Android menu cap, and Mobile far detail applied.")


if __name__ == "__main__":
    main()

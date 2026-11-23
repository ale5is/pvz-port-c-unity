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


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit("usage: patch_starling_090_performance.py HORIZON_SOURCE")

    root = Path(sys.argv[1]).resolve()
    patch_frame_pacing(root)
    patch_android_menu_cap(root)
    print("Starling 0.9.0: fixed-grid frame pacing and Android's 60 FPS menu cap applied.")


if __name__ == "__main__":
    main()

#pragma once

#include <algorithm>
#include <chrono>
#include <cstdint>

namespace pavo {

struct Gles2LodFade {
    using Clock = std::chrono::steady_clock;
    using TimePoint = Clock::time_point;

    static constexpr std::uint32_t kHeld = 65u;
    static constexpr std::uint32_t kSteps = 64u;
    static constexpr std::uint32_t kLevelStride = 129u;
    static constexpr std::uint32_t kFadeOutOffset = 64u;
    static constexpr std::uint8_t kMaximumLevel = 22u;

    static std::uint32_t progress(TimePoint since, TimePoint now) {
        if (now <= since) return 0u;
        const auto elapsed = std::chrono::duration_cast<std::chrono::microseconds>(
            now - since).count();
        constexpr std::int64_t duration_us = 600000;
        if (elapsed >= duration_us) return kSteps;
        return static_cast<std::uint32_t>(
            (elapsed * static_cast<std::int64_t>(kSteps)) / duration_us);
    }

    static std::uint32_t fade_in(TimePoint shown, std::uint8_t level, TimePoint now) {
        if (level > kMaximumLevel) return 0u;
        if (now >= shown + std::chrono::milliseconds(600)) return 0u;
        const std::uint32_t shown_step = progress(shown, now) + 1u;
        if (shown_step >= kSteps) return 0u;
        return 1u + static_cast<std::uint32_t>(level) * kLevelStride + shown_step;
    }

    static std::uint32_t fade_out(TimePoint since, std::uint8_t level, TimePoint now) {
        if (level > kMaximumLevel) return 0u;
        const std::uint32_t step = std::min(progress(since, now) + 1u, 64u);
        return 1u + static_cast<std::uint32_t>(level) * kLevelStride +
            kFadeOutOffset + step;
    }

    static constexpr std::uint32_t level(std::uint32_t code) {
        return code == 0u ? 0u : (code - 1u) / kLevelStride;
    }

    static constexpr std::uint32_t step(std::uint32_t code) {
        return code == 0u ? 0u : (code - 1u) % kLevelStride;
    }

    static constexpr bool is_fade_out(std::uint32_t code) {
        return code != 0u && step(code) > kFadeOutOffset;
    }
};

}  // namespace pavo

#pragma once

#include <algorithm>
#include <chrono>
#include <cstdint>

namespace pavo {

// Mirrors gfx/lod.rs's 600 ms transition encoding. Zero means fully shown.
struct Gles2LodFade {
    using Clock = std::chrono::steady_clock;
    using TimePoint = Clock::time_point;

    static constexpr std::chrono::milliseconds kDuration{600};
    static constexpr std::uint32_t kHeld = 65;

    static std::uint32_t fade_in(TimePoint shown, std::uint8_t level, TimePoint now) {
        const float elapsed = std::chrono::duration<float>(now - shown).count();
        const std::uint32_t progress = static_cast<std::uint32_t>(
            std::max(0.0f, elapsed) * (64.0f / 0.6f));
        if (progress >= 63u) return 0u;
        return 1u + static_cast<std::uint32_t>(level) * 129u + progress + 1u;
    }

    static std::uint32_t fade_out(TimePoint since, std::uint8_t level, TimePoint now) {
        const float elapsed = std::chrono::duration<float>(now - since).count();
        const std::uint32_t progress = static_cast<std::uint32_t>(
            std::max(0.0f, elapsed) * (64.0f / 0.6f));
        return 1u + static_cast<std::uint32_t>(level) * 129u + 64u +
            std::min(progress + 1u, 64u);
    }

    static constexpr std::uint32_t level(std::uint32_t code) {
        return code == 0u ? 0u : (code - 1u) / 129u;
    }

    static constexpr std::uint32_t step(std::uint32_t code) {
        return code == 0u ? 0u : (code - 1u) % 129u;
    }
};

}  // namespace pavo

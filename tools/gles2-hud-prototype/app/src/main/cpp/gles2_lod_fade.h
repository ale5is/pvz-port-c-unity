#pragma once

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdint>

namespace pavo {

struct Gles2LodFade {
    using Clock = std::chrono::steady_clock;
    using TimePoint = Clock::time_point;

    // Unscoped enum constants have no separate storage in C++14. This keeps
    // the helper safe in the standalone -std=c++14 native test target.
    enum : std::uint32_t {
        kHeld = 65u,
        kSteps = 64u,
        kLevelStride = 129u,
        kFadeOutOffset = 64u,
        kMaximumLevel = 22u,
        kDurationMicroseconds = 600000u
    };

    static std::uint32_t progress(TimePoint since, TimePoint now) {
        if (now <= since) return 0u;
        const auto elapsed = std::chrono::duration_cast<std::chrono::microseconds>(
            now - since).count();
        if (elapsed >= static_cast<std::int64_t>(kDurationMicroseconds)) return kSteps;
        return static_cast<std::uint32_t>(
            (elapsed * static_cast<std::int64_t>(kSteps)) /
            static_cast<std::int64_t>(kDurationMicroseconds));
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

    // Matches terrain.wgsl's stable per-column hash. Unsigned arithmetic is
    // intentional: the shader performs the same wrapping 32-bit operations.
    static std::uint32_t column_hash(std::int32_t cell_x, std::int32_t cell_z) {
        std::uint32_t hash =
            static_cast<std::uint32_t>(cell_x) * 73856093u ^
            static_cast<std::uint32_t>(cell_z) * 19349663u;
        hash = (hash ^ (hash >> 13u)) * 0x5bd1e995u;
        return hash ^ (hash >> 15u);
    }

    static float column_start(std::int32_t cell_x, std::int32_t cell_z) {
        return static_cast<float>(column_hash(cell_x, cell_z) & 1023u) / 2048.0f;
    }

    static std::uint32_t bayer_value(std::uint32_t x, std::uint32_t y) {
        static constexpr std::uint8_t matrix[16] = {
            0u, 8u, 2u, 10u, 12u, 4u, 14u, 6u,
            3u, 11u, 1u, 9u, 15u, 7u, 13u, 5u
        };
        return matrix[((y & 3u) << 2u) | (x & 3u)];
    }

    static float bayer_threshold(std::uint32_t x, std::uint32_t y) {
        return (static_cast<float>(bayer_value(x, y)) + 0.5f) / 16.0f;
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

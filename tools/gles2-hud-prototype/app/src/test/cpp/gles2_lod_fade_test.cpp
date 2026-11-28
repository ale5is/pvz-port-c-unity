#include "gles2_lod_fade.h"

#include <cassert>
#include <chrono>

int main() {
    using Fade = pavo::Gles2LodFade;
    const auto start = Fade::TimePoint{};
    const auto at = [start](std::chrono::milliseconds ms) { return start + ms; };

    assert(Fade::fade_in(start, 3, at(std::chrono::milliseconds(0))) == 1u + 3u * 129u + 1u);
    assert(Fade::level(Fade::fade_in(start, 10, at(std::chrono::milliseconds(300)))) == 10u);
    assert(Fade::step(Fade::fade_in(start, 10, at(std::chrono::milliseconds(300)))) >= 32u);
    assert(Fade::fade_in(start, 10, at(std::chrono::milliseconds(600))) == 0u);
    assert(Fade::fade_in(at(std::chrono::milliseconds(100)), 2, start) == 1u + 2u * 129u + 1u);

    assert(Fade::fade_out(start, 3, at(std::chrono::milliseconds(0))) == 1u + 3u * 129u + 65u);
    assert(Fade::level(Fade::fade_out(start, 3, at(std::chrono::milliseconds(300)))) == 3u);
    assert(Fade::step(Fade::fade_out(start, 3, at(std::chrono::milliseconds(300)))) >= 96u);
    assert(Fade::step(Fade::fade_out(start, 3, at(std::chrono::milliseconds(700)))) == 128u);
    assert(Fade::kHeld == 65u);
    return 0;
}

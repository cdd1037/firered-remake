# Intro movie presentation

The opening movie uses locally retained pret/pokefirered revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`, `src/intro.c` and `graphics/intro`. The offline importer does not execute reference code. Regenerate and check:

```
python3 scripts/imports/presentation/import-intro-movie.py
python3 scripts/imports/presentation/import-intro-movie.py --check
```

Runtime reads only `content/generated/ui/intro-movie`. The package holds independent tilemap layers and sprite strips, including the four-part Gengar OBJ assembled once. It does not bake full-screen animation frames, store source hashes, or read raw source. Palette-bank selection and horizontal/vertical tile flips are resolved offline. Runtime uses wrapping layer offsets, sprite frame regions, transforms, fades and theatrical masks.

Coverage: unskippable copyright; opening theatrical window; star and sparkle trail; Game Freak name/logo/presents reveal; animated close-up grass and forest zoom; opposing parallax forest/plants with the wide-shot Pokémon; vertical opposing close-up pan; battle entrance and foreground grass; Nidorino cry; Gengar wind-up/swipe; Nidorino recoil/dust/return hop; two preparation hops; shake, charge and jump; white background fade, Pokémon zoom, black fade and title handoff. Enter/A, Tab/Start and Space/Select skip after copyright. Backspace/B does not skip. R is accepted as the current shared input adapter's normalized Select alias. Escape remains the host's quit control.

The BCL presentation clock advances at 60 Hz and is independent of gameplay/session/RNG/save state. Sparkle randomness has a private fixed source seed. Source callback waits and sprite animation values are represented; DMA/setup waits are collapsed, and palette timing plus some particle/scanline behavior is approximated rather than claiming frame-exact hardware emulation. The source's title restart path can restart the movie; ordinary Continue/new-game/naming flows are owned by opening integration. This document does not claim native-window visual or real-speaker audio acceptance.

Tests: `IntroMoviePresentationTests` exercises stage coverage, delta partition invariance, source skip rules, invalid clock input and generated-only load. `IntroMovieImportTests` checks byte-exact regeneration of the compact generated package.

Music cues use the shared synthesized MUS_GAME_FREAK and MUS_INTRO_FIGHT playback. Nidorino now uses the source CRY_MODE_DOUBLES (1) preset: length 20, release 225, pitch 15360, chorus 0, volume 120 and pan 63, without BGM ducking. `SpriteCB_NidorinoCry` waits nine crouch callbacks and plays the cry on the next callback; the presentation clock queues it at Cry frame 10. Consuming that cue is once-only even when a delta crosses the whole Cry stage. Skipping clears a pending cue, a new movie arms it again, and a completed movie does not play a late catch-up cry.

The host resolves `CRY_<species>_DOUBLES` from that species' existing normal binding on demand. It does not duplicate species metadata or mutate the stored 1117-entry catalog. Shared normal (0) and encounter (2) entrypoints retain their parameter and mode contracts; unsupported species/modes have no normal-cry fallback.

Focused validation passed 60 audio tests, 11 intro/opening/content checks, 14 title/naming/capture presentation checks, two opening/intro importer checks and 13 audio-importer checks. An actual Godot 4.7.2 / .NET 10.0.12 host probe passed 14 checks, including right pan, no duck/restart, delta crossings and catalog non-mutation. A native-window run recorded one Nidorino start/finish pair without an audio fault. Audio used Dummy; these results do not verify real speakers or listening quality.


The 2026-10-16 native sample found inverted grayscale indices in the Game Freak name texture. Its only L-mode source PNG now follows the pinned gbagfx nibble inversion, with white source pixels becoming transparent index 0 and black glyph pixels becoming palette 15. The shared helper also covers the title border and NPC wireless signal. `scripts/checks/test-opening-grayscale-import.py` independently checks all intro L-mode inputs, title-border pixels and all 32 wireless signal frames; three tests pass and reject the old mapping. Regeneration passes; the corrected strip was inspected, without claiming an additional native movie walkthrough.

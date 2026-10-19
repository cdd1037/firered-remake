# Field Flash window raster

Generate the local fixture explicitly from the pinned source input:

```sh
python tests/Reference/generate_field_flash_window_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/field-flash-window-source-vectors.json
```

The generator extracts only `SetFlashScanlineEffectWindowBoundary` and
`SetFlashScanlineEffectWindowBoundaries` from `src/field_screen_effect.c` at the
repository's pinned source revision. It compiles an authored C host with the
existing portable compiler helper. It never runs upstream scripts or the managed
implementation. The source hash belongs to this import/test tool, not runtime
data. Output remains under the existing ignored `Fixtures` directory; ordinary
managed tests fail with the command above when it is missing and do not compile C.

All 201 supported radii, 0 through 200, are represented by their complete 160
visible left and right endpoints. This includes source level radii 24, 40, 56,
72 and 200, all 89 even illumination radii 24 through 200, and the odd neighboring
and zero radii. The extra source y=160 line is recorded separately and a canary at
y=161 rejects writes beyond the source buffer boundary.

The shared sampler keeps source integer update order, its repeated writes at the
circle's poles, and register clamps 0 through 255. Coordinates are centered on
(120,80); right endpoints remain exclusive. Screen clipping to width 240 is a
renderer responsibility. Radius zero gives an empty window, whereas source Flash
level zero disables the window in the surrounding presentation state. Sampling
clears all visible lines and does not mutate destination tails or prior buffers.

This oracle validates window boundaries, not native screenshots, palette behavior,
or field-task timing. The ordinary field mask is black outside WIN0 because
`fieldmap.c:LoadTilesetPalette` installs black at primary palette entry zero.
`overworld.c:InitOverworldGraphicsRegisters` keeps BG0 enabled outside WIN0, so
ordinary UI is drawn after the field mask rather than being erased by it.

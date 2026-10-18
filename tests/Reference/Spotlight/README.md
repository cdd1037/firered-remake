# Encore / Flatter source oracle

`generate.py` verifies every input in `pinned-inputs.json` against the locally
pinned pret/pokefirered revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
It reuses the shared bounded extractor and native sprite frame/affine harness;
no source repository script is executed and no managed playback supplies expected
values. Every executable is compiled at both `-O0` and `-O2` with exact parity.

Run from the checkout:

```
python tests/Reference/Spotlight/generate.py --root . \
  --output tests/Game.Presentation.Tests/Fixtures/spotlight-source-vectors.json
```

Outputs are ignored generated data. Existing fixtures are never overwritten.
`--importer` supports external staging while resolving the common parser from the
checkout; no second source-script parser or move-support table is introduced.

## Coverage

- 46 isolated callback cases / 3,444 frames: all four attacker slots, source
  target positioning, Encore sway phases, Flatter open delay 80 and boundary 1,
  both hands and silent OBJ-window duplicates, all 12-tick clapping phases,
  confetti selectors and three seed boundary values.
- 38 sequential native confetti trajectories (1,216 frames), preserving the
  authored selector order and all 114 presentation RNG draws.
- Original CreateSpotlight/RemoveSpotlight WININ, WIN1, DISPCNT register writes.
- Seven hardware fade cases / 350 Begin, Update, Transfer rows. These include
  authored delay-3 fades in both directions, endpoint equality, zero delay,
  lighten mode, and six-bit delay narrowing at 64 and 255. Rows expose active,
  delay counter, coefficient, target, finishing/reset, mode and actual registers.
- Original load/delay/wait scheduling plus original hardware task and palette
  callbacks establish births: Encore's five sprites at frame 48; Flatter mask
  48, dips 50/62 and all 38 confetti at 64. Canonical parsed calls are expanded
  without synthetic waits. After the sole initial hardware wait, only authored
  delays precede the final births, so unrelated visual callbacks cannot affect
  this prefix. Complete-runtime tests separately require all owners to drain.

Platform boundaries are explicit: battler coordinates are fixed source doubles
layout inputs; GPU registers are stored; sound calls counted; destruction marks
the isolated slot dead. The original frame/affine interpreter and callbacks are
extracted unchanged. Zero-scale affine division uses the existing reviewed
`global.h` UBFIX behavior, matching the shared managed affine player. BIOS matrix
geometry is already covered by shared matrix/composition tests. The fixture
compares original scales, rotation and END handoffs rather than inventing a BIOS
matrix oracle. Dummy END frame programs are visually normalized to an immutable
single-frame loop by the catalog; both retain the same tile.

The original shared RNG's gameplay/VBlank coupling is deliberately excluded:
these visual callbacks use the existing explicit presentation stream seeded by
Application, while retaining original Random() arithmetic and draw order.

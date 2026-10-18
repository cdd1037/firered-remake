# Item exchange, Recycle and flying particles

Generate the representative native corpus explicitly:

```sh
python tests/Reference/ItemWind/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/item-wind-source-vectors.json
```

The tracked generator contains the case definitions. It extracts bounded callbacks,
frame/affine commands and the existing sprite interpreter from the pinned local
037335f4 source. No upstream build script is executed. Both O0 and O2 must produce
identical rows. Every selected case runs through destruction; no frame sampling.
The representative profile has 115 cases and 11,671 rows.

Trick covers both authored bags, fall-state/phase boundaries, affine restart,
front/back subpriority switches and terminal blinking. Recycle covers every alpha
write, both fade directions, hold/destruction, heights and the top clamp. Silver
Wind uses all three original affine programs, every authored trajectory, both
sides, all shared positional modes and selector/speed boundaries. GetTargetSide
uses its original immediate command and preserves other argument registers.

Coordinate/picture inputs and BG priorities are explicit harness inputs; BIOS
matrix output is not fabricated. The comparator checks native affine state and
the existing matrix player separately. Source dummy END sentinels leave the
initial tile and END flag unchanged. Full roots use the canonical resolver,
materials and compositor in the maintained Presentation tests.

For all singles/doubles actor-target permutations, pass `--full` and write to an
external directory. Point `FIRERED_SOURCE_VECTOR_DIR` there to use the same managed
comparator. Generated JSON, binaries and extracted C are local ignored data.

# Fixed-damage family proof

Run `bash tests/Game.FixedDamage.Tests/run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT`.
Requires the authenticated complete product content, fixed original source commit
037335f4c725d7c9aecdac87066f2002b4bd7e14, existing scripts/env.sh and locked
.toolhome/recovery-local-only.config. No installs, upstream scripts or ROM are used.

This harness verifies one shared canonical singles/four-slot family, with real position
selection, immunity, PP/Pressure, Protect/Endure, contact, HP-one behavior, exact Psywave
RNG, actual KO/reward continuation and atomic failures. All fixtures use authentic
species/abilities and naturally learned moves. The C oracle is controlled host scaffolding
around six byte-identical original command bodies and authenticated script order.

Immutable inputs:
- singles-baseline-3c78f0a.json: 384 complete state/event/RNG-result hashes captured by
  compiling the test driver against unmodified product 3c78f0a before implementation.
- known-base-super-fang.json: original successful ordinary canonical public-call
  reproduction of the missing mandatory secondary-effect RNG draw. Its red result
  is retained; the golden is not changed to match the corrected implementation.
- source-vectors.json: 1,376 original C outputs, authenticated source hashes and source
  script segments. The runner regenerates it and requires exact bytes. Optimized and
  UBSan outputs must be identical. The generator reuses the existing extraction helper
  and C scaffold from Game.SourceFocusBand.Tests/source_probe.py and source-prefix.h.

The candidate asserts 347 old full-result hashes unchanged; exactly 37 Super Fang
cases must differ for the proven source correction and still obey the explicit source
command assertions. Public mechanics admission for Wonder Guard, Substitute and
four-slot Focus Band remains outside this batch.

To independently reproduce the frozen base, compile this same Program.cs and project
against selected unmodified 3c78f0a Domain/Content source, then pass --capture-3c78 and
write to a new output path. That mode rejects an assembly containing the new family.
Compare the newly produced file with the immutable baseline; never replace the latter.

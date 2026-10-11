# Genuine four-slot action switching

See `docs/source-four-slot-action-switches.md` for the shared recipe, original source contract, continuation authority and remaining boundaries.

Run `bash tests/Game.FourSwitching.Tests/run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT`.

The runner verifies real short complete turns, six bounded original-C source command comparisons, and exact state/event/RNG output against immutable old singles source `92a781d`. It copies source only into the isolated evidence directory, reuses authenticated content and tools, and takes `/tmp/firered-remake-build.lock` only during each build. Failure logs from development are retained separately from final acceptance. The actual four-slot suite requires the compiled source oracle path as its third argument; the parity suite takes content root and a new output JSONL filename.

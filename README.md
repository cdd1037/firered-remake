# FireRed remake — source-only snapshot

This private code archive exports fixed source snapshots, without the original repositories’ Git history:

- `firered-remake/`: `a08317fe445d44b12cc4100b645091f2427d2254`
- `firered-audio-player/`: `38b3b6ef421e3b3e3e3ced5cafa0d505859bdb26`

Both projects are preserved as adjacent directories. Source code and project references are unchanged. Run project commands from their own directory; start with `firered-remake/README.md` and `firered-remake/dev`. The custom audio player’s core, command-line player, demo, source tools, and technical specifications are included. The game also includes its committed `OriginalAudioCore_*.cs` adapter sources.

## Resources and reproducibility

This is a source-only archive, not a runnable packaged game. Raw/generated game content, graphics, images, sound/music samples, ROMs, save files, recorded evidence, generated fixtures, caches, compiled binaries, and previous Git history are excluded. Some tests depend on intentionally omitted fixtures or resources and cannot run from this archive alone.

Restore required resources from the private stage backup, or rebuild the content using the pinned source-lock files and import recipes where available, before attempting full builds, gameplay or integration tests. This archive does not grant rights to obtain or distribute third-party game resources. Existing project documentation may describe files or historical commits absent from this snapshot; those references are retained for technical context.

`SOURCE_MANIFEST.json` indexes `source-manifest/*.json`, which record source commit, original path, Git blob identity, size and SHA-256 for every unchanged exported source file. This README and the manifest are export metadata and do not attest to a successful build. No source logic was refactored for this export.

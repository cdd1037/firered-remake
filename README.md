# FireRed remake: source-only private snapshots

Current fixed source commits:

- `firered-remake/`: `864ca433b42e9dfa46043ff0a1782601b89400c2`
- `firered-audio-player/`: `70a77d7bcdf1a1ffe3bfd56320083a19299a670f`

Both projects remain adjacent, with unchanged source logic and paths. Start in each project's README; run game commands from `firered-remake/`.

This is not a runnable packaged game. Raw/generated resources, media, ROMs, saves, generated fixtures, recorded evidence, binaries, caches and old Git history are excluded. Restore required resources from the private stage backup or use available import recipes where supported. The resource reconstruction workflow is incomplete; this export does not promise full reproducibility or grant third-party resource rights. Some source tests cannot run without omitted inputs. No build or visible GUI verification is claimed by the export process.

`SOURCE_MANIFEST.json` indexes immutable baseline parts and ordered delta files. Resolve baseline records, remove each delta's deleted paths, then replace/add its upserted records. Unchanged records keep their original source commit; the top-level repositories map identifies this snapshot. Each source record includes exact size, SHA-256 and Git blob identity. Prior project documentation can refer to absent assets, local paths or historical commits.

`tools/source-sync/source_sync.py` performs offline fixed-object exports and incremental upload planning. It never uploads or stores credentials. See its README before use.

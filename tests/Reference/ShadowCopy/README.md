# Shadow-copy source oracle

Nightmare's native initializer, phase callback, raw clone helper, fixed-point translation, and source sheet-aware destructor are extracted unchanged from the pinned local source. An authored bounded hardware fixture runs them under C `-O0` and `-O2`; both outputs must match before any numerical fixture is written. The generator refuses to overwrite an existing fixture. No upstream scripts or network inputs are used.

Generate explicitly from the repository root:

    python tests/Reference/ShadowCopy/generate.py content/raw/pokefirered/037335f4 --repo . --output /tmp/shadow-copy-candidate.json

The reviewed fixture has 36 cases / 2,052 rows. Successful tasks terminate at frame 84; missing target or full scene exits in the initializer. Both actor sides, four-position geometry, hidden-but-present targets, capacity failure, and nonidentity native battler/sprite mappings are covered. The runtime does not adopt those native numeric sprite IDs.

Spite's scanline dependency is unavailable locally and is not admitted. Role Play's material selection requires exact opposite-party identity and is not counted by this oracle.

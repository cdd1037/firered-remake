# Contextual HELP content

`HelpCatalog` reads only `content/generated/ui/help/help.json`. It validates immutable definitions once at load time, then projects the ordered topics and answers from the current authoritative `HelpProgress`. UI state and audio are separate consumers; querying HELP does not mutate progression.

The single document contains the complete original text vocabulary, paired questions/answers, context topic flags and ordering, basic-term order, item/flag/caught-count conditions, and map classifications. It contains no source paths, source hashes, conversion receipts or file inventories. All 425 source maps are classified, including currently unused maps; no authored regional allowlist is used.

## Original behavior

- Topic order is About this game, What should I do, How do I do this, Terms, Type matchup, Exit; each context enables its original subset.
- Available questions depend on the actual visited/story/badge flags, Town Map possession, and Kanto caught count. Switch/Shift requires more than one Kanto species caught.
- HMs use the seven original acquisition flags, including HM07's Icefall Cave pickup flag, rather than inferring a received-HM history from current inventory.
- After `FLAG_DEFEATED_BROCK`, basic terms already present in a context are removed before the complete original basic-term sequence is appended. Reopening HELP never accumulates duplicates.
- Surfing takes precedence over dungeon and indoor classifications. Dungeon ranges come from original map groups; Tanoby chambers become dungeon context only after the original unlock flag. Centers, marts, gyms, home and Oak's Lab follow the original tables and precedence.
- `{PLAYER}` and `{STR_VAR_1}` are rendered from the current player name and Bill's PC discovery flag. Original revision-zero text is selected where the source has revision branches. Original line/prompt controls become HELP line breaks, as in its dedicated text renderer.

## Regeneration and verification

Pinned inputs are read from the project's retained raw tree. No upstream scripts execute. The importer supports a selected raw directory or pinned Git reference and rejects source/output overlap and symlinked output paths.

```sh
python scripts/imports/rebuild-resources.py help
python scripts/imports/rebuild-resources.py help --check
python scripts/checks/test-help-system-import.py
source scripts/env.sh
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~HelpCatalogTests
```

Source checks compare the complete document and mutate original text, topic order, flags, HM conditions and map metadata to prove the output follows inputs. Content tests load from a generated-only temporary root, check source classifications, exercise unlock boundaries, and verify Brock's duplicate suppression. The importer check is also registered in `Game.ResourceImport.Tests`.

2026-10-16 focused evidence: Python checks 3/3; serial Content.Tests build zero warnings/errors; HELP content tests 4/4 on runtime major 10. This is content evidence, not native-screen or audible-output verification. The generated document occupies 135,814 bytes, containing 369 text labels, 177 paired entries and 36 contexts.

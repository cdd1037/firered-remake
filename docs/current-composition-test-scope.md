# Current composition smoke

`tests/Game.Application.Tests/Campaign/CampaignCompositionTests.cs` loads the current game, creates a seeded New Game,
round-trips a save through the production codec, restores it, and executes a public movement
command. It checks actual maps and move definitions rather than a historical resource digest
or a fixed count from an earlier milestone.

Run after the current product build:

```sh
source scripts/env.sh
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --filter FullyQualifiedName~CampaignCompositionTests
```

An optional final argument is a real current-rules checkpoint. That mode reads the old content
version explicitly, validates its state, and writes a new file with the current content version.
The payload and generation remain unchanged; the source is never overwritten. This is a
one-time test-local checkpoint conversion, not automatic fallback in the game.

Source-authentication assertions and the old `baseline-a52074b.json` were retired during the
deep simplification. Their earlier revisions remain in Git. Current gameplay assertions,
normal format errors and save validation remain applicable. This short check makes no native
rendering/audio or full-route claim.

# Native party presentation

The field party menu and script-owned party selectors (Name Rater, Daisy, size records, NPC trade and Day Care) share `CampaignPartyArtView`. The single-party arrangement uses a lead panel on the left, five 24px panels on the right, and the lower-right CANCEL button. Empty slots do not accept input. Left returns to the lead; right restores the last occupied right-column slot, bounded to the current party size. Up/down include CANCEL.

`PartyPresentationAssets` holds the engine-independent geometry/navigation used by the renderer and tests. Service handlers still own eligibility, identity checks and authoritative commands; neither rendering nor cursor movement changes party data. Field swapping continues through `party_swap`. Summary uses its existing separate presentation.

The compact atlas is imported from retained pret commit `037335f4c725d7c9aecdac87066f2002b4bd7e14` party graphics and palette definitions. It includes normal/selected, fainted/selected-fainted, switching/selected-switching and egg/no-HP panels, held item/mail markers, open/closed Poké Balls, background and cancel buttons. Runtime reads only `content/generated/ui/party-screen/atlas.png`; no runtime source files or source hashes are required.

Regenerate and check:

```sh
python scripts/imports/presentation/import-party-screen.py
python scripts/checks/test-party-screen.py
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~PartyPresentationTests
```

Battle-specific selectors still use their existing compact geometry and event-time HP paths. Their battle message overlay and native party bottom panel need a coordinated transition before reusing the full-screen skin. This change does not claim battle-screen parity, full-game completion or actual-audio acceptance.

## Actual window check

2026-10-16, MVID `64302ba8-7caa-4c08-a564-211869c5b329`: checked both NPC exchange selection and ordinary field-party screens. Native asymmetric panels, selected main member, five right-side members, egg without HP, held-item markers, numeric HP and cancel region are visible. Exchange changed PRIZE to MIMIEN exactly once. This is a representative window check, not all battle/service layouts.

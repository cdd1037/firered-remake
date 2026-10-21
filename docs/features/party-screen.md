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

## Target and battle adoption

Field medicine, evolution stones, TM/HM compatibility, held-item targets, Mailbox GIVE, Move Tutor, Reminder and Deleter party selection now use this same renderer. Description-bearing slots reuse the no-HP panel: TM/HM shows the existing ABLE / NOT ABLE / LEARNED result. Following `DisplayPartyPokemonDataForMoveTutorOrEvolutionItem`, evolution stones keep HP for eligible members and show NO USE for ineligible members. Existing commands and stale-target checks remain authoritative.

First-rival tutorial party/medicine, single battle replacement/switch/medicine and the Teachy TV demonstration share the single layout. Double battle switch/medicine uses the source double layout: two 80x56 left panels and four 144x24 right panels. The renderer does not reorder party indices. Directional navigation and pointer hit regions use the same layout object; forced replacement has no selectable CANCEL.

Displayed body/status snapshots preserve owned identity, held mail and acquisition metadata. Single battle uses the current displayed active body/status; doubles use the current frame party, with existing interpolated medicine HP/status overriding only the affected member. Native prompt and dialog frames replace battle-text overlays while a party selector is visible. Move-PP rows and move-forgetting screens retain their separate layouts.

The expanded batch passed a full serial solution build and all six Party presentation tests. The bounded transaction suite passed 66 tests plus two public trainer/double-flow tests. The later Mailbox/Tutor/first-rival additions passed the 15:36 integration solution build (zero warnings/errors), six Party tests and 18 FirstBattleCurrentCatalog tests. No dedicated public Mailbox/Tutor command suite was found; expanded representative native checks remain pending. This does not claim full-game completion or actual-audio acceptance.

## Actual window check

2026-10-16, MVID `64302ba8-7caa-4c08-a564-211869c5b329`: checked both NPC exchange selection and ordinary field-party screens. Native asymmetric panels, selected main member, five right-side members, egg without HP, held-item markers, numeric HP and cancel region are visible. Exchange changed PRIZE to MIMIEN exactly once. This is a representative window check, not all battle/service layouts.

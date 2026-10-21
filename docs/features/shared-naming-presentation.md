# Shared naming presentation

Player/rival names, ordinary and Safari capture nicknames, gifts, hatch names, Name Rater and PC box names use one source naming presenter and one native text editor. The existing Application commands still own identity checks, validation and commits.

The maintained opening producer reads the five `NamingScreenTemplate` bindings in pinned `naming_screen.c` and their shared layout. Generated templates supply the title, species-name prefix, icon selector/position, gender marker, initial keyboard page and character limit. Player/rival remain seven characters, boxes eight, and Pokémon ten. The PC icon is assembled from the source subsprite definitions and two source graphics; existing opening assets are retained unchanged.

The shared keyboard preserves source navigation and page animation while the host LineEdit retains direct typing, IME, selection and caret editing. Context adapters preserve existing behavior: opening Escape returns to the prior entry step; gift/hatch Escape keeps the original name; Name Rater/box naming keep editing; ordinary capture retains the host quit behavior. Empty submission and all actual naming transactions remain governed by their existing Application rules. This is not a Chinese-input/content implementation.

The prior generic nickname panels and separate opening editor are removed rather than kept as alternate render paths. Naming input is isolated from field/battle/menu commands, and the shared screen releases focus on exit. Source metadata does not introduce a new item, move, acquisition route or save field.

## Verification

Full solution build passed with zero warnings/errors. Focused Presentation passed29/29 (including9 shared naming cases); Content5/5, ResourceImport2/2, existing capture/gift/hatch Application15/15, and maintained layout28/28 passed. The importer checks exact source templates/layout and independent PC/layer pixels, rejects altered selectors/bindings/tiles, and loads generated-only data. All44 pre-existing opening PNGs remain byte-identical.

The native candidate is Godot MVID `e90e2df1-86fc-490a-9a05-e1bddae272c1`, SHA256 `d0735a63ea3d6e151baabe9e8a53a4a7aba93779f0a243f3445d980cd7f0ca8e`. Native outcomes are recorded after the bounded window; managed tests alone do not establish native IME or every possible naming flow.

The bounded native window passed on that exact DLL: PC blank submission preserved BOX1, direct typing/deletion plus a mouse lowercase key produced Test, and mouse OK committed Test to the box title. An ordinary Route 1 Pidgey capture succeeded on the third Ultra Ball; its icon, male marker, species title and shared keyboard rendered, and direct Bird plus Enter committed the nickname and returned to the field. All 11 PC and 25 capture commands were accepted, with no runtime error/exception. Text survived page changes and clock updates. Help hide/restore and IME composition were not independently exercised. Both game processes closed and the DLL hash remained unchanged. Evidence: `/workspace/shared/firered-naming-native-20261017/REPORT.md`.

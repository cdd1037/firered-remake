# Story import boundary

Run `source scripts/env.sh && python3 scripts/import-story.py` from the project root.
Use `--check` to compile independently and compare the checked-in artifact without changing it.

`recipe.json` is the ordered import recipe: source paths and provenance, the selected
symbol inventory, and explicit display-string transformations. It contains no raw
source bodies. `scripts/import-story.py` resolves source constants, `.string` and
`.braille` labels and UI text. The original C# compiler (moved here from runtime)
selects FireRed branches, expands macros and emits typed instructions, movements,
map headers, conditions, menus and declaration operands. The three historical
fallthrough completions are applied during import.

Only `content/generated/story/runtime.json` is consumed by `PretCampaignCatalog.LoadCurrent`.
The runtime never invokes the compiler, reads the recipe or parses the original
assembly/C files. Source paths and line numbers in instruction provenance remain
for diagnostics; they are not read as files by the runtime.

The schema-2 program encoding stores `SourcePath` once per program and `Line` on
each instruction. The program ID supplies the usual source symbol; the nine
fallthrough instructions crossing a label retain a sparse `SourceSymbol` override.
`PretStoryRuntime.JsonOptions` is the shared importer/runtime codec. Decoding
restores the exact `StorySource` used by dispatch and diagnostics without changing
program IDs or instruction indexes. Old schema-1 resources are rejected; regenerate
them with the command above.

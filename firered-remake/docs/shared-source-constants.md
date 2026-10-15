# Shared source constants

Generated packages read one numeric symbol table at
`content/generated/shared-constants/values.json`. Its JSON object maps original
source symbols directly to integer values. `PretSharedConstants` loads one
immutable table per generated directory and rejects empty tables, duplicate
symbols, and non-integer values. Package loaders do not read local constants
files or fall back to another representation.

The complete pre-change inventory had 59 package constants files containing
175,238 entries: 166,483 annotated records and 8,755 plain numeric entries.
Their union contains 6,996 symbols. Every repeated symbol has the same numeric
value, so there are no package overrides or per-package symbol membership lists.
All 175,238 prior entries were compared with the shared table before removal.

The original package constants occupied 46,457,690 bytes. The redundant Pokemon
Tower constants-source document added 1,070,519 bytes. The shared numeric table
occupies 202,062 bytes. National Dex movement command bindings remain separate
actual data in `national-dex-source/movement-bindings.json`; they are not numeric
script operands and were not discarded with source annotations.

`scripts/shared_constants.py` publishes resolved importer contributions and
refuses conflicting numeric values. A different actual value must receive an
explicit semantic scope before publication; a source-line annotation alone does
not create a different constant. Check mode verifies the current importer's
contribution without writing or requiring that it owns the entire shared table.
The helpers strip source records before publication, and package output
inventories contain only their own resources. Rebuild affected source packages
with their existing import commands to add their symbols to a fresh table.

`content/sources/shared-constants.recipe.json` retains the source repository,
pinned commit, upstream paths and contributing packages. Individual package
source locks and importer expression resolvers remain the detailed source
recipe. They are not required by the runtime constant reader. No digest index,
content-addressed blob store, per-value provenance table, or legacy format loader
is involved.

The focused National Dex source check continues to cover 31 source blobs,
95 script closure blocks, 12 prerequisite boundary cases, four arrival
directions, and all 22 movement bindings. Shared publication was also checked
for value conflicts, duplicate symbols, and mutation-free verification.

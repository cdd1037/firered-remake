# Current Pokémon admissions

`data/game-pokemon.json` is the authored gameplay admission policy. The runtime
copy is `content/generated/data/pokemon/game/catalog.json`; both use schema 2.
The game reads only the generated copy.

- `definitions` contains one entry per admitted species: its stable identity,
  approved evolution projection and optional form scope.
- `owned`, `opponents` and `trainerTowerOpponents` contain ordered `species`
  references, not repeated definitions. A canonical species does not become
  available merely because its definition exists.
- Each roster has an explicit `evolutionOverrides` array. An override replaces
  only that species' evolution edges in that roster. It must differ from the
  shared entry and cannot add species, change base stats or change form scope.
  The ordinary-opponent and Trainer Tower differences are intentional and remain
  separate from the owned roster.

Species statistics, moves, source evolution rules and form definitions remain in
the existing canonical catalog. Admission edges must match those rules, and each
resolved evolution target must belong to its actual roster. Missing, duplicate,
unknown and unused definitions/references are rejected, as are redundant,
duplicate and unadmitted overrides. Runtime JSON retains strict property checking.
There is no schema-1 fallback: regenerate the matching resources with the code.

## Regeneration

```sh
python scripts/imports/pokemon/import-game-pokemon.py
```

This validates the shared definitions and all resolved rosters before writing the
compact schema-2 runtime catalog. It also regenerates the separate breeding
document from retained egg-move and machine-order inputs. The source selection
remains readable; the generated artifact is compact. Other importers use
`scripts/lib/game_pokemon_admissions.py` when they need resolved roster rows.

This format does not change the save schema, gameplay eligibility, RNG, evolution
rules, or breeding data. The migration compared all 620 previous resolved
admission rows, including their order and every field, against the new format.
It retained 229 shared species definitions and 12 explicit evolution overrides
across seven species. The separate breeding artifact was byte-identical.

## Focused verification

```sh
python scripts/checks/test-game-pokemon.py
python scripts/checks/test-pokemon-collection-presentation.py
python scripts/checks/test-learnset-import.py
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore \
  --filter 'FullyQualifiedName~CurrentPokemonAdmissionTests|FullyQualifiedName~PokemonAdmissionParityTests|FullyQualifiedName~GeneratedBreedingMetadataTests'
```

Use the repository's normal serial build first. Cross-boundary changes also need
the generated-only campaign and composition tests. Parser and source checks do
not substitute for native graphics or audio acceptance.

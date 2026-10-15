# Import inputs

These recipes are used only by source import tools. `source.json` specifies the
pinned original source and map-inspection whitelist. The remaining recipes hold
actual file selections, source roles, or the selected deferred localization scope.
They are not runtime dependencies. Dynamic regional importers discover their
inputs directly and do not publish duplicate source locks. Canonical Pokémon and
move imports use the explicit file lists already owned by their extractors.

`content/` contains only original inputs in `raw/` and compiled resources in
`generated/`. The three project-owned trainer palette variants remain in
`raw/local/forest-trainer-art` because they are actual image-conversion inputs.
Fetch receipts and derived source inventory snapshots are not game assets and
are no longer written there. The audio audit can emit an optional external report.

Regional rules importers rederive source declarations in memory using their
existing extractors. FirstSevii, Cinnabar, SeviiRuby, and VictoryRoad publish no
source declaration snapshots in generated content. Itemfinder originals are
materialized into the pinned raw tree; generated content contains its typed
contract and converted icons only.

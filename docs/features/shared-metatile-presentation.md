# Shared edited-metatile presentation

The live field, Quest Log main map, and Quest Log connected neighbors use
`CampaignView.PatchMapLayer`. Each caller supplies the actual unedited source map
and its layer texture. Historical rendering does not read current-session flags
or current-session layout selection to find replacement pixels.

`MapMetatileArtResolver` owns copied, immutable map/layout + metatile + layer
bindings. Composition adapts the existing imported assets once after their
textures load. Layout-specific CUT pixels take precedence, then a map's explicit
atlas binding, then an existing cell from the supplied source layout. A missing
tile fails; there is no global Mart fallback for an unrelated map. Source crops,
three separate layers, and alpha replacement are preserved.

Explicit composition bindings are content wiring, not runtime feature switches.
The resolver consumes the existing [map and atlas formats](../resources/resource-layout.md#地图格式);
it does not introduce another generated format or source-parsing path.
Gameplay checks for allowed coordinates, collision, flags, and changes remain in
Application. Presentation no longer repeats these using the live save while
showing a historical projection.

## Verification boundary

`MapMetatileArtResolverTests` exercises common binding identity across the three
projection contexts, layout and map isolation, layer selection, supplied-layout
fallback, missing-art failure, and immutable binding ownership. These are
resolver unit tests, not native window or GPU pixel verification. The Godot
build and actual startup must also pass to verify asset composition; a windowed
live/recap comparison remains a separate visual check. No audio claim follows
from these tests.

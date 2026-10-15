using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string SurfContentProfile="firered-surf-campaign-v1";
    public PretSurfPokemon? SurfPokemon { get; }
    public PretSurfArt? SurfArt { get; }
    public PretSurfEncounters? SurfEncounters { get; }
    public SurfOwnedPokemonCatalog? SurfOwnership { get; }
    public bool SurfEnabled=>SurfOwnership is not null;
    public IReadOnlyDictionary<MapId,WaterEncounterTable>? WaterEncounters { get; }

    // The source assembler places these three labels contiguously. The general compiler
    // represents one program per label, so the successor explicitly includes the original
    // fallthrough suffix instead of depending on dictionary order or inventing instructions.


}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Game.Domain.Roaming;

namespace Game.Content;

/// <summary>Five successor: original Sapphire handoff, source world flags and a real initialized roamer.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string CelioContentProfile = "firered-celio-sapphire-campaign-v1";
    public IReadOnlyDictionary<MapId, RoamerMap>? CelioRoamerMaps { get; }

    // These are separate gameplay gates, not fake implementations of the link flag's consumers.
    public static ImmutableArray<string> CelioUnsupportedInteractions =>
        FiveIslandUnsupportedInteractions.Where(x => x is not "OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire" and not "InitRoamer")
            .Concat(new[] { "PokemonLeague.Rematch","CeruleanCave","Mewtwo","HoennLink.Multiplayer" })
            .ToImmutableArray();

}

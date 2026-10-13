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
    public CelioPokemonContent? CelioPokemon { get; }
    public IReadOnlyDictionary<MapId, RoamerMap>? CelioRoamerMaps { get; }
    public bool CelioEnabled => CelioPokemon is not null;
    public CelioOwnedPokemonCatalog? CelioOwnership => CelioPokemon?.Owned;
    public CelioOpponentPokemonCatalog? CelioOpponents => CelioPokemon?.Opponents;
    public CelioMachineCatalog? CelioMachines => CelioPokemon?.Machines;

    // These are separate gameplay gates, not fake implementations of the link flag's consumers.
    public static ImmutableArray<string> CelioUnsupportedInteractions =>
        FiveIslandUnsupportedInteractions.Where(x => x is not "OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire" and not "InitRoamer")
            .Concat(new[] { "PokemonLeague.Rematch", "CeruleanCave", "Mewtwo", "FourIsland_LoreleisHouse", "HoennLink.Multiplayer" })
            .ToImmutableArray();

    public static PretCampaignCatalog LoadCelioProfile(string root, bool useCompactMapData = true,
        string? celioContentRoot = null, string? fiveIslandContentRoot = null, string? fiveIslandRulesRoot = null,
        string? sixIslandContentRoot = null, string? sixIslandRulesRoot = null,
        string? fourIslandContentRoot = null, string? fourIslandRulesRoot = null,
        string? seviiRubyContentRoot = null, string? seviiRubyRulesRoot = null, string? nationalDexSourceRoot = null) =>
        LoadPokemonLeagueProfile(root, useCompactMapData, includeNationalDex: true,
            nationalDexSourceRoot: nationalDexSourceRoot, includeSeviiRuby: true,
            seviiRubyContentRoot: seviiRubyContentRoot, seviiRubyRulesRoot: seviiRubyRulesRoot,
            includeFourIsland: true, fourIslandContentRoot: fourIslandContentRoot, fourIslandRulesRoot: fourIslandRulesRoot,
            includeSixIsland: true, sixIslandContentRoot: sixIslandContentRoot, sixIslandRulesRoot: sixIslandRulesRoot,
            includeFiveIsland: true, fiveIslandContentRoot: fiveIslandContentRoot, fiveIslandRulesRoot: fiveIslandRulesRoot,
            includeCelio: true, celioContentRoot: celioContentRoot);

    private static void ValidateCelioComposition(ImmutableDictionary<MapId, PretWorldMap> maps, PretStoryCompilation scripts,
        CampaignPokemonContent pokemon, CelioPokemonContent celio, FiveIslandOwnedPokemonCatalog owned,
        FiveIslandOpponentPokemonCatalog opponents, FiveIslandMachineCatalog machines)
    {
        if (maps.Count != 254 || celio.Owned.Catalog.Species.Count != 138 || celio.Opponents.Catalog.Species.Count != 162 ||
            !ReferenceEquals(celio.Owned.Predecessor, owned) || !ReferenceEquals(celio.Opponents.Predecessor, opponents) ||
            !ReferenceEquals(celio.Machines.Predecessor, machines) || !ReferenceEquals(pokemon.Catalog, celio.Owned.Catalog) ||
            !ReferenceEquals(celio.Owned.Catalog.Moves, owned.Catalog.Moves) || pokemon.Catalog.Moves.Count != 354)
            throw new InvalidDataException("celio_composition_scope");
        PretCelioStory.Validate(scripts);
    }
}

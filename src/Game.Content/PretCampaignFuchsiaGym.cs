using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    // Regional composition for the Fuchsia Gym.
    public const string FuchsiaGymContentProfile = "firered-fuchsia-gym-source-content-v1";
    public const string FuchsiaGymShopContentProfile = FuchsiaGymItemRules.Profile;

    public PretFuchsiaGymRegion FuchsiaGymRegion { get; }
    public FuchsiaGymTrainerDocument? FuchsiaGymTrainerDocument { get; }
    public FuchsiaGymTrainerCatalogContent? FuchsiaGymTrainers { get; }
    Game.Domain.Trainers.FuchsiaGymTrainerCatalog? Game.Domain.World.ICampaignCatalog.FuchsiaGymTrainerParties =>
        FuchsiaGymTrainers is { } trainers ? new(trainers.Trainers,trainers.Battles) : null;
    public PretFuchsiaGymItemsDocument? FuchsiaGymItems { get; }
    /// <summary>Explicit source successor opt-in; native acceptance remains a separate gate.</summary>
    public PokemonMoveDefinition? FuchsiaGymToxic => PokemonContent!.Catalog.Moves[new("pret:MOVE_TOXIC")];

    private static void ValidateFuchsiaGymComposition(ImmutableDictionary<MapId, PretWorldMap> maps, FuchsiaGymTrainerDocument trainers, PretStoryCompilation scripts)
    {
        var gymId = new MapId("map.fuchsia-city-gym"); var cityId = new MapId("map.fuchsia-city");
        if (!maps.ContainsKey(gymId))
            throw new InvalidDataException("fuchsia_gym_composition_scope");
        if (maps[cityId].Warps[4].DestinationMapId != gymId || maps[cityId].Warps[4].DestinationWarpId != 1 ||
            maps[gymId].Warps.Length != 3 || maps[gymId].Warps.Any(w => w.DestinationMapId != cityId || w.DestinationWarpId != 4))
            throw new InvalidDataException("fuchsia_gym_warp_source");
        foreach (var battle in trainers.Battles)
            if (scripts.FindProgram(battle.ScriptEntry) is null ||
                battle.PostBattleScript is { } postBattle && scripts.FindProgram(postBattle) is null)
                throw new InvalidDataException("fuchsia_gym_script_missing");
    }
}

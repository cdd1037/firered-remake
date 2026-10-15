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
    public const string FuchsiaGymMachineContentProfile = FuchsiaGymMachineRules.Profile;
    public const string FuchsiaGymShopContentProfile = FuchsiaGymItemRules.Profile;

    public PretFuchsiaGymRegion? FuchsiaGymRegion { get; }
    public FuchsiaGymTrainerDocument? FuchsiaGymTrainerDocument { get; }
    public FuchsiaGymTrainerCatalogContent? FuchsiaGymTrainers { get; }
    Game.Domain.Trainers.FuchsiaGymTrainerCatalog? Game.Domain.World.ICampaignCatalog.FuchsiaGymTrainerParties =>
        FuchsiaGymTrainers is { } trainers ? new(trainers.SourceCommit,trainers.Profile,trainers.CompleteSourceRoster,trainers.Trainers,trainers.Battles) : null;
    public PretFuchsiaGymItemsDocument? FuchsiaGymItems { get; }
    public PretRockTunnelMachinesDocument? FuchsiaGymMachines { get; }
    /// <summary>Explicit source successor opt-in; native acceptance remains a separate gate.</summary>
    public bool FuchsiaGymEnabled => FuchsiaGymRegion is not null;
    public PokemonMoveDefinition? FuchsiaGymToxic => FuchsiaGymEnabled
        ? PokemonContent!.Catalog.Moves[new("pret:MOVE_TOXIC")] : null;

    private static void AppendFuchsiaGymSources(PretFuchsiaGymRegion region,PretMetronomeMoveCatalog moveSource,List<PretSourceScriptFile> sources,List<string> texts,Dictionary<string, int> constants)
    {
        var paths = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var source in region.Scripts)
        {
            if (paths.TryGetValue(source.Path, out var prior))
            {
                if (prior.Text != source.Text)
                    throw new InvalidDataException("fuchsia_gym_script_conflict: " + source.Path);
                continue;
            }
            paths.Add(source.Path, source); sources.Add(source);
            if (!texts.Contains(source.Text, StringComparer.Ordinal)) texts.Add(source.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        MergeRegionalServiceConstants(constants, moveSource.Constants.Where(p=>p.Key.StartsWith("MOVE_",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal));
    }

    private static PokemonCatalog FuchsiaGymPokemon(PokemonCatalog predecessor,FuchsiaGymTrainerDocument trainers,PretMetronomeMoveCatalog source)
    {
        PokemonFactory.ValidateCatalog(predecessor);
        if (predecessor.Moves.ContainsKey(new("pret:MOVE_TOXIC")) || predecessor.Moves.Values.Any(m => !source.MatchesSource(m)) ||
            trainers.Moves.Any(m => !source.MatchesSource(m)))
            throw new InvalidDataException("fuchsia_gym_pokemon_predecessor_or_source");
        // The source turn kernel may call any imported move. Extend metadata only:
        // preserve every species/learnset and existing move object unchanged.
        var result = source.Extend(predecessor);
        PokemonFactory.ValidateCatalog(result);
        if (trainers.Moves.Any(m => !result.Moves.ContainsKey(m.Id)) ||
            trainers.Trainers.SelectMany(t => t.Party).Any(p => !result.Species.ContainsKey(p.Species)))
            throw new InvalidDataException("fuchsia_gym_trainer_metadata_closure");
        return result;
    }

    private static PewterMachineCatalog ComposeFuchsiaGymMachines(PewterMachineCatalog predecessor,PretRockTunnelMachinesDocument document)
    {
        FuchsiaMachineRules.ValidateCatalog(predecessor);
        var machine = document.Machines.Single();
        if (machine.Machine.Item.Id != PretFuchsiaGymItems.Tm06Id ||
            predecessor.AdditionalMachines!.Value.Any(m => m.Machine.Item.Id == machine.Machine.Item.Id))
            throw new InvalidDataException("fuchsia_gym_machine_predecessor");
        var result=predecessor with { Profile = FuchsiaGymMachineRules.Profile,AdditionalMachines = predecessor.AdditionalMachines.Value.Add(machine) };
        FuchsiaGymMachineRules.ValidateCatalog(result);
        return result;
    }

    private static EarlyShopCatalog ComposeFuchsiaGymShop(EarlyShopCatalog predecessor,PretFuchsiaGymItemsDocument document)
    {
        var item = PretFuchsiaGymItems.Definition(document);
        if (predecessor.Items.Any(i => i.Id == item.Id)) throw new InvalidDataException("fuchsia_gym_shop_item_conflict");
        var result=predecessor with { Profile = FuchsiaGymItemRules.Profile,Items = predecessor.Items.Add(item) };
        FuchsiaGymItemRules.ValidateShop(result);
        return result;
    }

    private static void ValidateFuchsiaGymComposition(ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> predecessorMaps, FuchsiaGymTrainerDocument trainers, PretStoryCompilation scripts)
    {
        var gymId = new MapId("map.fuchsia-city-gym"); var cityId = new MapId("map.fuchsia-city");
        if (!maps.Keys.Except(predecessorMaps.Keys).SequenceEqual([gymId]))
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

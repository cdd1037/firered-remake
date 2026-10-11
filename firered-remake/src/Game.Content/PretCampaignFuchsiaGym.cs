using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    // Explicit successor composition. The predecessor route retains its 244 moves
    // and its old Domain profiles; source trainer admission belongs to Application.
    public const string FuchsiaGymContentProfile = "firered-fuchsia-gym-source-content-v1";
    public const string FuchsiaGymMachineContentProfile = FuchsiaGymMachineRules.Profile;
    public const string FuchsiaGymShopContentProfile = FuchsiaGymItemRules.Profile;
    public static PretCampaignCatalog LoadFuchsiaGymProfile(string root, bool useCompactMapData = true, bool includePresetDoubles = false, bool includeSafari = false, bool includeSurf = false, string? surfContentRoot = null) => Load(root,
        includeStarterEvents:true, includeFirstBattle:true, includeOpeningItems:true,
        includeViridianRegion:true, includeParcelQuest:true, includeCapture:true, includeViridianCenter:true,
        includeOldManTutorial:true, includePartyTraining:true, includeForest:true, includeForestTrainers:true,
        includePewter:true, includeEarlyShop:true, includeEarlyStarter:true, includeEarlyParty:true,
        includePokemonStorage:true, includeFieldPickups:true, includeRepel:true, includeTownMap:true,
        includeOakRating:true, includeRoute22:true, useCompactMapData:useCompactMapData, includeRoute3:true,
        includeMagikarp:true, includeMtMoon:true, includeCerulean:true, includeBill:true, includeVermilion:true,
        includeSsAnne:true, includeVermilionGym:true, includeCityServices:true, includeRoute11:true,
        includeRockTunnel:true, includeCeladonApproach:true, includeCeladonGym:true,
        includeRocketHideout:true, includePokemonTower:true, includeSnorlax:true,
        includeRegionalFlavor:true, includeRegionalServices:true, includeRegionalInteriorServices:true,
        includeFuchsiaRoute:true, includeFuchsiaGym:true, includePresetDoubles:includePresetDoubles, includeSafari:includeSafari, includeSurf:includeSurf, surfContentRoot:surfContentRoot);

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

    private static void AppendFuchsiaGymSources(PretFuchsiaGymRegion region, PretFuchsiaGymItemsDocument items,
        PretMetronomeMoveCatalog moveSource, ImmutableDictionary<MapId, PretWorldMap> maps,
        List<PretSourceScriptFile> sources, List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        void Identity(string path, string sha)
        {
            if (identities.TryGetValue(path, out var prior) && prior != sha)
                throw new InvalidDataException("fuchsia_gym_source_conflict: " + path);
            identities[path] = sha;
        }
        foreach (var map in maps.Values)
            foreach (var file in map.Metadata.Files) Identity(file.Path, file.Sha256);
        foreach (var source in sources) Identity(source.Path, source.SourceRef.Sha256!);
        foreach (var file in region.SourceFiles) Identity(file.Path, file.Sha256);
        foreach (var file in items.Inputs) Identity(file.Path, file.Sha256);
        foreach (var file in moveSource.SourceFiles) Identity(file.Path, file.Sha256);
        var paths = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var source in region.Scripts)
        {
            if (paths.TryGetValue(source.Path, out var prior))
            {
                if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
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
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.AddRange(moveSource.AdditionalSourceHashes);
        hashes.Add("fuchsia-gym-trainers:" + PretFuchsiaGymTrainers.LockSha256 + ":" + PretFuchsiaGymTrainers.GeneratedSha256);
        hashes.Add("fuchsia-gym-items:" + PretFuchsiaGymItems.LockSha256 + ":" + PretFuchsiaGymItems.ManifestSha256 + ":" + PretFuchsiaGymItems.GeneratedSha256);
        hashes.Add("fuchsia-gym-machines:" + PretFuchsiaGymMachines.GeneratedSha256);
        hashes.Add("fuchsia-gym-content:" + FuchsiaGymContentProfile);
    }

    private static PokemonCatalog FuchsiaGymPokemon(PokemonCatalog predecessor, FuchsiaGymTrainerDocument trainers,
        PretMetronomeMoveCatalog source)
    {
        PokemonFactory.ValidateCatalog(predecessor);
        if (predecessor.Species.Count != 107 || predecessor.Moves.Count != 244 ||
            predecessor.Moves.ContainsKey(new("pret:MOVE_TOXIC")) || predecessor.Moves.Values.Any(m => !source.MatchesSource(m)) ||
            trainers.Moves.Any(m => !source.MatchesSource(m)))
            throw new InvalidDataException("fuchsia_gym_pokemon_predecessor_or_source");
        // The source turn kernel may call any authenticated move. Extend metadata only:
        // preserve every species/learnset and existing move object unchanged.
        var result = source.Extend(predecessor);
        PokemonFactory.ValidateCatalog(result);
        if (trainers.Moves.Any(m => !result.Moves.ContainsKey(m.Id)) ||
            trainers.Trainers.SelectMany(t => t.Party).Any(p => !result.Species.ContainsKey(p.Species)))
            throw new InvalidDataException("fuchsia_gym_trainer_metadata_closure");
        return result;
    }

    private static PewterMachineCatalog ComposeFuchsiaGymMachines(PewterMachineCatalog predecessor,
        PretRockTunnelMachinesDocument document)
    {
        FuchsiaMachineRules.ValidateCatalog(predecessor);
        var machine = document.Machines.Single();
        if (machine.Machine.Item.Id != PretFuchsiaGymItems.Tm06Id ||
            predecessor.AdditionalMachines!.Value.Any(m => m.Machine.Item.Id == machine.Machine.Item.Id))
            throw new InvalidDataException("fuchsia_gym_machine_predecessor");
        var result=predecessor with { Profile = FuchsiaGymMachineRules.Profile,
            AdditionalMachines = predecessor.AdditionalMachines.Value.Add(machine) };
        FuchsiaGymMachineRules.ValidateCatalog(result);
        return result;
    }

    private static EarlyShopCatalog ComposeFuchsiaGymShop(EarlyShopCatalog predecessor, PretFuchsiaGymItemsDocument document)
    {
        var item = PretFuchsiaGymItems.Definition(document);
        if (predecessor.Items.Any(i => i.Id == item.Id)) throw new InvalidDataException("fuchsia_gym_shop_item_conflict");
        var result=predecessor with { Profile = FuchsiaGymItemRules.Profile, Items = predecessor.Items.Add(item) };
        FuchsiaGymItemRules.ValidateShop(result);
        return result;
    }

    private static void ValidateFuchsiaGymComposition(PretFuchsiaGymRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> predecessorMaps,
        PokemonCatalog pokemon, PokemonCatalog predecessorPokemon, FuchsiaGymTrainerDocument trainers, PretStoryCompilation scripts)
    {
        var gymId = new MapId("map.fuchsia-city-gym"); var cityId = new MapId("map.fuchsia-city");
        if (maps.Count != 154 || predecessorMaps.Count != 153 || region.NewMaps.Count != 1 ||
            !maps.Keys.Except(predecessorMaps.Keys).SequenceEqual([gymId]) ||
            predecessorMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) || !ReferenceEquals(maps[gymId], region.NewMaps[gymId]) ||
            pokemon.Species.Count != 107 || pokemon.Moves.Count != 354 || !ReferenceEquals(pokemon.Species, predecessorPokemon.Species) ||
            predecessorPokemon.Moves.Any(p => !ReferenceEquals(p.Value, pokemon.Moves[p.Key])))
            throw new InvalidDataException("fuchsia_gym_composition_scope");
        if (!JsonElement.DeepEquals(maps[cityId].Events, region.ExternalFuchsiaEvents) ||
            maps[cityId].Warps[4].DestinationMapId != gymId || maps[cityId].Warps[4].DestinationWarpId != 1 ||
            maps[gymId].Warps.Length != 3 || maps[gymId].Warps.Any(w => w.DestinationMapId != cityId || w.DestinationWarpId != 4))
            throw new InvalidDataException("fuchsia_gym_warp_source");
        foreach (var battle in trainers.Battles)
            if (scripts.FindProgram(battle.ScriptEntry) is null ||
                battle.PostBattleScript is { } postBattle && scripts.FindProgram(postBattle) is null)
                throw new InvalidDataException("fuchsia_gym_script_missing");
    }
}

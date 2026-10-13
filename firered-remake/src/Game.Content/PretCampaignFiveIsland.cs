using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Authenticated Six successor through Gideon's original Sapphire recovery, before Celio's handoff.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string FiveIslandContentProfile = "firered-five-island-campaign-v1";
    public PretFiveIslandRegion? FiveIslandRegion { get; }
    public FiveIslandOwnedPokemonCatalog? FiveIslandOwnership { get; }
    public FiveIslandOpponentPokemonCatalog? FiveIslandOpponents { get; }
    public FiveIslandTrainerCatalog? FiveIslandTrainers { get; }
    public FiveIslandTrainerCatalog? FiveIslandTrainerParties => FiveIslandTrainers;
    public FiveIslandItemContent? FiveIslandItems { get; }
    public FiveIslandShopCatalog? FiveIslandShop => FiveIslandItems?.Shop;
    public FiveIslandMachineCatalog? FiveIslandMachines => FiveIslandItems?.Machines;
    public FiveIslandWaterEncounterCatalog? FiveIslandWaterEncounters { get; }
    public bool FiveIslandEnabled => FiveIslandRegion is not null;

    public static ImmutableArray<string> FiveIslandUnsupportedInteractions =>
        ["FiveIsland_WaterLabyrinth", "FiveIsland_MemorialPillar", "FiveIsland_House1", "FiveIsland_House2",
         "FiveIsland_PokemonCenter_2F", "OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire", "InitRoamer",
         "SevenIsland", "GoodRod", "SuperRod", "Porygon.TradeEvolution", "VsSeeker.Rematch"];

    public static PretCampaignCatalog LoadFiveIslandProfile(string root, bool useCompactMapData = true,
        string? fiveIslandContentRoot = null, string? fiveIslandRulesRoot = null,
        string? sixIslandContentRoot = null, string? sixIslandRulesRoot = null,
        string? fourIslandContentRoot = null, string? fourIslandRulesRoot = null,
        string? seviiRubyContentRoot = null, string? seviiRubyRulesRoot = null, string? nationalDexSourceRoot = null) =>
        LoadPokemonLeagueProfile(root, useCompactMapData, includeNationalDex: true,
            nationalDexSourceRoot: nationalDexSourceRoot, includeSeviiRuby: true,
            seviiRubyContentRoot: seviiRubyContentRoot, seviiRubyRulesRoot: seviiRubyRulesRoot,
            includeFourIsland: true, fourIslandContentRoot: fourIslandContentRoot, fourIslandRulesRoot: fourIslandRulesRoot,
            includeSixIsland: true, sixIslandContentRoot: sixIslandContentRoot, sixIslandRulesRoot: sixIslandRulesRoot,
            includeFiveIsland: true, fiveIslandContentRoot: fiveIslandContentRoot, fiveIslandRulesRoot: fiveIslandRulesRoot);

    private static void AppendFiveIslandSources(PretFiveIslandRegion region, List<PretSourceScriptFile> sources,
        List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var old))
            {
                if (old.SourceRef.Sha256 != file.SourceRef.Sha256 || old.Text != file.Text)
                    throw new InvalidDataException("five_island_source_conflict: " + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("five-island-trainers:" + PretFiveIslandRules.TrainersSha256);
        hashes.Add("five-island-owned:" + PretFiveIslandRules.PokemonSha256);
        hashes.Add("five-island-rules:" + PretFiveIslandRules.RulesSha256);
        hashes.Add("five-island-source-inputs:" + PretFiveIslandRules.SourceInputsSha256);
        hashes.Add("five-island-item-source-inputs:" + PretFiveIslandRules.ItemSourceInputsSha256);
        hashes.Add("five-island-encounters:" + PretFiveIslandRules.EncountersSha256);
        hashes.Add("five-island-wild-source:" + PretFiveIslandRules.WildSourceWitnessSha256);
        hashes.Add("five-island-campaign:" + FiveIslandContentProfile);
    }

    private static void ValidateFiveIslandAnchors(PretFiveIslandRegion region, ImmutableDictionary<MapId, PretWorldMap> maps)
    {
        if (maps.Count != 249 || region.NewMaps.Count != 5 || region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("five_island_predecessor_maps");
        foreach (var (name, events) in region.ReusedAnchorEvents)
        {
            var map = maps.Values.SingleOrDefault(m => m.SourceRef.Path == "data/maps/" + name + "/map.json");
            if (map is null || !JsonElement.DeepEquals(map.Events, events))
                throw new InvalidDataException("five_island_anchor_events: " + name);
        }
    }

    private static IEnumerable<KeyValuePair<int, string>> FiveIslandSpeciesNames(PretPokedexMenu pokedex, FiveIslandOwnedPokemonCatalog owned) =>
        FiveIslandOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
        {
            int sourceId = owned.Catalog.Species[id].SourceSpeciesId;
            return KeyValuePair.Create(sourceId, pokedex.Entries[sourceId].Name);
        });

    private static void ValidateFiveIslandComposition(PretFiveIslandRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        FiveIslandOwnedPokemonCatalog owned, FiveIslandOpponentPokemonCatalog opponents, FiveIslandTrainerCatalog trainers,
        PretStoryCompilation scripts, CampaignPokemonContent pokemon, FiveIslandItemContent items)
    {
        if (maps.Count != 254 || previous.Count != 249 || previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            region.NewMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) || owned.Catalog.Species.Count != 135 ||
            owned.Predecessor.Catalog.Species.Count != 132 || opponents.Catalog.Species.Count != 159 ||
            opponents.Predecessor.Catalog.Species.Count != 153 || !ReferenceEquals(pokemon.Catalog, owned.Catalog) ||
            !ReferenceEquals(owned.Catalog.Moves, owned.Predecessor.Catalog.Moves) || pokemon.Catalog.Moves.Count != 354 ||
            items.Pickups.Length != 8 || items.Pickups.Any(p => p.ItemSymbol == "ITEM_SAPPHIRE") || items.Shop.Catalog.Items.Length != 111 ||
            items.Machines.Catalog.AdditionalMachines!.Value.Length != 38 || trainers.Trainers.Count != 9 || trainers.BattlesByScript.Count != 9)
            throw new InvalidDataException("five_island_composition_scope");
        FiveIslandTrainerPartyFactory.ValidateCatalog(trainers);
        foreach (var battle in trainers.BattlesByScript.Values)
        {
            var program = scripts.FindProgram(battle.ScriptEntry) ?? throw new InvalidDataException("five_island_battle_missing");
            if (!program.Instructions.Any(i => i.Op == StoryOp.TrainerBattle && i.Source.Path == battle.Source.Path &&
                i.Source.Line >= battle.Source.StartLine && i.Source.Line <= battle.Source.EndLine))
                throw new InvalidDataException("five_island_trainer_instruction: " + battle.ScriptEntry);
        }
        ValidateFiveIslandStory(scripts);
        foreach (var symbol in new[] { "MAP_FIVE_ISLAND_WATER_LABYRINTH", "MAP_FIVE_ISLAND_MEMORIAL_PILLAR",
            "MAP_FIVE_ISLAND_HOUSE1", "MAP_FIVE_ISLAND_HOUSE2", "MAP_FIVE_ISLAND_POKEMON_CENTER_2F", "MAP_SEVEN_ISLAND" })
            if (maps.Values.Any(m => m.SourceMapSymbol == symbol)) throw new InvalidDataException("five_island_excluded_map_admitted");
    }

    internal static void ValidateFiveIslandStory(PretStoryCompilation scripts)
    {
        StoryInstruction[] Program(string label) => (scripts.FindProgram(label) ??
            throw new InvalidDataException("five_island_story_missing: " + label)).Instructions.ToArray();
        var recovery = Program("FiveIsland_RocketWarehouse_EventScript_DefeatedGideon");
        if (recovery.Length != 7 || recovery.Any(i => i.Op == StoryOp.Unsupported) ||
            recovery[0].Opcode != "msgbox" || !recovery[0].Args.SequenceEqual(new[] { "FiveIsland_RocketWarehouse_Text_GideonPostBattle" }) ||
            recovery[1].Opcode != "setflag" || !recovery[1].Args.SequenceEqual(new[] { "FLAG_RECOVERED_SAPPHIRE" }) ||
            recovery[2].Opcode != "setorcopyvar" || !recovery[2].Args.SequenceEqual(new[] { "VAR_0x8000", "ITEM_SAPPHIRE" }) ||
            recovery[3].Opcode != "setorcopyvar" || !recovery[3].Args.SequenceEqual(new[] { "VAR_0x8001", "1" }) ||
            recovery[4].Opcode != "call" || !recovery[4].Args.SequenceEqual(new[] { "Std_ObtainItem" }) ||
            recovery[5].Op != StoryOp.Release || recovery[6].Op != StoryOp.End)
            throw new InvalidDataException("five_island_recovery_order");
        foreach (var boss in new[] { "Admin1", "Admin2", "Gideon" })
        {
            var first = Program("FiveIsland_RocketWarehouse_EventScript_" + boss)[0];
            if (first.Op != StoryOp.TrainerBattle || first.Opcode != "trainerbattle_single" || first.Args.Length != 4 ||
                first.Args[3] != "FiveIsland_RocketWarehouse_EventScript_Defeated" + boss)
                throw new InvalidDataException("five_island_boss_continuation");
        }
        var arrows = Program("FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry");
        if (arrows.Length != 3 || arrows[2].Op != StoryOp.Return || arrows.Take(2).Where((i, x) => i.Opcode != "setmetatile" ||
            !i.Args.SequenceEqual(new[] { (26 + x).ToString(System.Globalization.CultureInfo.InvariantCulture), "20", "METATILE_SilphCo_Arrow_Up", "0" })).Any())
            throw new InvalidDataException("five_island_admin1_arrows");
        var onLoad = Program("FiveIsland_RocketWarehouse_OnLoad");
        if (onLoad.Length != 2 || onLoad[0].Opcode != "call_if_defeated" ||
            !onLoad[0].Args.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_ADMIN", "FiveIsland_RocketWarehouse_EventScript_SetArrowsForReEntry" }))
            throw new InvalidDataException("five_island_admin1_reentry");
        var door = Program("FiveIsland_Meadow_EventScript_WarehouseDoor");
        if (!door[1].Args.SequenceEqual(new[] { "FLAG_UNLOCKED_ROCKET_WAREHOUSE", "FiveIsland_Meadow_EventScript_WarehouseDoorAlreadyOpen" }) ||
            !door[2].Args.SequenceEqual(new[] { "FLAG_LEARNED_YES_NAH_CHANSEY", "FiveIsland_Meadow_EventScript_OpenWarehouseDoor" }))
            throw new InvalidDataException("five_island_door_password_order");
        var tile = Program("FiveIsland_Meadow_EventScript_SetWarehouseDoorUnlocked");
        if (tile.Length != 2 || tile[0].Opcode != "setmetatile" ||
            !tile[0].Args.SequenceEqual(new[] { "12", "21", "METATILE_SeviiIslands45_RocketWarehouseDoor_Unlocked", "1" }) || tile[1].Op != StoryOp.Return)
            throw new InvalidDataException("five_island_door_collision");
        var depart = Program("FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin2");
        int hide = Array.FindIndex(depart, i => i.Opcode == "setflag" && i.Args[0] == "FLAG_HIDE_FIVE_ISLAND_ROCKETS");
        int won = Array.FindIndex(depart, i => i.Opcode == "setflag" && i.Args[0] == "FLAG_DEFEATED_ROCKETS_IN_WAREHOUSE");
        if (hide < 0 || won <= hide || !depart.Where(i => i.Opcode == "removeobject").Select(i => i.Args[0]).SequenceEqual(new[] {
            "LOCALID_WAREHOUSE_GRUNT2", "LOCALID_WAREHOUSE_GRUNT3", "LOCALID_WAREHOUSE_ADMIN2", "LOCALID_WAREHOUSE_ADMIN1" }) ||
            depart.Any(i => i.Op == StoryOp.Unsupported || i.Args.Contains("ITEM_SAPPHIRE") || i.Args.Contains("FLAG_RECOVERED_SAPPHIRE")))
            throw new InvalidDataException("five_island_departure_is_not_recovery");
        foreach (var label in new[] { "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin1", "FiveIsland_RocketWarehouse_EventScript_DefeatedAdmin2" })
            if (Program(label)[0].Opcode != "getplayerxy" || !Program(label)[0].Args.SequenceEqual(new[] { "VAR_0x8004", "VAR_0x8005" }))
                throw new InvalidDataException("five_island_player_coordinate_aliases");
        if (Program("OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire").Any(i => i.Op != StoryOp.Unsupported))
            throw new InvalidDataException("five_island_celio_boundary");
    }
}

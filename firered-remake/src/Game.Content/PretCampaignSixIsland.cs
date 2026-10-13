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

/// <summary>Bounded Four Island successor through the original Dotted Hole theft and second password.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string SixIslandContentProfile = "firered-six-island-campaign-v1";
    public PretSixIslandRegion? SixIslandRegion { get; }
    public SixIslandOwnedPokemonCatalog? SixIslandOwnership { get; }
    public SixIslandOpponentPokemonCatalog? SixIslandOpponents { get; }
    public SixIslandTrainerCatalog? SixIslandTrainers { get; }
    public SixIslandTrainerCatalog? SixIslandTrainerParties => SixIslandTrainers;
    public SixIslandItemContent? SixIslandItems { get; }
    public SixIslandShopCatalog? SixIslandShop => SixIslandItems?.Shop;
    public SixIslandMachineCatalog? SixIslandMachines => SixIslandItems?.Machines;
    public SixIslandWaterEncounterCatalog? SixIslandWaterEncounters { get; }
    public bool SixIslandEnabled => SixIslandRegion is not null;

    // These remain explicit admission boundaries; importing source closure is not runtime support.
    public static ImmutableArray<string> SixIslandUnsupportedInteractions =>
        ["SixIsland_House", "SixIsland_WaterPath_House1", "SixIsland_WaterPath_House2", "SixIsland_PokemonCenter_2F",
         "SixIsland_GreenPath", "FiveIsland", "SevenIsland", "DreamMail.ComposeAttachSend", "VsSeeker.Rematch"];

    private static void AppendSixIslandSources(PretSixIslandRegion region, List<PretSourceScriptFile> sources,
        List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var old))
            {
                if (old.SourceRef.Sha256 != file.SourceRef.Sha256 || old.Text != file.Text)
                    throw new InvalidDataException("six_island_source_conflict: " + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("six-island-trainers:" + PretSixIslandRules.TrainersSha256);
        hashes.Add("six-island-owned:" + PretSixIslandRules.PokemonSha256);
        hashes.Add("six-island-rules:" + PretSixIslandRules.RulesSha256);
        hashes.Add("six-island-source-inputs:" + PretSixIslandRules.SourceInputsSha256);
        hashes.Add("six-island-campaign:" + SixIslandContentProfile);
    }

    private static void ValidateSixIslandAnchors(PretSixIslandRegion region, ImmutableDictionary<MapId, PretWorldMap> maps)
    {
        if (maps.Count != 237 || region.NewMaps.Count != 12 || region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("six_island_predecessor_maps");
        foreach (var (name, events) in region.ReusedAnchorEvents)
        {
            var map = maps.Values.SingleOrDefault(m => m.SourceRef.Path == "data/maps/" + name + "/map.json");
            if (map is null || !JsonElement.DeepEquals(map.Events, events))
                throw new InvalidDataException("six_island_anchor_events: " + name);
        }
    }

    private static IEnumerable<KeyValuePair<int, string>> SixIslandSpeciesNames(PretPokedexMenu pokedex, SixIslandOwnedPokemonCatalog owned) =>
        SixIslandOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
        {
            int sourceId = owned.Catalog.Species[id].SourceSpeciesId;
            return KeyValuePair.Create(sourceId, pokedex.Entries[sourceId].Name);
        });

    private static void ValidateSixIslandComposition(PretSixIslandRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        SixIslandOwnedPokemonCatalog owned, SixIslandOpponentPokemonCatalog opponents, SixIslandTrainerCatalog trainers,
        PretStoryCompilation scripts, CampaignPokemonContent pokemon, SixIslandItemContent items)
    {
        if (maps.Count != 249 || previous.Count != 237 || previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            region.NewMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) || owned.Catalog.Species.Count != 132 ||
            owned.Predecessor.Catalog.Species.Count != 125 || opponents.Catalog.Species.Count != 153 ||
            opponents.Predecessor.Catalog.Species.Count != 141 || !ReferenceEquals(pokemon.Catalog, owned.Catalog) ||
            !ReferenceEquals(owned.Catalog.Moves, owned.Predecessor.Catalog.Moves) || pokemon.Catalog.Moves.Count != 354 ||
            items.Pickups.Length != 9 || items.Shop.Catalog.Items.Length != 108 ||
            items.Machines.Catalog.AdditionalMachines!.Value.Length != 37 || trainers.Trainers.Count != 11 || trainers.BattlesByScript.Count != 12)
            throw new InvalidDataException("six_island_composition_scope");
        SixIslandTrainerPartyFactory.ValidateCatalog(trainers);
        foreach (var battle in trainers.BattlesByScript.Values)
        {
            var program = scripts.FindProgram(battle.ScriptEntry) ?? throw new InvalidDataException("six_island_battle_missing");
            if (!program.Instructions.Any(i => i.Op == StoryOp.TrainerBattle && i.Source.Path == battle.Source.Path &&
                i.Source.Line >= battle.Source.StartLine && i.Source.Line <= battle.Source.EndLine))
                throw new InvalidDataException("six_island_trainer_instruction: " + battle.ScriptEntry);
        }
        foreach (var twin in new[] { "Miu", "Mia" })
        {
            var first = scripts.FindProgram("SixIsland_WaterPath_EventScript_" + twin)!.Instructions[0];
            if (first.Op != StoryOp.TrainerBattle || first.Opcode != "trainerbattle_double" || first.Args.Length != 4 ||
                first.Args[0] != "TRAINER_TWINS_MIU_MIA" || !first.Args[3].EndsWith(twin + "NotEnoughMons", StringComparison.Ordinal))
                throw new InvalidDataException("six_island_twins_double_required");
        }
        ValidateSixIslandStory(scripts);
        foreach (var symbol in new[] { "MAP_SIX_ISLAND_HOUSE", "MAP_SIX_ISLAND_POKEMON_CENTER_2F", "MAP_SIX_ISLAND_GREEN_PATH",
            "MAP_SIX_ISLAND_WATER_PATH_HOUSE1", "MAP_SIX_ISLAND_WATER_PATH_HOUSE2", "MAP_FIVE_ISLAND", "MAP_SEVEN_ISLAND" })
            if (maps.Values.Any(m => m.SourceMapSymbol == symbol))
                throw new InvalidDataException("six_island_excluded_map_admitted");
    }

    internal static void ValidateSixIslandStory(PretStoryCompilation scripts)
    {
        StoryInstruction[] Program(string label) => (scripts.FindProgram(label) ??
            throw new InvalidDataException("six_island_story_missing: " + label)).Instructions.ToArray();
        var theft = Program("SixIsland_DottedHole_SapphireRoom_EventScript_Sapphire");
        int Remove(string local) => Array.FindIndex(theft, i => i.Opcode == "removeobject" && i.Args.SequenceEqual(new[] { local }));
        int password = Array.FindIndex(theft, i => i.Opcode == "setflag" && i.Args.SequenceEqual(new[] { "FLAG_LEARNED_YES_NAH_CHANSEY" }));
        if (theft.Any(i => i.Op == StoryOp.Unsupported || i.Opcode is "giveitem" or "additem" || i.Args.Contains("ITEM_SAPPHIRE")) ||
            Remove("LOCALID_SAPPHIRE") < 0 || Remove("LOCALID_DOTTED_HOLE_THIEF") <= Remove("LOCALID_SAPPHIRE") ||
            password <= Remove("LOCALID_DOTTED_HOLE_THIEF") || theft[^2].Op != StoryOp.Release || theft[^1].Op != StoryOp.End)
            throw new InvalidDataException("six_island_theft_reward_order");
        foreach (var facing in new[] { "NORTH", "SOUTH", "EAST", "WEST" })
        foreach (var phase in new[] { "ThiefLookAtSapphire", "ThiefGetSapphire", "ThiefExit" })
        {
            string label = "SixIsland_DottedHole_SapphireRoom_EventScript_" + phase + facing[0] + facing[1..].ToLowerInvariant();
            if (!theft.Any(i => i.Op == StoryOp.ConditionalCall && i.Args.SequenceEqual(new[] { "VAR_FACING", "DIR_" + facing, label })) ||
                Program(label).Any(i => i.Op == StoryOp.Unsupported)) throw new InvalidDataException("six_island_theft_facing_branch");
        }
        var center = Program("SixIsland_PokemonCenter_1F_EventScript_RivalScene");
        if (center.Any(i => i.Op is StoryOp.TrainerBattle or StoryOp.Unsupported) ||
            !center.Any(i => i.Opcode == "setvar" && i.Args.SequenceEqual(new[] { "VAR_MAP_SCENE_SIX_ISLAND_POKEMON_CENTER_1F", "1" })) ||
            !center.Any(i => i.Opcode == "setvar" && i.Args.SequenceEqual(new[] { "VAR_MAP_SCENE_FOUR_ISLAND", "1" })))
            throw new InvalidDataException("six_island_rival_dialogue_only");
        var door = Program("SixIsland_RuinValley_EventScript_OpenDottedHoleDoor");
        if (door.Length != 2 || door[0].Opcode != "setmetatile" ||
            !door[0].Args.SequenceEqual(new[] { "24", "24", "METATILE_SeviiIslands67_DottedHoleDoor_Open", "0" }) || door[1].Op != StoryOp.Return)
            throw new InvalidDataException("six_island_door_reconstruction");
        var inspect = Program("SixIsland_RuinValley_EventScript_DottedHoleDoor");
        if (inspect.Any(i => i.Op is StoryOp.SetFlag or StoryOp.Unsupported) || !inspect.Any(i => i.Opcode == "braillemessage" && i.Args.SequenceEqual(new[] { "Braille_Text_Cut" })))
            throw new InvalidDataException("six_island_reading_is_not_cut");
        var plaque = Program("SixIsland_DottedHole_SapphireRoom_EventScript_BrailleMessage");
        if (plaque.Any(i => i.Op == StoryOp.Unsupported) || plaque.Count(i => i.Opcode == "braillemessage") != 16 ||
            plaque.Count(i => i.Opcode == "getbraillestringwidth") != 15 ||
            plaque.Count(i => i.Opcode == "call" && i.Args.SequenceEqual(new[] { "EventScript_BrailleCursorWaitButton" })) != 15 ||
            plaque[^3].Op != StoryOp.WaitButtonPress || plaque[^2].Op != StoryOp.Release || plaque[^1].Op != StoryOp.End)
            throw new InvalidDataException("six_island_braille_wait_sequence");
    }
}

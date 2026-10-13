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

/// <summary>Bounded Ruby successor: eight Four Island maps through the original Icefall Rocket scene.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string FourIslandContentProfile = "firered-four-island-campaign-v1";
    public PretFourIslandRegion? FourIslandRegion { get; }
    public FourIslandOwnedPokemonCatalog? FourIslandOwnership { get; }
    public FourIslandOpponentPokemonCatalog? FourIslandOpponents { get; }
    public FourIslandTrainerCatalog? FourIslandTrainers { get; }
    public FourIslandTrainerCatalog? FourIslandTrainerParties => FourIslandTrainers;
    public FourIslandItemContent? FourIslandItems { get; }
    public FourIslandShopCatalog? FourIslandShop => FourIslandItems?.Shop;
    public FourIslandMachineCatalog? FourIslandMachines => FourIslandItems?.Machines;
    public FourIslandWaterEncounterCatalog? FourIslandWaterEncounters { get; }
    public bool FourIslandEnabled => FourIslandRegion is not null;

    public static PretCampaignCatalog LoadFourIslandProfile(string root, bool useCompactMapData = true,
        string? fourIslandContentRoot = null, string? fourIslandRulesRoot = null,
        string? seviiRubyContentRoot = null, string? seviiRubyRulesRoot = null, string? nationalDexSourceRoot = null) =>
        LoadPokemonLeagueProfile(root, useCompactMapData, includeNationalDex: true,
            nationalDexSourceRoot: nationalDexSourceRoot, includeSeviiRuby: true,
            seviiRubyContentRoot: seviiRubyContentRoot, seviiRubyRulesRoot: seviiRubyRulesRoot,
            includeFourIsland: true, fourIslandContentRoot: fourIslandContentRoot, fourIslandRulesRoot: fourIslandRulesRoot);

    private static void AppendFourIslandSources(PretFourIslandRegion region, List<PretSourceScriptFile> sources,
        List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var old))
            {
                if (old.SourceRef.Sha256 != file.SourceRef.Sha256 || old.Text != file.Text)
                    throw new InvalidDataException("four_island_source_conflict: " + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("four-island-trainers:" + PretFourIslandRules.TrainersSha256);
        hashes.Add("four-island-owned:" + PretFourIslandRules.PokemonSha256);
        hashes.Add("four-island-rules:" + PretFourIslandRules.RulesSha256);
        hashes.Add("four-island-source-inputs:" + PretFourIslandRules.SourceInputsSha256);
        hashes.Add("four-island-campaign:" + FourIslandContentProfile);
    }

    private static void ValidateFourIslandAnchors(PretFourIslandRegion region, ImmutableDictionary<MapId, PretWorldMap> maps)
    {
        if (maps.Count != 229 || region.NewMaps.Count != 8 || region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("four_island_predecessor_maps");
        foreach (var (name, events) in region.ReusedAnchorEvents)
        {
            var map = maps.Values.SingleOrDefault(m => m.SourceRef.Path == "data/maps/" + name + "/map.json");
            if (map is null || !JsonElement.DeepEquals(map.Events, events))
                throw new InvalidDataException("four_island_anchor_events: " + name);
        }
    }

    private static IEnumerable<KeyValuePair<int, string>> FourIslandSpeciesNames(PretPokedexMenu pokedex, FourIslandOwnedPokemonCatalog owned) =>
        FourIslandOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
        {
            int sourceId = owned.Catalog.Species[id].SourceSpeciesId;
            return KeyValuePair.Create(sourceId, pokedex.Entries[sourceId].Name);
        });

    private static void ValidateFourIslandComposition(PretFourIslandRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        FourIslandOwnedPokemonCatalog owned, FourIslandOpponentPokemonCatalog opponents, FourIslandTrainerCatalog trainers,
        PretStoryCompilation scripts, CampaignPokemonContent pokemon, FourIslandItemContent items)
    {
        if (maps.Count != 237 || previous.Count != 229 || previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            region.NewMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) || owned.Catalog.Species.Count != 125 ||
            owned.Predecessor.Catalog.Species.Count != 118 || opponents.Catalog.Species.Count != 141 ||
            opponents.Predecessor.Catalog.Species.Count != 135 || !ReferenceEquals(pokemon.Catalog, owned.Catalog) ||
            !ReferenceEquals(owned.Catalog.Moves, owned.Predecessor.Catalog.Moves) || pokemon.Catalog.Moves.Count != 354 ||
            items.Pickups.Length != 8 || items.Shop.Catalog.Items.Length != 105 ||
            items.Machines.Catalog.AdditionalMachines!.Value.Length != 37)
            throw new InvalidDataException("four_island_composition_scope");
        FourIslandTrainerPartyFactory.ValidateCatalog(trainers);
        var battle = trainers.BattlesByScript[FourIslandTrainerPartyFactory.BattleScript];
        var scene = scripts.FindProgram(battle.ScriptEntry) ?? throw new InvalidDataException("four_island_scene_missing");
        var instructions = scene.Instructions.ToArray();
        int fight = Array.FindIndex(instructions, i => i.Op == StoryOp.TrainerBattle && i.Opcode == "trainerbattle_no_intro" &&
            i.Source.Path == battle.Source.Path && i.Source.Line >= battle.Source.StartLine && i.Source.Line <= battle.Source.EndLine &&
            i.Args.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_GRUNT_45", "FourIsland_IcefallCave_Back_Text_GruntDefeat" }));
        int scientist = Array.FindIndex(instructions, i => i.Opcode == "setflag" && i.Args.SequenceEqual(new[] { "FLAG_HIDE_RUIN_VALLEY_SCIENTIST" }));
        int home = Array.FindIndex(instructions, i => i.Opcode == "clearflag" && i.Args.SequenceEqual(new[] { "FLAG_HIDE_LORELEI_IN_HER_HOUSE" }));
        int completed = Array.FindIndex(instructions, i => i.Opcode == "setvar" && i.Args.SequenceEqual(new[] { "VAR_MAP_SCENE_ICEFALL_CAVE_BACK", "1" }));
        if (fight < 0 || scientist <= fight || home <= scientist || completed <= home ||
            instructions.Any(i => i.Op == StoryOp.Unsupported) || FourIslandTrainerPartyFactory.BasePrize(trainers, FourIslandTrainerPartyFactory.Grunt45) != 1216)
            throw new InvalidDataException("four_island_battle_reward_order");
        foreach (var symbol in new[] { "MAP_FOUR_ISLAND_POKEMON_CENTER_2F", "MAP_FOUR_ISLAND_POKEMON_DAY_CARE",
            "MAP_FOUR_ISLAND_HOUSE1", "MAP_FOUR_ISLAND_HOUSE2", "MAP_FOUR_ISLAND_LORELEIS_HOUSE" })
            if (maps.ContainsKey(PretFourIslandRegion.MapIdFromSourceSymbol(symbol)))
                throw new InvalidDataException("four_island_excluded_map_admitted");
    }

    private static PretStoryCompilation CompleteFourIslandSourceFallthrough(PretStoryCompilation compiled)
    {
        StoryProgram Program(string label, int[] lines, string[] opcodes)
        {
            var program = compiled.FindProgram(label) ?? throw new InvalidDataException("four_island_fallthrough_missing: " + label);
            if (!program.Instructions.Select(i => i.Source.Line).SequenceEqual(lines) ||
                !program.Instructions.Select(i => i.Opcode).SequenceEqual(opcodes) ||
                program.Instructions.Any(i => i.Source.Path != "data/scripts/field_moves.inc" || i.Source.Symbol != label))
                throw new InvalidDataException("four_island_fallthrough_identity: " + label);
            return program;
        }
        var cant = Program("EventScript_CantUseWaterfall", [192], ["lockall"]);
        var wall = Program("EventScript_WaterCrashingDown", [194], ["msgbox"]);
        var end = Program("EventScript_EndWaterfall", [196, 197], ["releaseall", "end"]);
        return compiled with { Programs = compiled.Programs
            .SetItem(cant.Id, cant with { Instructions = cant.Instructions.AddRange(wall.Instructions).AddRange(end.Instructions) })
            .SetItem(wall.Id, wall with { Instructions = wall.Instructions.AddRange(end.Instructions) }) };
    }
}

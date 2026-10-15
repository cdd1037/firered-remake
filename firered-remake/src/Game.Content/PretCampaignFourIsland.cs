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
    public GamePokemonCatalog? FourIslandOwnership { get; }
    public GameOpponentPokemonCatalog? FourIslandOpponents { get; }
    public FourIslandTrainerCatalog? FourIslandTrainers { get; }
    public FourIslandTrainerCatalog? FourIslandTrainerParties => FourIslandTrainers;
    public FourIslandItemContent? FourIslandItems { get; }
    public MachineCatalog? FourIslandMachines => FourIslandItems?.Machines;
    public FourIslandWaterEncounterCatalog? FourIslandWaterEncounters { get; }
    public bool FourIslandEnabled => FourIslandRegion is not null;

    private static void ValidateFourIslandAnchors(PretFourIslandRegion region,ImmutableDictionary<MapId, PretWorldMap> maps)
    {
        if (region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("Duplicate campaign map ID.");
    }


    private static void ValidateFourIslandComposition(ImmutableDictionary<MapId, PretWorldMap> maps, FourIslandTrainerCatalog trainers, PretStoryCompilation scripts)
    {
        FourIslandTrainerPartyFactory.ValidateCatalog(trainers);
        var battle = trainers.BattlesByScript[FourIslandTrainerPartyFactory.BattleScript];
        var scene = scripts.FindProgram(battle.ScriptEntry) ?? throw new InvalidDataException("four_island_scene_missing");
        var instructions = scene.Instructions.ToArray();
        int fight = Array.FindIndex(instructions, i => i.Op == StoryOp.TrainerBattle && i.Opcode == "trainerbattle_no_intro" &&
            i.Source.Path == battle.Source.Path && i.Source.Line >= battle.Source.StartLine && i.Source.Line <= battle.Source.EndLine &&
            i.Args.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_GRUNT_45","FourIsland_IcefallCave_Back_Text_GruntDefeat" }));
        int scientist = Array.FindIndex(instructions, i => i.Opcode == "setflag" && i.Args.SequenceEqual(new[] { "FLAG_HIDE_RUIN_VALLEY_SCIENTIST" }));
        int home = Array.FindIndex(instructions, i => i.Opcode == "clearflag" && i.Args.SequenceEqual(new[] { "FLAG_HIDE_LORELEI_IN_HER_HOUSE" }));
        int completed = Array.FindIndex(instructions, i => i.Opcode == "setvar" && i.Args.SequenceEqual(new[] { "VAR_MAP_SCENE_ICEFALL_CAVE_BACK","1" }));
        if (fight < 0 || scientist <= fight || home <= scientist || completed <= home ||
            instructions.Any(i => i.Op == StoryOp.Unsupported) || FourIslandTrainerPartyFactory.BasePrize(trainers, FourIslandTrainerPartyFactory.Grunt45) != 1216)
            throw new InvalidDataException("four_island_battle_reward_order");
        foreach (var symbol in new[] { "MAP_FOUR_ISLAND_POKEMON_CENTER_2F","MAP_FOUR_ISLAND_POKEMON_DAY_CARE","MAP_FOUR_ISLAND_HOUSE1","MAP_FOUR_ISLAND_HOUSE2","MAP_FOUR_ISLAND_LORELEIS_HOUSE" })
            if (maps.ContainsKey(PretFourIslandRegion.MapIdFromSourceSymbol(symbol)))
                throw new InvalidDataException("four_island_excluded_map_admitted");
    }

}

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

/// <summary>Four Island maps, Icefall Rocket scene and Lorelei’s doll collection.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string FourIslandContentProfile = "firered-four-island-campaign-v1";
    public PretFourIslandRegion FourIslandRegion { get; }
    public FourIslandTrainerCatalog? FourIslandTrainers { get; }
    public FourIslandTrainerCatalog? FourIslandTrainerParties => FourIslandTrainers;
    public FourIslandItemContent? FourIslandItems { get; }
    public WaterEncounterCatalog? FourIslandWaterEncounters { get; }
    public ImmutableDictionary<string, FieldStatFlagUnlocks> StatFlagUnlocks { get; }
    public ImmutableDictionary<string, FieldMilestoneCollection> FieldMilestones => FourIslandRegion?.FieldMilestones ?? ImmutableDictionary<string, FieldMilestoneCollection>.Empty;



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
        var house = maps[new("map.four-island-loreleis-house")];
        foreach (var actor in house.Events.GetProperty("object_events").EnumerateArray())
        {
            string script = actor.GetProperty("script").GetString()!;
            if (scripts.FindProgram(script) is not {} program || program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("lorelei_house_actor_script_missing");
        }
        if (scripts.FindProgram("FourIsland_LoreleisHouse_OnTransition") is not {} transition ||
            !transition.Instructions.Select(i => i.Opcode).SequenceEqual(new[] { "special", "end" }) ||
            !transition.Instructions[0].Args.SequenceEqual(new[] { "UpdateLoreleiDollCollection" }))
            throw new InvalidDataException("lorelei_house_collection_transition_missing");
        var stickerHouse = maps[new("map.four-island-house2")];
        var door = maps[new("map.four-island")].FieldWarps.Single(w => w.Index == 5);
        if (door is not { X: 25, Y: 26, DestinationWarpId: 0 } || door.DestinationMapId != stickerHouse.Id ||
            stickerHouse.FieldWarps.Length != 1 || stickerHouse.FieldWarps[0] is not { X: 4, Y: 7, DestinationWarpId: 5 } ||
            stickerHouse.FieldWarps[0].DestinationMapId != new MapId("map.four-island") ||
            stickerHouse.Events.GetProperty("object_events").GetArrayLength() != 1)
            throw new InvalidDataException("sticker_house_original_door_or_actor");
        foreach (var program in scripts.Programs.Values.Where(p => p.Id.StartsWith("FourIsland_House2_", StringComparison.Ordinal)))
            if (program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("sticker_house_unsupported_script:" + program.Id);
        if (scripts.FindProgram("FourIsland_House2_EventScript_StickerMan") is not { Instructions.IsEmpty: false })
            throw new InvalidDataException("sticker_house_missing_entry");
    }

}

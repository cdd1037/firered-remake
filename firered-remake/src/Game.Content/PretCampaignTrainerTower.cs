using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretTrainerTowerRegion TrainerTowerRegion { get; }
    public bool TrainerTowerEnabled => TrainerTowerRegion is not null;
    public TrainerTowerCatalog TrainerTowerCatalog { get; }
    public TrainerTowerOpponentPokemonCatalog TrainerTowerOpponents { get; }
    public TrainerTowerShopCatalog TrainerTowerShop { get; }
    private readonly ImmutableDictionary<(MapId Map, PretTrainerTowerLayoutFormat Format), ConnectedFieldMap> _trainerTowerConnectedMaps;

    public IFieldMap? TrainerTowerMap(MapId map,TrainerTowerBattleFormat format) =>
        TrainerTowerRegion.Maps.ContainsKey(map) ? TrainerTowerRegion.GetMap(map, format) : null;

    public ConnectedFieldMap? TrainerTowerConnectedMap(MapId map,TrainerTowerBattleFormat format)
    {
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        return _trainerTowerConnectedMaps.GetValueOrDefault((map, PretTrainerTowerLayoutFormat.Common)) ??
            _trainerTowerConnectedMaps.GetValueOrDefault((map, (PretTrainerTowerLayoutFormat)format));
    }

    public ImmutableDictionary<string, int> TrainerTowerNpcGraphics(TrainerTowerBattleFormat format,TrainerTowerFloorTemplate template) =>
        TrainerTowerRegion.NpcGraphicsValues(format, template);
    public int TrainerTowerTextGender(TrainerTowerFloorTemplate template,int trainerIndex,bool secondDoubleSpeaker)=>
        TrainerTowerRegion.OpponentTextGender(template,trainerIndex,secondDoubleSpeaker);
    public string TrainerTowerEncounterMusic(TrainerTowerFloorTemplate template,int trainerIndex) =>
        TrainerTowerRegion.EncounterMusic(template, trainerIndex);



    private static void ValidateTrainerTowerComposition(PretTrainerTowerRegion region, PretStoryCompilation scripts, TrainerTowerCatalog courses, TrainerTowerShopCatalog shop)
    {
        foreach (var kind in Enum.GetValues<TrainerTowerChallengeKind>())
        {
            var course = courses.Course(kind);
            if (course.Floors.Length != 8 || !shop.Catalog.Items.Any(i => i.SourceItemSymbol == course.PrizeItem))
                throw new InvalidDataException("trainer_tower_course_prize_binding");
            foreach (var floor in course.Floors)
            {
                var map = region.GetMap(new("map.trainer-tower-" + (floor.Index + 1) + "f"), floor.Format);
                if (map.Events.GetProperty("allow_cycling").GetBoolean() || map.Events.GetProperty("allow_escaping").GetBoolean() ||
                    !map.Events.GetProperty("allow_running").GetBoolean()) throw new InvalidDataException("trainer_tower_field_rules");
            }
        }
        var programs = scripts.Programs.Values.Where(p => p.Instructions.Any(i =>
            i.Source.Path == "data/scripts/trainer_tower.inc" || i.Source.Path.StartsWith("data/maps/TrainerTower_", StringComparison.Ordinal))).ToArray();
        if (programs.Length == 0 || programs.Any(p => p.Instructions.IsEmpty || p.Instructions.Any(i => i.Op == StoryOp.Unsupported)))
            throw new InvalidDataException("trainer_tower_story_incomplete");
        foreach (var label in new[] { "TrainerTower_Lobby_EventScript_EntryTrigger","TrainerTower_EventScript_DoBattle","TrainerTower_EventScript_SetFloorCleared","TrainerTower_EventScript_WarpToLobbyLost","TrainerTower_EventScript_SpeakToOwner","TrainerTower_Elevator_EventScript_FloorSelect" })
            if (scripts.FindProgram(label) is null) throw new InvalidDataException("trainer_tower_story_missing:" + label);
        var battle = scripts.FindProgram("TrainerTower_EventScript_DoBattle")!;
        if (battle.Instructions.Count(i => i.Op == StoryOp.TrainerBattle) != 1 || battle.Instructions.Any(i => i.Op == StoryOp.WaitState) ||
            !battle.Instructions[0].Args.SequenceEqual(new[] { "TRAINER_TOWER" }))
            throw new InvalidDataException("trainer_tower_battle_wait_binding");
    }
}

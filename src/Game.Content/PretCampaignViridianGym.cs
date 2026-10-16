using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string ViridianGymContentProfile="firered-viridian-gym-campaign-v1";
    public PretViridianGymRegion? ViridianGymRegion { get; }
    public ViridianGymTrainerCatalog? ViridianGymTrainerParties { get; }
    public bool ViridianGymEnabled=>ViridianGymRegion is not null;


    private static void ValidateViridianGymComposition(PretViridianGymRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,ViridianGymTrainerCatalog trainers,PretStoryCompilation scripts,MachineCatalog machines,GamePokemonCatalog owned)
    {
        var gym=new MapId("map.viridian-city-gym");
        ViridianGymTrainerPartyFactory.ValidateCatalog(trainers);
        var cityDoor = maps[new MapId("map.viridian-city")].FieldWarps.Single(w => w.Index == 2);
        if (cityDoor.X != 36 || cityDoor.Y != 10 || cityDoor.Elevation != 0 ||
            cityDoor.DestinationWarpId != 1 || cityDoor.DestinationMapId != gym)
            throw new InvalidDataException("viridian_gym_city_door");
        var sourceWarps=maps[gym].Warps;
        if(sourceWarps.Length!=3||sourceWarps.Any(w=>w.DestinationMapId.Value!="map.viridian-city"||w.DestinationWarpId!=2)||
            sourceWarps[1].X!=17||sourceWarps[1].Y!=22||maps[gym].SourceBehaviorAt(17,22)!=0x65)
            throw new InvalidDataException("viridian_gym_warp_source");
        foreach(var battle in trainers.BattlesByScript.Values)
        {
            var program=scripts.FindProgram(battle.ScriptEntry)??throw new InvalidDataException("viridian_gym_trainer_script_missing");
            if(!program.Instructions.Any(i=>i.Op==StoryOp.TrainerBattle&&i.Source.Path==battle.Source.Path&&
                i.Args[0]==battle.Trainer.Value[5..])||battle.PostBattleScript is {} callback&&scripts.FindProgram(callback) is null)
                throw new InvalidDataException("viridian_gym_trainer_source");
        }
        if(scripts.FindProgram("EventScript_SetViridianGymTrainers") is null||scripts.FindProgram("ViridianCity_EventScript_TryUnlockGym") is null)
            throw new InvalidDataException("viridian_gym_callback_missing");
    }

}

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



    private static void ValidateViridianGymAnchors(PretViridianGymRegion region,ImmutableDictionary<MapId,PretWorldMap> maps)
    {
        if (region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("Duplicate campaign map ID.");
    }

    private static void ValidateViridianGymComposition(PretViridianGymRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,ImmutableDictionary<MapId,PretWorldMap> previous,ViridianGymTrainerCatalog trainers,PretStoryCompilation scripts,PewterMachineCatalog machines,CinnabarOwnedPokemonCatalog owned)
    {
        var gym=new MapId("map.viridian-city-gym");
        if(owned.Catalog.Species.Count!=115||owned.Catalog.Moves.Count!=354||
            ViridianGymMachineCatalog.For(machines) is not {} proof)
            throw new InvalidDataException("viridian_gym_composition_scope");
        ViridianGymTrainerPartyFactory.ValidateCatalog(trainers);
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

    // Preserve every historical PretWorldMap and its source resolver. Only this successor's
    // executable city adapter resolves the old stable destination to the authenticated gym.
    private sealed class ViridianGymCityFieldMap:IFieldMap
    {
        private readonly IFieldMap _source;
        public ViridianGymCityFieldMap(IFieldMap source)
        {
            _source=source;
            var warp=source.FieldWarps.Single(w=>w.Index==2);
            if(source.Id.Value!="map.viridian-city"||warp.X!=36||warp.Y!=10||warp.Elevation!=0||
                warp.DestinationWarpId!=1||warp.DestinationMapId.Value!="map.viridian-gym")
                throw new InvalidDataException("viridian_gym_city_warp_alias");
            FieldWarps=source.FieldWarps.Select(w=>w.Index==2?w with{DestinationMapId=new("map.viridian-city-gym")}:w).ToImmutableArray();
        }
        public MapId Id=>_source.Id;
        public int Width=>_source.Width;
        public int Height=>_source.Height;
        public TileCell? GetTile(int x,int y)=>_source.GetTile(x,y);
        public int? SourceBehaviorAt(int x,int y)=>_source.SourceBehaviorAt(x,y);
        public int? SourceEncounterTypeAt(int x,int y)=>_source.SourceEncounterTypeAt(x,y);
        public ImmutableArray<FieldWarp> FieldWarps { get; }
    }
}

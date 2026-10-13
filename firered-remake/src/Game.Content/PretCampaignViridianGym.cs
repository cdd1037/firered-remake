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

    private static void AppendViridianGymSources(PretViridianGymRegion region,List<PretSourceScriptFile> sources,
        List<string> texts,Dictionary<string,int> constants,List<string> hashes)
    {
        var prior=sources.ToDictionary(s=>s.Path,StringComparer.Ordinal);
        foreach(var source in region.Scripts)
        {
            if(prior.TryGetValue(source.Path,out var old))
            {
                if(old.SourceRef.Sha256!=source.SourceRef.Sha256||old.Text!=source.Text)
                    throw new InvalidDataException("viridian_gym_source_conflict: "+source.Path);
            }
            else{prior.Add(source.Path,source);sources.Add(source);}
            if(!texts.Contains(source.Text,StringComparer.Ordinal))texts.Add(source.Text);
        }
        foreach(var text in region.TextSources.Values)if(!texts.Contains(text,StringComparer.Ordinal))texts.Add(text);
        MergeRegionalServiceConstants(constants,region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("viridian-gym-trainers:"+PretViridianGymRules.TrainersSha256);
        hashes.Add("viridian-gym-machines:"+PretViridianGymRules.MachinesSha256);
        hashes.Add("viridian-gym-rules:"+PretViridianGymRules.RulesSha256+":"+PretViridianGymRules.SourceInputsSha256);
        hashes.Add("viridian-gym-campaign:"+ViridianGymContentProfile);
    }

    private static void ValidateViridianGymAnchors(PretViridianGymRegion region,ImmutableDictionary<MapId,PretWorldMap> maps)
    {
        if(maps.Count!=206||region.NewMaps.Count!=1||region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("viridian_gym_predecessor_maps");
        if(!JsonElement.DeepEquals(maps[new("map.viridian-city")].Events,region.ReusedAnchorEvents["ViridianCity"]))
            throw new InvalidDataException("viridian_gym_anchor_events");
    }

    private static void ValidateViridianGymComposition(PretViridianGymRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,
        ImmutableDictionary<MapId,PretWorldMap> previous,ViridianGymTrainerCatalog trainers,PretStoryCompilation scripts,
        PewterMachineCatalog machines,CinnabarOwnedPokemonCatalog owned)
    {
        var gym=new MapId("map.viridian-city-gym");
        if(maps.Count!=207||previous.Any(p=>!ReferenceEquals(p.Value,maps[p.Key]))||
            !ReferenceEquals(region.NewMaps[gym],maps[gym])||owned.Catalog.Species.Count!=115||owned.Catalog.Moves.Count!=354||
            ViridianGymMachineCatalog.For(machines) is not {} proof||!ReferenceEquals(proof.Pokemon,owned))
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

using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

// A bounded visible map projection, not a campaign or executable script snapshot.
public sealed record CampaignQuestLogMapProjection(string Map,ImmutableArray<CampaignQuestLogActor> Actors,string? Layout,ImmutableArray<FieldMetatileEdit> Tiles)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? FlashLevel{get;init;}
}

public static partial class CampaignQuestLogRecorder
{
    public const int VisualMemoryLimit=1636;
    private static CampaignQuestLogScene CurrentProjection(CampaignQuestLogScene scene)
    {
        var projection=scene.Actions.LastOrDefault(a=>a.Transition is not null)?.Transition;
        return projection is null?scene:ProjectionScene(projection);
    }
    private static CampaignQuestLogScene ProjectionScene(CampaignQuestLogMapProjection projection)=>new(projection.Map,projection.Actors,[]){Layout=projection.Layout,Tiles=projection.Tiles,FlashLevel=projection.FlashLevel};
    private static int ProjectionBytes(CampaignQuestLogScene scene)=>scene.Actors.IsDefault?VisualMemoryLimit+1:8+scene.Actors.Length*20+(scene.Layout is null?0:2)+(scene.Tiles.IsDefault?0:scene.Tiles.Length*5)+(scene.FlashLevel.HasValue?1:0);
    // One explicit allocation across every segment. The fixed256 reservation covers
    // all ordinary sparse deltas/actions; map-entry projections share the remainder.
    public static int VisualMemoryBudget(CampaignQuestLogScene scene)=>QuestLogSource.ScriptBytes+ProjectionBytes(scene)+scene.Actions.Where(a=>a is not null&&a.Transition is not null).Sum(a=>ProjectionBytes(ProjectionScene(a.Transition!)));
    private static string? ValidateProjection(CampaignQuestLogScene scene,QuestLogSource? source,ICampaignCatalog? catalog)
    {
        if(!Identifier(scene.Map)||source is not null&&!source.MapNames.ContainsKey(scene.Map))return "save_quest_log_map_invalid";
        if(scene.Actors.IsDefault||scene.Actors.Length is <1 or >ActorLimit||scene.Actors.Any(a=>!ValidActor(a))||scene.Actors.Select(a=>a.Id).Distinct().Count()!=scene.Actors.Length||scene.Actors.Count(a=>a.Id==PlayerId)!=1)return "save_quest_log_actor_invalid";
        if(ValidateAppearance(scene,catalog) is {} error)return error;
        if(catalog is not null)
        {
            if(!scene.Tiles.IsDefault&&scene.Tiles.Any(t=>!ValidAppearanceCoordinate(catalog,scene,t)))return "save_quest_log_tiles_invalid";
            if(scene.Layout is {} layout&&catalog.MapLayout(new(scene.Map),layout) is null)return "save_quest_log_layout_invalid";
        }
        return null;
    }
    private static bool TryRecordMapTransition(CampaignQuestLogScene scene,CampaignState after,QuestLogSource? source,int? recordingFrame,out CampaignQuestLogScene updated)
    {
        updated=scene;
        var destination=CaptureAppearance(new(after.World.Player.MapId.Value,Actors(after,source),[]),after);
        if(destination is null)return false;
        int start=Math.Max(recordingFrame??0,scene.Actions.Where(a=>!IsCaption(a)).Select(a=>a.StartFrame+a.Frames).DefaultIfEmpty(0).Max());
        if(start>=ushort.MaxValue)return false;
        var marker=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,1)
        {StartFrame=start,Transition=new(destination.Map,destination.Actors,destination.Layout,destination.Tiles){FlashLevel=destination.FlashLevel}};
        var candidate=scene with{Actions=scene.Actions.Add(marker)};
        if(!FitsActions(candidate.Actions,source)||VisualMemoryBudget(candidate)>VisualMemoryLimit)return false;
        updated=candidate;return true;
    }
}

using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

public static partial class CampaignQuestLogRecorder
{
    // Our compact projection allocation, informed by the source's fixed0x668 scene:
    // header8 + objects16*20 + layout2 + edits210*5 + script256 =1636 bytes.
    // This is a logical bound, not the original format or a JSON-byte claim.
    public const int AppearanceTileLimit=210;
    private static bool ValidTile(FieldMetatileEdit tile,bool removal=false)=>tile.X is >=-7 and <1024&&tile.Y is >=-7 and <1024&&tile.SourceMetatileId>=(removal?-1:0)&&tile.SourceMetatileId<1024&&tile.Collision is >=0 and <=3&&tile.Elevation is >=0 and <=15;
    private static bool ValidAppearanceCoordinate(ICampaignCatalog catalog,CampaignQuestLogScene scene,FieldMetatileEdit tile)
    {
        var id=new Game.Domain.Ids.MapId(scene.Map);
        var map=scene.Layout is {} layout?catalog.MapLayout(id,layout):catalog.Maps.GetValueOrDefault(id);
        if(map?.GetTile(tile.X,tile.Y) is not null)return true;
        return catalog.ConnectedFieldMaps is {} maps&&ConnectedFieldCoordinates.Resolve(maps,id,tile.X,tile.Y) is not null;
    }
    private static string? ValidateAppearance(CampaignQuestLogScene scene,ICampaignCatalog? catalog=null)
    {
        if(catalog is not null&&!catalog.Maps.ContainsKey(new(scene.Map)))return "save_quest_log_map_invalid";
        if(scene.FlashLevel is {} level&&(level is not (0 or FieldFlashSourcePresentation.MaximumLevel)||
            level!=0&&catalog is not null&&(!catalog.Maps.ContainsKey(new(scene.Map))||!catalog.MapDefinition(new(scene.Map)).RequiresFlash)))return "save_quest_log_flash_level_invalid";
        if(scene.Layout is {} layout&&!Identifier(layout))return "save_quest_log_layout_invalid";
        if(!scene.Tiles.IsDefault&&(scene.Tiles.Length>AppearanceTileLimit||scene.Tiles.Any(t=>t is null||!ValidTile(t))||scene.Tiles.Select(t=>(t.X,t.Y)).Distinct().Count()!=scene.Tiles.Length))return "save_quest_log_tiles_invalid";
        return null;
    }
    private static CampaignQuestLogScene? CaptureAppearance(CampaignQuestLogScene scene,CampaignState state)
    {
        // quest_log.c records/restores the scene flags. Keep only the stable visible
        // consequence here, without executable flags or inferred historical state.
        var result=scene with{Layout=state.MapLayout,Tiles=state.MapEdits?.Values.ToImmutableArray()??[],FlashLevel=state.FlashLevel};
        return ValidateAppearance(result) is null&&VisualMemoryBudget(result)<=VisualMemoryLimit?result:null;
    }
    private static bool TryRecordAppearance(CampaignQuestLogScene scene,CampaignState before,CampaignState after,QuestLogSource? source,out CampaignQuestLogScene updated,int? recordingFrame=null)
    {
        updated=scene;
        // Flash use already ends the scene at its field-move event. Any other
        // unrepresented visibility change cuts recording rather than rewriting it.
        if(before.FlashLevel!=after.FlashLevel)return false;
        if(before.MapLayout==after.MapLayout&&ReferenceEquals(before.MapEdits,after.MapEdits))return true;
        var actions=scene.Actions;int start=actions.Where(a=>a.Event==CampaignQuestLogEvent.None).Select(a=>a.StartFrame+a.Frames).DefaultIfEmpty(0).Max();
        start=Math.Max(start,recordingFrame??0);
        var additions=ImmutableArray.CreateBuilder<CampaignQuestLogAction>();
        CampaignQuestLogAction Action()=>new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,1){StartFrame=start};
        if(before.MapLayout!=after.MapLayout)additions.Add(Action() with{Layout=after.MapLayout??""});
        var old=before.MapEdits?.Values.ToDictionary(t=>(t.X,t.Y))??[];
        var next=after.MapEdits?.Values.ToDictionary(t=>(t.X,t.Y))??[];
        foreach(var (position,tile) in next)if(!old.TryGetValue(position,out var prior)||prior!=tile)additions.Add(Action() with{Tile=tile});
        foreach(var (position,tile) in old)if(!next.ContainsKey(position))additions.Add(Action() with{Tile=tile with{SourceMetatileId=-1}});
        if(additions.Count==0)return true;
        var combined=actions.AddRange(additions);
        // Stop before the unrepresentable change. Never truncate a patch set or show
        // a partly applied room. The next qualifying event may begin a fresh scene.
        if(!FitsActions(combined,source))return false;
        updated=scene with{Actions=combined};return true;
    }
}

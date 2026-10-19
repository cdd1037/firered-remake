using System.Text.Json;
using System.Collections.Immutable;
using Game.Domain.World;
using Godot;
public partial class CampaignView
{
    private Dictionary<Game.Domain.Ids.MapId,MapArt> _borderEditArt=[];
    private string? _mapEditKey;
    private ImmutableSortedDictionary<string,FieldMetatileEdit>? _mapEditState;
    private CampaignResourceCache.Owner? _mapEditOwner;
    private CampaignResourceCache.Owner? _drawMapEditOwner;
    private void ReleaseMapEdits()
    {
        if(!ReferenceEquals(_mapEditOwner,_drawMapEditOwner))_mapEditOwner?.Dispose();
        _mapEditOwner=null;_mapEditKey=null;_mapEditState=null;_borderEditArt.Clear();
    }
    private void BeginMapFrame()
    {
        // Godot has cleared the previous canvas command list before calling _Draw.
        if(!ReferenceEquals(_drawMapEditOwner,_mapEditOwner))_drawMapEditOwner?.Dispose();
        _drawMapEditOwner=null;
    }
    private void ApplyMapEdits(Game.Application.CampaignState state)
    {
        // Immutable edit collections can be reused by identity; no JSON/hash work on ordinary movement.
        var key=PresentationMapKey(state.World.Player.MapId);
        if(_mapEditKey==key&&ReferenceEquals(_mapEditState,state.MapEdits))return;
        var original=GetMapArt(state.World.Player.MapId);
        if(state.MapEdits is not {Count:>0}){_art=original;ReleaseMapEdits();_mapEditKey=key;_mapEditState=state.MapEdits;return;}
        var replacement=_mapResources.CreateOwner("map-edits");
        try
        {
            var projected=state.MapEdits.Values.Select(edit=>(Edit:edit,Cell:ConnectedFieldCoordinates.Resolve(_catalog.ConnectedFieldMaps!,state.World.Player.MapId,edit.X,edit.Y)))
                .Where(pair=>pair.Cell is not null).GroupBy(pair=>pair.Cell!.Value.Map);
            var patchedMaps=new Dictionary<Game.Domain.Ids.MapId,MapArt>();
            foreach(var group in projected)
            {
                var art=GetMapArt(group.Key);
                var edits=group.Select(p=>p.Edit with{X=p.Cell!.Value.X,Y=p.Cell.Value.Y}).ToArray();
                patchedMaps[group.Key]=art with{Bottom=PatchMapLayer(replacement,art.Source,art.Bottom,0,edits),Middle=PatchMapLayer(replacement,art.Source,art.Middle,1,edits),Top=PatchMapLayer(replacement,art.Source,art.Top,2,edits)};
            }
            _art=patchedMaps.GetValueOrDefault(state.World.Player.MapId,original);
            ReleaseMapEdits();_borderEditArt=patchedMaps;_mapEditOwner=replacement;_mapEditKey=key;_mapEditState=state.MapEdits;
        }
        catch{replacement.Dispose();throw;}
    }
    private IReadOnlyDictionary<string,Game.Content.OverworldSprite>? _overworldSprites;
    private Dictionary<string,SpriteAsset> LoadWorldNpcs(Game.Content.PretWorldMap map,string catalogPath,CampaignResourceCache.Owner owner)
    {
        var generated=System.IO.Path.Combine(_root,"content/generated");
        _overworldSprites ??= Game.Content.PretOverworldTextures.Load(generated);
        if(System.IO.Path.GetFullPath(catalogPath)!=System.IO.Path.GetFullPath(System.IO.Path.Combine(generated,"sprites/overworld/catalog.json")))
            throw new InvalidDataException("Unexpected overworld catalog binding.");
        var graphics=map.Events.GetProperty("object_events").EnumerateArray()
            .Select(entry=>entry.GetProperty("graphics_id").GetString()!).ToHashSet(StringComparer.Ordinal);
        // Variable and cloned actors can change graphics through executable story
        // state. The shared catalog contains their source-selected possibilities.
        if(graphics.Any(id=>id=="0"||id.StartsWith("OBJ_EVENT_GFX_VAR_",StringComparison.Ordinal)))
            graphics=_overworldSprites.Keys.ToHashSet(StringComparer.Ordinal);
        var result=new Dictionary<string,SpriteAsset>(StringComparer.Ordinal);
        foreach(var id in graphics)
        {
            var sprite=_overworldSprites[id]; var r=sprite.Region;
            var texture=owner.NativeTexture(System.IO.Path.Combine(generated,"sprites/overworld",sprite.TexturePath),new Rect2I(r.X,r.Y,r.Width,r.Height));
            if(r.Width!=sprite.FrameWidth*sprite.FrameCount||r.Height!=sprite.FrameHeight)
                throw new InvalidDataException("Region NPC sheet size invalid.");
            result.Add(id,new(texture,sprite.FrameWidth,sprite.FrameHeight,sprite.FrameCount,
                new(sprite.AnchorX,sprite.AnchorY),sprite.Inanimate));
        }
        return result;
    }
    private string ResolveRegionGraphics(string graphics)
    {
        if(TrainerTowerGraphics(graphics) is {} towerGraphics)return towerGraphics;
        if(graphics!="OBJ_EVENT_GFX_VAR_0")return graphics;
        var value=_session.State.Script.Variables.GetValueOrDefault("VAR_OBJ_GFX_ID_0");
        if(_session.State.World.Player.MapId.Value=="map.birth-island-exterior"&&
            _catalog.TryConstant("OBJ_EVENT_GFX_DEOXYS_N",out var deoxys)&&value==deoxys)return "OBJ_EVENT_GFX_DEOXYS_N";
        foreach(var candidate in new[]{"OBJ_EVENT_GFX_OLD_MAN_LYING_DOWN","OBJ_EVENT_GFX_OLD_MAN_1","OBJ_EVENT_GFX_OMANYTE","OBJ_EVENT_GFX_KABUTO"})if(_catalog.TryConstant(candidate,out var id)&&id==value)return candidate;
        throw new InvalidDataException("unsupported_dynamic_npc_graphics:"+value);
    }
    private bool IsConnectedNeighborCell(int x,int y)
    {
        if(_catalog.ConnectedFieldMaps is not {} maps||!maps.TryGetValue(_art.Source.Id,out var current))return false;
        foreach(var c in current.Connections)
        {
            if(!maps.TryGetValue(c.DestinationMapId,out var d))continue;
            var origin=c.Direction switch{Direction.North=>(c.Offset,-d.Height),Direction.South=>(c.Offset,current.Height),Direction.West=>(-d.Width,c.Offset),_=>(current.Width,c.Offset)};
            if(x>=origin.Item1&&y>=origin.Item2&&x<origin.Item1+d.Width&&y<origin.Item2+d.Height)return true;
        }
        return false;
    }
    private IEnumerable<(Game.Domain.Ids.MapId Id,Vector2 Origin)> VisibleMapNeighbors(Vector2 camera)
    {
        if(_catalog.ConnectedFieldMaps is not {} maps||!maps.TryGetValue(_art.Source.Id,out var current))yield break;
        foreach(var connection in current.Connections)
        {
            if(!maps.TryGetValue(connection.DestinationMapId,out var destination))continue;
            var origin=connection.Direction switch
            {
                Direction.North=>new Vector2(connection.Offset*16,-destination.Height*16),
                Direction.South=>new Vector2(connection.Offset*16,current.Height*16),
                Direction.West=>new Vector2(-destination.Width*16,connection.Offset*16),
                _=>new Vector2(current.Width*16,connection.Offset*16)
            };
            if(!new Rect2(origin,new(destination.Width*16,destination.Height*16)).Intersects(new(camera,new(240,160))))continue;
            yield return (destination.Id,origin);
        }
    }
    private void PrepareVisibleMapResources(Vector2 camera)
    {
        BeginMapFrame();_drawMapEditOwner=_mapEditOwner;
        _drawMapPins.Clear();_drawMapPins.Add(PresentationMapKey(_art.Source.Id));
        foreach(var neighbor in VisibleMapNeighbors(camera))_drawMapPins.Add(PresentationMapKey(neighbor.Id));
        MapCache.SetPins(_drawMapPins);
    }
    private void DrawConnectedNeighbors(Vector2 camera,int layer, Rid? target = null)
    {
        foreach(var neighbor in VisibleMapNeighbors(camera))
        {
            var art=_borderEditArt.TryGetValue(neighbor.Id,out var patched)?patched:GetMapArt(neighbor.Id);DrawMapTexture(layer==0?art.Bottom:layer==1?art.Middle:art.Top,neighbor.Origin-camera, target);
        }
    }
}

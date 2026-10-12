using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.World;
using Godot;
public partial class CampaignView
{
    private string? _mapEditKey;
    private CampaignResourceCache.Owner? _mapEditOwner;
    private CampaignResourceCache.Owner? _drawMapEditOwner;
    private void ReleaseMapEdits()
    {
        if(!ReferenceEquals(_mapEditOwner,_drawMapEditOwner))_mapEditOwner?.Dispose();
        _mapEditOwner=null;_mapEditKey=null;
    }
    private void BeginMapFrame()
    {
        // Godot has cleared the previous canvas command list before calling _Draw.
        if(!ReferenceEquals(_drawMapEditOwner,_mapEditOwner))_drawMapEditOwner?.Dispose();
        _drawMapEditOwner=null;
    }
    private void ApplyMapEdits(Game.Application.CampaignState state)
    {
        var key=state.World.Player.MapId.Value+":"+Game.Application.CanonicalJson.Hash(state.MapEdits);
        if(_mapEditKey==key)return;
        var original=GetMapArt(state.World.Player.MapId);
        if(state.MapEdits is not {Count:>0}){_art=original;ReleaseMapEdits();_mapEditKey=key;return;}
        var definitions=state.World.Player.MapId.Value=="map.vermilion-city-gym"
            ?_catalog.VermilionGymRegion?.BeamDefinitions:_catalog.MartMetatiles?.Definitions;
        if(definitions is null&&_catalog.CinnabarRegion?.NewMaps.ContainsKey(state.World.Player.MapId)!=true&&_catalog.SilphRegion?.NewMaps.ContainsKey(state.World.Player.MapId)!=true&&_catalog.RocketHideoutRegion?.NewMaps.ContainsKey(state.World.Player.MapId)!=true)throw new InvalidDataException("Metatile art missing for "+state.World.Player.MapId.Value);
        var replacement=_mapResources.CreateOwner("map-edits");
        using var tiles=_mapResources.CreateOwner("map-edit-tiles");
        Texture2D Patch(Texture2D texture,int layer)
        {
            using var canvas=texture.GetImage();
            foreach(var edit in state.MapEdits.Values)
            {
                if(TryCinnabarPatch(canvas,edit,layer,state.World.Player.MapId))continue;
                if(TrySilphPatch(canvas,edit,layer,state.World.Player.MapId))continue;
                if(TryRocketPatch(canvas,edit,layer,state.World.Player.MapId.Value))continue;
                var d=definitions![edit.SourceMetatileId];var path=layer==0?d.BottomTexturePath:layer==1?d.MiddleTexturePath:d.TopTexturePath;
                using var tile=tiles.Texture(path).GetImage();canvas.BlitRect(tile,new(0,0,16,16),new(edit.X*16,edit.Y*16));
            }
            return replacement.FromImage(canvas);
        }
        try
        {
            var patched=original with{Bottom=Patch(original.Bottom,0),Middle=Patch(original.Middle,1),Top=Patch(original.Top,2)};
            _art=patched;ReleaseMapEdits();_mapEditOwner=replacement;_mapEditKey=key;
        }
        catch{replacement.Dispose();throw;}
    }
    private Dictionary<string,SpriteAsset> LoadRegionNpcs(string manifest,CampaignResourceCache.Owner owner)
    {
        // PretViridianRegion authenticated this exact manifest and every PNG before exposing maps.
        var root=System.IO.Path.GetDirectoryName(manifest)!;using var doc=JsonDocument.Parse(System.IO.File.ReadAllBytes(manifest));var result=new Dictionary<string,SpriteAsset>(StringComparer.Ordinal);
        foreach(var e in doc.RootElement.GetProperty("graphics").EnumerateArray())
        {
            var path=e.GetProperty("texturePath").GetString()!;
            if(System.IO.Path.IsPathRooted(path)||path.Split('/').Contains(".."))throw new InvalidDataException("Region NPC path invalid.");
            var full=System.IO.Path.Combine(root,path);
            if(Convert.ToHexStringLower(SHA256.HashData(System.IO.File.ReadAllBytes(full)))!=e.GetProperty("sha256").GetString())throw new InvalidDataException("Region NPC image differs.");
            var texture=owner.Texture(full,expectedSha256:e.GetProperty("sha256").GetString()!);int w=e.GetProperty("frameWidth").GetInt32(),h=e.GetProperty("frameHeight").GetInt32(),count=e.GetProperty("frameCount").GetInt32();var anchor=e.GetProperty("tileCenterAnchor");
            if(texture.GetWidth()!=w*count||texture.GetHeight()!=h)throw new InvalidDataException("Region NPC sheet size invalid.");
            bool inanimate=e.GetProperty("declaration").GetProperty("inanimate").GetString()=="TRUE";
            result.Add(e.GetProperty("graphicsId").GetString()!,new(texture,w,h,count,new(anchor.GetProperty("x").GetInt32(),anchor.GetProperty("y").GetInt32()),inanimate));
        }
        return result;
    }
    private string ResolveRegionGraphics(string graphics)
    {
        if(graphics!="OBJ_EVENT_GFX_VAR_0")return graphics;
        var value=_session.State.Script.Variables.GetValueOrDefault("VAR_OBJ_GFX_ID_0");
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
        _drawMapPins.Clear();_drawMapPins.Add(_art.Source.Id.Value);
        foreach(var neighbor in VisibleMapNeighbors(camera))_drawMapPins.Add(neighbor.Id.Value);
        MapCache.SetPins(_drawMapPins);
    }
    private void DrawConnectedNeighbors(Vector2 camera,int layer)
    {
        foreach(var neighbor in VisibleMapNeighbors(camera))
        {
            var art=GetMapArt(neighbor.Id);DrawTexture(layer==0?art.Bottom:layer==1?art.Middle:art.Top,neighbor.Origin-camera);
        }
    }
}

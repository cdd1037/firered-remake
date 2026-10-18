using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    // One detached field projection serves recap and live map transitions. Poses
    // belong to the visible snapshot, never to the already-committed destination.
    private sealed record FieldSnapshot(object Owner,int Scene,int Revision,bool SavedGameVisible,
        CampaignQuestLogMapProjection Projection,IReadOnlyDictionary<string,Pose> Poses,double Frame,int PlayerGender);
    private (object Owner,int Scene,int Revision,bool Saved)? _fieldSnapshotArtState;
    private int _fieldSnapshotArtSerial;
    private string? _fieldSnapshotArtKey;
    private void ResetQuestLogArt()
    {
        _fieldSnapshotArtState=null;_fieldSnapshotArtKey=null;
    }
    private string FieldSnapshotNeighborArtKey(MapId map)=>_fieldSnapshotArtKey+":"+map.Value+":neighbor";
    private MapArt GetFieldSnapshotMapArt(FieldSnapshot snapshot,IReadOnlyList<(MapId Map,Vector2 Origin)> neighbors)
    {
        var state=(snapshot.Owner,snapshot.Scene,snapshot.Revision,snapshot.SavedGameVisible);
        var projection=snapshot.Projection;
        if(_fieldSnapshotArtState!=state)
        {
            _fieldSnapshotArtState=state;
            _fieldSnapshotArtKey=projection.Map+":"+(projection.Layout??"base")+":snapshot:"+(++_fieldSnapshotArtSerial);
        }
        _drawMapPins.Clear();_drawMapPins.Add(PresentationMapKey(_session.State.World.Player.MapId));
        // Pin the complete frame before acquiring anything. Derived palette keys may
        // borrow these originals until the visible set changes on the following draw.
        foreach(var neighbor in neighbors)_drawMapPins.Add(FieldSnapshotNeighborArtKey(neighbor.Map));
        if(snapshot.SavedGameVisible){MapCache.SetPins(_drawMapPins);return _art;}
        _drawMapPins.Add(_fieldSnapshotArtKey!);MapCache.SetPins(_drawMapPins);
        return MapCache.Get(_fieldSnapshotArtKey!,owner=>
        {
            var id=new MapId(projection.Map);
            var source=projection.Layout is {} layout?(PretWorldMap)(_catalog.MapLayout(id,layout)??throw new InvalidDataException("quest_log_layout_art_missing")):_catalog.SourceMaps[id];
            var binding=(_mapPresentation??=PretCampaignMapPresentation.Create(_catalog))[id];
            var npcs=LoadWorldNpcs(source,binding.OverworldCatalogPath,owner);
            foreach(var graphics in projection.Actors.Where(a=>a.Id!=CampaignQuestLogRecorder.PlayerId).Select(a=>a.Graphics).Distinct(StringComparer.Ordinal))
            {
                if(npcs.ContainsKey(graphics))continue;
                var sprite=_overworldSprites![graphics];var r=sprite.Region;
                var texture=owner.NativeTexture(Path.Combine(_root,"content/generated/sprites/overworld",sprite.TexturePath),new(r.X,r.Y,r.Width,r.Height));
                npcs.Add(graphics,new(texture,sprite.FrameWidth,sprite.FrameHeight,sprite.FrameCount,new(sprite.AnchorX,sprite.AnchorY),sprite.Inanimate));
            }
            var bottom=owner.MapTexture(source.BottomTexturePath);var middle=owner.MapTexture(source.MiddleTexturePath);var top=owner.MapTexture(source.TopTexturePath);
            var original=new MapArt(source,bottom,middle,top,new(owner.Texture(source.PlayerTexturePath),16,32,9,new(8,24),false),npcs,
                binding.UsesLocalBorder?new(source.BorderWidth,source.BorderHeight,owner.MapTexture(source.BorderBottomTexturePath),owner.MapTexture(source.BorderMiddleTexturePath),owner.MapTexture(source.BorderTopTexturePath)):null);
            if(projection.Tiles.IsDefaultOrEmpty)return original;
            var edits=projection.Tiles.Where(edit=>source.GetTile(edit.X,edit.Y) is not null).ToArray();
            Texture2D Patch(Texture2D texture,int layer)=>PatchMapLayer(owner,source,texture,layer,edits);
            return original with{Bottom=Patch(bottom,0),Middle=Patch(middle,1),Top=Patch(top,2)};
        });
    }
    private IEnumerable<(MapId Map,Vector2 Origin)> FieldSnapshotNeighbors(MapId map)
    {
        if(_catalog.ConnectedFieldMaps is not {} maps||!maps.TryGetValue(map,out var current))yield break;
        foreach(var connection in current.Connections)
        {
            if(!maps.TryGetValue(connection.DestinationMapId,out var destination))continue;
            var origin=connection.Direction switch
            {Direction.North=>new Vector2(connection.Offset*16,-destination.Height*16),Direction.South=>new Vector2(connection.Offset*16,current.Height*16),Direction.West=>new Vector2(-destination.Width*16,connection.Offset*16),_=>new Vector2(current.Width*16,connection.Offset*16)};
            yield return(destination.Id,origin);
        }
    }
    private void DrawFieldSnapshotLayer(FieldSnapshot snapshot,MapArt art,Vector2 camera,int layer,IReadOnlyList<(MapId Map,Vector2 Origin)> neighbors)
    {
        if(art.Border is {} border)
        {
            var texture=FieldSnapshotPaletteTexture(layer==0?border.Bottom:layer==1?border.Middle:border.Top,true);
            for(int y=(int)Math.Floor(camera.Y/16);y<=Math.Floor((camera.Y+159)/16);y++)for(int x=(int)Math.Floor(camera.X/16);x<=Math.Floor((camera.X+239)/16);x++)
            {
                if(x>=0&&y>=0&&x<art.Source.Width&&y<art.Source.Height)continue;
                if(neighbors.Any(n=>new Rect2(n.Origin,new(_catalog.Maps[n.Map].Width*16,_catalog.Maps[n.Map].Height*16)).HasPoint(new(x*16,y*16))))continue;
                DrawTextureRectRegion(texture,new(new Vector2(x*16,y*16)-camera,new(16,16)),new(MapBorderPattern.Coordinate(x,border.Width)*16,MapBorderPattern.Coordinate(y,border.Height)*16,16,16));
            }
        }
        foreach(var neighbor in neighbors)
        {
            var source=_catalog.SourceMaps[neighbor.Map];
            // Neighbor layouts are static source connections; never project the current
            // saved session's dynamic layout onto a different historical scene.
            var key=FieldSnapshotNeighborArtKey(neighbor.Map);
            var texture=MapCache.Get(key,owner=>
            {
                Texture2D Layer(string path,int number)
                {
                    var original=owner.MapTexture(path);
                    var edits=snapshot.Projection.Tiles.Select(edit=>(Edit:edit,At:_catalog.ConnectedFieldMaps is {} maps?ConnectedFieldCoordinates.Resolve(maps,art.Source.Id,edit.X,edit.Y):null)).Where(e=>e.At?.Map==neighbor.Map).ToArray();
                    if(edits.Length==0)return original;
                    return PatchMapLayer(owner,source,original,number,
                        edits.Select(row=>row.Edit with{X=row.At!.Value.X,Y=row.At.Value.Y}));
                }
                return new MapArt(source,Layer(source.BottomTexturePath,0),Layer(source.MiddleTexturePath,1),Layer(source.TopTexturePath,2),art.Player,[],null);
            });
            DrawTexture(FieldSnapshotPaletteTexture(layer==0?texture.Bottom:layer==1?texture.Middle:texture.Top,true),neighbor.Origin-camera);
        }
        DrawTexture(FieldSnapshotPaletteTexture(layer==0?art.Bottom:layer==1?art.Middle:art.Top,true),-camera);
    }
}

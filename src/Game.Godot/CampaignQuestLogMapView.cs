using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private CampaignQuestLogPlayback? _questArtPlayback;
    private int _questArtScene=-1,_questArtRevision=-1,_questArtSerial;
    private string? _questArtKey;
    private MapArt GetQuestLogMapArt(CampaignQuestLogPlayback playback)
    {
        if(playback.SavedGameVisible)return _art;
        if(!ReferenceEquals(playback,_questArtPlayback)||playback.SceneIndex!=_questArtScene||playback.ArtRevision!=_questArtRevision)
        {
            _questArtPlayback=playback;_questArtScene=playback.SceneIndex;_questArtRevision=playback.ArtRevision;
            _questArtKey=playback.Map+":"+(playback.Layout??"base")+":quest:"+(++_questArtSerial);
        }
        _drawMapPins.Clear();_drawMapPins.Add(_questArtKey!);_drawMapPins.Add(PresentationMapKey(_session.State.World.Player.MapId));MapCache.SetPins(_drawMapPins);
        return MapCache.Get(_questArtKey!,owner=>
        {
            var id=new MapId(playback.Map);
            var source=playback.Layout is {} layout?(PretWorldMap)(_catalog.MapLayout(id,layout)??throw new InvalidDataException("quest_log_layout_art_missing")):_catalog.SourceMaps[id];
            var binding=(_mapPresentation??=PretCampaignMapPresentation.Create(_catalog))[id];
            var npcs=LoadWorldNpcs(source,binding.OverworldCatalogPath,owner);
            foreach(var graphics in playback.Actors.Where(a=>a.Id!=CampaignQuestLogRecorder.PlayerId).Select(a=>a.Graphics).Distinct(StringComparer.Ordinal))
            {
                if(npcs.ContainsKey(graphics))continue;
                var sprite=_overworldSprites![graphics];var r=sprite.Region;
                var texture=owner.NativeTexture(Path.Combine(_root,"content/generated/sprites/overworld",sprite.TexturePath),new(r.X,r.Y,r.Width,r.Height));
                npcs.Add(graphics,new(texture,sprite.FrameWidth,sprite.FrameHeight,sprite.FrameCount,new(sprite.AnchorX,sprite.AnchorY),sprite.Inanimate));
            }
            var bottom=owner.MapTexture(source.BottomTexturePath);var middle=owner.MapTexture(source.MiddleTexturePath);var top=owner.MapTexture(source.TopTexturePath);
            var original=new MapArt(source,bottom,middle,top,new(owner.Texture(source.PlayerTexturePath),16,32,9,new(8,24),false),npcs,
                binding.UsesLocalBorder?new(source.BorderWidth,source.BorderHeight,owner.MapTexture(source.BorderBottomTexturePath),owner.MapTexture(source.BorderMiddleTexturePath),owner.MapTexture(source.BorderTopTexturePath)):null);
            if(playback.Tiles.IsDefaultOrEmpty)return original;
            Texture2D Patch(Texture2D layerTexture,int layer)
            {
                using var canvas=(Image)layerTexture.GetImage().Duplicate();
                foreach(var edit in playback.Tiles)
                {
                    if(source.GetTile(edit.X,edit.Y) is null)continue; // Direct connected-border edits are drawn on their neighboring map below.
                    if(TryCutGrassPatch(canvas,edit,layer,id))continue;
                    var existing=source.Cells.FirstOrDefault(c=>c.MetatileId==edit.SourceMetatileId);
                    if(existing is not null)
                    {
                        using var pixels=layerTexture.GetImage();canvas.BlitRect(pixels,new(existing.X*16,existing.Y*16,16,16),new(edit.X*16,edit.Y*16));continue;
                    }
                    if(TryCelioMachinePatch(canvas,edit,layer,id)||TryFiveIslandPatch(canvas,edit,layer,id)||TrySixIslandPatch(canvas,edit,layer,id)||TryFourIslandPatch(canvas,edit,layer,id)||TrySeviiRubyPatch(canvas,edit,layer,id)||TryPokemonLeaguePatch(canvas,edit,layer,id)||TryVictoryRoadPatch(canvas,edit,layer,id)||TryCinnabarPatch(canvas,edit,layer,id)||TrySilphPatch(canvas,edit,layer,id)||TryRocketPatch(canvas,edit,layer,id.Value))continue;
                    var definitions=id.Value=="map.vermilion-city-gym"?_catalog.VermilionGymRegion?.BeamDefinitions:_catalog.MartMetatiles?.Definitions;
                    if(definitions is null||!definitions.TryGetValue(edit.SourceMetatileId,out var definition))throw new InvalidDataException("quest_log_metatile_art_missing");
                    using var tile=owner.Texture(layer==0?definition.BottomTexturePath:layer==1?definition.MiddleTexturePath:definition.TopTexturePath).GetImage();
                    canvas.BlitRect(tile,new(0,0,16,16),new(edit.X*16,edit.Y*16));
                }
                return owner.FromImage(canvas);
            }
            return original with{Bottom=Patch(bottom,0),Middle=Patch(middle,1),Top=Patch(top,2)};
        });
    }
    private IEnumerable<(MapId Map,Vector2 Origin)> QuestLogNeighbors(MapId map)
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
    private void DrawQuestLogLayer(MapArt art,Vector2 camera,int layer)
    {
        var neighbors=QuestLogNeighbors(art.Source.Id).ToArray();
        if(art.Border is {} border)
        {
            var texture=layer==0?border.Bottom:layer==1?border.Middle:border.Top;
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
            if(!new Rect2(neighbor.Origin,new(source.Width*16,source.Height*16)).Intersects(new(camera,new(240,160))))continue;
            // Neighbor layouts are static source connections; never project the current
            // saved session's dynamic layout onto a different historical scene.
            var key=(_questArtKey??"quest-final")+":"+neighbor.Map.Value+":neighbor";_drawMapPins.Add(key);MapCache.SetPins(_drawMapPins);
            var texture=MapCache.Get(key,owner=>
            {
                Texture2D Layer(string path,int number)
                {
                    var original=owner.MapTexture(path);
                    var edits=(_questLogPlayback?.Tiles??[]).Select(edit=>(Edit:edit,At:_catalog.ConnectedFieldMaps is {} maps?ConnectedFieldCoordinates.Resolve(maps,art.Source.Id,edit.X,edit.Y):null)).Where(e=>e.At?.Map==neighbor.Map).ToArray();
                    if(edits.Length==0)return original;
                    using var canvas=(Image)original.GetImage().Duplicate();
                    foreach(var row in edits)
                    {
                        var edit=row.Edit with{X=row.At!.Value.X,Y=row.At.Value.Y};
                        if(TryCutGrassPatch(canvas,edit,number,neighbor.Map))continue;
                        var cell=source.Cells.FirstOrDefault(c=>c.MetatileId==edit.SourceMetatileId)??throw new InvalidDataException("quest_log_neighbor_tile_missing");
                        using var pixels=original.GetImage();canvas.BlitRect(pixels,new(cell.X*16,cell.Y*16,16,16),new(edit.X*16,edit.Y*16));
                    }
                    return owner.FromImage(canvas);
                }
                return new MapArt(source,Layer(source.BottomTexturePath,0),Layer(source.MiddleTexturePath,1),Layer(source.TopTexturePath,2),art.Player,[],null);
            });
            DrawTexture(layer==0?texture.Bottom:layer==1?texture.Middle:texture.Top,neighbor.Origin-camera);
        }
        DrawTexture(layer==0?art.Bottom:layer==1?art.Middle:art.Top,-camera);
    }
}

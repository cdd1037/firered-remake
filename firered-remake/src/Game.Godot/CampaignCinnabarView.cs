using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private readonly Dictionary<string,string> _cinnabarTrainerPortraits = new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> _cinnabarTrainerClasses = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Family,int Id),CinnabarFieldTile> _cinnabarTiles = [];
    private readonly Dictionary<(string Family,int Layer),Texture2D> _cinnabarTileLayers = [];
    private string? _cinnabarFallId;
    private double _cinnabarFallFrames, _cinnabarDustFrames=24;
    private bool _cinnabarFallSound, _cinnabarLandSound;
    private TilePosition? _cinnabarDustTile;
    private int _cinnabarSpriteBottom=88;
    private CinnabarSourcePresentation.FallPose CinnabarFallPose => CinnabarSourcePresentation.Landing((int)Math.Floor(_cinnabarFallFrames+1e-7),_cinnabarSpriteBottom);
    private bool CinnabarLanding => _session?.State.CinnabarFall is {Landing:true};
    private bool CinnabarInputLocked => _session?.State.CinnabarFall is not null;
    private Vector2 CinnabarCameraOffset => CinnabarLanding ? new(0,CinnabarFallPose.CameraY) : Vector2.Zero;
    private bool IsCinnabarGymLeader(string id) => _catalog.CinnabarEnabled && id == "pret:TRAINER_LEADER_BLAINE";
    private void LoadCinnabarPresentation()
    {
        if (!_catalog.CinnabarEnabled) return;
        var art = _catalog.CinnabarArt ?? throw new InvalidDataException("Cinnabar art catalog missing.");
        if (_battleArt.Count != 464 || _storageIcons.Count != 116 || _route22Footprints.Count != 87)
            throw new InvalidDataException("Cinnabar requires the complete Saffron presentation.");
        foreach (var sprite in art.Sprites)
        {
            using var image = Image.CreateFromData(sprite.Width, sprite.Height, false, Image.Format.Rgba8, art.Files[sprite.RgbaPath]);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = art.Files[icon.RgbaPath].ToArray();
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = Image.CreateFromData(footprint.Width, footprint.Height, false, Image.Format.Rgba8, art.Files[footprint.RgbaPath]);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (var sprite in art.Sprites.Where(s => s.Side == "front" && s.PaletteVariant == "normal"))
            _portraits.Add(sprite.SourceSpeciesId, _battleArt[(sprite.SourceSpeciesId, "front", false)].Texture);
        if (_battleArt.Count != 488 || _storageIcons.Count != 122 || _route22Footprints.Count != 93)
            throw new InvalidDataException("Cinnabar enemy presentation inventory differs.");
        LoadCinnabarTrainerAndFieldArt();LoadCinnabarItemArt();
        Write("cinnabarPresentation", new { newSpecies = new[] {38,59,78,91,98,99}, battleSprites = _battleArt.Count,
            icons = _storageIcons.Count, footprints = _route22Footprints.Count, playableMaps = _catalog.CinnabarRegion!.NewMaps.Count,
            PretCinnabarArt.ManifestSha256, reusedDust = true, badgeFlag = "FLAG_BADGE07_GET", audibleOutputVerified = false });
    }
    private void LoadCinnabarTrainerAndFieldArt()
    {
        var art=_catalog.CinnabarPresentation??throw new InvalidDataException("Cinnabar presentation missing.");
        foreach(var row in art.TrainerManifest.GetProperty("portraits").EnumerateArray())
        {
            string id=row.GetProperty("id").GetString()!,picture=row.GetProperty("trainerPic").GetString()!;
            byte[] rgba=art.TrainerFiles[row.GetProperty("rgbaPath").GetString()!];
            if(_forestTrainerPortraits.TryGetValue(id,out var old))
            {using var current=old.GetImage();current.Convert(Image.Format.Rgba8);if(!current.GetData().SequenceEqual(rgba))throw new InvalidDataException("Inherited Cinnabar trainer pixels differ: "+id);}
            else {using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,rgba);_forestTrainerPortraits.Add(id,(ImageTexture)SceneTextures.FromImage(image));}
            _cinnabarTrainerPortraits.Add(picture,id);
        }
        foreach(var row in art.TrainerClasses)_cinnabarTrainerClasses.Add(row.Key,row.Value);
        foreach(var trainer in _catalog.CinnabarTrainerParties!.Trainers.Values)
            if(!_cinnabarTrainerPortraits.ContainsKey(trainer.TrainerPic)||!_cinnabarTrainerClasses.ContainsKey(trainer.TrainerClass))throw new InvalidDataException("Cinnabar trainer presentation missing.");
        foreach(var tile in art.Field.Tiles)_cinnabarTiles.Add((tile.Family,tile.MetatileId),tile);
        foreach(var layer in art.Field.Layers)
        {using var image=Image.CreateFromData(layer.Width,layer.Height,false,Image.Format.Rgba8,art.FieldFiles[layer.RgbaPath]);_cinnabarTileLayers.Add((layer.Family,layer.Layer switch {"bottom"=>0,"middle"=>1,_=>2}),SceneTextures.FromImage(image));}
    }
    private string? VisibleCinnabarTrainerClass() => _session.State.ForestTrainerBattle is {} battle && _catalog.CinnabarTrainerParties?.Trainers.TryGetValue(battle.Trainer,out var trainer)==true ? _cinnabarTrainerClasses[trainer.TrainerClass] : null;
    private string? VisibleCinnabarTrainerPortrait() => _session.State.ForestTrainerBattle is {} battle && _catalog.CinnabarTrainerParties?.Trainers.TryGetValue(battle.Trainer,out var trainer)==true ? _cinnabarTrainerPortraits[trainer.TrainerPic] : null;
    private string DoubleTrainerClass => SixIslandDoubleText("className") ?? SeviiRubyDoubleText("className") ?? VictoryRoadDoubleTrainer?.ClassName ?? FirstSeviiDoubleTrainer?.ClassName ?? ( _session.State.DoubleBattle is {} battle && _catalog.CinnabarTrainerParties?.Trainers.TryGetValue(battle.Trainer,out var trainer)==true ? _cinnabarTrainerClasses[trainer.TrainerClass] : "TWINS");
    private string DoubleTrainerName => SixIslandDoubleText("name") ?? SeviiRubyDoubleText("name") ?? VictoryRoadDoubleTrainer?.Name ?? FirstSeviiDoubleTrainer?.Name ?? ( _session.State.DoubleBattle is {} battle && _catalog.CinnabarTrainerParties?.Trainers.TryGetValue(battle.Trainer,out var trainer)==true ? trainer.SourceTrainerName : "ELI & ANNE");
    private Texture2D DoubleTrainerPortrait => SixIslandDoublePortrait ?? SeviiRubyDoublePortrait ?? VictoryRoadDoublePortrait ?? FirstSeviiDoublePortrait ?? ( _session.State.DoubleBattle is {} battle && _catalog.CinnabarTrainerParties?.Trainers.TryGetValue(battle.Trainer,out var trainer)==true ? _forestTrainerPortraits[_cinnabarTrainerPortraits[trainer.TrainerPic]] : _doubleTextures["twins-front"]);
    private bool TryCinnabarPatch(Image canvas,FieldMetatileEdit edit,int layer,MapId map)
    {
        if(!_catalog.CinnabarEnabled||_catalog.CinnabarRegion?.NewMaps.ContainsKey(map)!=true)return false;
        string family=map.Value.StartsWith("map.pokemon-mansion-",StringComparison.Ordinal)?"mansion":map.Value=="map.cinnabar-island-gym"?"gym":throw new InvalidDataException("Cinnabar map has no patch atlas.");
        if(!_cinnabarTiles.TryGetValue((family,edit.SourceMetatileId),out var tile))throw new InvalidDataException("Cinnabar metatile pixels missing.");
        using var atlas=_cinnabarTileLayers[(family,layer)].GetImage();canvas.BlitRect(atlas,new(tile.X,tile.Y,16,16),new(edit.X*16,edit.Y*16));return true;
    }
    private void SynchronizeCinnabarPresentation(CampaignState? before,CampaignState after)
    {
        if(!CinnabarLanding){_cinnabarFallId=null;return;}
        var fall=after.CinnabarFall!;
        if(_cinnabarFallId==fall.PresentationId)return;
        if(before is null||before.CinnabarFall is not {Landing:false}||fall.To!=after.World.Player)throw new InvalidDataException("Cinnabar fall landing context differs.");
        _cinnabarFallId=fall.PresentationId;_cinnabarFallFrames=0;_cinnabarFallSound=false;_cinnabarLandSound=false;
        // The camera centers the tile at screen80; sprite bottom is 80-anchorY+height.
        var sprite=CurrentPlayerSprite;_cinnabarSpriteBottom=80-(int)sprite.Anchor.Y+sprite.Height;
        _tracks.Clear();_held=null;_repeat=0;_fade=0;
        _poses["LOCALID_PLAYER"].Visible=false;
    }
    private bool ProcessCinnabarPresentation(double delta)
    {
        if(!_catalog.CinnabarEnabled)return false;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        _cinnabarDustFrames=Math.Min(24,_cinnabarDustFrames+delta*60);
        if(!CinnabarLanding)return false;
        _held=null;_repeat=0;_cinnabarFallFrames+=delta*60;var pose=CinnabarFallPose;
        var player=_poses["LOCALID_PLAYER"];player.Visible=pose.Visible;player.DrawYOffset=pose.OffsetY;player.Frame=0;
        if(pose.FallCue&&!_cinnabarFallSound){_cinnabarFallSound=true;SourceSound("SE_FALL");}
        if(pose.LandingCue&&!_cinnabarLandSound){_cinnabarLandSound=true;SourceSound("SE_M_STRENGTH");_cinnabarDustTile=_session.State.World.Player;_cinnabarDustFrames=pose.LandingFrames;}
        if(pose.Complete&&!_busy)
        {
            string id=_cinnabarFallId!;Apply(new("cinnabar_fall_ack",Text:id));
            if(_session.State.CinnabarFall is not null)throw new InvalidDataException("Cinnabar landing acknowledgement rejected.");
            Write("cinnabarLanding",new { id,sourceFrames=(int)Math.Floor(_cinnabarFallFrames+1e-7),destination=_session.State.World.Player,audibleOutputVerified=false });
        }
        QueueRedraw();return true;
    }
    private void DrawCinnabarLandingDust(Vector2 camera)
    {
        if(_cinnabarDustTile is not {} tile||_cinnabarDustFrames>=24||tile.MapId!=_session.State.World.Player.MapId)return;
        DrawTextureRectRegion(_fieldStrengthDust!,new(new Vector2(tile.X*16,tile.Y*16+8)-camera,new(16,8)),new(0,(int)Math.Floor(_cinnabarDustFrames+1e-7)/8*8,16,8));
    }
    private void DrawCinnabarFade()
    {if(CinnabarLanding&&CinnabarFallPose.Fade>0)DrawRect(new(0,0,240,160),new Color(0,0,0,CinnabarFallPose.Fade));}
}

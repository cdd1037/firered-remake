using Game.Application;
using Game.Content;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private Texture2D? _fieldSurfRed, _fieldSurfGreen, _fieldSurfBlob;
    private string? _fieldSurfWait, _fieldSurfPhase;
    private double _fieldSurfFrames, _fieldSurfBlobFrames;
    private Direction? _fieldSurfBlobDirection;
    private bool _fieldSurfCry;
    private TilePosition? _fieldSurfLanding;
    private double _fieldSurfLandingFrames = 24;
    private int FieldSurfFrame => (int)Math.Floor(_fieldSurfFrames + 1e-7);
    private int FieldSurfPlayerFrame => FieldSurfFrame < 16 ? FieldSurfFrame / 4 : 4;
    private bool FieldSurfRiding => _session.State.SurfEffect?.Phase != FieldSurfSourcePresentation.ShowMon &&
        (_session.State.IsSurfing || SeafoamDismountOrigin is not null || _session.State.SurfEffect?.Phase == FieldSurfSourcePresentation.DismountJump);

    private void LoadSurfPresentation()
    {
        if (!_catalog.SurfEnabled) return;
        var art = _catalog.SurfArt ?? throw new InvalidDataException("Surf art catalog missing.");
        foreach (var sprite in art.Sprites)
        {
            using var image = NativeImages.LoadPng(art.Files[sprite.TexturePath], sprite.Width, sprite.Height);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = NativeImages.ReadPixels(art.Files[icon.TexturePath], icon.Width, icon.Height);
            for (int i = 0; i < rgba.Length; i += 4)
                if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = NativeImages.LoadPng(art.Files[footprint.TexturePath], footprint.Width, footprint.Height);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (var sprite in art.Sprites.Where(s => s.Side == "front" && s.PaletteVariant == "normal"))
            _portraits.Add(sprite.SourceSpeciesId, _battleArt[(sprite.SourceSpeciesId, "front", false)].Texture);

        Texture2D Load(string id, int width, int height = 32)
        {
            var bytes = Godot.FileAccess.GetFileAsBytes("res://Assets/Reference/field-surf-" + id + ".rgba");
            if (bytes.Length != width * height * 4)
                throw new InvalidDataException("Original Surf " + id + " pixels differ.");
            using var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes);
            return SceneTextures.FromImage(image);
        }
        _fieldSurfRed = Load("red", 48);
        _fieldSurfGreen = Load("green", 48);
        _fieldSurfBlob = Load("blob", 192);
        _cityBattleScenes.Add("water", Load("water", 240, 112));
        _cityBattleScenes.Add("pond", Load("pond", 240, 112));
    }

    private void SynchronizeFieldSurfPresentation(CampaignState? before, CampaignState after)
    {
        var effect = after.SurfEffect;
        if (effect?.WaitId != _fieldSurfWait || effect?.Phase != _fieldSurfPhase)
        {
            _fieldSurfWait = effect?.WaitId; _fieldSurfPhase = effect?.Phase;
            _fieldSurfFrames = 0; _fieldSurfCry = false;
        }
        if (effect?.Phase == FieldSurfSourcePresentation.MountJump && before?.SurfEffect?.Phase != effect.Phase)
        { _fieldSurfBlobFrames = 0; _fieldSurfBlobDirection = after.World.Player.Facing; }
        if(before is null || before.World.Player.MapId != after.World.Player.MapId)
        { _fieldSurfBlobFrames = 0; _fieldSurfBlobDirection = after.World.Player.Facing; _fieldSurfLanding = null; }
        if(before?.SurfEffect is {Phase:FieldSurfSourcePresentation.DismountJump} landed && effect is null &&
            after.World.Player == landed.Destination && _catalog.SourceMaps[after.World.Player.MapId].SourceBehaviorAt(after.World.Player.X,after.World.Player.Y) == 0)
        { _fieldSurfLanding = after.World.Player; _fieldSurfLandingFrames = 0; }
        if (effect is not null) { _held = null; _repeat = 0; }
    }

    private bool ProcessFieldSurfPresentation(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _fieldSurfLandingFrames = Math.Min(24, _fieldSurfLandingFrames + delta * 60);
        if (FieldSurfRiding)
        {
            var direction = _session.State.World.Player.Facing;
            if (_fieldSurfBlobDirection != direction) { _fieldSurfBlobDirection = direction; _fieldSurfBlobFrames = 0; }
            _fieldSurfBlobFrames += delta * 60;
        }
        if (_session.State.SurfEffect is not { } effect) return false;
        bool show = effect.Phase == FieldSurfSourcePresentation.ShowMon;
        int duration = show ? FieldSurfSourcePresentation.ShowMonCueFrames : FieldSurfSourcePresentation.JumpCueFrames;
        _fieldSurfFrames = Math.Min(duration, _fieldSurfFrames + delta * 60);
        if (show && !_fieldSurfCry && FieldSurfFrame >= FieldCutSourcePresentation.PlayerFrameDurations.Sum() +
            FieldCutSourcePresentation.OutdoorBannerOpenFrames + FieldCutSourcePresentation.MonSlideInFrames)
        { _fieldSurfCry = true; SourceCry(_session.State.Party[effect.PartyIndex].Pokemon); }
        _held = null; _repeat = 0;
        if (!_busy && FieldSurfFrame >= duration && _tracks.All(t => t.Time + 1e-9 >= t.Duration))
            Apply(new("field_surf_complete", Text: effect.WaitId));
        return true;
    }

    private void UseFieldPartySurf()
    {
        bool accepted = Apply(new("field_surf", PartyIndex: _fieldPartyCursor));
        if (accepted) { ResetFieldMenu(); return; }
        _fieldMenuMode = "party";
        _fieldMenuNotice = _status == "field_surf_badge_required" ? "This can't be used until a new\nBADGE is obtained." : "Can't use that here.";
    }

    // DoJumpSpecialSpriteMovement: alternate pixel updates and each original high arc entry twice.
    private static int FieldSurfJumpYOffset(double elapsed)
    {
        int[] high = [-4,-6,-8,-10,-11,-12,-12,-12,-11,-10,-9,-8,-6,-4,0,0];
        int update = (int)Math.Floor(elapsed * 60 + 1e-7);
        return update >= 32 ? 0 : high[Math.Clamp((update - 1) / 2, 0, 15)];
    }

    private int FieldSurfBlobPhase => (int)Math.Floor(_fieldSurfBlobFrames + 1e-7) / 48 % 2;

    private bool DrawFieldSurfActor(string id, Pose pose, Vector2 camera, bool drawPlayer = true)
    {
        if (id != "LOCALID_PLAYER" || !FieldSurfRiding) return false;
        var effect = _session.State.SurfEffect;
        int direction = pose.Facing switch { Direction.South => 0, Direction.North => 1, _ => 2 };
        int phase = FieldSurfBlobPhase;
        bool jumping = effect is not null || SeafoamDismountOrigin is not null;
        var blobTile = SeafoamDismountOrigin ?? (effect is null ? pose.Position : new Vector2(
            (effect.Phase == FieldSurfSourcePresentation.MountJump ? effect.Destination.X : effect.Origin.X) * 16,
            (effect.Phase == FieldSurfSourcePresentation.MountJump ? effect.Destination.Y : effect.Origin.Y) * 16));
        DrawSurfSheet(_fieldSurfBlob!, blobTile.Floor() + new Vector2(-8, -8) - camera, 32, direction * 2 + phase, pose.Facing == Direction.East);
        if (!drawPlayer) return true;
        // The source blob's direction is zero: no sinusoidal bob. Its second animation command adds one to player y2.
        var player = pose.Position.Floor() + new Vector2(0, -16 + (jumping ? pose.DrawYOffset : phase)) - camera;
        DrawSurfSheet(_session.State.PlayerGender == 0 ? _fieldSurfRed! : _fieldSurfGreen!, player, 16, direction, pose.Facing == Direction.East);
        return true;
    }
    private void DrawFieldSurfLanding(Vector2 camera)
    {
        if(_fieldSurfLanding is not {} tile || _fieldSurfLandingFrames >= 24 || tile.MapId != _session.State.World.Player.MapId)return;
        var position = new Vector2(tile.X * 16, tile.Y * 16 + 8) - camera;
        DrawTextureRectRegion(_fieldStrengthDust!,new(position,new(16,8)),new(0,(int)Math.Floor(_fieldSurfLandingFrames+1e-7)/8*8,16,8));
    }
    private void DrawSurfSheet(Texture2D sheet, Vector2 position, int width, int frame, bool flip)
    {
        if (flip) { DrawSetTransform(position + new Vector2(width, 0), 0, new(-1, 1)); DrawTextureRectRegion(sheet, new(0,0,width,32), new(frame*width,0,width,32)); DrawSetTransform(Vector2.Zero); }
        else DrawTextureRectRegion(sheet, new(position,new(width,32)), new(frame*width,0,width,32));
    }
}

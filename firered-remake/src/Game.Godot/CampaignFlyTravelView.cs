using Game.Application;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private Texture2D? _fieldFlyBird;
    private FieldFlyTravelTimeline? _fieldFlyTravel;
    private string? _fieldFlyTravelWait, _fieldFlyTravelPhase;
    private double _fieldFlyTickRemainder;
    private bool _fieldFlyCry;
    private int? FieldFlyShowMonFrame => _fieldFlyTravel?.ShowMonFrame;
    private int FieldFlyShowMonPartyIndex => FieldFlyShowMonFrame is not null && _session.State.FlyEffect is {} fly ? fly.PartyIndex : -1;

    private void LoadFieldFlyTravelPresentation()
    {
        const string path = "res://Assets/Reference/field-fly-bird.rgba";
        // Only the current composition's Fly content enables these hooks.
        var bytes = Godot.FileAccess.GetFileAsBytes(path);
        if (bytes.Length != 64 * 320 * 4)
            throw new InvalidDataException("Original Fly bird pixels differ.");
        using var image = Image.CreateFromData(64, 320, false, Image.Format.Rgba8, bytes);
        _fieldFlyBird = SceneTextures.FromImage(image);
    }

    private void SynchronizeFieldFlyTravelPresentation(CampaignState after)
    {
        var fly = after.FlyEffect;
        if (_fieldFlyTravelWait != fly?.WaitId || _fieldFlyTravelPhase != fly?.Phase)
        {
            _fieldFlyTravelWait = fly?.WaitId; _fieldFlyTravelPhase = fly?.Phase;
            _fieldFlyTickRemainder = 0; _fieldFlyCry = false;
            _fieldFlyTravel = fly?.Phase is "out" or "in"
                ? new(fly.Phase == "out", after.PlayerGender != 0, FieldCutSourcePresentation.ShowMonCueFrames,
                    FieldCutSourcePresentation.PlayerFrameDurations.Sum()) : null;
            if (_fieldFlyTravel is not null) _fade = 0;
        }
        if (fly is not null) { _held = null; _repeat = 0; }
    }

    private bool ProcessFieldFlyTravelPresentation(double delta)
    {
        if (_fieldFlyTravel is not {} travel || _session.State.FlyEffect is not {} fly) return false;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _held = null; _repeat = 0;
        _fieldFlyTickRemainder += delta * 60;
        while (_fieldFlyTickRemainder + 1e-7 >= 1 && !travel.Complete)
        {
            _fieldFlyTickRemainder -= 1;
            string stage = travel.Stage;
            travel.Tick();
            if (stage != travel.Stage)
                Write("fieldFlyStage", new { fly.WaitId, fly.Phase, travel.Stage, travel.Ticks, travel.BirdX, travel.BirdY });
            if (travel.SwoopSound)
            {
                if (_sourceAudio?.HasClip("SE_M_FLY") == true) SourceSound("SE_M_FLY");
                else Write("fieldFlyAudioPending", new { id = "SE_M_FLY", audioEnabled = _sourceAudio is not null, recordedFallback = false });
            }
            if (!_fieldFlyCry && travel.ShowMonFrame >= FieldCutSourcePresentation.PlayerFrameDurations.Sum() +
                FieldCutSourcePresentation.OutdoorBannerOpenFrames + FieldCutSourcePresentation.MonSlideInFrames)
            { _fieldFlyCry = true; SourceCry(_session.State.Party[fly.PartyIndex].Pokemon); }
        }
        if (travel.Complete && !_busy && _tracks.All(t => t.Time + 1e-9 >= t.Duration))
            Apply(new("field_fly_ack", Text: fly.WaitId));
        return true;
    }

    private bool DrawFieldFlyTravelActor(string id, SpriteAsset asset, Pose pose, Vector2 camera)
    {
        if (id != "LOCALID_PLAYER" || _fieldFlyTravel is not {} travel) return false;
        if (_session.State.IsSurfing && _session.State.FlyEffect?.Phase == "out")
        {
            int direction = pose.Facing switch { Direction.South => 0, Direction.North => 1, _ => 2 };
            int phase = travel.Ticks / 48 % 2;
            DrawSurfSheet(_fieldSurfBlob!, pose.Position.Floor() + new Vector2(-8,-8) - camera,
                32, direction * 2 + phase, pose.Facing == Direction.East);
        }
        switch (travel.Pose)
        {
            case FieldFlyTravelTimeline.PlayerPose.Hidden:
            case FieldFlyTravelTimeline.PlayerPose.ScreenRide:
                return true;
            case FieldFlyTravelTimeline.PlayerPose.Summon:
                DrawTextureRectRegion(_session.State.PlayerGender == 0 ? _fieldCutRed! : _fieldCutGreen!,
                    new(pose.Position.Floor() + new Vector2(8,8) - asset.Anchor - camera, new(16,32)),
                    new(travel.SummonFrame * 16,0,16,32));
                return true;
            case FieldFlyTravelTimeline.PlayerPose.MapRide:
                DrawSurfSheet(_session.State.PlayerGender == 0 ? _fieldSurfRed! : _fieldSurfGreen!,
                    pose.Position.Floor() + new Vector2(0,-16 + travel.PlayerYOffset) - camera,16,2,false);
                return true;
            case FieldFlyTravelTimeline.PlayerPose.MapWest:
                DrawActor(asset,new Pose {Position = pose.Position, Facing = Direction.West, Visible = true},camera);
                return true;
            default: return false;
        }
    }

    private void DrawFieldFlyTravelOverlay()
    {
        if (_fieldFlyTravel is not {} travel) return;
        if (travel.Pose == FieldFlyTravelTimeline.PlayerPose.ScreenRide)
            DrawSurfSheet(_session.State.PlayerGender == 0 ? _fieldSurfRed! : _fieldSurfGreen!,
                new(travel.PlayerX - 8, travel.PlayerY - 16 + travel.PlayerYOffset),16,2,false);
        if (travel.BirdVisible)
        {
            DrawSetTransform(new(travel.BirdX,travel.BirdY), travel.BirdRotation * Mathf.Tau / 256,
                Vector2.One * (travel.BirdScale / 256f));
            DrawTextureRectRegion(_fieldFlyBird!,new(-32,-32,64,64),new(0,travel.BirdFrame * 64,64,64));
            DrawSetTransform(Vector2.Zero);
        }
        if (travel.FadeLevel > 0) DrawRect(new(0,0,240,160), new Color(0,0,0,travel.FadeLevel / 16f));
    }
}

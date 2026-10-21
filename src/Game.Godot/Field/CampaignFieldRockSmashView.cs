using Game.Application;
using Godot;

/// <summary>Original obstacle frames and shared field-move banner; presentation consumes no gameplay RNG.</summary>
public partial class CampaignView
{
    private string? _fieldRockSmashWait, _fieldRockSmashPhase;
    private double _fieldRockSmashFrames;
    private bool _fieldRockSmashCry;
    private int FieldRockSmashFrame => (int)Math.Floor(_fieldRockSmashFrames + 1e-7);
    private bool FieldRockSmashVisualComplete => _session.State.RockSmashEffect is { } effect &&
        FieldRockSmashFrame >= (effect.Phase == FieldRockSmashSourcePresentation.ShowMon
            ? FieldCutSourcePresentation.ShowMonCueFrames : 64);
    private int FieldRockSmashPlayerFrame
    {
        get
        {
            int tick = FieldRockSmashFrame;
            for (int i = 0; i < FieldCutSourcePresentation.PlayerFrameDurations.Length; i++)
            {
                int duration = FieldCutSourcePresentation.PlayerFrameDurations[i];
                if (tick < duration) return i;
                tick -= duration;
            }
            return FieldCutSourcePresentation.PlayerFrameDurations.Length - 1;
        }
    }

    private void SynchronizeFieldRockSmashPresentation(CampaignState after)
    {
        var effect = after.RockSmashEffect;
        string? wait = effect is null ? null : after.Script.Wait?.CorrelationId;
        if (wait != _fieldRockSmashWait || effect?.Phase != _fieldRockSmashPhase)
        {
            _fieldRockSmashWait = wait; _fieldRockSmashPhase = effect?.Phase;
            _fieldRockSmashFrames = 0; _fieldRockSmashCry = false;
        }
        if (effect is not null) { _held = null; }
    }

    private void ProcessFieldRockSmashPresentation(double delta)
    {
        if (_session.State.RockSmashEffect is not { } effect) return;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        bool show = effect.Phase == FieldRockSmashSourcePresentation.ShowMon;
        _fieldRockSmashFrames = Math.Min(show ? FieldCutSourcePresentation.ShowMonCueFrames : 64,
            _fieldRockSmashFrames + delta * 60);
        if (show && !_fieldRockSmashCry && FieldRockSmashFrame >=
            FieldCutSourcePresentation.PlayerFrameDurations.Sum() +
            FieldCutSourcePresentation.OutdoorBannerOpenFrames + FieldCutSourcePresentation.MonSlideInFrames)
        { _fieldRockSmashCry = true; SourceCry(_session.State.Party[effect.PartyIndex].Pokemon); }
    }

    private bool DrawFieldRockSmashActor(string id, SpriteAsset asset, Pose pose, Vector2 camera)
    {
        if (_session.State.RockSmashEffect is not { Phase: FieldRockSmashSourcePresentation.RockBreak } effect ||
            id != effect.ActorId) return false;
        // object_event_anims.h: four frames of eight ticks; movement then blinks for 32 ticks.
        int tick = FieldRockSmashFrame, blink = tick - 32;
        if (blink >= 0 && (blink >= 32 || (blink & 1) == 0)) return true;
        if (asset.Count < 4 || asset.Width != 16 || asset.Height != 16)
            throw new InvalidDataException("Rock Smash requires its original four-frame source sprite.");
        var position = pose.Position.Floor() + new Vector2(8, 8) - asset.Anchor - camera;
        DrawTextureRectRegion(asset.Texture, new(position, new(asset.Width, asset.Height)),
            new(Math.Min(3, tick / 8) * asset.Width, 0, asset.Width, asset.Height));
        return true;
    }

    private void UseFieldPartyRockSmash()
    {
        bool accepted = Apply(new("field_rock_smash", PartyIndex: _fieldPartyCursor));
        if (accepted) { ResetFieldMenu(); return; }
        _fieldMenuMode = "party";
        _fieldMenuNotice = _status == "field_rock_smash_badge_required"
            ? "This can't be used until a new\nBADGE is obtained."
            : "Can't use that here.";
    }
}

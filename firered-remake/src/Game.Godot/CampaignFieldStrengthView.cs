using System.Security.Cryptography;
using Game.Application;
using Game.Domain.Events;
using Godot;

/// <summary>Source field-move presentation; the session owns activation, collision and live actor positions.</summary>
public partial class CampaignView
{
    private Texture2D? _fieldStrengthDust;
    private string? _fieldStrengthWait, _fieldStrengthPhase;
    private double _fieldStrengthFrames;
    private bool _fieldStrengthCry;
    private int FieldStrengthFrame => (int)Math.Floor(_fieldStrengthFrames + 1e-7);
    private int FieldStrengthPlayerFrame => FieldStrengthFrame < 16 ? FieldStrengthFrame / 4 : 4;

    private void LoadFieldStrengthPresentation()
    {
        if (!_catalog.SafariEnabled) return;
        var rgba = Godot.FileAccess.GetFileAsBytes("res://Assets/Reference/field-strength-dust.rgba");
        if (rgba.Length != 16 * 24 * 4 || Convert.ToHexStringLower(SHA256.HashData(rgba)) !=
            "24819e82f884ce9e5267fa5abd227cfea0d544d781d9c2505664e99930c681f5")
            throw new InvalidDataException("Field Strength dust source pixels differ.");
        using var image = Image.CreateFromData(16, 24, false, Image.Format.Rgba8, rgba);
        _fieldStrengthDust = SceneTextures.FromImage(image);
    }

    private void SynchronizeFieldStrengthPresentation(CampaignState after)
    {
        var strength = after.StrengthEffect;
        if (strength?.WaitId != _fieldStrengthWait || strength?.Phase != _fieldStrengthPhase)
        {
            _fieldStrengthWait = strength?.WaitId; _fieldStrengthPhase = strength?.Phase;
            _fieldStrengthFrames = 0; _fieldStrengthCry = false;
        }
        if (strength is not null) { _held = null; _repeat = 0; }
    }

    // Runs after shared motion tracks advance, so a callback cannot settle a boulder early.
    private bool ProcessFieldStrengthPresentation(double delta)
    {
        if (_session.State.StrengthEffect is not { } strength) return false;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        bool showMon = strength.Phase == FieldStrengthSourcePresentation.ShowMon;
        int duration = showMon ? FieldStrengthSourcePresentation.ShowMonCueFrames : FieldStrengthSourcePresentation.PushCueFrames;
        _fieldStrengthFrames = Math.Min(duration, _fieldStrengthFrames + delta * 60);
        if (showMon && !_fieldStrengthCry && FieldStrengthFrame >=
            FieldFlashSourcePresentation.PlayerFrameDurations.Sum() + FieldFlashSourcePresentation.IndoorBannerOpenFrames + FieldFlashSourcePresentation.MonSlideInFrames)
        { _fieldStrengthCry = true; SourceCry(_session.State.Party[strength.PartyIndex].Pokemon); }
        _held = null; _repeat = 0;
        if (!_busy && FieldStrengthFrame >= duration && _tracks.All(t => t.Time + 1e-9 >= t.Duration))
        {
            if (!showMon) Apply(new("field_strength_complete", Text: strength.WaitId));
            else if (_session.State.Script.Wait is { Kind: StoryWaitKind.Presentation } wait &&
                wait.CorrelationId == strength.WaitId && wait.CorrelationId != _blockedAutomaticWait && SourceAudioWaitReady())
                Apply(new("ack"));
        }
        return true;
    }

    private void UseFieldPartyStrength()
    {
        long revision = _session.Revision;
        Apply(new("field_strength", PartyIndex: _fieldPartyCursor));
        if (_session.Revision > revision) { ResetFieldMenu(); return; }
        _fieldMenuMode = "party";
        _fieldMenuNotice = _status == "field_strength_badge_required"
            ? "This can't be used until a new\nBADGE is obtained." : "Can't use that here.";
    }

    private void DrawFieldStrengthBanner()
    {
        if (_session.State.StrengthEffect is { Phase: FieldStrengthSourcePresentation.ShowMon } strength)
            DrawFieldIndoorShowMonBanner(FieldStrengthFrame, strength.PartyIndex);
    }

    // Source UpdateWalkSlowerAnim advances one pixel on update indices 0,2,...,30.
    private static float FieldSlowerStepFraction(double elapsed) =>
        (float)Math.Clamp(Math.Floor((elapsed * 60 + 1 + 1e-7) / 2) / 16, 0, 1);

    private void DrawFieldStrengthDust(Vector2 camera)
    {
        if (_session.State.StrengthEffect is not { Phase: FieldStrengthSourcePresentation.Push } strength || FieldStrengthFrame >= 24) return;
        // FldEff_Dust centers its 16x8 sprite at tile+(8,12); three source frames last 8 updates each.
        var position = new Vector2(strength.BoulderOrigin.X * 16, strength.BoulderOrigin.Y * 16 + 8) - camera;
        DrawTextureRectRegion(_fieldStrengthDust!, new(position, new(16, 8)), new(0, FieldStrengthFrame / 8 * 8, 16, 8));
    }
}

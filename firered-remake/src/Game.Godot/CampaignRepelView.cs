using Game.Application;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private string? _repelWaitId;
    private double _repelWaitElapsed;
    private ulong _repelFieldAckFrame = ulong.MaxValue;

    private bool ConsumedRepelDismissalFrame(InputEvent input) => _repelFieldAckFrame == Engine.GetProcessFrames() &&
        input is not InputEventKey { Keycode: Key.Escape or Key.F12 or Key.F5 or Key.F9 };

    private void SynchronizeRepelPresentation()
    {
        var pending = _session.State.Repel;
        if (pending?.WaitId == _repelWaitId) return;
        _repelWaitId = pending?.WaitId; _repelWaitElapsed = 0;
        if (pending is not null) { _held = null; _repeat = 0; ReleaseFieldTextInput(); }
    }

    private bool ProcessRepelPresentation(double delta)
    {
        if (_session.State.Repel is not { Phase: CampaignRepelPhase.UseSound } pending) return false;
        _repelWaitElapsed += Math.Max(0, delta);
        // Source sequencer status ends at authenticated VBlank 27. The rendered
        // sample's release tail may continue independently; audio backend sample
        // counts never advance gameplay steps or either RNG.
        double duration = PretRepelAudio.SourceSequenceStoppedVBlank / SourceAudioFramesPerSecond;
        if (!_busy && _repelWaitElapsed >= duration)
            Apply(new("repel_presented", Text: pending.WaitId));
        return true;
    }

    private bool TryRepelInput(InputEventKey key)
    {
        if (_session.State.Repel is not { } pending) return false;
        if (key.Keycode is Key.Escape or Key.F12 or Key.F5 or Key.F9) return false;
        _held = null; _repeat = 0;
        if (pending.Phase == CampaignRepelPhase.UseSound) return true;
        if (FieldTextInput(key)) return true;
        if (key.Pressed && !key.Echo && key.Keycode is Key.Enter or Key.Backspace &&
            FieldTextComplete && _tracks.All(t => t.Time >= t.Duration))
        { _repelFieldAckFrame = Engine.GetProcessFrames(); Apply(new("ack")); }
        return true;
    }
}

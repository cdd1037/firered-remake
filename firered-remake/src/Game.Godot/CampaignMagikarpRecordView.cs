using Game.Application;
using Game.Domain.Events;
using Godot;

/// <summary>Presentation only: reuse the shared occupied-slot chooser for the source callback.</summary>
public partial class CampaignView
{
    private int _magikarpRecordCursor;
    private ulong _magikarpRecordClosedFrame = ulong.MaxValue;
    private bool MagikarpRecordChoosing => _session?.State.MagikarpRecord is { Stage: CampaignMagikarpRecordStage.Party } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection;

    private void SynchronizeMagikarpRecordPresentation(CampaignState? before, CampaignState after)
    {
        bool wasChoosing = before?.MagikarpRecord?.Stage == CampaignMagikarpRecordStage.Party;
        bool choosing = after.MagikarpRecord?.Stage == CampaignMagikarpRecordStage.Party;
        if (wasChoosing == choosing) return;
        _held = null; _repeat = 0; ReleaseFieldTextInput(); _fade = 16.0 / 60;
        if (choosing) _magikarpRecordCursor = 0;
        else _magikarpRecordClosedFrame = Engine.GetProcessFrames();
    }

    private bool ConsumeMagikarpRecordTransitionInput(InputEvent input) => ConsumeServiceTransitionInput(input, _magikarpRecordClosedFrame);

    private bool MagikarpRecordInput(InputEventKey key)
    {
        if (!MagikarpRecordChoosing || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        int count = _session.State.Party.Length + 1;
        if (key.Keycode is Key.Up or Key.Left) _magikarpRecordCursor = (_magikarpRecordCursor + count - 1) % count;
        else if (key.Keycode is Key.Down or Key.Right) _magikarpRecordCursor = (_magikarpRecordCursor + 1) % count;
        else if (key.Keycode is Key.Backspace or Key.Escape) Apply(new("magikarp_record_cancel"));
        else if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            if (_magikarpRecordCursor == _session.State.Party.Length) Apply(new("magikarp_record_cancel"));
            else if (_magikarpRecordCursor >= 0 && _magikarpRecordCursor < _session.State.Party.Length)
                Apply(new("magikarp_record_choose", PartyIndex: _magikarpRecordCursor)
                    { TargetInstanceId = _session.State.Party[_magikarpRecordCursor].InstanceId });
        }
        return true;
    }

    private bool NavigateMagikarpRecordPointer(Vector2 point, int direction)
    {
        if (!MagikarpRecordChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_magikarpRecordCursor == row) return false;
        _magikarpRecordCursor = row; return true;
    }

    private bool MouseMagikarpRecord(Vector2 point)
    {
        if (!MagikarpRecordChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        _magikarpRecordCursor = row; MouseKey(Key.Enter); return true;
    }

    private bool DrawMagikarpRecordParty()
    {
        if (!MagikarpRecordChoosing) return false;
        var source = _catalog.NameRaterSource ?? throw new InvalidDataException("Shared party chooser text missing.");
        DrawServicePartyChooser(_magikarpRecordCursor, source.ChoosePokemonText);
        return true;
    }
}

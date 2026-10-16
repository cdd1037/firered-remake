using Game.Application;
using Game.Domain.Events;
using Godot;

/// <summary>Presentation only: reuse the shared occupied-slot chooser for the source callback.</summary>
public partial class CampaignView
{
    private int _daisyGroomingCursor;
    private ulong _daisyGroomingClosedFrame = ulong.MaxValue;
    private bool DaisyGroomingChoosing => _session?.State.DaisyGrooming is { ChoosingParty: true } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection;

    private void SynchronizeDaisyGroomingPresentation(CampaignState? before, CampaignState after)
    {
        bool wasChoosing = before?.DaisyGrooming?.ChoosingParty == true;
        bool choosing = after.DaisyGrooming?.ChoosingParty == true;
        if (wasChoosing == choosing) return;
        _held = null; _repeat = 0; ReleaseFieldTextInput(); _fade = 16.0 / 60;
        if (choosing) _daisyGroomingCursor = 0;
        else _daisyGroomingClosedFrame = Engine.GetProcessFrames();
    }

    private bool ConsumeDaisyGroomingTransitionInput(InputEvent input) => ConsumeServiceTransitionInput(input, _daisyGroomingClosedFrame);

    private bool DaisyGroomingInput(InputEventKey key)
    {
        if (!DaisyGroomingChoosing || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _fade > 0) return true;
        int count = _session.State.Party.Length + 1;
        if (key.Keycode is Key.Up or Key.Left) _daisyGroomingCursor = (_daisyGroomingCursor + count - 1) % count;
        else if (key.Keycode is Key.Down or Key.Right) _daisyGroomingCursor = (_daisyGroomingCursor + 1) % count;
        else if (key.Keycode is Key.Backspace or Key.Escape) Apply(new("daisy_cancel"));
        else if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            if (_daisyGroomingCursor == _session.State.Party.Length) Apply(new("daisy_cancel"));
            else if (_daisyGroomingCursor >= 0 && _daisyGroomingCursor < _session.State.Party.Length)
                Apply(new("daisy_choose", PartyIndex: _daisyGroomingCursor)
                    { TargetInstanceId = _session.State.Party[_daisyGroomingCursor].InstanceId });
        }
        return true;
    }

    private bool NavigateDaisyGroomingPointer(Vector2 point, int direction)
    {
        if (!DaisyGroomingChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_daisyGroomingCursor == row) return false;
        _daisyGroomingCursor = row; return true;
    }

    private bool MouseDaisyGrooming(Vector2 point)
    {
        if (!DaisyGroomingChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        _daisyGroomingCursor = row; MouseKey(Key.Enter); return true;
    }

    private bool DrawDaisyGroomingParty()
    {
        if (!DaisyGroomingChoosing) return false;
        var source = _catalog.NameRaterSource ?? throw new InvalidDataException("Shared party chooser text missing.");
        DrawServicePartyChooser(_daisyGroomingCursor, source.ChoosePokemonText);
        return true;
    }
}

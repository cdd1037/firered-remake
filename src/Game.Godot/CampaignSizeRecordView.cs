using Game.Application;
using Game.Domain.Events;
using Godot;

/// <summary>Presentation only: reuse the shared occupied-slot chooser for the source callback.</summary>
public partial class CampaignView
{
    private int _sizeRecordCursor;
    private ulong _sizeRecordClosedFrame = ulong.MaxValue;
    private bool SizeRecordChoosing => _session?.State.SizeRecord is { Stage: CampaignSizeRecordStage.Party } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection;

    private void SynchronizeSizeRecordPresentation(CampaignState? before, CampaignState after)
    {
        bool wasChoosing = before?.SizeRecord?.Stage == CampaignSizeRecordStage.Party;
        bool choosing = after.SizeRecord?.Stage == CampaignSizeRecordStage.Party;
        if (wasChoosing == choosing) return;
        _held = null; ReleaseFieldTextInput(); _fade = 16.0 / 60;
        if (choosing) { _sizeRecordCursor = 0; _partyLastRightSlot = 1; }
        else _sizeRecordClosedFrame = Engine.GetProcessFrames();
    }

    private bool ConsumeSizeRecordTransitionInput(InputEvent input) => ConsumeServiceTransitionInput(input, _sizeRecordClosedFrame);

    private bool SizeRecordInput(InputEventKey key)
    {
        if (!SizeRecordChoosing || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; if (!key.Pressed || key.Echo || _fade > 0) return true;
        int count = _session.State.Party.Length + 1;
        if (NativePartyMove(key.Keycode, ref _sizeRecordCursor, count - 1)) { }
        else if (key.Keycode is Key.Backspace or Key.Escape) Apply(new("size_record_cancel"));
        else if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            if (_sizeRecordCursor == _session.State.Party.Length) Apply(new("size_record_cancel"));
            else if (_sizeRecordCursor >= 0 && _sizeRecordCursor < _session.State.Party.Length)
                Apply(new("size_record_choose", PartyIndex: _sizeRecordCursor)
                    { TargetInstanceId = _session.State.Party[_sizeRecordCursor].InstanceId });
        }
        return true;
    }

    private bool NavigateSizeRecordPointer(Vector2 point, int direction)
    {
        if (!SizeRecordChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_sizeRecordCursor == row) return false;
        _sizeRecordCursor = row; return true;
    }

    private bool MouseSizeRecord(Vector2 point)
    {
        if (!SizeRecordChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        _sizeRecordCursor = row; MouseKey(Key.Enter); return true;
    }

    private bool DrawSizeRecordParty()
    {
        if (!SizeRecordChoosing) return false;
        var source = _catalog.NameRaterSource ?? throw new InvalidDataException("Shared party chooser text missing.");
        DrawServicePartyChooser(_sizeRecordCursor, source.ChoosePokemonText);
        return true;
    }
}

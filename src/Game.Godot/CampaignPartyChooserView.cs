using Godot;

/// <summary>Shared presentation for script-owned occupied party slots and CANCEL.
/// Eligibility and the meaning of a selected row remain with the calling service.</summary>
public partial class CampaignView
{
    // These services share the same transition-frame input barrier. Each caller
    // still owns when a transition occurs; other overlays have different hotkeys.
    private bool ConsumeServiceTransitionInput(InputEvent input, ulong transitionFrame)
    {
        if (transitionFrame != Engine.GetProcessFrames() || input is InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 }) return false;
        GetViewport().SetInputAsHandled(); return true;
    }

    private static Rect2 ServicePartyRow(int index) => NativePartySlot(index);
    private static readonly Rect2 ServicePartyCancelRow = NativePartyCancel;
    private Rect2 ServicePartyChooserRow(int index) => index == _session.State.Party.Length
        ? ServicePartyCancelRow : ServicePartyRow(index);

    private int ServicePartyPointerRow(Vector2 point)
    {
        for (int i = 0; i <= _session.State.Party.Length; i++)
            if (ServicePartyChooserRow(i).HasPoint(point)) return i;
        return -1;
    }

    private void DrawServicePartyChooser(int cursor, string prompt, string cancelText = "CANCEL")
    {
        DrawNativeParty(_session.State.Party, cursor, prompt, cancelText: cancelText);
        if (_fade > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (float)Math.Clamp(_fade / (16.0 / 60), 0, 1)));
    }
}

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

    private static Rect2 ServicePartyRow(int index) => new(8, 6 + index * 18, 224, 18);
    private static readonly Rect2 ServicePartyCancelRow = new(8, 116, 224, 18);
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
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        for (int i = 0; i < _session.State.Party.Length; i++)
        {
            var owned = _session.State.Party[i]; var mon = owned.Pokemon; var row = ServicePartyRow(i); float y = row.Position.Y;
            if (i == cursor) DrawRect(row, new Color("e4e4c8"));
            DrawTextureRectRegion(PresentedOwnedIcon(owned), new(18, y, 16, 16), new(0, 0, 32, 32));
            SourceText(_sourceSmall, new(36, y), PresentedOwnedName(owned));
            if (!owned.IsEgg)
            {
            SourceText(_sourceSmall, new(112, y), "Lv" + mon.Level);
            SourceText(_sourceSmall, new(147, y), $"{mon.CurrentHp}/{mon.Stats.MaxHp}");
            DrawForestPartyStatus(owned, new(207, y + 2));
            }
            if (i == cursor) SourceText(_sourceSmall, new(8, y), "▶");
        }
        if (cursor == _session.State.Party.Length) SourceText(_sourceNormal, new(8, ServicePartyCancelRow.Position.Y), "▶");
        SourceText(_sourceNormal, new(20, ServicePartyCancelRow.Position.Y), cancelText);
        SourceText(_sourceSmall, new(8, 140), prompt);
        if (_fade > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (float)Math.Clamp(_fade / (16.0 / 60), 0, 1)));
    }
}

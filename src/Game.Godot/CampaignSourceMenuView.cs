using Game.Domain.Events;
using Godot;

public partial class CampaignView
{
    private SourceVerticalMenuPresentation? _sourceMenu;
    private string? _sourceMenuWait;
    private StoryMenu? SourceMenu => _session?.SourceMenu;
    private bool SourceMenuActive => SourceMenu is not null;
    private bool SourceMenuVisible => SourceMenuActive && FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length);
    private bool SourceMenuReady => SourceMenuVisible && _waitElapsed >= _waitDuration && _fade <= 0 &&
        _fanfareRemaining <= 0 && _tracks.All(t => t.Time >= t.Duration);
    private int SourceMenuWidth => (int)_sourceMenu!.Options.Max(t => _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(t), fontSize: _sourceNormal.Size).X);

    private void SynchronizeSourceMenu()
    {
        if (SourceMenu is not { } menu) { _sourceMenu = null; _sourceMenuWait = null; return; }
        string? wait = _session.State.Script.Wait?.CorrelationId;
        if (_sourceMenuWait == wait) return;
        _sourceMenu = new(menu.Definition.Options.Select(o => o.Text), menu.Left, menu.Top, menu.DefaultChoice);
        _sourceMenuWait = wait; _held = null; _repeat = 0;
    }
    private bool TrySourceMenuInput(InputEventKey key)
    {
        if (!SourceMenuActive || key.Keycode is Key.Escape or Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || !SourceMenuReady) return true;
        if (key.Keycode is Key.Up or Key.Down)
        { if (_sourceMenu!.Move(key.Keycode == Key.Up ? -1 : 1)) SourceSound("SE_SELECT"); }
        else if (key.Keycode == Key.Enter || key.Keycode == Key.Backspace && !SourceMenu!.IgnoreB)
        { SourceSound("SE_SELECT"); Apply(new("ack", Choice: key.Keycode == Key.Enter ? _sourceMenu!.Cursor : StoryMenu.CancelResult)); }
        return true;
    }
    private bool NavigateSourceMenuPointer(Vector2 point, int direction)
    {
        if (!SourceMenuReady || _sourceMenu is not { } menu) return false;
        int row = menu.RowAt(point.X, point.Y, SourceMenuWidth);
        return row >= 0 && (direction != 0 ? MouseNavigationKey(direction) : menu.Select(row));
    }
    private bool MouseSourceMenu(Vector2 point)
    {
        if (!SourceMenuReady || _sourceMenu is not { } menu) return false;
        int row = menu.RowAt(point.X, point.Y, SourceMenuWidth);
        if (row < 0) return false;
        menu.Select(row); return MouseKey();
    }
    private void DrawSourceMenu()
    {
        if (!SourceMenuVisible || _sourceMenu is not { } menu) return;
        var f = menu.Frame(SourceMenuWidth);
        _battleSkin.DrawUserFrame(this, new(f.X, f.Y, f.Width, f.Height));
        for (int i = 0; i < menu.Options.Count; i++) SourceText(_sourceNormal, new(f.X + 16, f.Y + 10 + i * 14), menu.Options[i]);
        SourceText(_sourceNormal, new(f.X + 8, f.Y + 10 + menu.Cursor * 14), "▶");
    }
}

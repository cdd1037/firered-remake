using Game.Domain.Events;
using Godot;

public partial class CampaignView
{
    private string? _sourceGridWait;
    private int _sourceGridCursor;
    private StoryGridMenu? SourceGridMenu => _session?.SourceGridMenu;
    private bool SourceGridMenuActive => SourceGridMenu is not null;
    private bool SourceGridMenuVisible => SourceGridMenuActive && FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length);

    private void SynchronizeSourceGridMenu()
    {
        string? wait = SourceGridMenuActive ? _session.State.Script.Wait?.CorrelationId : null;
        if (_sourceGridWait == wait) return;
        _sourceGridWait = wait;
        _sourceGridCursor = 0;
    }

    private bool TrySourceGridMenuInput(InputEventKey key)
    {
        if (SourceGridMenu is not { } menu || key.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (!key.Pressed || !SourceGridMenuVisible || _tracks.Any(t => t.Time < t.Duration) || _waitElapsed < _waitDuration || _fanfareRemaining > 0) return true;
        if (key.Keycode is Key.Up or Key.Down or Key.Left or Key.Right)
            _sourceGridCursor = menu.MoveCursor(_sourceGridCursor, key.Keycode == Key.Left ? -1 : key.Keycode == Key.Right ? 1 : 0,
                key.Keycode == Key.Up ? -1 : key.Keycode == Key.Down ? 1 : 0);
        else if (key.Keycode == Key.Enter) Apply(new("ack", Choice: _sourceGridCursor));
        else if (!menu.IgnoreB) Apply(new("ack", Choice: StoryGridMenu.CancelResult));
        return true;
    }

    private bool SourceGridPointerCell(Vector2 point, out int cell)
    {
        cell = -1;
        if (!SourceGridMenuVisible || SourceGridMenu is not { } menu) return false;
        int x = (menu.Left + 1) * 8, y = (menu.Top + 1) * 8, width = menu.Definition.ColumnWidthTiles * 8;
        if (!new Rect2(x, y, width * menu.Columns, menu.Rows * 16).HasPoint(point)) return false;
        cell = (int)(point.X - x) / width + (int)(point.Y - y) / 16 * menu.Columns;
        return true;
    }
    private bool NavigateSourceGridPointer(Vector2 point, int direction)
    {
        if (!SourceGridPointerCell(point, out int cell)) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_sourceGridCursor == cell) return false;
        _sourceGridCursor = cell;
        return true;
    }
    private bool MouseSourceGridMenu(Vector2 point)
    {
        if (!SourceGridPointerCell(point, out int cell)) return false;
        _sourceGridCursor = cell;
        MouseKey(Key.Enter);
        return true;
    }
    private void DrawSourceGridMenu()
    {
        if (!SourceGridMenuVisible || SourceGridMenu is not { } menu) return;
        int x = menu.Left * 8, y = menu.Top * 8, width = menu.Definition.ColumnWidthTiles * 8;
        _battleSkin.DrawUserFrame(this, new(x, y, width * menu.Columns + 16, menu.Rows * 16 + 16));
        for (int i = 0; i < menu.Definition.Options.Length; i++)
            SourceText(_sourceNormal, new(x + 16 + i % menu.Columns * width, y + 9 + i / menu.Columns * 16), menu.Definition.Options[i].Text);
        SourceText(_sourceNormal, new(x + 8 + _sourceGridCursor % menu.Columns * width, y + 9 + _sourceGridCursor / menu.Columns * 16), "▶");
    }
}

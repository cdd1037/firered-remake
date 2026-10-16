using Game.Domain.Events;
using Godot;

public partial class CampaignView
{
    private SourceVerticalMenuPresentation? _sourceMenu;
    private string? _sourceMenuWait;
    private StoryMenu? SourceMenu => _session?.SourceMenu;
    private StoryMenu? DisplaySourceMenu => _session?.DisplaySourceMenu;
    private bool SourceMenuActive => SourceMenu is not null;
    private bool SourceMenuVisible => DisplaySourceMenu is not null && (SourceMenu is null || FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length));
    private bool SourceMenuReady => SourceMenuActive && SourceMenuVisible && _waitElapsed >= _waitDuration && _fade <= 0 &&
        _fanfareRemaining <= 0 && _tracks.All(t => t.Time >= t.Duration);
    private int SourceMenuWidth => (int)_sourceMenu!.Options.Select(SourceMenuLabelPresentation.Parse).Max(t =>
        t.SuffixX is {} offset ? offset+_sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(t.Suffix!),fontSize:_sourceSmall.Size).X :
        _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(t.Label),fontSize:_sourceNormal.Size).X);

    private void SynchronizeSourceMenu()
    {
        if (DisplaySourceMenu is not { } menu) { _sourceMenu = null; _sourceMenuWait = null; return; }
        string? wait = _session.State.Script.Wait?.CorrelationId;
        if (menu.Definition.ListLayout is not null && _sourceMenu?.IsScrollingList == true)
        {
            // Native topics suspend the same window, retaining both cursor and scroll.
            if (_sourceMenuWait != wait && SourceMenu is not null) _sourceMenu.Select(menu.DefaultChoice);
            _sourceMenuWait = wait; return;
        }
        if (_sourceMenuWait == wait) return;
        var list = menu.Definition.ListLayout;
        _sourceMenu = new(menu.Definition.Options.Select(o => o.Text), menu.Left, menu.Top, menu.DefaultChoice,
            list?.VisibleRows, list?.HeightTiles);
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
        for (int i=0;i<menu.VisibleRows;i++)
        {
            var text=SourceMenuLabelPresentation.Parse(menu.Options[menu.ScrollOffset+i]);
            SourceText(_sourceNormal,new(f.X+16,f.Y+10+i*14),text.Label);
            if(text.SuffixX is {} offset)SourceText(_sourceSmall,new(f.X+16+offset,f.Y+10+i*14),text.Suffix!);
        }
        SourceText(_sourceNormal, new(f.X + 8, f.Y + 10 + (menu.Cursor - menu.ScrollOffset) * 14), "▶");
        if (SourceMenuActive && menu.IsScrollingList)
        {
            float center = f.X + f.Width / 2f;
            if (menu.CanScrollUp) DrawColoredPolygon([new(center - 3, f.Y + 6), new(center + 3, f.Y + 6), new(center, f.Y + 2)], Colors.Black);
            if (menu.CanScrollDown) DrawColoredPolygon([new(center - 3, f.Y + f.Height - 6), new(center + 3, f.Y + f.Height - 6), new(center, f.Y + f.Height - 2)], Colors.Black);
        }
    }
}

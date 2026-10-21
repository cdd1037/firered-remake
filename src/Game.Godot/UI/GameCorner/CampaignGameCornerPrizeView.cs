using Game.Content;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private int _gameCornerPrizeCursor;
    private string? _gameCornerPrizeWait;
    private GameCornerPrizeCounter? GameCornerPrizeMenu => _session.PendingGameCornerPrizeMenu;
    private bool GameCornerPrizeMenuActive => GameCornerPrizeMenu is not null;
    private bool GameCornerPrizeMenuVisible => GameCornerPrizeMenuActive && FieldTextComplete &&
        _session.State.Dialogue?.Page + 1 == _session.State.Dialogue?.Pages.Length;
    private (int Left, int Width) GameCornerPrizeLayout()
    {
        var menu = GameCornerPrizeMenu ?? throw new InvalidDataException("prize_menu_missing");
        float width = 0;
        foreach (var text in menu.MenuLabels.Select(SourceMenuLabelPresentation.Parse))
            width = Math.Max(width, text.SuffixX is {} offset ? offset + _sourceSmall.MeasureText(text.Suffix!).X :
                _sourceNormal.MeasureText(text.Label).X);
        int tiles = ((int)width + 9) / 8 + 1;
        int left = Math.Min(menu.Kind == "items" ? 10 : 11, 28 - tiles);
        if (left < 0 || tiles < 1) throw new InvalidDataException("prize_menu_width");
        return (left * 8, tiles * 8 + 16);
    }
    private void SynchronizeGameCornerPrizeMenu()
    {
        string? wait = GameCornerPrizeMenuActive ? _session.State.Script.Wait!.CorrelationId : null;
        if (wait != _gameCornerPrizeWait) { _gameCornerPrizeWait = wait; _gameCornerPrizeCursor = 0; }
    }
    private bool TryGameCornerPrizeInput(InputEventKey key)
    {
        if (!GameCornerPrizeMenuActive || key.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (!key.Pressed || !GameCornerPrizeMenuVisible || _tracks.Any(t => t.Time < t.Duration) || _waitElapsed < _waitDuration) return true;
        if (key.Keycode is Key.Up or Key.Down or Key.Enter or Key.Backspace) SourceSound("SE_SELECT");
        if (key.Keycode == Key.Up) _gameCornerPrizeCursor = (_gameCornerPrizeCursor + 5) % 6;
        else if (key.Keycode == Key.Down) _gameCornerPrizeCursor = (_gameCornerPrizeCursor + 1) % 6;
        else if (key.Keycode == Key.Enter) Apply(new("ack", Choice: _gameCornerPrizeCursor));
        else if (key.Keycode == Key.Backspace) Apply(new("ack", Choice: 127));
        return true;
    }
    private bool NavigateGameCornerPrizePointer(Vector2 point, int direction)
    {
        if (!GameCornerPrizeMenuVisible) return false;
        var layout = GameCornerPrizeLayout();
        return PointerRows(point, direction, ref _gameCornerPrizeCursor, 6, layout.Left + 8, 8, layout.Width - 16, 14);
    }
    private bool MouseGameCornerPrizeMenu(Vector2 point)
    {
        if (!GameCornerPrizeMenuVisible) return false;
        var layout = GameCornerPrizeLayout();
        return MouseSelect(point, ref _gameCornerPrizeCursor, 6, layout.Left + 8, 8, layout.Width - 16, 14);
    }
    private void DrawGameCornerPrizeMenu()
    {
        if (!GameCornerPrizeMenuVisible || GameCornerPrizeMenu is not {} menu) return;
        var layout = GameCornerPrizeLayout();
        _battleSkin.DrawUserFrame(this, new(layout.Left, 0, layout.Width, 104));
        for (int i = 0; i < menu.MenuLabels.Length; i++)
        {
            var text = SourceMenuLabelPresentation.Parse(menu.MenuLabels[i]);
            SourceText(_sourceNormal, new(layout.Left + 16, 10 + i * 14), text.Label);
            if (text.SuffixX is {} offset) SourceText(_sourceSmall, new(layout.Left + 16 + offset, 10 + i * 14), text.Suffix!);
        }
        SourceText(_sourceNormal, new(layout.Left + 8, 10 + _gameCornerPrizeCursor * 14), "▶");
    }
    private void LoadGameCornerPrizeIcons()
    {
        foreach (var icon in PretGameCornerPrizes.LoadItemsDocument(_root).Icons.Concat(PretGameCornerPrizes.LoadMachinesDocument(_root).Icons))
        {
            _earlyItemIcons.Add(icon.Item, ItemTexture(icon));
        }
        foreach (var alias in PretGameCornerPrizes.LoadMachinesDocument(_root).IconAliases)
        {
            // TM25 has no separate cached image. The authenticated original TM34
            // already supplies the same gItemIcon_TMHM / gItemIconPalette_ElectricTMHM pair.
            var cached = alias.ReusedItem.Value == "pret:ITEM_TM25" ? new Game.Domain.Ids.ItemId("pret:ITEM_TM34") : alias.ReusedItem;
            if (!_earlyItemIcons.TryGetValue(cached, out var icon))
                throw new InvalidDataException("prize_machine_icon_predecessor:" + cached.Value);
            _earlyItemIcons.Add(alias.Item, icon);
        }
    }
}

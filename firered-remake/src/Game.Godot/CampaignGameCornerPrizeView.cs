using System.Globalization;
using System.Text.RegularExpressions;
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
    private static (string Name, int? Offset, string? Price) PrizeMenuText(string label)
    {
        var match = Regex.Match(label, @"^([^{}]+)\{CLEAR_TO 0x([0-9A-Fa-f]+)\}\{FONT_SMALL\}([^{}]+)$");
        if (match.Success) return (match.Groups[1].Value, int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), match.Groups[3].Value);
        if (label != "NO THANKS") throw new InvalidDataException("prize_menu_text_control");
        return (label, null, null);
    }
    private (int Left, int Width) GameCornerPrizeLayout()
    {
        var menu = GameCornerPrizeMenu ?? throw new InvalidDataException("prize_menu_missing");
        float width = 0;
        foreach (var text in menu.MenuLabels.Select(PrizeMenuText))
            width = Math.Max(width, text.Offset is {} offset ? offset + _sourceSmall.Font.GetStringSize(text.Price!, fontSize: _sourceSmall.Size).X :
                _sourceNormal.Font.GetStringSize(text.Name, fontSize: _sourceNormal.Size).X);
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
            var text = PrizeMenuText(menu.MenuLabels[i]);
            SourceText(_sourceNormal, new(layout.Left + 16, 10 + i * 14), text.Name);
            if (text.Offset is {} offset) SourceText(_sourceSmall, new(layout.Left + 16 + offset, 10 + i * 14), text.Price!);
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

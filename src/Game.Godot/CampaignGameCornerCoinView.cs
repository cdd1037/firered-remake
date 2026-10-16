using System.Globalization;
using Game.Domain.Events;
using Godot;

public partial class CampaignView
{
    private int _gameCornerCoinCursor;
    private string? _gameCornerCoinWait;
    private bool GameCornerCoinMenuActive => _session.State.World.Player.MapId.Value == GameCornerCoinSource.Map && GameCornerCoinSource.Pending(_session.State.Script, _catalog);
    private bool GameCornerCoinMenuVisible => GameCornerCoinMenuActive && FieldTextComplete &&
        _session.State.Dialogue?.Page + 1 == _session.State.Dialogue?.Pages.Length;
    private void SynchronizeGameCornerCoinMenu()
    {
        string? wait = GameCornerCoinMenuActive ? _session.State.Script.Wait!.CorrelationId : null;
        if (wait != _gameCornerCoinWait) { _gameCornerCoinWait = wait; _gameCornerCoinCursor = 0; }
    }
    private bool TryGameCornerCoinInput(InputEventKey key)
    {
        if (!GameCornerCoinMenuActive || key.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (!key.Pressed || !GameCornerCoinMenuVisible || _tracks.Any(t => t.Time < t.Duration) || _waitElapsed < _waitDuration) return true;
        if (key.Keycode is Key.Up or Key.Down or Key.Enter or Key.Backspace) SourceSound("SE_SELECT");
        if (key.Keycode == Key.Up) _gameCornerCoinCursor = (_gameCornerCoinCursor + 2) % 3;
        else if (key.Keycode == Key.Down) _gameCornerCoinCursor = (_gameCornerCoinCursor + 1) % 3;
        else if (key.Keycode == Key.Enter) Apply(new("ack", Choice: _gameCornerCoinCursor));
        else if (key.Keycode == Key.Backspace) Apply(new("ack", Choice: 127));
        return true;
    }
    private bool NavigateGameCornerCoinPointer(Vector2 point, int direction) => GameCornerCoinMenuVisible &&
        PointerRows(point, direction, ref _gameCornerCoinCursor, 3, 112, 8, 112, 14);
    private bool MouseGameCornerCoinMenu(Vector2 point) => GameCornerCoinMenuVisible &&
        MouseSelect(point, ref _gameCornerCoinCursor, 3, 112, 8, 112, 14);
    private void DrawGameCornerCoinMenu()
    {
        if (!GameCornerCoinMenuVisible) return;
        _battleSkin.DrawUserFrame(this, new(104, 0, 128, 64));
        // The two original price rows select FONT_SMALL and CLEAR_TO 0x45/0x40.
        SourceText(_sourceSmall, new(120, 10), " 50 COINS");
        SourceText(_sourceSmall, new(120 + 0x45, 10), "¥1,000");
        SourceText(_sourceSmall, new(120, 24), "500 COINS");
        SourceText(_sourceSmall, new(120 + 0x40, 24), "¥10,000");
        SourceText(_sourceNormal, new(120, 38), "EXIT");
        SourceText(_sourceNormal, new(112, 10 + _gameCornerCoinCursor * 14), "▶");
    }
    private void DrawGameCornerCoinsBox()
    {
        if (_session.State.CoinsBox is not {} box) return;
        if (box.X != 0 || box.Y is not (0 or 5) || box.Amount is < 0 or > GameCornerCoinSource.Maximum)
            throw new InvalidDataException("game_corner_coin_box_view");
        // src/coins.c: 8x3 tile interior, x+1/y+1, normal label and small right-aligned amount.
        _itemSkin.DrawStandardFrame(this, new(box.X * 8, box.Y * 8, 80, 40));
        SourceText(_sourceNormal, new(box.X * 8 + 8, box.Y * 8 + 8), "COINS");
        string amount = box.Amount.ToString(CultureInfo.InvariantCulture).PadLeft(4) + " COINS";
        float width = _sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(amount), fontSize: _sourceSmall.Size).X;
        SourceText(_sourceSmall, new(box.X * 8 + 72 - width, box.Y * 8 + 20), amount);
    }
}

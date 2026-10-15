using Game.Domain.Events;
using Game.Content;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private void LoadRooftopIcons()
    {
        if (!_catalog.DepartmentRooftopEnabled) return;
        _earlyItemIcons.Add(new ItemId("pret:ITEM_LEMONADE"), ItemTexture(PretDepartmentRooftopItems.LoadDocument(_root).Icons[0]));
        foreach (var alias in PretDepartmentRooftopItems.LoadMachinesDocument(_root).IconAliases)
        {
            if (!_earlyItemIcons.TryGetValue(alias.ReusedItem, out var icon)) throw new InvalidDataException("department_rooftop_shared_icon_missing");
            _earlyItemIcons.Add(alias.Item, icon);
        }
    }
    private int _rooftopCursor;
    private string? _rooftopWait;
    private DepartmentRooftopMenu? RooftopMenu => _catalog.DepartmentRooftopEnabled &&
        _session.State.World.Player.MapId.Value == DepartmentRooftopSource.Map
        ? DepartmentRooftopSource.Pending(_session.State.Script, _catalog) : null;
    private bool RooftopMenuActive => RooftopMenu is not null;
    private bool RooftopMenuVisible => RooftopMenuActive && FieldTextComplete &&
        _session.State.Dialogue?.Page + 1 == _session.State.Dialogue?.Pages.Length;
    private int RooftopLeft => RooftopMenu?.Vending == true ? 96 : 0;
    private int RooftopWidth => RooftopMenu?.Vending == true ? 144 :
        (((int)RooftopMenu!.Rows.Max(row => _sourceNormal.Font.GetStringSize(row, fontSize: _sourceNormal.Size).X) + 9) / 8 + 1) * 8 + 16;
    private void SynchronizeRooftopMenu()
    {
        string? wait = RooftopMenuActive ? _session.State.Script.Wait!.CorrelationId : null;
        if (wait != _rooftopWait) { _rooftopWait = wait; _rooftopCursor = 0; }
    }
    private bool TryRooftopInput(InputEventKey key)
    {
        if (RooftopMenu is not {} menu || key.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (!key.Pressed || !RooftopMenuVisible || _tracks.Any(t => t.Time < t.Duration) || _waitElapsed < _waitDuration) return true;
        if (key.Keycode is Key.Up or Key.Down or Key.Enter or Key.Backspace) SourceSound("SE_SELECT");
        if (key.Keycode == Key.Up) _rooftopCursor = (_rooftopCursor + menu.Rows.Length - 1) % menu.Rows.Length;
        else if (key.Keycode == Key.Down) _rooftopCursor = (_rooftopCursor + 1) % menu.Rows.Length;
        else if (key.Keycode == Key.Enter) Apply(new("ack", Choice: _rooftopCursor));
        else if (key.Keycode == Key.Backspace) Apply(new("ack", Choice: 127));
        return true;
    }
    private bool NavigateRooftopPointer(Vector2 point, int direction) => RooftopMenuVisible &&
        PointerRows(point, direction, ref _rooftopCursor, RooftopMenu!.Rows.Length, RooftopLeft + 8, 8, RooftopWidth - 16, 14);
    private bool MouseRooftopMenu(Vector2 point) => RooftopMenuVisible &&
        MouseSelect(point, ref _rooftopCursor, RooftopMenu!.Rows.Length, RooftopLeft + 8, 8, RooftopWidth - 16, 14);
    private void DrawRooftopMenu()
    {
        if (!RooftopMenuVisible || RooftopMenu is not {} menu) return;
        _battleSkin.DrawUserFrame(this, new(RooftopLeft, 0, RooftopWidth, ((menu.Rows.Length * 14 + 23) / 8) * 8));
        for (int row = 0; row < menu.Rows.Length; row++)
        {
            SourceText(_sourceNormal, new(RooftopLeft + 16, 10 + row * 14), menu.Rows[row]);
            // src/strings.c prices use CLEAR_TO 0x57 and FONT_SMALL; keep original amounts/right edge.
            if (menu.Vending && row < 3) SourceText(_sourceSmall, new(RooftopLeft + 103, 10 + row * 14), "¥" + new[] { 200, 300, 350 }[row]);
        }
        SourceText(_sourceNormal, new(RooftopLeft + 8, 10 + _rooftopCursor * 14), "▶");
    }
}

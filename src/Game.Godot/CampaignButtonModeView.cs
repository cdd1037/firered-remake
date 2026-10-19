using Game.Application;
using Game.Domain.Options;
using Godot;

public partial class CampaignView
{
    private readonly CampaignButtonInput _buttons = new();
    private ulong _buttonObservedEvent;
    private CampaignButtonEdge _buttonObservedEdge;
    private CampaignButton _buttonObservedPhysical;
    private int _shoulderRepeatDirection;
    private double _shoulderRepeat;
    private bool NormalizedAHeld => (_buttons.Held & CampaignButton.A) != 0;

    // Q/W were audited against all non-text key handlers: neither was bound.
    // Focused names retain Q/W as letters; real joypad shoulders still open Help.
    private void ObserveCampaignButtonInput(InputEvent input)
    {
        _buttonObservedEvent = 0;
        if (!_startupReady || _session is null) return;
        if (!PhysicalButton(input, out var button, out int device, out long control, out bool pressed)) return;
        _buttonObservedEvent = input.GetInstanceId(); _buttonObservedPhysical = button;
        _buttonObservedEdge = _buttons.Observe(device, control, button, pressed);
        // GUI consumes releases before _UnhandledInput, so release held text here.
        if ((_buttonObservedEdge.Released & CampaignButton.A) != 0 && GetViewport().GuiGetFocusOwner() is LineEdit)
            ReleaseNormalizedConfirm();
    }
    private bool PhysicalButton(InputEvent input, out CampaignButton button, out int device, out long control, out bool pressed)
    {
        button = CampaignButton.None; device = -1; control = 0; pressed = false;
        if (input is InputEventKey key)
        {
            if (GetViewport().GuiGetFocusOwner() is LineEdit && key.Pressed && key.Keycode is Key.Q or Key.W) return false;
            button = key.Keycode switch
            {
                Key.Enter or Key.KpEnter => CampaignButton.A, Key.Backspace => CampaignButton.B,
                Key.Tab => CampaignButton.Start, Key.Space => CampaignButton.Select,
                Key.Up => CampaignButton.Up, Key.Down => CampaignButton.Down,
                Key.Left => CampaignButton.Left, Key.Right => CampaignButton.Right,
                Key.Q => CampaignButton.L, Key.W => CampaignButton.R, _ => CampaignButton.None
            };
            control = (long)key.Keycode; pressed = key.Pressed;
        }
        else if (input is InputEventJoypadButton joy)
        {
            button = joy.ButtonIndex switch
            {
                JoyButton.A => CampaignButton.A, JoyButton.B => CampaignButton.B,
                JoyButton.Start => CampaignButton.Start, JoyButton.Back => CampaignButton.Select,
                JoyButton.DpadUp => CampaignButton.Up, JoyButton.DpadDown => CampaignButton.Down,
                JoyButton.DpadLeft => CampaignButton.Left, JoyButton.DpadRight => CampaignButton.Right,
                JoyButton.LeftShoulder => CampaignButton.L, JoyButton.RightShoulder => CampaignButton.R,
                _ => CampaignButton.None
            };
            device = joy.Device; control = (long)joy.ButtonIndex; pressed = joy.Pressed;
        }
        return button != CampaignButton.None;
    }
    private bool NormalizeCampaignButtonInput(ref InputEvent input)
    {
        // Mouse commands call _UnhandledInput directly, deliberately bypassing owners.
        if (_buttonObservedEvent != input.GetInstanceId()) return true;
        var edge = _buttonObservedEdge; var physical = _buttonObservedPhysical;
        _buttonObservedEvent = 0;
        bool pressed = edge.Pressed != CampaignButton.None;
        var changed = pressed ? edge.Pressed : edge.Released;
        var logical = physical == CampaignButton.L && _buttons.Mode == SourceButtonMode.LEqualsA ? CampaignButton.A : physical;
        if ((changed & logical) == 0)
        {
            // Existing owners decide whether a keyboard echo is relevant; never
            // manufacture another A edge or double the Help controller's repeats.
            if (!_help.Active && input is InputEventKey { Echo: true } &&
                logical is CampaignButton.Up or CampaignButton.Down or CampaignButton.Left or CampaignButton.Right) return true;
            GetViewport().SetInputAsHandled(); return false; }
        if (logical is CampaignButton.L or CampaignButton.R && !_help.Active && _buttons.Mode != SourceButtonMode.Help)
        {
            if (pressed && _buttons.Mode == SourceButtonMode.Lr)
            { _shoulderRepeatDirection = logical == CampaignButton.R ? 1 : -1; _shoulderRepeat = 20; HandleShoulderPage(_shoulderRepeatDirection); }
            GetViewport().SetInputAsHandled(); return false;
        }
        if (pressed && logical is CampaignButton.Left or CampaignButton.Right && !_help.Active &&
            _buttons.Mode == SourceButtonMode.LEqualsA && (_buttons.Held & CampaignButton.R) != 0 &&
            _fieldMenuMode.StartsWith("pokedex", StringComparison.Ordinal))
        { HandleShoulderPage(logical == CampaignButton.Right ? 1 : -1); GetViewport().SetInputAsHandled(); return false; }
        Key mapped = logical switch
        {
            CampaignButton.A => Key.Enter, CampaignButton.B => Key.Backspace, CampaignButton.Start => Key.Tab,
            CampaignButton.Select => FameCheckerActive || _session.State.TeachyTv is not null ||
                (_session.State.Pc is null && _session.State.ItemMenu is null && !FieldMenuOpen) ? Key.R : Key.Space,
            CampaignButton.Up => Key.Up, CampaignButton.Down => Key.Down, CampaignButton.Left => Key.Left,
            CampaignButton.Right => Key.Right, CampaignButton.L => Key.Q, CampaignButton.R => Key.W, _ => Key.None
        };
        if (input is InputEventKey original && original.Keycode == mapped && original.Pressed == pressed) return true;
        input = new InputEventKey { Keycode = mapped, Pressed = pressed };
        return true;
    }
private void ProcessCampaignButtonRepeats(double delta)
    {
        if (_buttons.Mode != SourceButtonMode.Lr || _help.Active || NamingScreenActive ||
            _session.State.Pc?.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move or CampaignPcView.MoveItems) ||
            _session.State.Pc.Summary is not null) { _shoulderRepeatDirection = 0; return; }
        int direction = (_buttons.Held & CampaignButton.L) != 0 ? -1 : (_buttons.Held & CampaignButton.R) != 0 ? 1 : 0;
        if (direction == 0) { _shoulderRepeatDirection = 0; return; }
        if (direction != _shoulderRepeatDirection) { _shoulderRepeatDirection = direction; _shoulderRepeat = 20; }
        _shoulderRepeat -= delta * SourceAudioTiming.SourceFramesPerSecond;
        if (_shoulderRepeat <= 0) { HandleShoulderPage(direction); _shoulderRepeat = 5; }
    }
    private void HandleShoulderPage(int direction)
    {
        if (_busy || _fade > 0 || NamingScreenActive) return;
        Key key = direction < 0 ? Key.Left : Key.Right;
        bool battleSummary = _battlePartySummaryPage >= 0 && _battlePartySelection is not null;
        bool dayCareSummary = DayCareChoosing && _dayCareSummaryPage >= 0;
        if (battleSummary || _session.State.Pc?.Summary is not null || _fieldMenuMode == "summary" || dayCareSummary)
        {
            using var page = new InputEventKey { Keycode = key, Pressed = true };
            // Route only an active browsable Summary through its existing input owner.
            // Move-learning/deletion screens stay on their fixed move-details page.
            if (battleSummary) BattlePartyInspectionInput(page);
            else if (_session.State.Pc?.Summary is not null) StorageSummaryInput(page);
            else if (_fieldMenuMode == "summary") TryFieldMenuInput(page);
            else DayCarePartyInput(page);
        }
        else if (_fieldMenuMode == "pokedex-habitat")
        {
            int next = Math.Clamp(_pokedexHabitatPage + direction, 0, _pokedexHabitats[_pokedexHabitatIndex].Pages.Length - 1);
            if (next != _pokedexHabitatPage) { _pokedexHabitatPage = next; _pokedexHabitatSlot = 0; SetPokedexHabitatPage(); SourceSound("SE_SELECT"); }
        }
        else if (_fieldMenuMode == "pokedex" && _pokedexList is not null)
            _pokedexCursor = _pokedexMenu!.MoveList(_pokedexList, _pokedexCursor, direction, page: true);
        else if (_session.State.Pc?.View is CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move or CampaignPcView.MoveItems)
        {
            using var page = new InputEventKey { Keycode = direction < 0 ? Key.Pageup : Key.Pagedown, Pressed = true };
            StorageInput(page);
        }
        QueueRedraw();
    }
    private void ReleaseNormalizedConfirm()
    {
        ReleaseFieldTextInput(); _fameTextHeld = _fameTextPress = false;
        _teachyFieldConfirm = false; _teachyBattleAHeld = false;
        _teachyFieldText.ReleaseInput(); _teachyBattleVoiceText.ReleaseInput();
        _diplomaConfirmKeys.Remove(Key.Enter); _diplomaConfirmKeys.Remove(Key.KpEnter);
        _diplomaConfirmJoypads.Clear();
        _diplomaPresentation?.Confirm(_diplomaMouseConfirm || _diplomaConfirmKeys.Contains(Key.A), false);
    }
    private void ReconcileCampaignButtonMode(SourceButtonMode mode)
    {
        var edge = _buttons.SetMode(mode);
        // A held shoulder becomes held A, but a commit never invents a new press.
        if ((edge.Released & CampaignButton.A) != 0) ReleaseNormalizedConfirm();
    }
    private void ResetCampaignButtonInput(bool preserveHeld=false)
    { if(!preserveHeld)_buttons.Reset(); _buttonObservedEvent = 0; ReleaseNormalizedConfirm(); _helpHeld = CampaignButton.None; }
    private void OnCampaignJoyConnectionChanged(long device, bool connected)
    {
        if (connected) return;
        _storageRectangleAnchor = null;
        var edge = _buttons.Disconnect((int)device);
        if ((edge.Released & CampaignButton.A) != 0) ReleaseNormalizedConfirm();
        _held = null; _repeat = 0; _helpHeld = CampaignButton.None;
    }
}

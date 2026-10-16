using Game.Application;
using Game.Content;
using Game.Domain.Slots;
using Godot;

public partial class CampaignView
{
    private SlotMachinePresentationData? _slotArt;
    private SlotMachineScreenRenderer? _slotRenderer;
    private ImageTexture? _slotTexture;
    private CampaignSlotKeys _slotHeld;
    private double _slotFrameTime;
    private string? _slotCallback;
    private string _slotFairy = "neutral";
    private long _slotFairySince, _slotDrawFrame = -1;
    private bool SlotMachineActive => _session?.State.SlotMachine is { Phase: not CampaignSlotPhase.Selected };

    private void SynchronizeSlotMachine()
    {
        if (!SlotMachineActive)
        {
            _slotHeld = 0; _slotFrameTime = 0; _slotCallback = null; _slotDrawFrame = -1;
            _slotTexture?.Dispose(); _slotTexture = null;
            return;
        }
        var slot = _session.State.SlotMachine!;
        if (_slotCallback != slot.CallbackId)
        {
            _slotArt ??= PretSlotMachinePresentation.Load(_root);
            _slotRenderer ??= new(_slotArt, _catalog.GameCornerSlots!.Rules);
            _slotCallback = slot.CallbackId; _slotFrameTime = 0; _slotHeld = 0;
            _slotFairy = "neutral"; _slotFairySince = slot.Frame; _slotDrawFrame = -1;
            _held = null; _repeat = 0;
        }
        string fairy = slot.Phase switch
        {
            CampaignSlotPhase.Betting when slot.MainStep is 3 or 4 => "spinning",
            CampaignSlotPhase.Lose when slot.MainStep < 2 => "lose",
            CampaignSlotPhase.Payout when slot.MainStep < 4 => "payout",
            _ => "neutral"
        };
        if (fairy != _slotFairy) { _slotFairy = fairy; _slotFairySince = slot.Frame; }
    }

    private bool ProcessSlotMachine(double delta)
    {
        if (!SlotMachineActive) return false;
        _held = null;
        _slotFrameTime += Math.Min(delta, .25);
        for (int frames = 0; frames < 8 && _slotFrameTime >= 1d / 60 && SlotMachineActive; frames++)
        {
            var slot = _session.State.SlotMachine!;
            var keys = _slotHeld;
            Apply(new("slot_tick") { SlotInput = new(slot.CallbackId, checked(slot.Frame + 1), keys & ~slot.PreviousKeys, keys) });
            _slotFrameTime -= 1d / 60;
            if (_session.State.SlotMachine?.Frame == slot.Frame) { _slotFrameTime = 0; break; }
        }
        QueueRedraw();
        return true;
    }

    private bool TrySlotMachineInput(InputEvent input)
    {
        if (!SlotMachineActive) return false;
        if (input is InputEventKey key && !key.Echo)
        {
            if (key.Pressed && key.Keycode is Key.Escape or Key.F12) { RequestQuit(); return true; }
            CampaignSlotKeys button = key.Keycode switch
            {
                Key.Enter or Key.Z => CampaignSlotKeys.A,
                Key.Backspace or Key.X => CampaignSlotKeys.B,
                Key.Tab => CampaignSlotKeys.Start,
                Key.Up => CampaignSlotKeys.Up, Key.Down => CampaignSlotKeys.Down,
                Key.Left => CampaignSlotKeys.Left, Key.Right => CampaignSlotKeys.Right,
                Key.R => CampaignSlotKeys.R, Key.L => CampaignSlotKeys.L,
                _ => CampaignSlotKeys.None
            };
            if (key.Pressed) _slotHeld |= button; else _slotHeld &= ~button;
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool DrawSlotMachine()
    {
        if (!SlotMachineActive) return false;
        var slot = _session.State.SlotMachine!;
        if (_slotRenderer is null || _slotArt is null) throw new InvalidOperationException("slot_presentation_not_loaded");
        if (_slotDrawFrame != slot.Frame)
        {
            int lines = slot.FlashStep is 0 or 1 && slot.Result is {} result
                ? result.Lines.Where(l => l.Enabled && l.Coins > 0).Aggregate(0, (mask, l) => mask | (1 << (int)l.Line)) : 0;
            byte[] pixels = _slotRenderer.Render(slot.Reels, _session.State.Coins, slot.PayoutRemaining, slot.Bet,
                slot.PressedButtons, lines, null, _slotFairy, checked((int)(slot.Frame - _slotFairySince)), slot.HelpWidth,
                slot.Palette?.Y ?? (slot.Phase == CampaignSlotPhase.Initialize ? 16 : 0));
            using var image = Image.CreateFromData(240, 160, false, Image.Format.Rgba8, pixels);
            if (_slotTexture is null) _slotTexture = ImageTexture.CreateFromImage(image); else _slotTexture.Update(image);
            _slotDrawFrame = slot.Frame;
        }
        DrawTexture(_slotTexture!, Vector2.Zero);
        if (slot.Phase is CampaignSlotPhase.Quit or CampaignSlotPhase.NoCoins && slot.MainStep > 0)
        {
            _battleSkin.DrawUserFrame(this, new(32, 112, 176, 48));
            SourceText(_sourceNormal, new(41, 122), _slotArt.Texts.GetProperty(slot.Phase == CampaignSlotPhase.Quit ? "gString_QuitPlaying" : "gString_OutOfCoins").GetString()!);
            if (slot.Phase == CampaignSlotPhase.Quit && slot.MainStep <= 2)
            {
                _itemSkin.DrawStandardFrame(this, new(144, 64, 64, 48));
                SourceText(_sourceNormal, new(152, 74), "  YES\n  NO");
                SourceText(_sourceNormal, new(152, 74 + slot.QuitChoice * 14), "▶");
            }
        }
        return true;
    }

    private void DisposeSlotMachinePresentation()
    { _slotTexture?.Dispose(); _slotTexture = null; _slotRenderer = null; _slotArt = null; }
}

using Game.Application;
using Game.Content;
using Game.Domain.Slots;
using Godot;

public partial class CampaignView
{
    private SlotMachinePresentationData? _slotArt;
    private SlotMachineScreenRenderer? _slotRenderer;
    private ImageTexture? _slotTexture;
    private Image? _slotImage;
    private long _slotFlashSince;
    private bool _slotFlashActive;
    private int _slotDisplayBet;
    private string? _slotMessage;
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
            _slotTexture?.Dispose(); _slotTexture = null;_slotImage?.Dispose();_slotImage=null;_slotFlashActive=false;
            return;
        }
        var slot = _session.State.SlotMachine!;
        if (_slotCallback != slot.CallbackId)
        {
            _slotArt ??= PretSlotMachinePresentation.Load(_root);
            _slotRenderer ??= new(_slotArt, _catalog.GameCornerSlots!.Rules);
            _slotCallback = slot.CallbackId; _slotFrameTime = 0; _slotHeld = 0;
            _slotFairy = "neutral"; _slotFairySince = slot.Frame; _slotDrawFrame = -1;_slotDisplayBet=0;_slotMessage=null;
            _held = null; }
        // Tilemap lights change only when UPDATE_LINE_LIGHTS runs. A wallet refund on Quit
        // does not clear that tilemap before the exit fade.
        if(slot.Phase==CampaignSlotPhase.Betting)_slotDisplayBet=slot.Bet;
        else if(slot.Phase==CampaignSlotPhase.Lose&&slot.MainStep>=2 || slot.Phase==CampaignSlotPhase.Payout&&slot.MainStep>=5)_slotDisplayBet=0;
        if(slot.Phase==CampaignSlotPhase.Quit&&slot.MainStep is >=1 and <=3)_slotMessage="message-quit";
        else if(slot.Phase==CampaignSlotPhase.NoCoins&&slot.MainStep>0)_slotMessage="message-no-coins";
        else if(slot.Phase==CampaignSlotPhase.Betting||slot.Phase==CampaignSlotPhase.Quit&&slot.MainStep==4)_slotMessage=null;
        // Exit deliberately retains the last window; its pixels fade with BG palettes.
        string fairy = slot.Phase switch
        {
            CampaignSlotPhase.Betting when slot.MainStep is 3 or 4 => "spinning",
            CampaignSlotPhase.Lose or CampaignSlotPhase.Payout when slot.MainStep == 0 => _slotFairy,
            CampaignSlotPhase.Lose when slot.MainStep < 2 => "lose",
            CampaignSlotPhase.Payout when slot.MainStep < 4 || slot.MainStep == 4 && slot.Setup[0] is not null => "payout",
            _ => "neutral"
        };
        if (fairy != _slotFairy) { _slotFairy = fairy; _slotFairySince = slot.Frame; }
        bool flash=slot.FlashStep is 0 or 1;
        if(flash&&!_slotFlashActive)_slotFlashSince=slot.Frame;
        _slotFlashActive=flash;
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
            if (key.Pressed && key.Keycode == Key.Escape) { RequestQuit(); return true; }
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
            int wins=slot.Result?.Lines.Where(l=>l.Enabled&&l.Coins>0).Aggregate(0,(mask,l)=>mask|(1<<(int)l.Line))??0;
            int lines=_slotFlashActive?wins:0;
            int normalLines=slot.Phase==CampaignSlotPhase.Payout&&slot.MainStep>=4&&!_slotFlashActive?wins:0;
            var flash=_slotFlashActive?SlotMachineVisualPresentation.Flash(_slotArt.Layout,slot.Frame-_slotFlashSince):default;
            var fade=slot.Palette is {} palette?SlotMachineVisualPresentation.Fade(palette.Y,palette.Target,palette.ObjectHalf,palette.Finish)
                :new SlotMachineVisualPresentation.FadeState(slot.Phase==CampaignSlotPhase.Initialize?16:0,slot.Phase==CampaignSlotPhase.Initialize?16:0);
            byte[] pixels = _slotRenderer.Render(slot.Reels, _session.State.Coins, slot.PayoutRemaining, _slotDisplayBet,
                slot.PressedButtons, lines, flash.LightFrame, _slotFairy, checked((int)(slot.Frame - _slotFairySince)), slot.HelpWidth,
                fade.Background, fade.Objects, flash.LineDarken, normalLines, _slotMessage);
            if(_slotImage is null)_slotImage=Image.CreateFromData(240,160,false,Image.Format.Rgba8,pixels);
            else _slotImage.SetData(240,160,false,Image.Format.Rgba8,pixels);
            if (_slotTexture is null) _slotTexture = ImageTexture.CreateFromImage(_slotImage); else _slotTexture.Update(_slotImage);
            _slotDrawFrame = slot.Frame;
        }
        DrawTexture(_slotTexture!, Vector2.Zero);
        if (slot.Phase is CampaignSlotPhase.Quit or CampaignSlotPhase.NoCoins && slot.MainStep > 0)
        {
            if (slot.Phase == CampaignSlotPhase.Quit && slot.MainStep <= 2)
            {
                _battleSkin.DrawUserFrame(this, new(144, 64, 64, 48));
                SourceText(_sourceNormal, new(160, 74), InventorySourceText("gText_Yes"));
                SourceText(_sourceNormal, new(160, 88), InventorySourceText("gText_No"));
                SourceText(_sourceNormal, new(152, 74 + slot.QuitChoice * 14), "▶");
            }
        }
        return true;
    }

    private void DisposeSlotMachinePresentation()
    { _slotTexture?.Dispose(); _slotTexture = null; _slotImage?.Dispose();_slotImage=null; _slotRenderer = null; _slotArt = null; }
}

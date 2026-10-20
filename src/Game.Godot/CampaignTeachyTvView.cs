using Game.Domain.Options;
using Game.Domain.Tutorials;
using Godot;

/// <summary>Outer Teachy TV bridge. Real battle/TM/registration demonstration views own the
/// AwaitingDemonstration phase through their sibling lifecycle hooks.</summary>
public partial class CampaignView
{
    private TeachyTvPresentation? _teachyTvPresentation;
    private TeachyTvRenderer? _teachyTvRenderer;
    private ulong _teachyTvClosedFrame = ulong.MaxValue;
    private bool TeachyTvOuterActive => _teachyTvPresentation?.OwnsOuterScreen == true;

    private void SynchronizeTeachyTvPresentation()
    {
        var scene = _session.State.TeachyTv;
        if (_teachyTvPresentation is null && scene is null) return;
        if (_teachyTvPresentation is null)
        {
            _teachyTvPresentation = new();
            _teachyTvPresentation.SoundRequested += id => SourceSound(id);
            // Music is committed by Application and played by the existing audio owner.
            _teachyTvRenderer = TeachyTvRenderer.Load(_root, _textCursor);
        }
        bool wasActive = _teachyTvPresentation.OwnsOuterScreen;
        _teachyTvPresentation.Synchronize(_session.TeachyTvCatalog!, scene?.Playback,
            _session.EffectiveOptions.TextSpeed switch { SourceTextSpeed.Slow => 8, SourceTextSpeed.Mid => 4, _ => 1 });
        if (wasActive && scene is null) _teachyTvClosedFrame = Engine.GetProcessFrames();
        if (scene is not null) { _held = null; }
    }
    private void FlushTeachyTvRequests()
    {
        while (_teachyTvPresentation!.TryTakeRequest(out var request))
        {
            string kind = request!.Action switch
            {
                TeachyTvPresentationAction.Navigate => "teachy_tv_navigate",
                TeachyTvPresentationAction.ConfirmMenu => "teachy_tv_choose",
                TeachyTvPresentationAction.Acknowledge => "teachy_tv_presented",
                TeachyTvPresentationAction.ConfirmText => "teachy_tv_confirm",
                TeachyTvPresentationAction.Cancel => "teachy_tv_cancel",
                TeachyTvPresentationAction.Select => "teachy_tv_select_button",
                TeachyTvPresentationAction.BeginDemonstration => "teachy_tv_begin_demo",
                _ => throw new InvalidOperationException("Unknown Teachy TV presentation request.")
            };
            Apply(new(kind, Text: request.WaitId,
                Choice: request.Action == TeachyTvPresentationAction.Navigate ? request.Value : null));
        }
    }
    private bool ProcessTeachyTvPresentation(double delta)
    {
        if (_teachyTvPresentation?.Advance(delta) != true) return false;
        FlushTeachyTvRequests(); QueueRedraw(); return true;
    }
    private bool DrawTeachyTv() => _teachyTvRenderer is not null && _teachyTvPresentation is not null
        && _teachyTvRenderer.Draw(this, _teachyTvPresentation);
    private bool TryTeachyTvInput(InputEvent input)
    {
        if (!TeachyTvOuterActive)
        {
            if (_teachyTvClosedFrame != Engine.GetProcessFrames()) return false;
            GetViewport().SetInputAsHandled(); return true;
        }
        _held = null; if (input is InputEventKey key && !key.Echo)
        {
            if (key.Pressed && key.Keycode == Key.Escape) { RequestQuit(); return true; }
            var button = TeachyTvButtonForKey(key.Keycode);
            if (button is {} b) _teachyTvPresentation!.Input(b, key.Pressed);
        }
        else if (input is InputEventJoypadButton joy)
        {
            TeachyTvButton? button = joy.ButtonIndex switch
            {
                JoyButton.A => TeachyTvButton.A, JoyButton.B => TeachyTvButton.B,
                JoyButton.Back => TeachyTvButton.Select, JoyButton.DpadUp => TeachyTvButton.Up,
                JoyButton.DpadDown => TeachyTvButton.Down, JoyButton.DpadLeft => TeachyTvButton.Left,
                JoyButton.DpadRight => TeachyTvButton.Right, _ => null
            };
            if (button is {} b) _teachyTvPresentation!.Input(b, joy.Pressed);
        }
        else if (input is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            // Mouse A/B is an accessibility alias; it never bypasses printer/input waits.
            _teachyTvPresentation!.Input(mouse.ButtonIndex == MouseButton.Right ? TeachyTvButton.B : TeachyTvButton.A, mouse.Pressed);
        }
        FlushTeachyTvRequests(); QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
    }
    private static TeachyTvButton? TeachyTvButtonForKey(Key key) => key switch
    {
        Key.Enter or Key.Z => TeachyTvButton.A, Key.Backspace or Key.X => TeachyTvButton.B,
        Key.R => TeachyTvButton.Select, Key.Up => TeachyTvButton.Up, Key.Down => TeachyTvButton.Down,
        Key.Left => TeachyTvButton.Left, Key.Right => TeachyTvButton.Right, _ => null
    };
    private void DisposeTeachyTvPresentation()
    {
        _teachyTvRenderer?.Dispose(); _teachyTvRenderer = null;
        _teachyTvPresentation = null;
    }
}

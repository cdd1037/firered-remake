using Game.Application;
using Godot;
using Direction = Game.Domain.World.Direction;

public partial class CampaignView
{
    private Direction? _regionalSignHeld;
    private double _regionalSignFrameTime;
    private ulong? _regionalSignClosedFrame;
    private bool RegionalSignActive => _session?.RegionalSignWalkawayActive == true;

    private void ResetRegionalSignInput()
    {
        _regionalSignHeld = null;
        _regionalSignFrameTime = 0;
    }

    private void SynchronizeRegionalSignInput(CampaignState before, CampaignState after)
    {
        if (!RegionalSignActive)
        {
            if (before.SignMessage is { Cancelable: true } && _catalog.RegionalFlavorEnabled)
            {
                _regionalSignClosedFrame = Engine.GetProcessFrames();
                _held = null; _repeat = 0;
            }
            ResetRegionalSignInput();
            return;
        }
        if (before.SignMessage is not { Cancelable: true })
        {
            // Retain the direction which caused the bump. The normal walking
            // repeat loop must never inherit a direction from this dialogue.
            _regionalSignHeld = _held;
            _regionalSignFrameTime = 0;
        }
        _held = null; _repeat = 0;
    }

    private bool TryRegionalSignDirectionInput(InputEvent input)
    {
        if (_regionalSignClosedFrame == Engine.GetProcessFrames() &&
            input is not InputEventKey { Keycode: Key.Escape or Key.F12 }) return true;
        if (!RegionalSignActive || input is not InputEventKey key) return false;
        Direction? direction = key.Keycode switch
        {
            Key.Up => Direction.North, Key.Down => Direction.South,
            Key.Left => Direction.West, Key.Right => Direction.East, _ => null
        };
        if (direction is null) return false;
        if (!key.Echo)
        {
            if (key.Pressed) _regionalSignHeld = direction;
            else if (_regionalSignHeld == direction) _regionalSignHeld = null;
        }
        _held = null; _repeat = 0;
        return true;
    }

    // Called from the existing Start/Tab handler before its stable-field gate.
    // Start is a press edge: a press during inhibition is consumed, never queued.
    private bool TryRegionalSignStart(bool pressed)
    {
        if (!RegionalSignActive) return false;
        if (!pressed || _busy || _session.State.SignMessage!.InhibitTicks != 0) return true;
        var facing = _session.State.World.Player.Facing;
        var direction = _regionalSignHeld ?? facing;
        if (direction != facing && _session.State.SignMessage.WalkawayDisabled) return true;
        bool accepted = Apply(new("sign_cancel", direction, Text: "start"));
        if (accepted && direction == facing && CanOpenFieldMenu) OpenFieldStartMenu();
        return true;
    }

    private bool ProcessRegionalSignWalkaway(double delta)
    {
        if (!RegionalSignActive) { ResetRegionalSignInput(); return false; }
        if (_busy || !double.IsFinite(delta) || delta <= 0) return false;
        // At most six decrement commands plus one cancellation, even after a
        // long presentation stall. Once ready and idle there are zero commands.
        _regionalSignFrameTime += Math.Min(delta, 7.0 / 60);
        while (_regionalSignFrameTime + 1e-9 >= 1.0 / 60 && RegionalSignActive)
        {
            _regionalSignFrameTime -= 1.0 / 60;
            var sign = _session.State.SignMessage!;
            if (sign.InhibitTicks > 0) { Apply(new("sign_tick")); continue; }
            if (_regionalSignHeld is { } direction && direction != _session.State.World.Player.Facing && !sign.WalkawayDisabled)
            {
                Apply(new("sign_cancel", direction, Text: "direction"));
                return true;
            }
            _regionalSignFrameTime = 0;
            break;
        }
        return false;
    }
}

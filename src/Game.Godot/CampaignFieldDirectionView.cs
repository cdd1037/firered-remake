using Game.Application;
using Game.Domain.World;

public partial class CampaignView
{
    private readonly FieldDirectionCadence _fieldDirections = new();
    // Legacy modal owners clear logical input through this one shared seam.
    // They must not cancel an accepted movement's remaining duration.
    private Direction? _held { get => _fieldDirections.Held; set {
        if (value is {} direction) _fieldDirections.SetHeld(direction, true);
        else _fieldDirections.ClearHeld();
    } }
    private bool FieldDirectionReady => _fieldDirections.Ready && !_busy &&
        _session.State.Script.Completed && _session.State.Script.Wait is null &&
        _session.State.Dialogue is null && _tracks.All(t => t.Time >= t.Duration);

    private void ResetFieldDirectionCadence()
    {
        _fieldDirections.Reset();
    }
    private void FieldDirectionInput(Direction direction, bool pressed)
    {
        _fieldDirections.SetHeld(direction, pressed);
        // Resolve all physical edges once in the field callback, preserving D-pad priority.
    }
    private void ProcessFieldDirection()
    {
        if (!FieldDirectionReady || _held is not {} direction) return;
        var command = CyclingRoadInput(new(_fieldDirections.NeedsTurn(_session.State.World.Player.Facing) ? "turn" : "move", direction));
        ApplyFieldDirection(command);
    }
    private bool ApplyFieldDirection(CampaignCommand command)
    {
        var before = _session.State;
        var slope = command.Kind == "cycle_input" ? CyclingRoadRules.Resolve(before.BicycleSlope,
            before.World.Player.Facing, command.Text == "idle" ? null : command.Direction, command.Choice == 1) : null;
        if (!Apply(command))
        {
            _fieldDirections.Complete(false, 0, false, command.Text == "idle");
            return false;
        }
        var after = _session.State;
        if (before.World.Player.MapId != after.World.Player.MapId) return true;
        bool moved = before.World.Player.X != after.World.Player.X || before.World.Player.Y != after.World.Player.Y;
        bool moving = slope is null ? command.Kind == "move" : slope.Action == CyclingRoadAction.Move;
        int frames = FieldDirectionCadence.ActionFrames(before.IsCycling, moved, command.Kind == "turn", _fieldStepTicks, slope?.Action);
        _fieldDirections.Complete(true, frames, moving);
        return true;
    }
}

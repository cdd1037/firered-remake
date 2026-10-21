using Game.Application;
using Game.Domain.World;

public partial class CampaignView
{
    private bool CyclingRoadSlope => _session.State.IsCycling &&
        CyclingRoadRules.IsSlope(_catalog.SourceMaps[_session.State.World.Player.MapId]
            .SourceBehaviorAt(_session.State.World.Player.X, _session.State.World.Player.Y));

    private CampaignCommand CyclingRoadInput(CampaignCommand command) =>
        CyclingRoadSlope && command.Kind is "move" or "turn"
            ? new("cycle_input", command.Direction, Choice: _runningShiftHeld ? 1 : 0) : command;

    private int CyclingRoadInputTicks(CampaignState state, CampaignCommand command) =>
        CyclingRoadRules.Resolve(state.BicycleSlope, state.World.Player.Facing,
            command.Text == "idle" ? null : command.Direction, command.Choice == 1).Ticks;

    private bool ProcessCyclingRoadPull()
    {
        if (!CyclingRoadSlope || _held is not null || _fieldDirections.IdlePullFailed ||
            !_session.State.Script.Completed || _session.State.Script.Wait is not null ||
            _session.State.Dialogue is not null || _busy || _runningShiftHeld && !_session.State.BicycleSlope)
            return false;
        if (FieldDirectionReady)
            ApplyFieldDirection(new("cycle_input", Choice: _runningShiftHeld ? 1 : 0, Text: "idle"));
        return true;
    }
}

using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Call once for new game / explicit profile upgrade, never on every EnterMap or Restore.
    private CampaignState InitializeRenewableHiddenItems(CampaignState state) => !_content.FirstSeviiEnabled ? state :
        state with { Script = state.Script with { Flags = RenewableHiddenItemRules.SetAllRenewableItemFlags(state.Script.Flags) } };

    // Caller owns source tookStep dispatch: after trainer/on-frame preemption, before step scripts.
    private CampaignState ApplyRenewableHiddenItemWalkingStep(CampaignState state)
    {
        if (!_content.FirstSeviiEnabled) return state;
        int old = state.Script.Variables.GetValueOrDefault(RenewableHiddenItemRules.CounterVariable);
        int next = RenewableHiddenItemRules.IncrementStepCounter(old);
        if(next != old) state = state with { Script = state.Script with
        { Variables = state.Script.Variables.SetItem(RenewableHiddenItemRules.CounterVariable, next) } };
        if(_content.SixIslandEnabled)state=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_MASSAGE_COOLDOWN_STEP_COUNTER",Math.Min(500,state.Script.Variables.GetValueOrDefault("VAR_MASSAGE_COOLDOWN_STEP_COUNTER")+1))}};
        return state;
    }

    private CampaignState ApplyRenewableHiddenItemMapEntry(CampaignState state)
    {
        if (!_content.FirstSeviiEnabled) return state;
        var result = RenewableHiddenItemRules.TryRegenerate(
            _content.MapDefinition(state.World.Player.MapId).SourceMapSymbol,
            state.Script.Variables.GetValueOrDefault(RenewableHiddenItemRules.CounterVariable),
            state.Script.Flags, state.World.Rng);
        return !result.Regenerated ? state : state with
        {
            Script = state.Script with
            {
                Variables = state.Script.Variables.SetItem(RenewableHiddenItemRules.CounterVariable, result.StepCounter),
                Flags = result.Flags
            },
            World = state.World with { Rng = result.Rng }
        };
    }

    private string? ValidateRenewableHiddenItems(CampaignState state)
    {
        if (!_content.FirstSeviiEnabled) return null;
        if (_content.SixIslandEnabled && state.Script.Variables.GetValueOrDefault("VAR_MASSAGE_COOLDOWN_STEP_COUNTER") is < 0 or > 500)
            return "save_massage_cooldown_counter_invalid";
        if (state.Script.Variables.GetValueOrDefault(RenewableHiddenItemRules.CounterVariable) is < 0 or > RenewableHiddenItemRules.StepThreshold)
            return "save_renewable_counter_invalid";
        return RenewableHiddenItemRules.AllFlags.Any(flag => !state.Script.Flags.ContainsKey(flag))
            ? "save_renewable_flags_uninitialized" : null;
    }
}

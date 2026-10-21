using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplyStatFlagUnlockEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "special" || effect.Args.Length != 1 ||
            !_content.StatFlagUnlocks.TryGetValue(effect.Args[0], out var definition)) return false;
        if (state.World.Player.MapId != definition.Map ||
            _content.MapDefinition(definition.Map).Programs.OnTransition != effect.Source.Symbol)
            throw new InvalidDataException("stat_flag_unlock_transition_context");
        next = state with { Script = state.Script with { Flags = definition.Apply(state.Script.Flags, state.GameStats) } };
        return true;
    }
}

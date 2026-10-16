using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool TryApplyStoryRandomEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "random") return false;
        var result = StoryRandomRules.Apply(state.Script, state.World.Rng, effect);
        next = state with { Script = result.Story, World = state.World with { Rng = result.Rng } };
        return true;
    }
}

using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplyFieldMilestoneEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "specialvar" || effect.Args.Length != 2 ||
            !_content.FieldMilestones.TryGetValue(effect.Args[1], out var collection)) return false;
        if (state.World.Player.MapId != collection.Map ||
            _content.FindProgram(effect.Source.Symbol)?.Instructions.Any(i => i.Source == effect.Source &&
                i.Opcode == effect.Opcode && i.Args.SequenceEqual(effect.Args)) != true)
            throw new InvalidDataException("field_milestone_effect_context");
        next = state with { Script = state.Script with {
            Variables = collection.Query(state.Script.Variables, state.GameStats, effect.Args[0]) } };
        return true;
    }

    private string? ValidateFieldMilestones(CampaignState state)
    {
        foreach (var collection in _content.FieldMilestones.Values)
            foreach (var milestone in collection.Milestones)
            {
                int awarded = state.Script.Variables.GetValueOrDefault(milestone.AwardVariable);
                // Stored stickers survive later counter wrapping. Only available categories can be awarded.
                int lifetimeCount = state.GameStats?.GetValueOrDefault(milestone.Stat) ?? 0;
                int earnedTier = milestone.Available ? milestone.MinimumCounts.Count(minimum => lifetimeCount >= minimum) : 0;
                if (awarded < 0 || awarded > earnedTier)
                    return "save_field_milestone_award_invalid";
            }
        return null;
    }
}

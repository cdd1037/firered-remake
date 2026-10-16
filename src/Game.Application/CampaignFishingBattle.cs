using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignState BeginFishingWildBattle(CampaignState state,OldRodFishingState fishing)
    {
        if (InSafariArea(state)) return BeginSafariFishingBattle(state, fishing);
        var profile = FishingProfile(state);
        if (!_content.CityServicesEnabled || state.Fishing is null || fishing.Status != OldRodFishingStatus.EncounterReady ||
            fishing.Map != state.World.Player.MapId || !profile.HasTable(fishing.Rod, fishing.Map))
            throw new InvalidDataException("fishing_encounter_context_invalid");
        var candidate = profile.GenerateCandidate(fishing, state.World.Rng);
        var origin = new FishingEncounterOrigin(fishing.Rod, fishing.Map, candidate.Candidate.SlotIndex,
            candidate.Candidate.Species, candidate.Candidate.Level);
        var generated = profile.GeneratePokemon(_binding.Ownership, origin, candidate.GlobalRng, state.TrainerId);
        var next = CompleteFishingTask(state);
        // Shared source battle entry owns the original AI-init, held-item and initial order draws.
        // Battle entry resets land immunity/rate modifiers, but fishing does not consume
        // a land candidate, draw its independent rate RNG, reduce Repel steps, or add a field step.
        return BeginWildBattle(next, new(generated.Pokemon, generated.GlobalRng, [..candidate.RandomTrace, ..generated.RandomTrace]),
            fishingOrigin: origin);
    }
    private CampaignState CompleteFishingTask(CampaignState state)
    {
        var stats = state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty;
        return state with { Fishing = null, ItemMenu = null, Dialogue = null,
            Exploration = ResetEncounterModifiers(state.Exploration!),
            GameStats = stats.SetItem("GAME_STAT_FISHING_CAPTURES", Math.Min(0xffffff, stats.GetValueOrDefault("GAME_STAT_FISHING_CAPTURES") + 1)) };
    }

}

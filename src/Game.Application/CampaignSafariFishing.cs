using Game.Domain.Encounters;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool SafariFishingActive(CampaignState state) => _content.SafariEnabled && _content.SafariFishing is not null &&
        state.SafariVisit is { Active: true, Balls: > 0, Steps: > 0 } &&
        state.Script.Flags.GetValueOrDefault(SafariFlag) && InSafariArea(state);

    private CampaignState BeginSafariFishingBattle(CampaignState state, OldRodFishingState fishing)
    {
        if (!SafariFishingActive(state) || state.Fishing is null || fishing.Map != state.World.Player.MapId ||
            fishing.Status != OldRodFishingStatus.EncounterReady)
            throw new InvalidDataException("safari_fishing_context_invalid");
        var profile = FishingProfile(state);
        var candidate = profile.GenerateCandidate(fishing, state.World.Rng);
        var origin = new FishingEncounterOrigin(fishing.Rod, fishing.Map, candidate.Candidate.SlotIndex,
            candidate.Candidate.Species, candidate.Candidate.Level);
        var generated = profile.GeneratePokemon(_binding.Ownership, origin, candidate.GlobalRng, state.TrainerId);
        var rng = generated.GlobalRng;
        var trace = candidate.RandomTrace.AddRange(generated.RandomTrace).ToBuilder();
        // The same Safari entry sequence as land/surf: AI setup, held item, then turn counter.
        for (int slot = 0; slot < 4; slot++)
        {
            var draw = Gen3Rng.Next(rng, "safari.entry.ai-setup.slot-" + slot, 16);
            rng = draw.State; trace.Add(draw.Trace);
        }
        var held = _content.SafariFishing!.AssignHeldItem(origin, rng);
        trace.AddRange(held.RandomTrace);
        var next = CompleteFishingTask(state);
        return BeginSafariBattle(next, generated.Pokemon, held.GlobalRng, trace.ToImmutable(), held.HeldItem, fishingOrigin: origin);
    }

    private bool IsSafariFishingHistory(CampaignWildHistory history) => history.Safari && history.Fishing is { } origin &&
        _content.SafariEnabled && _content.SafariFishing?.SourceSlot(origin) is not null &&
        _content.PokemonContent!.Catalog.Species.TryGetValue(origin.Species, out var species) &&
        species.SourceSpeciesId == history.SourceSpeciesId && origin.Level == history.Level &&
        history.ExperienceGained == 0 && history.Outcome is WildBattleOutcome.Caught or WildBattleOutcome.Escaped &&
        history.SourceTableReceipt is null && history.RockSmashReceipt is null && history.SeafoamReceipt is null &&
        history.PowerPlantReceipt is null && history.EventIslandReceipt is null && history.CelioRoamer is null &&
        history.CeruleanCaveOrigin is null && history.SevenIsland is null && history.SevenIslandReceipt is null &&
        history.MewtwoReceipt is null && history.FiveIsland is null && history.SixIsland is null && history.FourIsland is null &&
        history.SeviiRuby is null && history.VictoryRoad is null && history.LostelleHypno is null &&
        !history.ScriptedMarowak && history.ScriptedSnorlaxRoute == 0;

    private bool IsSafariFishingCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.SingleOrDefault(h => h.Id == capture.BattleId);
        return capture.Fishing is { } origin && capture.Ball == "ITEM_SAFARI_BALL" &&
            capture.MetLocation == "MAPSEC_KANTO_SAFARI_ZONE" && capture.MetLevel == origin.Level &&
            capture.SourceTable is null && capture.RockSmash is null && capture.Seafoam is null && capture.PowerPlant is null &&
            capture.EventIsland is null && capture.SevenIsland is null && capture.CeruleanCaveOrigin is null &&
            capture.CelioRoamer is null && capture.FiveIsland is null && capture.SixIsland is null && capture.FourIsland is null &&
            history is not null && history.Fishing == origin && IsSafariFishingHistory(history) &&
            history.Outcome == WildBattleOutcome.Caught && history.SourceSpeciesId == capture.SourceSpeciesId;
    }
}

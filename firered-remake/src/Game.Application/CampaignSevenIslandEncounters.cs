using Game.Domain.Encounters;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed record CampaignSevenIslandWildReceipt(RngState RngBefore, RngState RngAfter, PokemonInstance InitialPokemon);

public sealed partial class CampaignSession
{
    private bool IsSevenIslandWildHistory(CampaignWildHistory history)
    {
        if (!_content.SevenIslandEnabled || history.SevenIsland is not { } origin || history.SevenIslandReceipt is not { } receipt ||
            history.CeruleanCaveOrigin is not null || history.MewtwoReceipt is not null || history.CelioRoamer is not null ||
            history.FiveIsland is not null || history.SixIsland is not null || history.SeviiRuby is not null || history.FourIsland is not null ||
            history.VictoryRoad is not null || history.Safari || history.ScriptedMarowak || history.ScriptedSnorlaxRoute != 0 || history.LostelleHypno is not null ||
            origin.Kind is not (SevenIslandEncounterKind.Land or SevenIslandEncounterKind.Surf or SevenIslandEncounterKind.OldRod) ||
            receipt.InitialPokemon is not { } initial || initial.SourceSpeciesId != history.SourceSpeciesId || initial.Level != history.Level ||
            receipt.RngBefore.AlgorithmId != Gen3Rng.AlgorithmId || receipt.RngAfter.AlgorithmId != Gen3Rng.AlgorithmId ||
            receipt.RngBefore.Cursor >= receipt.RngAfter.Cursor ||
            !SevenIslandWildPokemonFactory.SourceInitialInstance(_content.SevenIslandOwnership!, origin, initial)) return false;
        try
        {
            var made = SevenIslandWildPokemonFactory.GenerateSourceSlot(_content.SevenIslandOwnership!, origin,
                checked((byte)initial.Level), receipt.RngBefore, initial.OriginalTrainerId);
            return made.GlobalRng == receipt.RngAfter && SameSourceOwnedPokemon(made.Pokemon, initial);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException) { return false; }
    }

    private bool IsSevenIslandCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.SingleOrDefault(h => h.Id == capture.BattleId);
        return history is not null && capture.SevenIsland is not null && history.SevenIsland == capture.SevenIsland &&
            (capture.Ball is null || AdditionalCaptureBallSupported(capture.Ball)) &&
            capture.CeruleanCaveOrigin is null && capture.CelioRoamer is null && capture.FiveIsland is null && capture.SixIsland is null && capture.FourIsland is null &&
            IsSevenIslandWildHistory(history) && history.Outcome == WildBattleOutcome.Caught && history.ExperienceGained == 0 &&
            history.SourceSpeciesId == capture.SourceSpeciesId && history.Level == capture.MetLevel &&
            history.SevenIslandReceipt!.InitialPokemon.Personality == capture.Personality && history.SevenIslandReceipt.InitialPokemon.Ivs == capture.Ivs &&
            _content.MapDefinition(capture.SevenIsland.Map).RegionMapSection == capture.MetLocation;
    }

    private string? ValidateUnownPokedex(CampaignState state)
    {
        var histories = state.Exploration?.Battles ?? [];
        foreach (var history in histories.Where(h => h.SevenIsland is not null))
        {
            if (!IsSevenIslandWildHistory(history) || history.SevenIslandReceipt!.InitialPokemon.OriginalTrainerId != state.TrainerId ||
                history.SevenIslandReceipt.RngAfter.Cursor > state.World.Rng.Cursor) return "save_seven_island_wild_receipt";
            if (history.SourceSpeciesId == 201 && !state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS")) return "save_unown_locked_origin";
        }
        // This is a Pokédex fact: the first capture replaces the first-seen form,
        // and releasing that individual never changes the representative.
        if (state.SeenSpecies.Contains(201) != state.UnownPokedexPersonality.HasValue ||
            state.SeenSpecies.Contains(201) && !state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS"))
            return "save_unown_pokedex_representative";
        return null;
    }
}

using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Authenticated source-table generation, before battle-entry draws or damage.</summary>
public sealed record CampaignSourceTableReceipt(SourceTableWildOrigin Origin, RngState RngBefore,
    RngState RngAfter, PokemonInstance InitialPokemon);

public sealed partial class CampaignSession
{
    private bool IsSourceTableMap(MapId map) => _content.Maps.ContainsKey(map) &&
        (SourceTableEncounterRegistry.Land.Any(t => t.Map == map) || SourceTableEncounterRegistry.Water.Any(t => t.Map == map) ||
         SourceTableEncounterRegistry.OldRod.Any(t => t.Map == map) || SourceScriptedEncounterRegistry.Definitions.Any(d => d.Map == map));

    private CampaignSourceTableReceipt? PrepareSourceTableReceipt(CampaignState state, Route1WildPokemonResult made,
        SourceTableWildOrigin? origin, RngState? generationRng, bool scripted, bool surfing, bool fishing,
        FishingEncounterOrigin? fishingOrigin, bool scriptedSnorlax = false, bool rockSmash = false)
    {
        if (origin is null)
        {
            bool typedFishing = !scripted && !surfing && fishing && fishingOrigin is not null &&
                fishingOrigin.Map == state.World.Player.MapId && IsTypedFishingOrigin(fishingOrigin) &&
                FishingBattleContract.Matches(fishingOrigin, made.Pokemon, _content.FishingTables);
            // Route16 retains the separately authenticated scripted Snorlax encounter.
            bool route16Snorlax = scriptedSnorlax && state.World.Player.MapId == Route16LandEncounterTables.Map;
            if (IsSourceTableMap(state.World.Player.MapId) && !typedFishing && !route16Snorlax && !rockSmash)
                throw new InvalidDataException("source_table_wild_missing_origin");
            return null;
        }
        var expectedKind = scripted ? SourceTableEncounterKind.Scripted : surfing ? SourceTableEncounterKind.Surf : fishing ? SourceTableEncounterKind.OldRod : SourceTableEncounterKind.Land;
        if (fishingOrigin is not null || !IsSourceTableMap(origin.Map) || origin.Map != state.World.Player.MapId ||
            origin.Kind != expectedKind || made.Pokemon.OriginalTrainerId != state.TrainerId ||
            !SourceTableWildBattleContract.Matches(origin, made.Pokemon))
            throw new InvalidDataException("source_table_wild_field_origin");
        var receipt = new CampaignSourceTableReceipt(origin, generationRng ?? state.World.Rng, made.GlobalRng, made.Pokemon);
        if (!IsSourceTableReceipt(receipt)) throw new InvalidDataException("source_table_creation_receipt");
        return receipt;
    }

    private bool IsSourceTableReceipt(CampaignSourceTableReceipt receipt)
    {
        if (receipt.Origin is not { } origin || !IsSourceTableMap(origin.Map) || receipt.InitialPokemon is not { } initial ||
            receipt.RngBefore.AlgorithmId != Gen3Rng.AlgorithmId || receipt.RngAfter.AlgorithmId != Gen3Rng.AlgorithmId ||
            receipt.RngBefore.Cursor >= receipt.RngAfter.Cursor || !SourceTableWildBattleContract.Matches(origin, initial)) return false;
        try
        {
            var made = SourceTableWildBattleContract.Generate(_binding.Ownership, origin, checked((byte)initial.Level),
                receipt.RngBefore, initial.OriginalTrainerId);
            return made.GlobalRng == receipt.RngAfter && SameSourceOwnedPokemon(made.Pokemon, initial);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { return false; }
    }

    private bool IsSourceTableHistory(CampaignWildHistory history) => history.SourceTableReceipt is { } receipt &&
        history.RockSmashReceipt is null && history.SeafoamReceipt is null && history.Fishing is null && history.PowerPlantReceipt is null && history.EventIslandReceipt is null &&
        history.SevenIsland is null && history.SevenIslandReceipt is null && history.CeruleanCaveOrigin is null && history.MewtwoReceipt is null &&
        history.CelioRoamer is null && history.FiveIsland is null && history.SixIsland is null && history.FourIsland is null &&
        history.SeviiRuby is null && history.VictoryRoad is null && history.LostelleHypno is null && history.ScriptedSnorlaxRoute == 0 &&
        !history.ScriptedMarowak && !history.Safari && Enum.IsDefined(history.Outcome) && history.Outcome != WildBattleOutcome.Ongoing &&
        history.ExperienceGained >= 0 && (history.Outcome == WildBattleOutcome.Won || history.ExperienceGained == 0) &&
        IsSourceTableReceipt(receipt) && receipt.InitialPokemon.SourceSpeciesId == history.SourceSpeciesId && receipt.InitialPokemon.Level == history.Level;

    private bool IsSourceTableCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.FirstOrDefault(b => b.Id == capture.BattleId);
        if (capture.SourceTable is not { } origin || history is null || !IsSourceTableHistory(history) ||
            history.SourceTableReceipt!.Origin != origin || history.Outcome != WildBattleOutcome.Caught ||
            capture.RockSmash is not null || capture.Seafoam is not null || capture.Fishing is not null || capture.PowerPlant is not null || capture.EventIsland is not null ||
            capture.SevenIsland is not null || capture.CeruleanCaveOrigin is not null || capture.CelioRoamer is not null ||
            capture.FiveIsland is not null || capture.SixIsland is not null || capture.FourIsland is not null ||
            capture.SourceSpeciesId != history.SourceSpeciesId || capture.MetLevel != history.Level ||
            capture.MetLocation != _content.MapDefinition(origin.Map).RegionMapSection) return false;
        var initial = history.SourceTableReceipt.InitialPokemon;
        var owned = OwnedPokemon(state).FirstOrDefault(p => p.InstanceId == capture.InstanceId);
        return owned is not null && !owned.ModernFatefulEncounter && initial.OriginalTrainerId == state.TrainerId &&
            capture.Personality == initial.Personality && capture.Ivs == initial.Ivs &&
            owned.Pokemon.OriginalTrainerId == initial.OriginalTrainerId && owned.Pokemon.Personality == initial.Personality &&
            owned.Pokemon.Ivs == initial.Ivs && owned.Pokemon.Nature == initial.Nature && owned.Pokemon.Gender == initial.Gender;
    }

    private string? ValidateSourceTableProgress(CampaignState state)
    {
        if (ValidateSourceScriptedProgress(state) is {} scriptedError) return scriptedError;
        var histories = (state.Exploration?.Battles ?? []).Where(b => b.SourceTableReceipt is not null).ToArray();
        if (histories.Any(b => !IsSourceTableHistory(b) || b.SourceTableReceipt!.InitialPokemon.OriginalTrainerId != state.TrainerId ||
            b.SourceTableReceipt.RngAfter.Cursor > state.World.Rng.Cursor)) return "save_source_table_history";
        for (int i = 1; i < histories.Length; i++)
            if (histories[i].SourceTableReceipt!.RngBefore.Cursor < histories[i - 1].SourceTableReceipt!.RngAfter.Cursor)
                return "save_source_table_creation_order";
        return null;
    }
}

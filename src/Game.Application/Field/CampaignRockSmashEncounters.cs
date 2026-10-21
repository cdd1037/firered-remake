using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsRockSmashReceipt(CampaignRockSmashReceipt receipt)
    {
        if (receipt.Origin is not { } origin || (_content.FieldRockSmash is null || !_content.Maps.ContainsKey(origin.Map) || _content.FieldRockSmash.EncountersFor(origin.Map) is null) || receipt.InitialPokemon is not { } initial ||
            receipt.RngBefore.AlgorithmId != Gen3Rng.AlgorithmId || receipt.RngAfter.AlgorithmId != Gen3Rng.AlgorithmId ||
            receipt.RngBefore.Cursor >= receipt.RngAfter.Cursor || !RockSmashWildBattleContract.Matches(origin, initial)) return false;
        try
        {
            var made = RockSmashWildBattleContract.Generate(_binding.Ownership, _content.FieldRockSmash!, origin, checked((byte)initial.Level),
                receipt.RngBefore, initial.OriginalTrainerId);
            return made.GlobalRng == receipt.RngAfter && SameSourceOwnedPokemon(made.Pokemon, initial);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { return false; }
    }

    private bool IsRockSmashHistory(CampaignWildHistory history) => history.RockSmashReceipt is { } receipt &&
        history.SourceTableReceipt is null && history.SeafoamReceipt is null && history.Fishing is null && history.PowerPlantReceipt is null && history.EventIslandReceipt is null &&
        history.SevenIsland is null && history.SevenIslandReceipt is null && history.CeruleanCaveOrigin is null && history.MewtwoReceipt is null &&
        history.CelioRoamer is null && history.FiveIsland is null && history.SixIsland is null && history.FourIsland is null &&
        history.SeviiRuby is null && history.VictoryRoad is null && history.LostelleHypno is null && history.ScriptedSnorlaxRoute == 0 &&
        !history.ScriptedMarowak && !history.Safari && Enum.IsDefined(history.Outcome) && history.Outcome != WildBattleOutcome.Ongoing &&
        history.ExperienceGained >= 0 && (history.Outcome == WildBattleOutcome.Won || history.ExperienceGained == 0) &&
        IsRockSmashReceipt(receipt) && receipt.InitialPokemon.SourceSpeciesId == history.SourceSpeciesId && receipt.InitialPokemon.Level == history.Level;

    private bool IsRockSmashCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        var history = state.Exploration?.Battles.FirstOrDefault(b => b.Id == capture.BattleId);
        if (capture.RockSmash is not { } origin || history is null || !IsRockSmashHistory(history) ||
            history.RockSmashReceipt!.Origin != origin || history.Outcome != WildBattleOutcome.Caught ||
            capture.SourceTable is not null || capture.Seafoam is not null || capture.Fishing is not null || capture.PowerPlant is not null || capture.EventIsland is not null ||
            capture.SevenIsland is not null || capture.CeruleanCaveOrigin is not null || capture.CelioRoamer is not null ||
            capture.FiveIsland is not null || capture.SixIsland is not null || capture.FourIsland is not null ||
            capture.SourceSpeciesId != history.SourceSpeciesId || capture.MetLevel != history.Level ||
            capture.MetLocation != _content.MapDefinition(origin.Map).RegionMapSection) return false;
        var initial = history.RockSmashReceipt.InitialPokemon;
        var owned = OwnedPokemon(state).FirstOrDefault(p => p.InstanceId == capture.InstanceId);
        return owned is not null && !owned.ModernFatefulEncounter && initial.OriginalTrainerId == state.TrainerId &&
            capture.Personality == initial.Personality && capture.Ivs == initial.Ivs &&
            owned.Pokemon.OriginalTrainerId == initial.OriginalTrainerId && owned.Pokemon.Personality == initial.Personality &&
            owned.Pokemon.Ivs == initial.Ivs && owned.Pokemon.Nature == initial.Nature && owned.Pokemon.Gender == initial.Gender;
    }

    private string? ValidateRockSmashProgress(CampaignState state)
    {
        var histories = (state.Exploration?.Battles ?? []).Where(b => b.RockSmashReceipt is not null).ToArray();
        if (histories.Any(b => !IsRockSmashHistory(b) || b.RockSmashReceipt!.InitialPokemon.OriginalTrainerId != state.TrainerId ||
            b.RockSmashReceipt.RngAfter.Cursor > state.World.Rng.Cursor)) return "save_rock_smash_history";
        for (int i = 1; i < histories.Length; i++)
            if (histories[i].RockSmashReceipt!.RngBefore.Cursor < histories[i - 1].RockSmashReceipt!.RngAfter.Cursor)
                return "save_rock_smash_creation_order";
        return null;
    }
}

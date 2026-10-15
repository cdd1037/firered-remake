using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>
/// Retains business-referenced settlements, not a chronological audit log.
/// Special encounter ledgers remain intact until their consumers have explicit state models.
/// </summary>
public static class CampaignWildHistoryRetention
{
    public static bool HasValidIdentity(CampaignExploration exploration)
    {
        if (exploration.Battles.IsDefault || exploration.NextWildBattleId < 1)
            return false;
        long previous = 0;
        foreach (var battle in exploration.Battles)
        {
            if (battle is null || battle.Id is null || !battle.Id.StartsWith("wild-battle:", StringComparison.Ordinal) ||
                !long.TryParse(battle.Id.AsSpan(12), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
                battle.Id != "wild-battle:" + id.ToString(CultureInfo.InvariantCulture) ||
                id <= previous || id >= exploration.NextWildBattleId)
                return false;
            previous = id;
        }
        // The latest settlement is retained for callbacks that consume LastOrDefault.
        return exploration.NextWildBattleId == 1 || previous == exploration.NextWildBattleId - 1;
    }

    /// <summary>Only call after validation or an authoritative battle/evolution close.</summary>
    public static CampaignState Compact(CampaignState state)
    {
        // Rewards, capture delivery, evolution and callback consumers still own the active receipt.
        if (state.WildBattle is not null || state.SafariBattle is not null || state.Evolution is not null ||
            state.Exploration is not { } exploration || exploration.Battles.Length < 2)
            return state;
        var referenced = (state.EvolutionHistory ?? []).Where(e => e.BattleId is not null)
            .Select(e => e.BattleId!).ToHashSet(StringComparer.Ordinal);
        foreach (var capture in exploration.Captured ?? []) referenced.Add(capture.BattleId);
        var retained = ImmutableArray.CreateBuilder<CampaignWildHistory>();
        var rockSmashSeen = new HashSet<int>();
        var seafoamSeen = new HashSet<int>();
        var powerPlantSeen = new HashSet<int>();
        var sevenIslandSeen = new HashSet<int>();
        for (var i = 0; i < exploration.Battles.Length; i++)
        {
            var battle = exploration.Battles[i];
            // Evaluate first-sighting membership even when another reference keeps this row.
            // Unown uses its first sighting unless a (separately retained) capture supersedes it.
            bool firstSighting = false;
            if (battle.RockSmashReceipt is not null) firstSighting |= rockSmashSeen.Add(battle.SourceSpeciesId);
            if (battle.SeafoamReceipt is not null) firstSighting |= seafoamSeen.Add(battle.SourceSpeciesId);
            if (battle.PowerPlantReceipt is not null) firstSighting |= powerPlantSeen.Add(battle.SourceSpeciesId);
            if (battle.SevenIsland is not null) firstSighting |= sevenIslandSeen.Add(battle.SourceSpeciesId);
            if (i == exploration.Battles.Length - 1 || battle.Outcome == WildBattleOutcome.Caught ||
                referenced.Contains(battle.Id) || firstSighting || HasSpecialConsumer(battle))
                retained.Add(battle);
        }
        var removed = exploration.Battles.Length - retained.Count;
        return removed == 0 ? state : state with
        {
            Exploration = exploration with
            {
                Battles = retained.ToImmutable()
            }
        };
    }

    private static bool HasSpecialConsumer(CampaignWildHistory b) =>
        b.EventIslandReceipt is not null || b.CelioRoamer is not null ||
        b.CeruleanCaveOrigin?.Kind == CeruleanCaveEncounterKind.Mewtwo || b.MewtwoReceipt is not null ||
        b.SeafoamReceipt?.Origin.Kind == SeafoamEncounterKind.Articuno ||
        b.PowerPlantReceipt is { Origin.Kind: not PowerPlantEncounterKind.Land } ||
        b.ScriptedMarowak || b.ScriptedSnorlaxRoute != 0 || b.LostelleHypno is not null;

}

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideWithWildHistoryRetention(CampaignState state, CampaignCommand command)
    {
        var result = Decide(state, command);
        if (result.Status != CommandStatus.Accepted) return result;
        var next = result.State;
        if ((state.WildBattle is not null && next.WildBattle is null) ||
            (state.SafariBattle is not null && next.SafariBattle is null) ||
            (state.Evolution is not null && next.Evolution is null))
            return result with { State = CampaignWildHistoryRetention.Compact(next) };
        return result;
    }
}

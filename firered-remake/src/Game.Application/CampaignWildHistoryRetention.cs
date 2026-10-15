using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>
/// Retains business-referenced settlements, not a chronological audit log.
/// Special encounters and the roamer retain their current settlement, not a lifecycle ledger.
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
        // Each event's latest outcome owns its flags/retry eligibility. Older captures and
        // evolution-producing wins already have independent references above.
        var latestEvents = new Dictionary<(string Family, int Variant), string>();
        string? hiddenMewtwoEscape = null;
        string? marowakVictory = null;
        foreach (var battle in exploration.Battles)
        {
            if (EventKey(battle) is { } key) latestEvents[key] = battle.Id;
            if (battle.MewtwoReceipt is not null && battle.Outcome == WildBattleOutcome.Escaped)
                hiddenMewtwoEscape = battle.Id;
            if (battle.ScriptedMarowak && battle.Outcome == WildBattleOutcome.Won)
                marowakVictory = battle.Id;
        }
        foreach (var id in latestEvents.Values) referenced.Add(id);
        // The accepted Mewtwo hide contract uses Any(Escaped), unlike Articuno's latest
        // outcome contract. Preserve that one hide reason while it is still relevant.
        if (hiddenMewtwoEscape is not null && state.Script is { } script &&
            script.Flags.GetValueOrDefault("FLAG_HIDE_MEWTWO") && !script.Flags.GetValueOrDefault("FLAG_FOUGHT_MEWTWO"))
            referenced.Add(hiddenMewtwoEscape);
        if (marowakVictory is not null) referenced.Add(marowakVictory);
        var retained = ImmutableArray.CreateBuilder<CampaignWildHistory>();
        for (var i = 0; i < exploration.Battles.Length; i++)
        {
            var battle = exploration.Battles[i];
            if (i == exploration.Battles.Length - 1 || referenced.Contains(battle.Id))
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

    private static (string Family, int Variant)? EventKey(CampaignWildHistory b)
    {
        if (b.CelioRoamer is not null) return ("celio-roamer", 0);
        if (b.EventIslandReceipt is { } island) return ("event-island", (int)island.Origin.Kind);
        if (b.CeruleanCaveOrigin?.Kind == CeruleanCaveEncounterKind.Mewtwo || b.MewtwoReceipt is not null)
            return ("mewtwo", 0);
        if (b.SeafoamReceipt?.Origin.Kind == SeafoamEncounterKind.Articuno) return ("articuno", 0);
        if (b.PowerPlantReceipt is { Origin.Kind: not PowerPlantEncounterKind.Land } plant)
            return ("power-plant", (int)plant.Origin.Kind);
        if (b.ScriptedMarowak) return ("marowak", 0);
        if (b.ScriptedSnorlaxRoute != 0) return ("snorlax", b.ScriptedSnorlaxRoute);
        if (b.LostelleHypno is not null) return ("lostelle", 0);
        return null;
    }

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

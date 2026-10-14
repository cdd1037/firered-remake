using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Application;

/// <summary>
/// One completed original NPC exchange. Incoming is the exact creation snapshot;
/// Outgoing is the final owned snapshot, including the held item that left with it.
/// Fresh incoming IDs order acquisitions and retirement without reusing an identity.
/// </summary>
public sealed record CampaignNpcTradeReceipt(int TradeId, int PartyIndex, PartyPokemon Outgoing,
    PartyPokemon Incoming, RngState RngBefore, RngState RngAfter)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceMailMessage? OutgoingMail { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceMailMessage? IncomingMail { get; init; }
}

public sealed partial class CampaignSession
{
    // History resolution is deliberately separate from current gameplay ownership.
    // ValidateStorageState checks released snapshots before acquisition/origin validation uses history.
    private ImmutableArray<PartyPokemon> HistoricalPokemon(CampaignState state) =>
        OwnedPokemon(state).AddRange((state.NpcTrades ?? []).Select(t => t.Outgoing)).AddRange(state.ReleasedPokemon ?? []);

    private static int NpcTradeCount(CampaignState state) => (state.NpcTrades ?? []).Length;
    private static IEnumerable<int> NpcTradeSourceSpecies(CampaignState state) =>
        (state.NpcTrades ?? []).Select(t => t.Incoming.Pokemon.SourceSpeciesId);

    private bool HasSupportedCampaignOriginalTrainer(CampaignState state, PartyPokemon owned)
    {
        if (owned.Pokemon.NpcTradeOrigin is null)
            return owned.Pokemon.OriginalTrainerId == state.TrainerId &&
                owned.OriginalTrainerName == state.PlayerName && owned.OriginalTrainerGender == state.PlayerGender;
        if (_content.NpcTrades is null || state.NpcTrades is not { IsDefault: false } receipts) return false;
        var matches = receipts.Where(t => t is not null && t.Incoming is not null &&
            t.Incoming.InstanceId == owned.InstanceId).ToArray();
        return matches.Length == 1 && NpcTradeIdentityMatches(state, owned, matches[0].Incoming);
    }

    private bool NpcTradeIdentityMatches(CampaignState state, PartyPokemon current, PartyPokemon initial) =>
        current.Pokemon.NpcTradeOrigin == initial.Pokemon.NpcTradeOrigin &&
        NpcTradePokemonOriginRules.ValidateParty(_content.PokemonContent!.Catalog, current) is null &&
        current.InstanceId == initial.InstanceId && current.Pokemon.Level >= initial.Pokemon.Level &&
        current.ModernFatefulEncounter == initial.ModernFatefulEncounter &&
        EvolutionSpeciesMatchesOrigin(state, current, initial.Pokemon.SourceSpeciesId);

    private string? ValidateNpcTradeHistory(CampaignState state)
    {
        if (!_content.IsNpcTradeStablePosition(state.World.Player.MapId,state.World.Player.X,state.World.Player.Y))
            return "save_npc_trade_access_boundary";
        var receipts = state.NpcTrades ?? [];
        if (receipts.IsDefault || receipts.Any(t => t is null) ||
            state.EvolutionHistory is { IsDefault: true })
            return "save_npc_trade_history_invalid";
        var owned = OwnedPokemon(state);
        var trades = _content.NpcTrades;
        if (trades is null)
            return state.NpcTrades is not null || owned.Any(p => p.Pokemon.NpcTradeOrigin is not null)
                ? "save_npc_trade_profile_missing" : null;
        if (receipts.Length > trades.Definitions.Keys.Count(trades.IsEnabled))
            return "save_npc_trade_history_invalid";
        foreach (var definition in trades.Definitions.Values)
            if (state.Script.Flags.GetValueOrDefault(definition.CompletionFlag) !=
                receipts.Any(t => t.TradeId == definition.TradeId)) return "save_npc_trade_flag_inconsistent";

        var retired = new HashSet<long>();
        var incoming = new HashSet<long>();
        var offers = new HashSet<int>();
        long lastIncoming = 1;
        ulong lastCursor = 0;
        foreach (var receipt in receipts)
        {
            if (!trades.IsEnabled(receipt.TradeId) || !trades.Definitions.TryGetValue(receipt.TradeId, out var definition) ||
                !offers.Add(receipt.TradeId) || receipt.PartyIndex is < 0 or > 5 ||
                receipt.Outgoing is not { Pokemon: { } outgoing } || receipt.Incoming is not { Pokemon: { } })
                return "save_npc_trade_receipt_invalid";
            if (PokemonStateValidator.ValidateParty(_content.PokemonContent, receipt.Outgoing) is not null ||
                PokemonStateValidator.ValidateParty(_content.PokemonContent, receipt.Incoming) is not null ||
                receipt.Outgoing.IsEgg || outgoing.Species != definition.RequestedSpecies ||
                receipt.Outgoing.InstanceId >= receipt.Incoming.InstanceId || receipt.Incoming.InstanceId <= lastIncoming ||
                receipt.Incoming.InstanceId >= state.NextPokemonId || !retired.Add(receipt.Outgoing.InstanceId) ||
                !incoming.Add(receipt.Incoming.InstanceId) || owned.Any(p => p.InstanceId == receipt.Outgoing.InstanceId) ||
                !OwnedHeldItemSupported(state, receipt.Outgoing)) return "save_npc_trade_retirement_invalid";
            if (ValidateNpcTradeMailReceipt(state, receipt, definition) is {} mailError) return mailError;
            if (receipt.RngBefore.Cursor < lastCursor || receipt.RngAfter.Cursor > state.World.Rng.Cursor)
                return "save_npc_trade_rng_invalid";
            try
            {
                var expected = NpcTradePokemonFactory.Create(trades, _content.PokemonContent!, receipt.TradeId,
                    outgoing.Level, receipt.Incoming.InstanceId, receipt.RngBefore);
                if (expected.Rng != receipt.RngAfter || CanonicalJson.Hash(expected.Pokemon) != CanonicalJson.Hash(receipt.Incoming))
                    return "save_npc_trade_creation_invalid";
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or
                NotSupportedException or OverflowException)
            { return "save_npc_trade_creation_invalid"; }
            lastIncoming = receipt.Incoming.InstanceId;
            lastCursor = receipt.RngAfter.Cursor;
        }

        var history = HistoricalPokemon(state);
        if (history.Select(p => p.InstanceId).Distinct().Count() != history.Length)
            return "save_npc_trade_ownership_duplicate";
        // Every ID is acquired exactly once and remains either live or retired exactly once.
        var acquisitions = new HashSet<long>();
        if (!history.IsEmpty) acquisitions.Add(1);
        var captures = state.Exploration?.Captured ?? [];
        var fossils = state.FossilRevivals ?? [];
        if (captures.IsDefault || fossils.IsDefault || captures.Any(c => c is null) || fossils.Any(f => f is null))
            return "save_npc_trade_acquisition_invalid";
        foreach (var capture in captures)
            if (!acquisitions.Add(capture.InstanceId)) return "save_npc_trade_acquisition_duplicate";
        var gifts = new long?[] { state.MagikarpGift?.InstanceId, state.EeveeGift?.InstanceId,
            state.LaprasGift?.InstanceId, state.DojoGift?.InstanceId };
        foreach (var id in gifts.OfType<long>().Concat(fossils.Select(f => f.InstanceId)).Concat((state.PrizePokemon ?? []).Select(p => p.InstanceId)).Concat(incoming).Concat(EggAcquisitionIds(state)))
            if (!acquisitions.Add(id)) return "save_npc_trade_acquisition_duplicate";
        if (state.NextPokemonId != acquisitions.Count + 1L || acquisitions.Any(id => id < 1 || id >= state.NextPokemonId) ||
            !acquisitions.SetEquals(history.Select(p => p.InstanceId))) return "save_npc_trade_acquisition_missing";
        foreach (var pokemon in history)
        {
            if (!HasSupportedCampaignOriginalTrainer(state, pokemon)) return "save_npc_trade_origin_missing";
            // Retirement does not relax the campaign metadata contract previously applied to
            // every living party/box entry. General Domain syntax alone admits other games/languages.
            if (pokemon.Language != (pokemon.IsEgg ? 1 : 2) || pokemon.MetGame != 4 ||
                ((!_content.CaptureEnabled || pokemon.InstanceId == 1) &&
                    (pokemon.MetLocation != "MAPSEC_PALLET_TOWN" || pokemon.MetLevel != 5)) ||
                pokemon.Ball != "ITEM_POKE_BALL" && !(pokemon.InstanceId != 1 &&
                    (AdditionalCaptureBallSupported(pokemon.Ball) || pokemon.Ball == "ITEM_SAFARI_BALL" && _content.SafariEnabled)) ||
                !OwnedHeldItemSupported(state, pokemon)) return "save_npc_trade_origin_invalid";
            if (incoming.Contains(pokemon.InstanceId) != (pokemon.Pokemon.NpcTradeOrigin is not null))
                return "save_npc_trade_origin_mismatch";
        }
        foreach (var receipt in receipts)
        {
            var current = history.Single(p => p.InstanceId == receipt.Incoming.InstanceId);
            if (!NpcTradeIdentityMatches(state, current, receipt.Incoming) ||
                !state.CaughtSpecies.Contains(receipt.Incoming.Pokemon.SourceSpeciesId) ||
                !state.SeenSpecies.Contains(receipt.Incoming.Pokemon.SourceSpeciesId)) return "save_npc_trade_received_identity";
        }
        return null;
    }
}

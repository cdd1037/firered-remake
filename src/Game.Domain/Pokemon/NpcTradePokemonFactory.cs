using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Pokemon;

public sealed record NpcTradePokemonCreationResult(PartyPokemon Pokemon, RngState Rng, ImmutableArray<RandomTrace> Trace);

/// <summary>Source CreateInGameTradePokemonInternal plus the incoming TradeMons friendship reset.
/// It prepares an immutable result; Application owns selection, exchange, receipts and one-time flags.</summary>
public static class NpcTradePokemonFactory
{
    public static NpcTradePokemonCreationResult Create(NpcTradePokemonCatalog trades,
        CampaignPokemonContent content, int tradeId, int offeredLevel, long instanceId, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(content);
        if (!trades.Definitions.OrderBy(p => p.Key).SequenceEqual(content.Catalog.NpcTrades.OrderBy(p => p.Key)))
            throw new ArgumentException("npc_trade_pokemon_catalog", nameof(content));
        if (!trades.IsEnabled(tradeId) || !trades.Definitions.TryGetValue(tradeId, out var definition))
            throw new NotSupportedException("npc_trade_not_enabled");
        if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
        if (offeredLevel is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(offeredLevel));
        if (definition.Mail is not (0 or 255) ||
            SourceMailRules.IsMailItem(definition.HeldItem) != (definition.Mail != 255))
            throw new NotSupportedException("npc_trade_mail_definition");
        if (PokemonStateValidator.ValidatePartyCollection(content, [], 1) is { } contentError)
            throw new ArgumentException(contentError, nameof(content));
        if (!content.Catalog.Species.TryGetValue(definition.ReceivedSpecies, out var species) ||
            !content.Catalog.Species.ContainsKey(definition.RequestedSpecies))
            throw new NotSupportedException("npc_trade_species_not_owned");

        // CreateMon still draws both random-IV words with fixed PID/preset OT. The source then
        // overwrites all six IVs. Keep that real shared call and its two draw purposes intact.
        var created = PokemonFactory.CreateCore(content.Catalog,
            new(definition.ReceivedSpecies, offeredLevel, definition.OriginalTrainerId,
                IvPolicy.Random, PersonalityPolicy.Fixed(definition.Personality)), rng, content.Catalog.MechanicsData);
        var stats = PokemonStatCalculation.Calculate(species, offeredLevel, created.Pokemon.Nature,
            definition.Ivs, SixStats.Zero, content.Catalog.MechanicsData);
        var pokemon = created.Pokemon with
        {
            Ivs = definition.Ivs, Stats = stats, CurrentHp = stats.MaxHp,
            AbilitySlot = definition.AbilitySlot,
            AbilityId = definition.AbilitySlot == 0 ? species.Ability1 : species.Ability2,
            Friendship = 70,
            NpcTradeOrigin = new(tradeId, offeredLevel),
            ContestConditions = definition.ContestConditions
        };
        var owned = new PartyPokemon(instanceId, pokemon, definition.Nickname, definition.OriginalTrainerName,
            definition.OriginalTrainerGender, 2, NpcTradePokemonOriginRules.MetLocation, offeredLevel, 4,
            "ITEM_POKE_BALL", definition.HeldItem) { Mail = definition.Mail };
        if (PokemonStateValidator.ValidateParty(content, owned) is { } error)
            throw new InvalidOperationException(error);
        return new(owned, created.Rng, created.Trace);
    }
}

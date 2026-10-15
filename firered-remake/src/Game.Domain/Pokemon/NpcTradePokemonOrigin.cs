using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Source order: Cool, Beauty, Cute, Smart, Tough, followed by sheen.</summary>
public sealed record PokemonContestConditions(int Cool, int Beauty, int Cute, int Smart, int Tough, int Sheen);

/// <summary>Immutable constructor provenance. Campaign ownership still requires its acquisition
/// receipt; possessing or deserializing this value alone does not prove ownership.</summary>
public sealed record NpcTradePokemonOrigin(int TradeId, int ReceivedLevel);

/// <summary>Authenticates fixed trade identities against the actual source/owned catalog.
/// Mutable battle state, moves, friendship, held items and nicknames are deliberately separate.</summary>
public static class NpcTradePokemonOriginRules
{
    public const string MetLocation = "METLOC_IN_GAME_TRADE";
    public const int SourceMetLocation = 0xFE;

    /// <summary>Validates source origin and immutable ownership fields, including later genuine
    /// source evolutions. Use PokemonStateValidator for the remaining party invariants.</summary>
    public static string? ValidateParty(PokemonCatalog catalog, PartyPokemon owned)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(owned);
        if (owned.Pokemon is null) return "pokemon_missing";
        if (ValidateInstance(catalog, owned.Pokemon, out _) is { } error) return error;
        if (owned.Pokemon.NpcTradeOrigin is not { } origin)
            return owned.MetLocation == MetLocation ? "party_npc_trade_origin_missing" : null;
        var definition = catalog.NpcTrades[origin.TradeId];
        if (owned.OriginalTrainerName != definition.OriginalTrainerName ||
            owned.OriginalTrainerGender != definition.OriginalTrainerGender)
            return "party_npc_trade_original_trainer";
        if (owned.Language != 2 || owned.MetGame != 4 || owned.MetLocation != MetLocation ||
            owned.MetLevel != origin.ReceivedLevel || owned.Ball != "ITEM_POKE_BALL" || owned.ModernFatefulEncounter)
            return "party_npc_trade_met_data";
        return null;
    }

    internal static string? ValidateInstance(PokemonCatalog catalog, PokemonInstance pokemon, out int? abilitySlot)
    {
        abilitySlot = null;
        if (pokemon.NpcTradeOrigin is not { } origin) return null;
        var trades = catalog.NpcTrades;
        if (catalog.MechanicsData is null || origin.TradeId is < 0 or > 8 || !trades.TryGetValue(origin.TradeId, out var definition))
            return "pokemon_npc_trade_origin";
        if (origin.ReceivedLevel is < 1 or > 100 || origin.ReceivedLevel > pokemon.Level)
            return "pokemon_npc_trade_received_level";
        if (pokemon.Personality != definition.Personality || pokemon.OriginalTrainerId != definition.OriginalTrainerId ||
            pokemon.Ivs != definition.Ivs || pokemon.ContestConditions != definition.ContestConditions)
            return "pokemon_npc_trade_identity";
        if (!IsReachableSpecies(catalog.MechanicsData!, definition.ReceivedSpecies,
                origin.ReceivedLevel, pokemon.Species, pokemon.Level))
            return "pokemon_npc_trade_species";
        if (!catalog.Species.TryGetValue(pokemon.Species, out var species) ||
            pokemon.SourceSpeciesId != species.SourceSpeciesId)
            return "pokemon_npc_trade_species";
        if (pokemon.AbilitySlot != definition.AbilitySlot ||
            pokemon.AbilityId != (definition.AbilitySlot == 0 ? species.Ability1 : species.Ability2))
            return "pokemon_npc_trade_ability";
        abilitySlot = definition.AbilitySlot;
        return null;
    }

    internal static bool MatchesRewardMetadata(PokemonCatalog catalog, PokemonInstance pokemon,
        string originalTrainerName, string metLocation, string ball)
    {
        if (pokemon.NpcTradeOrigin is not { } origin) return metLocation != MetLocation;
        return ValidateInstance(catalog, pokemon, out _) is null &&
            originalTrainerName == catalog.NpcTrades[origin.TradeId].OriginalTrainerName &&
            metLocation == MetLocation && ball == "ITEM_POKE_BALL";
    }

    // This only admits possible ancestry; Application proves the actual chronological evolution
    // receipts. A level evolution requires a gained level even when received above its threshold.
    private static bool IsReachableSpecies(PokemonMechanicsData mechanics, SpeciesId receivedSpecies,
        int receivedLevel, SpeciesId currentSpecies, int currentLevel)
    {
        var earliest = new Dictionary<SpeciesId, int> { [receivedSpecies] = receivedLevel };
        var queue = new Queue<SpeciesId>();
        queue.Enqueue(receivedSpecies);
        while (queue.TryDequeue(out var species))
        {
            int level = earliest[species];
            if (species == currentSpecies) return true;
            if (!mechanics.SpeciesMechanics.TryGetValue(species, out var definition)) return false;
            foreach (var evolution in definition.SourceEvolutions)
            {
                int minimum = evolution.Method switch
                {
                    "EVO_LEVEL" => Math.Max(level + 1, evolution.ParameterValue),
                    "EVO_ITEM" => level,
                    _ => 101
                };
                if (minimum > currentLevel || earliest.TryGetValue(evolution.Target, out int previous) && previous <= minimum)
                    continue;
                earliest[evolution.Target] = minimum;
                queue.Enqueue(evolution.Target);
            }
        }
        return false;
    }
}

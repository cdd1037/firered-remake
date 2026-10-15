using System.Collections.Immutable;
using Game.Domain.Growth;

namespace Game.Domain.Pokemon;

/// <summary>Pinned Route 5 single-slot rules. The caller owns the map/script/menu, money
/// transaction, global state commit and selection cancellation. No RNG, evolution or IO.</summary>
public static class Route5DayCareRules
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-route5-daycare-v1";

    /// <summary>Call only for original ShouldEggHatch steps: after source walking gates, excluding
    /// forced movement and a step preempted by Vs Seeker or poison. Merely facing, menu frames,
    /// failed moves and Fly/warp commands do not count. Source u32 wrapping is intentional here.</summary>
    public static Route5DayCareState AdvanceEligibleSteps(Route5DayCareState state, uint steps = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Resident is not { } resident || steps == 0 ? state :
            state with { Resident = resident with { Steps = unchecked(resident.Steps + steps) } };
    }

    /// <summary>Save/transaction validation includes resident uniqueness against party and all
    /// PC boxes, and optional allocator bounds. Mail is removed from attached slots while stored.</summary>
    public static string? Validate(CampaignPokemonContent content, GamePokemonCatalog ownership,
        Route5DayCareState? state, ImmutableArray<PartyPokemon> party, PokemonStorageState storage,
        SourceMailState mail, long? nextInstanceId = null)
    {
        if (!HasOwnership(content, ownership)) return "daycare_owned_catalog_identity";
        if (PokemonStorageValidator.Validate(content, party, storage, nextInstanceId) is { } storageError)
            return "daycare_" + storageError.Code;
        if (SourceMailRules.Validate(mail, party, storage) is { } mailError) return mailError;
        if (state is null) return "daycare_state_missing";
        if (state.Resident is not { } resident) return null;
        if (ValidateResident(content, resident) is { } error) return error;
        long id = resident.Pokemon.InstanceId;
        if (party.Any(p => p.InstanceId == id) || storage.Boxes.Any(b => b.Slots.Any(p => p?.InstanceId == id)))
            return "daycare_duplicate_instance_id";
        if (nextInstanceId.HasValue && id >= nextInstanceId.Value) return "daycare_next_instance_id";
        return null;
    }

    public static Route5DayCareResult Deposit(CampaignPokemonContent content, GamePokemonCatalog ownership,
        Route5DayCareState state, ImmutableArray<PartyPokemon> party, PokemonStorageState storage,
        SourceMailState mail, int partyIndex)
    {
        Route5DayCareResult No(string error) => new(state, party, mail, false, error, LearnedMoves: []);
        // Eggs can be displayed in a later party implementation but never selected for storage.
        if (!party.IsDefault && partyIndex >= 0 && partyIndex < party.Length && party[partyIndex].IsEgg)
            return No("daycare_egg");
        if (Validate(content, ownership, state, party, storage, mail) is { } error) return No(error);
        if (state.Resident is not null) return No("daycare_occupied");
        if (party.Count(p => !p.IsEgg) <= 1) return No("daycare_only_one_non_egg");
        if (partyIndex < 0 || partyIndex >= party.Length) return No("daycare_party_index");
        if (!party.Where((_, i) => i != partyIndex).Any(p => !p.IsEgg && p.Pokemon.CurrentHp > 0))
            return No("daycare_last_usable");

        var outgoing = SourceMailRules.RemoveOutgoing(mail, party, partyIndex);
        if (!outgoing.Succeeded) return No(outgoing.Error!);
        var pokemon = party[partyIndex];
        if (outgoing.OutgoingMail is not null)
            pokemon = pokemon with { Mail = SourceMailState.NoMail, HeldItem = "ITEM_NONE" };
        var resident = new SourceDayCareResident(PokemonStorageRules.ToBoxProjection(content.Catalog, pokemon), 0, outgoing.OutgoingMail);
        return new(new(resident), outgoing.Party, outgoing.State, true, null, LearnedMoves: []);
    }

    /// <summary>The displayed level uses total EXP + steps even before withdrawal. The original
    /// base fee is charged with zero growth and at level 100. Quote does not teach moves or evolve.</summary>
    public static SourceDayCareQuote Quote(CampaignPokemonContent content, GamePokemonCatalog ownership,
        Route5DayCareState state)
    {
        if (!HasOwnership(content, ownership)) throw new ArgumentException("daycare_owned_catalog_identity");
        if (state?.Resident is not { } resident) throw new ArgumentException("daycare_empty");
        if (ValidateResident(content, resident) is { } error) throw new ArgumentException(error);
        var pokemon = resident.Pokemon.Pokemon;
        // Source wraps the u32 sum after billions of steps, possibly producing level zero or
        // lowering a met-level. Those malformed outputs cannot enter ordinary owned invariants.
        // Preserve the counter, refuse the transaction explicitly, and never silently saturate it.
        if (resident.Steps > uint.MaxValue - pokemon.Experience)
            throw new NotSupportedException("daycare_experience_wrap_unsupported");
        int level = GrowthRules.LevelFromExperience(content.Catalog.Species[pokemon.Species].GrowthRate,
            pokemon.Experience + resident.Steps);
        int gained = level - pokemon.Level;
        return new(resident.Pokemon.InstanceId, pokemon.Level, level, gained, 100 + 100 * gained, resident.Steps);
    }

    /// <summary>Only successful withdrawal returns MoneyPaid. Commit that deduction with Party,
    /// Mail and State atomically. Party capacity is checked before funds, matching the script.</summary>
    public static Route5DayCareResult Withdraw(CampaignPokemonContent content, GamePokemonCatalog ownership,
        Route5DayCareState state, ImmutableArray<PartyPokemon> party, PokemonStorageState storage,
        SourceMailState mail, int money)
    {
        Route5DayCareResult No(string error, SourceDayCareQuote? quote = null) =>
            new(state, party, mail, false, error, quote, LearnedMoves: []);
        if (Validate(content, ownership, state, party, storage, mail) is { } error) return No(error);
        if (state.Resident is not { } resident) return No("daycare_empty");
        if (money is < 0 or > 999999) return No("daycare_money");
        SourceDayCareQuote quote;
        try { quote = Quote(content, ownership, state); }
        catch (NotSupportedException e) { return No(e.Message); }
        if (party.Length >= 6) return No("daycare_party_full", quote);
        if (money < quote.Cost) return No("daycare_insufficient_money", quote);

        var grown = SourceDayCareGrowth.Apply(content, ownership, resident);
        var incoming = grown.Pokemon;
        if (resident.Mail is not null) incoming = incoming with { HeldItem = resident.Mail.ItemSymbol };
        var received = SourceMailRules.ReceivePartyPokemon(mail, party, incoming, resident.Mail);
        if (!received.Succeeded) return No(received.Error!, quote);
        return new(Route5DayCareState.Empty, received.Party, received.State, true, null, quote,
            quote.Cost, grown.LearnedMoves);
    }

    internal static bool HasOwnership(CampaignPokemonContent? content, GamePokemonCatalog? ownership) =>
        content is not null && ownership is not null && ReferenceEquals(content.Catalog, ownership.Catalog) &&
        ReferenceEquals(content.Pokemon, ownership);

    internal static string? ValidateResident(CampaignPokemonContent content, SourceDayCareResident resident)
    {
        if (resident.Pokemon is null) return "daycare_pokemon_missing";
        if (resident.Pokemon.IsEgg) return "daycare_egg";
        if (PokemonStateValidator.ValidateParty(content, resident.Pokemon) is { } error) return error;
        if (SourceMailRules.ValidateBoxedPokemon(resident.Pokemon) is { } mailError) return mailError;
        var p = resident.Pokemon.Pokemon;
        if (resident.Pokemon.Status != 0 || p.CurrentHp != p.Stats.MaxHp || p.StatCalculationEvs is not null ||
            p.Moves.Any(m => m.CurrentPp != m.MaximumPp)) return "daycare_box_projection";
        if (resident.Mail is not null)
        {
            if (SourceMailRules.ValidateMessage(resident.Mail) is { } payloadError) return payloadError;
            if (resident.Pokemon.HeldItem != "ITEM_NONE") return "daycare_mail_held_item";
        }
        return null;
    }
}

/// <summary>Shared source withdrawal growth, separate from Route 5 selection and cost. Four Island
/// can reuse this exact operation later; breeding and eggs are deliberately not implemented here.</summary>
public static class SourceDayCareGrowth
{
    public sealed record Result(PartyPokemon Pokemon, ImmutableArray<SourceDayCareLearnedMove> LearnedMoves);

    public static Result Apply(CampaignPokemonContent content, GamePokemonCatalog ownership,
        SourceDayCareResident resident)
    {
        _ = Route5DayCareRules.Quote(content, ownership, new(resident));
        var before = resident.Pokemon.Pokemon;
        if (before.Level == GrowthRules.MaximumLevel) return new(resident.Pokemon, []);
        var species = content.Catalog.Species[before.Species];
        uint experience = before.Experience + resident.Steps;
        var moves = before.Moves;
        var learned = ImmutableArray.CreateBuilder<SourceDayCareLearnedMove>();
        // TryIncrementMonLevel uses strictly greater-than, unlike the final level calculation.
        // At an exact threshold the final level is gained but its moves are not taught.
        int teachingLevel = before.Level;
        while (teachingLevel < GrowthRules.MaximumLevel &&
            experience > GrowthRules.ExperienceAtLevel(species.GrowthRate, teachingLevel + 1))
        {
            teachingLevel++;
            experience = Math.Min(experience, GrowthRules.ExperienceAtLevel(species.GrowthRate, GrowthRules.MaximumLevel));
            foreach (var move in PokemonMoveRules.AtLevel(species, teachingLevel))
            {
                if (moves.Any(m => m.Move == move)) continue;
                learned.Add(new(teachingLevel, move, moves.Length == 4 ? moves[0].Move : null));
                moves = PokemonMoveRules.LearnAutomatically(content.Catalog, moves, move);
            }
        }
        int level = GrowthRules.LevelFromExperience(species.GrowthRate, experience);
        var stats = PokemonStatCalculation.Calculate(species, level, before.Nature, before.Ivs, before.Evs, content.Catalog.MechanicsData);
        var result = resident.Pokemon with { Pokemon = before with
        {
            Experience = experience, Level = level, Stats = stats, StatCalculationEvs = null,
            CurrentHp = stats.MaxHp, Moves = moves
        } };
        if (PokemonStateValidator.ValidateParty(content, result) is { } error)
            throw new InvalidOperationException("daycare_growth_result_" + error);
        return new(result, learned.ToImmutable());
    }
}

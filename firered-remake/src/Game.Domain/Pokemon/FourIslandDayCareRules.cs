using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Pokemon;

/// <summary>Pinned two-slot service/production rules. Reuses Route 5's resident, deposit,
/// mail, fee and withdrawal growth. Application owns script timing and atomic state commit.</summary>
public static class FourIslandDayCareRules
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-four-island-daycare-v1";
    public const int DittoEggGroup = 13;
    public const int UndiscoveredEggGroup = 15;

    public static int GetScriptState(FourIslandDayCareState state) =>
        state.HasPendingEgg ? 1 : state.Count == 0 ? 0 : state.Count + 1;

    public static string? Validate(CampaignPokemonContent content, GamePokemonCatalog ownership,
        FourIslandDayCareState? state, Route5DayCareState route5, ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, SourceMailState mail, long? nextInstanceId = null)
    {
        if (ValidateShape(state) is { } shapeError) return shapeError;
        if (Route5DayCareRules.Validate(content, ownership, route5, party, storage, mail, nextInstanceId) is { } routeError)
            return routeError;
        foreach (var resident in new[] { state!.First, state.Second })
        {
            if (resident is null) continue;
            if (Route5DayCareRules.Validate(content, ownership, new(resident), party, storage, mail, nextInstanceId) is { } error)
                return error;
            if (route5.Resident?.Pokemon.InstanceId == resident.Pokemon.InstanceId)
                return "daycare_duplicate_instance_id";
        }
        return null;
    }

    public static FourIslandDayCareResult Deposit(CampaignPokemonContent content, GamePokemonCatalog ownership,
        FourIslandDayCareState state, Route5DayCareState route5, ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, SourceMailState mail, int partyIndex)
    {
        FourIslandDayCareResult No(string error) => new(state, party, mail, false, error, LearnedMoves: []);
        if (Validate(content, ownership, state, route5, party, storage, mail) is { } error) return No(error);
        // The original woman's EggWaiting branch offers no deposit/withdrawal actions.
        if (state.HasPendingEgg) return No("daycare_egg_pending");
        if (state.Second is not null) return No("daycare_occupied");
        var result = Route5DayCareRules.Deposit(content, ownership, Route5DayCareState.Empty,
            party, storage, mail, partyIndex);
        if (!result.Succeeded) return No(result.Error!);
        var updated = state.First is null ? state with { First = result.State.Resident } :
            state with { Second = result.State.Resident };
        return new(updated, result.Party, result.Mail, true, null, LearnedMoves: result.LearnedMoves);
    }

    public static SourceDayCareQuote Quote(CampaignPokemonContent content, GamePokemonCatalog ownership,
        FourIslandDayCareState state, int slot)
    {
        if (ValidateShape(state) is { } error) throw new ArgumentException(error);
        return Route5DayCareRules.Quote(content, ownership, new(Select(state, slot)));
    }

    public static FourIslandDayCareResult Withdraw(CampaignPokemonContent content, GamePokemonCatalog ownership,
        FourIslandDayCareState state, Route5DayCareState route5, ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, SourceMailState mail, int slot, int money)
    {
        FourIslandDayCareResult No(string error, SourceDayCareQuote? quote = null) =>
            new(state, party, mail, false, error, quote, LearnedMoves: []);
        if (Validate(content, ownership, state, route5, party, storage, mail) is { } error) return No(error);
        if (state.HasPendingEgg) return No("daycare_egg_pending");
        if (slot is < 0 or > 1) return No("daycare_slot");
        var selected = Select(state, slot);
        if (selected is null) return No("daycare_empty");
        var result = Route5DayCareRules.Withdraw(content, ownership, new(selected), party, storage, mail, money);
        if (!result.Succeeded) return No(result.Error!, result.Quote);
        // Shift the complete second record, including its independent steps and detached mail.
        var updated = slot == 0 ? state with { First = state.Second, Second = null } : state with { Second = null };
        return new(updated, result.Party, result.Mail, true, null, result.Quote, result.MoneyPaid, result.LearnedMoves);
    }

    /// <summary>Full 32-bit OT identity. Only the first egg group is tested for Ditto and
    /// Undiscovered, as in source; ordinary overlap compares both groups of each parent.</summary>
    public static int Compatibility(FourIslandDayCareState state,
        IReadOnlyDictionary<SpeciesId, SourceBreedingSpecies> species)
    {
        if (ValidateShape(state) is { } error) throw new ArgumentException(error);
        ArgumentNullException.ThrowIfNull(species);
        if (state.First is null || state.Second is null) return 0;
        var a = state.First.Pokemon.Pokemon;
        var b = state.Second.Pokemon.Pokemon;
        var ga = GetSpecies(species, a.Species);
        var gb = GetSpecies(species, b.Species);
        if (ga.EggGroup1 == UndiscoveredEggGroup || gb.EggGroup1 == UndiscoveredEggGroup) return 0;
        if (ga.EggGroup1 == DittoEggGroup && gb.EggGroup1 == DittoEggGroup) return 0;
        bool sameTrainer = a.OriginalTrainerId == b.OriginalTrainerId;
        if (ga.EggGroup1 == DittoEggGroup || gb.EggGroup1 == DittoEggGroup) return sameTrainer ? 20 : 50;
        if (a.Gender == b.Gender || a.Gender == PokemonGender.Genderless || b.Gender == PokemonGender.Genderless) return 0;
        if (ga.EggGroup1 != gb.EggGroup1 && ga.EggGroup1 != gb.EggGroup2 &&
            ga.EggGroup2 != gb.EggGroup1 && ga.EggGroup2 != gb.EggGroup2) return 0;
        return a.Species == b.Species ? sameTrainer ? 50 : 70 : sameTrainer ? 20 : 50;
    }

    /// <summary>Call once per source-eligible walking step, in sequence with the same global
    /// RNG as other rules. No batching: RNG and party hatching can interrupt intervening steps.</summary>
    public static FourIslandDayCareStepResult AdvanceEligibleStep(FourIslandDayCareState state,
        IReadOnlyDictionary<SpeciesId, SourceBreedingSpecies> species, RngState rng)
    {
        if (ValidateShape(state) is { } error) throw new ArgumentException(error);
        ArgumentNullException.ThrowIfNull(species);
        var updated = state with
        {
            First = Route5DayCareRules.AdvanceEligibleSteps(new(state.First)).Resident,
            Second = Route5DayCareRules.AdvanceEligibleSteps(new(state.Second)).Resident,
            HatchStepCounter = unchecked((byte)(state.HatchStepCounter + 1))
        };
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int? compatibility = null, roll = null;
        bool produced = false;
        if (!updated.HasPendingEgg && updated.First is not null && updated.Second is { } second &&
            (second.Steps & 0xff) == 0xff)
        {
            compatibility = Compatibility(updated, species);
            // Even incompatible pairs consume this draw. This is not Random() % 100.
            var chance = Gen3Rng.Next(rng, "daycare.egg.compatibility");
            rng = chance.State;
            trace.Add(chance.Trace);
            roll = (int)((uint)chance.Value * 100 / ushort.MaxValue);
            if (compatibility > roll)
            {
                var personality = Gen3Rng.Next(rng, "daycare.egg.personality-low", 0xfffe);
                rng = personality.State;
                trace.Add(personality.Trace);
                updated = updated with { OffspringPersonality = (ushort)(personality.Value % 0xfffe + 1) };
                produced = true;
            }
        }
        return new(updated, rng, trace.ToImmutable(), produced, updated.HatchStepCounter == 255, compatibility, roll);
    }

    /// <summary>Original RemoveEggFromDayCare: use after successful receipt, or the second
    /// confirmed No. The caller clears FLAG_PENDING_DAYCARE_EGG atomically. Resident steps persist.</summary>
    public static FourIslandDayCareState ClearPendingEgg(FourIslandDayCareState state) =>
        state with { OffspringPersonality = 0, HatchStepCounter = 0 };

    private static SourceDayCareResident? Select(FourIslandDayCareState state, int slot) => slot switch
    {
        0 => state.First, 1 => state.Second, _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static string? ValidateShape(FourIslandDayCareState? state)
    {
        if (state is null) return "four_island_daycare_state_missing";
        if (state.First is null && state.Second is not null) return "four_island_daycare_slot_gap";
        if (state.HasPendingEgg && state.Count != 2) return "four_island_daycare_pending_parents";
        if ((state.First is not null && state.First.Pokemon is null) ||
            (state.Second is not null && state.Second.Pokemon is null)) return "daycare_pokemon_missing";
        if (state.First is not null && state.Second is not null &&
            state.First.Pokemon.InstanceId == state.Second.Pokemon.InstanceId) return "daycare_duplicate_instance_id";
        return null;
    }

    private static SourceBreedingSpecies GetSpecies(IReadOnlyDictionary<SpeciesId, SourceBreedingSpecies> species, SpeciesId id)
    {
        if (!species.TryGetValue(id, out var data) || data is null || data.Species != id ||
            data.EggGroup1 is < 1 or > 15 || data.EggGroup2 is < 1 or > 15)
            throw new ArgumentException("daycare_breeding_species");
        return data;
    }
}

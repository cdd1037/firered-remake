using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Pokemon;

/// <summary>The three ordinary save variables. RequestedSpecies uses source IDs, not National
/// Dex numbers: zero is no request and 0xffff is expired. Reward and steps remain after delivery.</summary>
public sealed record SelphyRequestState(ushort RequestedSpecies, ushort StepCounter, string RewardItemSymbol)
{
    public static SelphyRequestState Empty { get; } = new(0, 0, "ITEM_NONE");
}

/// <summary>FLAG_TEMP_2 belongs to the current map visit. Preserve it on an in-place save/restore
/// or battle return; clear only when the source calls ClearTempFieldEventData on map transition.</summary>
public sealed record SelphyRequestVisitState(bool ShownRequestedMon)
{
    public static SelphyRequestVisitState Empty { get; } = new(false);
}

public enum SelphyRequestOutcome
{
    Unavailable, QuestLogIgnored, NewRequest, ExpiredRequestReplaced, RequestRepeated,
    RewardOffered, RewardReceived, BagFull, JustFulfilled
}

/// <summary>RewardOffered is a transaction plan: the caller attempts to add exactly one item,
/// then resolves the offer and commits bag, request, visit and RNG together. No Pokemon is taken.</summary>
public sealed record SelphyRequestDecision(SelphyRequestState State, SelphyRequestVisitState Visit,
    RngState Rng, SelphyRequestOutcome Outcome, string? OfferedItem, ImmutableArray<RandomTrace> Trace);

public sealed record SelphyRequestSample(SelphyRequestState State, RngState Rng,
    ImmutableArray<RandomTrace> Trace);

/// <summary>Original Resort Gorgeous request service, independent of maps, bag capacity and IO.
/// The caller must finish Lost Cave rescue AND Selphy's outdoor return-home scene before making
/// the indoor object visible. These rules never unlock her or create a route to the house.</summary>
public static class SelphyRequestRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourcePath = "src/field_specials.c";
    public const string SourceSha256 = "9c49d3702f0f4dcba61ae26bccefce1c26fbd1247475289dd821780bc537d1d9";
    public const string ScriptPath = "data/maps/FiveIsland_ResortGorgeous_House/scripts.inc";
    public const string ScriptSha256 = "270a088d03b09cef871bbf99a1ff4d266a54f1f7d77b3d7451f87c53ac679d88";
    public const string HiddenInsideFlag = "FLAG_HIDE_RESORT_GORGEOUS_INSIDE_SELPHY";
    public const string ShownRequestedMonFlag = "FLAG_TEMP_2";
    public const string RequestedSpeciesVariable = "VAR_RESORT_GORGEOUS_REQUESTED_MON";
    public const string StepCounterVariable = "VAR_RESORT_GOREGEOUS_STEP_COUNTER";
    public const string RewardVariable = "VAR_RESORT_GORGEOUS_REWARD";
    public const ushort ExpiredSpecies = ushort.MaxValue;
    public const int LastSampledSpecies = 411; // NUM_SPECIES - 1; includes old Unown slots.
    public const int RandomSpeciesAttempts = 100;
    public const int ExpirySteps = 250;
    public static ImmutableArray<string> DeluxeRewards { get; } =
        ["ITEM_BIG_PEARL", "ITEM_PEARL", "ITEM_STARDUST", "ITEM_STAR_PIECE", "ITEM_NUGGET", "ITEM_RARE_CANDY"];

    /// <summary>One original ProcessPlayerFieldInput tookStep after the trainer/on-frame early
    /// returns and before step scripts/wild encounters. Do not count failed moves, menu frames,
    /// forced movement, or a movement intercepted before this hook. No wall-clock timer exists.</summary>
    public static SelphyRequestState AdvanceEligibleStep(SelphyRequestState state)
    {
        Validate(state);
        if (state.RequestedSpecies == 0) return state;
        ushort steps = unchecked((ushort)(state.StepCounter + 1));
        return steps >= ExpirySteps
            ? state with { RequestedSpecies = ExpiredSpecies, StepCounter = 0 }
            : state with { StepCounter = steps };
    }

    /// <summary>Clear only the temporary flag on an actual map transition. The three ordinary
    /// variables, and any previously forfeited reward, are unaffected.</summary>
    public static SelphyRequestVisitState OnMapTransition() => SelphyRequestVisitState.Empty;

    /// <summary>Equivalent to MON_DATA_SPECIES_OR_EGG. Eggs never match sampled species, while
    /// fainted Pokemon, traded Pokemon and any Unown form do. Boxes and daycare are not inspected.</summary>
    public static bool PartyContainsRequestedSpecies(ImmutableArray<PartyPokemon> party, ushort requestedSpecies)
    {
        if (party.IsDefault || party.Length > 6) throw new ArgumentException("selphy_party", nameof(party));
        if (requestedSpecies is 0 or ExpiredSpecies) return false;
        return party.Any(mon => mon is not null && !mon.IsEgg && mon.Pokemon.SourceSpeciesId == requestedSpecies);
    }

    /// <summary>seenSourceSpecies contains the source IDs for which GetSetPokedexFlag(
    /// SpeciesToNationalPokedexNum(id), FLAG_GET_SEEN) returns true. Use the complete seen set,
    /// never the caught set or the currently owned/implemented species subset. Old Unown slots
    /// 252..276 are still sampled and ordinarily fail this predicate; never compact the range.</summary>
    public static SelphyRequestSample SampleRequest(SelphyRequestState state,
        ImmutableHashSet<int> seenSourceSpecies, RngState rng)
    {
        Validate(state);
        if (state.RequestedSpecies is not (0 or ExpiredSpecies)) return new(state, rng, []);
        ArgumentNullException.ThrowIfNull(seenSourceSpecies);
        if (seenSourceSpecies.Any(id => id is < 1 or > LastSampledSpecies))
            throw new ArgumentException("selphy_seen_source_species", nameof(seenSourceSpecies));
        // The original loops forever with no seen species. Refuse that impossible ordinary-game
        // input explicitly instead of inventing a fallback species or consuming partial RNG.
        if (seenSourceSpecies.IsEmpty) throw new NotSupportedException("selphy_empty_seen_set_would_not_terminate");

        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int species = 0;
        for (int i = 0; i < RandomSpeciesAttempts; i++)
        {
            var draw = Gen3Rng.Next(rng, "selphy:requested-species", LastSampledSpecies);
            rng = draw.State;
            trace.Add(draw.Trace);
            species = draw.Value % LastSampledSpecies + 1;
            if (seenSourceSpecies.Contains(species)) break;
        }
        while (!seenSourceSpecies.Contains(species))
            species = species == 1 ? LastSampledSpecies : species - 1;

        var rewardDraw = Gen3Rng.Next(rng, "selphy:reward-tier", 100);
        rng = rewardDraw.State;
        trace.Add(rewardDraw.Trace);
        string reward = "ITEM_LUXURY_BALL";
        if (rewardDraw.Value % 100 < 30)
        {
            var deluxe = Gen3Rng.Next(rng, "selphy:deluxe-reward", DeluxeRewards.Length);
            rng = deluxe.State;
            trace.Add(deluxe.Trace);
            reward = DeluxeRewards[deluxe.Value % DeluxeRewards.Length];
        }
        return new(new(checked((ushort)species), 0, reward), rng, trace.ToImmutable());
    }

    /// <summary>Source branch order, including the map-temporary completion guard before expiry.
    /// hiddenInsideSelphy is the actual source object flag (initially true), not merely the trainer
    /// defeat flag. A missing party match repeats the same request and consumes no RNG.</summary>
    public static SelphyRequestDecision BeginInteraction(SelphyRequestState state, SelphyRequestVisitState visit,
        bool hiddenInsideSelphy, ImmutableArray<PartyPokemon> party, ImmutableHashSet<int> seenSourceSpecies,
        RngState rng, bool questLogPlayback = false)
    {
        Validate(state);
        ArgumentNullException.ThrowIfNull(visit);
        SelphyRequestDecision No(SelphyRequestOutcome outcome) => new(state, visit, rng, outcome, null, []);
        if (hiddenInsideSelphy) return No(SelphyRequestOutcome.Unavailable);
        if (questLogPlayback) return No(SelphyRequestOutcome.QuestLogIgnored);
        if (visit.ShownRequestedMon) return No(SelphyRequestOutcome.JustFulfilled);
        if (state.RequestedSpecies is 0 or ExpiredSpecies)
        {
            var sample = SampleRequest(state, seenSourceSpecies, rng);
            return new(sample.State, visit, sample.Rng, state.RequestedSpecies == 0
                ? SelphyRequestOutcome.NewRequest : SelphyRequestOutcome.ExpiredRequestReplaced, null, sample.Trace);
        }
        if (!PartyContainsRequestedSpecies(party, state.RequestedSpecies))
            return No(SelphyRequestOutcome.RequestRepeated);
        // The source calls SampleResortGorgeousMonAndReward here, but it is a no-op for an active request.
        return new(state, visit, rng, SelphyRequestOutcome.RewardOffered, state.RewardItemSymbol, []);
    }

    /// <summary>The original full-bag branch also clears the request and sets FLAG_TEMP_2:
    /// there is no pending gift or retry entitlement. Only the bag mutation depends on success.</summary>
    public static SelphyRequestDecision ResolveRewardOffer(SelphyRequestDecision offer, bool rewardAdded)
    {
        ArgumentNullException.ThrowIfNull(offer);
        Validate(offer.State);
        if (offer.Outcome != SelphyRequestOutcome.RewardOffered || offer.Visit.ShownRequestedMon ||
            offer.State.RequestedSpecies is 0 or ExpiredSpecies || offer.OfferedItem != offer.State.RewardItemSymbol)
            throw new ArgumentException("selphy_reward_offer", nameof(offer));
        return offer with
        {
            State = offer.State with { RequestedSpecies = 0 },
            Visit = new(true),
            Outcome = rewardAdded ? SelphyRequestOutcome.RewardReceived : SelphyRequestOutcome.BagFull,
            OfferedItem = null
        };
    }

    public static void Validate(SelphyRequestState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.RequestedSpecies > LastSampledSpecies && state.RequestedSpecies != ExpiredSpecies)
            throw new ArgumentException("selphy_requested_species", nameof(state));
        bool reward = state.RewardItemSymbol == "ITEM_LUXURY_BALL" || DeluxeRewards.Contains(state.RewardItemSymbol);
        if (!reward && !(state.RequestedSpecies == 0 && state.RewardItemSymbol == "ITEM_NONE"))
            throw new ArgumentException("selphy_reward_item", nameof(state));
    }
}

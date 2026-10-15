using System.Collections.Immutable;
using System.Numerics;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>The original packed variable and separate saved hour baseline. Scene
/// is the ordinary source map variable. No wall-clock or step approximation exists.</summary>
public sealed record TrainerFanClubState(ushort PackedCounter, ushort LoseFanHours, ushort Scene)
{
    public static TrainerFanClubState Empty { get; } = new(0, 0, 0);
    public int ActivityCounter => PackedCounter & 0x7f;
    public bool GotInitialFans => (PackedCounter & 0x80) != 0;
    public byte FanFlags => (byte)(PackedCounter >> 8);
}

/// <summary>FirstFansInitialized instructs the caller to clear the four original
/// hide flags in the same transaction. It never claims a historical initialization.</summary>
public sealed record TrainerFanClubClearResult(TrainerFanClubState State, bool FirstFansInitialized);
public sealed record TrainerFanClubRandomResult(TrainerFanClubState State, RngState Rng,
    int SourceMemberResult, ImmutableArray<RandomTrace> Trace);
public sealed record TrainerFanClubUpdate(TrainerFanClubState State, RngState Rng,
    ImmutableArray<RandomTrace> Trace);

/// <summary>Pure original fan-counter rules. Persist state, flags, text buffers and
/// explicit RNG together in the Application owner. Link-battle entry points and
/// populated link-name records are deliberately not admitted by this single-player API.</summary>
public static class TrainerFanClubRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourcePath = "src/trainer_fan_club.c";
    public const string CounterVariable = "VAR_FANCLUB_FAN_COUNTER";
    public const string LoseFanHoursVariable = "VAR_FANCLUB_LOSE_FAN_TIMER";
    public const string SceneVariable = "VAR_MAP_SCENE_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB";
    public static ImmutableArray<string> FirstClearHideFlags { get; } =
        ["FLAG_HIDE_SAFFRON_FAN_CLUB_BLACK_BELT", "FLAG_HIDE_SAFFRON_FAN_CLUB_ROCKER",
         "FLAG_HIDE_SAFFRON_FAN_CLUB_WOMAN", "FLAG_HIDE_SAFFRON_FAN_CLUB_BEAUTY"];
    private static readonly ImmutableArray<int> GainOrder = [1, 3, 5, 0, 7, 6, 4, 2];
    private static readonly ImmutableArray<int> LoseOrder = [5, 6, 3, 7, 4, 1, 0, 2];
    private static readonly ImmutableArray<int> CounterIncrements = [2, 1, 2, 1];

    public static int CountFans(TrainerFanClubState state)
    {
        Validate(state);
        return BitOperations.PopCount((uint)state.FanFlags);
    }

    public static bool IsPlayerFan(TrainerFanClubState state, int member)
    {
        Validate(state); ValidateMember(member);
        return (state.FanFlags & (1 << member)) != 0;
    }

    /// <summary>Source ResetTrainerFanClub clears only the two fan variables. It
    /// does not reset the map scene, which is separately owned by new-game setup.</summary>
    public static TrainerFanClubState Reset(TrainerFanClubState state)
    {
        Validate(state);
        return state with { PackedCounter = 0, LoseFanHours = 0 };
    }

    public static TrainerFanClubClearResult OnGameClear(TrainerFanClubState state, int playTimeHours)
    {
        Validate(state); ValidateHours(playTimeHours);
        if (state.GotInitialFans) return new(state, false);
        return new(state with
        {
            PackedCounter = (ushort)(state.PackedCounter | 0x0780),
            LoseFanHours = (ushort)playTimeHours,
            Scene = 1
        }, true);
    }

    /// <summary>The source has four increment indices, but only index zero is
    /// called by single-player map scripts, after Lance. Callers must authenticate
    /// the source call; ordinary battles are not an extra fan-counter trigger.</summary>
    public static TrainerFanClubUpdate TryGainFromCounter(TrainerFanClubState state, int incrementIndex, RngState rng)
    {
        Validate(state);
        if (incrementIndex is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(incrementIndex));
        if (state.Scene != 2) return new(state, rng, []);
        int nextCounter = state.ActivityCounter + CounterIncrements[incrementIndex];
        if (nextCounter < 20) return new(WithCounter(state, nextCounter), rng, []);
        if (CountFans(state) >= 3) return new(WithCounter(state, 20), rng, []);
        var gain = GainRandomFan(state, rng);
        return new(WithCounter(gain.State, 0), gain.Rng, gain.Trace);
    }

    /// <summary>Do not replace the source's biased scan with a uniform choice.
    /// It draws once for each nonfan in order and retains the last eligible fallback.</summary>
    public static TrainerFanClubRandomResult GainRandomFan(TrainerFanClubState state, RngState rng)
    {
        Validate(state);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int selected = GainOrder[0];
        foreach (int member in GainOrder)
        {
            if (IsPlayerFan(state, member)) continue;
            selected = member;
            var draw = Gen3Rng.Next(rng, "trainer-fan-club:gain-member", 2);
            rng = draw.State; trace.Add(draw.Trace);
            if (draw.Trace.Result != 0) break;
        }
        return new(WithFlags(state, state.FanFlags | (1 << selected)), rng, selected, trace.ToImmutable());
    }

    /// <summary>The exact-one-fan guard returns source value zero even when that
    /// remaining fan is another member. Zero fans returns member six with no change.</summary>
    public static TrainerFanClubRandomResult LoseRandomFan(TrainerFanClubState state, RngState rng)
    {
        Validate(state);
        if (CountFans(state) == 1) return new(state, rng, 0, []);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int selected = LoseOrder[0];
        foreach (int member in LoseOrder)
        {
            if (!IsPlayerFan(state, member)) continue;
            selected = member;
            var draw = Gen3Rng.Next(rng, "trainer-fan-club:lose-member", 2);
            rng = draw.State; trace.Add(draw.Trace);
            if (draw.Trace.Result != 0) break;
        }
        return new(WithFlags(state, state.FanFlags & ~(1 << selected)), rng, selected, trace.ToImmutable());
    }

    /// <summary>The caller supplies actual saved play-time hours, not a wall-clock
    /// timestamp. The original helper has no initialization or scene guard. Hour
    /// subtraction is signed, including when the saved baseline is in the future.</summary>
    public static TrainerFanClubUpdate TryLoseFromPlayTime(TrainerFanClubState state, int playTimeHours, RngState rng)
    {
        Validate(state); ValidateHours(playTimeHours);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        if (playTimeHours == 999) return new(state, rng, []);
        int losses = 0;
        while (true)
        {
            if (CountFans(state) < 5)
            {
                state = state with { LoseFanHours = (ushort)playTimeHours };
                break;
            }
            if (losses == 8 || playTimeHours - state.LoseFanHours < 12) break;
            var loss = LoseRandomFan(state, rng);
            state = loss.State with { LoseFanHours = unchecked((ushort)(loss.State.LoseFanHours + 12)) };
            rng = loss.Rng; trace.AddRange(loss.Trace); losses++;
        }
        return new(state, rng, trace.ToImmutable());
    }

    /// <summary>Only an explicitly empty link history uses this entry. Populated
    /// records require the original seven-byte/control-code naming implementation.</summary>
    public static string BufferTrainerNameWithEmptyLinkHistory(int member, string rivalName)
    {
        ValidateMember(member); ArgumentException.ThrowIfNullOrWhiteSpace(rivalName);
        return member switch { 4 => "LT. SURGE", 6 => "KOGA", _ => rivalName };
    }

    public static void Validate(TrainerFanClubState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Scene > 2) throw new ArgumentException("trainer_fan_club_scene", nameof(state));
    }

    private static void ValidateMember(int member)
    {
        if (member is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(member));
    }
    private static void ValidateHours(int hours)
    {
        if (hours is < 0 or > 999) throw new ArgumentOutOfRangeException(nameof(hours));
    }
    private static TrainerFanClubState WithCounter(TrainerFanClubState state, int counter) =>
        state with { PackedCounter = (ushort)((state.PackedCounter & 0xff80) | counter) };
    private static TrainerFanClubState WithFlags(TrainerFanClubState state, int flags) =>
        state with { PackedCounter = (ushort)((state.PackedCounter & 0xff) | (flags << 8)) };
}

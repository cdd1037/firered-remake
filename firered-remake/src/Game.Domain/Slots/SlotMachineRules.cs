using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Randomness;

namespace Game.Domain.Slots;

public enum SlotSymbol { Seven, Rocket, Pikachu, Psyduck, Cherries, Magnemite, Shellder }
public enum SlotReward { None, Cherries2, Cherries3, MagShell, PikaPsy, Rocket, Seven }
public enum SlotLine { TopLeftBottomRight, Top, Middle, Bottom, BottomLeftTopRight }

/// <summary>Original slot tables parsed from the generated resource.</summary>
public sealed class SlotMachineRules
{
    public const string Profile = "firered-slot-machine-rules-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int ReelLength = 21;
    public ImmutableArray<ImmutableArray<int>> Reels { get; }
    public ImmutableArray<ImmutableArray<int>> BiasChances { get; }
    public ImmutableArray<ImmutableArray<int>> Lines { get; }
    public ImmutableArray<ImmutableArray<int>> SecondReelChecks { get; }
    public ImmutableArray<ImmutableArray<int>> ThirdReelChecks { get; }
    public ImmutableArray<int> Payouts { get; }
    public ImmutableArray<int> MachineIndices { get; }

    private SlotMachineRules(JsonElement tables)
    {
        static ImmutableArray<int> Row(JsonElement row) => row.EnumerateArray().Select(v => v.GetInt32()).ToImmutableArray();
        ImmutableArray<ImmutableArray<int>> Rows(string key) => tables.GetProperty(key).EnumerateArray().Select(Row).ToImmutableArray();
        Reels = Rows("sReelIconAnimByReelAndPos");
        BiasChances = Rows("sReelBiasChances");
        Lines = Rows("sRowAttributes");
        SecondReelChecks = Rows("sSecondReelBiasCheckIndices");
        ThirdReelChecks = Rows("sThirdReelBiasCheckIndices");
        Payouts = Row(tables.GetProperty("sPayoutTable"));
        MachineIndices = Row(tables.GetProperty("sSlotMachineIndices"));
        bool Shape(ImmutableArray<ImmutableArray<int>> rows, int count, int width, int maximum) =>
            rows.Length == count && rows.All(row => row.Length == width && row.All(value => value >= 0 && value <= maximum));
        if (!Shape(Reels, 3, ReelLength, (int)SlotSymbol.Shellder) || !Shape(BiasChances, 6, 7, ushort.MaxValue) ||
            !Shape(Lines, 5, 4, 8) || Lines.Any(line => line[3] is < 1 or > 3) ||
            !Shape(SecondReelChecks, 15, 2, 8) || !Shape(ThirdReelChecks, 5, 3, 8) ||
            Payouts.Length != 7 || Payouts.Any(value => value < 0) ||
            MachineIndices.Length != 22 || MachineIndices.Any(value => value is < 0 or >= 6))
            throw new ArgumentException("slot_rules_table_shape");
    }

    public static SlotMachineRules FromAuthenticatedJson(ReadOnlyMemory<byte> data)
    {
        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new ArgumentException("slot_rules_source_identity");
        return new(root.GetProperty("tables"));
    }
}

public readonly record struct SlotBiasState(SlotReward Bias, ushort Cooldown);
public sealed record SlotMachineSelection(int MachineIndex, RngState Rng, RandomTrace Trace);
public sealed record SlotBiasDraw(SlotBiasState Bias, RngState Rng, ImmutableArray<RandomTrace> Trace);
public sealed record SlotLineResult(SlotLine Line, bool Enabled, SlotReward Reward, int Coins);
public sealed record SlotPayout(int Coins, SlotReward BestReward, ImmutableArray<SlotLineResult> Lines);
public sealed record SlotReel(int Position, int Subpixel, bool Spinning, int Destination);
public sealed record SlotReels(ImmutableArray<SlotReel> Reels, int NextStop, bool SecondReelBiasInPlay)
{
    public static SlotReels Initial { get; } = new([new(0, 0, false, 21), new(0, 0, false, 21), new(0, 0, false, 21)], 0, false);
}
public sealed record SlotStop(SlotReels Reels, RngState Rng, ImmutableArray<RandomTrace> Trace);
public sealed record SlotWager(int Coins, int Bet, bool AutoStart);
public sealed record SlotCredit(int Coins, int PayoutRemaining, int Credited);

/// <summary>
/// Deterministic port of the original non-presentation slot routines. The caller owns the one campaign
/// coin wallet, source interaction and explicit RNG. No implicit timing or new payout probability model.
/// </summary>
public static class SlotMachineCore
{
    private static int Wrap(int position) => (position + 21) % 21;
    private static void ValidateBias(SlotBiasState bias)
    {
        if (bias.Bias is < SlotReward.None or > SlotReward.Seven) throw new ArgumentOutOfRangeException(nameof(bias));
    }
    private static void ValidateReels(SlotReels state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Reels.IsDefault || state.Reels.Length != 3 || state.NextStop is < 0 or > 3 ||
            state.Reels.Any(r => r is null || r.Position is < 0 or >= 21 || r.Subpixel is < 0 or > 2 || r.Destination is < 0 or > 21))
            throw new ArgumentException("slot_reel_state");
    }
    private static void ValidateWallet(int coins)
    {
        if (coins is < 0 or > GameCornerCoinSource.Maximum) throw new ArgumentOutOfRangeException(nameof(coins));
    }
    private static void ValidateBet(int bet)
    {
        if (bet is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(bet));
    }

    public static SlotMachineSelection SelectMachine(SlotMachineRules rules, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var draw = Gen3Rng.Next(rng, "slot.machine_index", 22);
        return new(rules.MachineIndices[draw.Value % 22], draw.State, draw.Trace);
    }

    // CalcSlotBias consumes its first draw even while Rocket/Seven bias is sticky.
    // Cooldown is retained exactly: the original code does not use it to upgrade the selected bias.
    public static SlotBiasDraw ChooseBias(SlotMachineRules rules, int machineIndex, SlotBiasState bias, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(rules); ValidateBias(bias);
        if (machineIndex is < 0 or >= 6) throw new ArgumentOutOfRangeException(nameof(machineIndex));
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        ushort Random(string purpose)
        {
            var draw = Gen3Rng.Next(rng, purpose); rng = draw.State; trace.Add(draw.Trace); return draw.Value;
        }
        int value = Random("slot.bias_class") / 4;
        var chances = rules.BiasChances[machineIndex];
        int selected;
        for (selected = 0; selected < 6; selected++) if (value < chances[selected]) break;
        if (bias.Bias < SlotReward.Rocket)
        {
            int cooldown = bias.Cooldown;
            if (cooldown == 0 && (Random("slot.cooldown_trigger") & 0x3fff) < chances[6])
                cooldown = (Random("slot.cooldown_length") & 1) != 0 ? 5 : 60;
            if (cooldown != 0)
            {
                if (selected == 0 && (Random("slot.cooldown_refresh") & 0x3fff) < 11468)
                    cooldown = (Random("slot.cooldown_refresh_length") & 1) != 0 ? 5 : 60;
                cooldown--;
            }
            bias = new((SlotReward)selected, (ushort)cooldown);
        }
        return new(bias, rng, trace.ToImmutable());
    }

    public static SlotBiasState AfterPayout(SlotBiasState bias, SlotPayout payout)
    {
        ValidateBias(bias); ArgumentNullException.ThrowIfNull(payout);
        return payout.BestReward == SlotReward.None ? bias : bias with { Bias = SlotReward.None };
    }

    public static SlotReels StartReels(SlotReels state)
    {
        ValidateReels(state);
        if (state.Reels.Any(r => r.Spinning || r.Subpixel != 0 || r.Destination != 21))
            throw new InvalidOperationException("slot_reels_already_active");
        return new(state.Reels.Select(r => r with { Spinning = true }).ToImmutableArray(), 0, state.SecondReelBiasInPlay);
    }

    /// <summary>One original Task_SpinReels tick. A symbol advances every three ticks.</summary>
    public static SlotReels AdvanceReels(SlotReels state)
    {
        ValidateReels(state);
        var reels = state.Reels.ToBuilder();
        for (int i = 0; i < 3; i++)
        {
            var reel = reels[i];
            if (reel.Spinning || reel.Subpixel != 0)
            {
                if (reel.Subpixel != 0 || reel.Position != reel.Destination)
                {
                    reel = reel with { Subpixel = reel.Subpixel + 1 };
                    if (reel.Subpixel > 2) reel = reel with { Subpixel = 0, Position = Wrap(reel.Position - 1) };
                    if (reel.Position != reel.Destination) { reels[i] = reel; continue; }
                }
                reel = reel with { Destination = 21, Spinning = false };
            }
            reels[i] = reel;
        }
        return state with { Reels = reels.ToImmutable() };
    }

    /// <summary>Original left→middle→right stop request, at its actual tick/subpixel position.</summary>
    public static SlotStop StopCurrent(SlotMachineRules rules, SlotReels state, SlotBiasState bias, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(rules); ValidateReels(state); ValidateBias(bias);
        int reelId = state.NextStop;
        if (reelId == 3 || !state.Reels[reelId].Spinning || state.Reels[reelId].Destination != 21 ||
            state.Reels.Take(reelId).Any(r => r.Spinning || r.Subpixel != 0))
            throw new InvalidOperationException("slot_stop_order_or_pending_stop");
        var reel = state.Reels[reelId];
        int next = Wrap(reel.Position - (reel.Subpixel != 0 ? 1 : 0));
        int offset;
        bool secondBias = state.SecondReelBiasInPlay;
        var trace = ImmutableArray<RandomTrace>.Empty;
        if (reelId == 0)
        {
            var candidates = new List<int>(5);
            if (bias.Bias == SlotReward.None)
            {
                for (int i = 0; i < 5; i++)
                {
                    int j, position = next - i + 1;
                    for (j = 0; j < 3; j++, position++)
                    {
                        if (position >= 21) position = 0;
                        // Approved safety policy: wrap the source's negative internal read onto this reel.
                        // Caller positions remain validated; valid indices and RNG scheduling are unchanged.
                        if (position < 0) position = Wrap(position);
                        if (Matches(SlotReward.Cherries2, rules.Reels[0][position])) break;
                    }
                    if (j == 3) candidates.Add(i);
                }
            }
            else
            {
                for (int i = 0, position = next + 1; i < 3; i++, position++)
                {
                    if (position >= 21) position = 0;
                    if (Matches(bias.Bias, rules.Reels[0][position])) { candidates.Add(0); break; }
                }
                for (int i = 0, position = next; i < 4; i++, position--)
                {
                    if (position < 0) position = 20;
                    if (Matches(bias.Bias, rules.Reels[0][position])) candidates.Add(i + 1);
                }
            }
            int bound = candidates.Count == 0 ? 5 : candidates.Count;
            var draw = Gen3Rng.Next(rng, "slot.first_reel_stop", bound); rng = draw.State; trace = [draw.Trace];
            offset = candidates.Count == 0 ? draw.Value % 5 : candidates[draw.Value % candidates.Count];
        }
        else
        {
            offset = bias.Bias is SlotReward.Rocket or SlotReward.Seven ? 4 : 0;
            if (reelId == 1) secondBias = false;
            for (int i = 0; i < 5; i++)
            {
                bool valid = reelId == 1
                    ? TwoReelCheck(rules, state.Reels[0].Position, Wrap(next - i), bias.Bias)
                    : ThirdReelCheck(rules, state, Wrap(next - i), bias.Bias);
                if (!valid) continue;
                offset = i;
                if (reelId == 1) secondBias = true;
                break;
            }
        }
        var reels = state.Reels.SetItem(reelId, reel with { Destination = Wrap(next - offset) });
        return new(new(reels, reelId + 1, secondBias), rng, trace);
    }

    private static bool TwoReelCheck(SlotMachineRules rules, int firstPosition, int secondPosition, SlotReward bias)
    {
        int[] icons = [7, 7, 7, 7, 7, 7, 7, 7, 7];
        for (int i = 0; i < 3; i++)
        {
            icons[i] = rules.Reels[0][Wrap(firstPosition + 1 + i)];
            icons[3 + i] = rules.Reels[1][Wrap(secondPosition + 1 + i)];
        }
        if (bias == SlotReward.None)
        {
            if (icons.Take(3).Any(icon => Matches(SlotReward.Cherries2, icon))) return false;
            return rules.SecondReelChecks.Any(line => icons[line[0]] == icons[line[1]]);
        }
        if (bias == SlotReward.Cherries2)
        {
            for (int i = 0; i < 15; i += 3)
            {
                var line = rules.SecondReelChecks[i];
                if (icons[line[0]] == icons[line[1]]) return false;
            }
            return icons.Take(3).Any(icon => Matches(bias, icon));
        }
        return rules.SecondReelChecks.Any(line => icons[line[0]] == icons[line[1]] && Matches(bias, icons[line[0]]));
    }

    private static bool ThirdReelCheck(SlotMachineRules rules, SlotReels state, int thirdPosition, SlotReward bias)
    {
        int[] icons = VisibleIcons(rules, [state.Reels[0].Position, state.Reels[1].Position, thirdPosition]);
        bool Pair(ImmutableArray<int> line) => icons[line[0]] == icons[line[1]];
        bool Triple(ImmutableArray<int> line) => Pair(line) && icons[line[0]] == icons[line[2]];
        return bias switch
        {
            SlotReward.None => !icons.Take(3).Any(icon => Matches(SlotReward.Cherries2, icon)) && !rules.ThirdReelChecks.Any(Triple),
            SlotReward.Cherries2 => !rules.ThirdReelChecks.Any(line => Pair(line) && Matches(bias, icons[line[0]])) && icons.Take(3).Any(icon => Matches(bias, icon)),
            SlotReward.Cherries3 => rules.ThirdReelChecks.Any(line => Pair(line) && Matches(bias, icons[line[0]])),
            _ => rules.ThirdReelChecks.Any(line => Triple(line) && Matches(bias, icons[line[0]]))
        };
    }

    private static bool Matches(SlotReward bias, int icon) => bias switch
    {
        SlotReward.None => icon != (int)SlotSymbol.Cherries,
        SlotReward.Cherries2 or SlotReward.Cherries3 => icon == (int)SlotSymbol.Cherries,
        SlotReward.MagShell => icon is (int)SlotSymbol.Magnemite or (int)SlotSymbol.Shellder,
        SlotReward.PikaPsy => icon is (int)SlotSymbol.Pikachu or (int)SlotSymbol.Psyduck,
        SlotReward.Rocket => icon == (int)SlotSymbol.Rocket,
        SlotReward.Seven => icon == (int)SlotSymbol.Seven,
        _ => false
    };
    private static SlotReward Rank(int icon) => (SlotSymbol)icon switch
    {
        SlotSymbol.Magnemite or SlotSymbol.Shellder => SlotReward.MagShell,
        SlotSymbol.Pikachu or SlotSymbol.Psyduck => SlotReward.PikaPsy,
        SlotSymbol.Rocket => SlotReward.Rocket,
        SlotSymbol.Seven => SlotReward.Seven,
        _ => SlotReward.Cherries2
    };
    private static int[] VisibleIcons(SlotMachineRules rules, ImmutableArray<int> positions)
    {
        if (positions.IsDefault || positions.Length != 3 || positions.Any(p => p is < 0 or >= 21))
            throw new ArgumentException("slot_reel_positions");
        int[] icons = new int[9];
        for (int reel = 0; reel < 3; reel++)
            for (int row = 0; row < 3; row++) icons[reel * 3 + row] = rules.Reels[reel][Wrap(positions[reel] + 1 + row)];
        return icons;
    }

    public static SlotPayout Evaluate(SlotMachineRules rules, int bet, ImmutableArray<int> positions)
    {
        ArgumentNullException.ThrowIfNull(rules); ValidateBet(bet);
        int[] icons = VisibleIcons(rules, positions);
        var lines = ImmutableArray.CreateBuilder<SlotLineResult>(5);
        int coins = 0; var best = SlotReward.None;
        for (int i = 0; i < 5; i++)
        {
            var line = rules.Lines[i]; bool enabled = bet >= line[3];
            var reward = SlotReward.None;
            if (enabled)
            {
                if (Matches(SlotReward.Cherries2, icons[line[0]]))
                    reward = Matches(SlotReward.Cherries3, icons[line[1]]) ? SlotReward.Cherries3 : SlotReward.Cherries2;
                else if (icons[line[0]] == icons[line[1]] && icons[line[0]] == icons[line[2]]) reward = Rank(icons[line[0]]);
            }
            int amount = rules.Payouts[(int)reward]; coins += amount;
            if (reward > best) best = reward;
            lines.Add(new((SlotLine)i, enabled, reward, amount));
        }
        return new(coins, best, lines.ToImmutable());
    }

    public static SlotWager InsertCoins(int coins, int bet, bool maximum)
    {
        ValidateWallet(coins); ValidateBet(bet);
        if (coins == 0 || bet == 3) throw new InvalidOperationException("slot_cannot_bet");
        int amount = maximum ? Math.Min(3 - bet, coins) : 1;
        return new(coins - amount, bet + amount, bet + amount == 3 || coins == amount);
    }
    /// <summary>Only the confirmed pre-spin exit owns this refund; caller clears/closes that interaction atomically.</summary>
    public static int RefundUnplayedBet(int coins, int bet)
    {
        ValidateWallet(coins); ValidateBet(bet);
        return GameCornerCoinSource.Add(coins, bet).Coins;
    }
    /// <summary>Consumes all awarded coins even at cap, as MainTask_WinHandlePayout does.</summary>
    public static SlotCredit CollectPayout(int coins, int payoutRemaining, bool collectAll)
    {
        ValidateWallet(coins);
        if (payoutRemaining is < 1 or > 1500) throw new ArgumentOutOfRangeException(nameof(payoutRemaining));
        int amount = collectAll ? payoutRemaining : 1;
        int after = GameCornerCoinSource.Add(coins, amount).Coins;
        return new(after, payoutRemaining - amount, after - coins);
    }
}

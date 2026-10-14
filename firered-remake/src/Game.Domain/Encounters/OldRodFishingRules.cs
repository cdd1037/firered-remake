using System.Collections.Immutable;
using Game.Domain.Randomness;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>
/// FireRed Task_Fishing, shared on-foot rod entry. SourceStep retains the original zero-based
/// function index. Presentation callbacks and VBlank are not random gameplay draws.
/// </summary>
public static class OldRodFishingRules
{
    public const string RulesVersion = "firered-old-rod-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int WaterTerrain = 2;
    public const int ElevationMismatchCollision = 3;
    public const int WaterfallBehavior = 0x13;

    public static bool CanCast(OldRodCastContext context)
    {
        ValidateContext(context);
        return (context.Rod switch { FishingRod.Old => context.OwnsOldRod, FishingRod.Good => context.OwnsGoodRod, FishingRod.Super => context.OwnsSuperRod, _ => false }) && !context.IsSurfing && !context.IsUnderwater &&
            context.FrontMetatileBehavior != WaterfallBehavior && context.PlayerElevation == 3 &&
            context.FrontTerrain == WaterTerrain && context.FrontCollision == ElevationMismatchCollision;
    }

    /// <summary>
    /// StartFishing synchronously runs Fishing1, which locks controls without a draw.
    /// Cancellation is the item-selection cancellation before StartFishing, never a post-cast B rule.
    /// </summary>
    public static OldRodFishingResult Start(OldRodCastContext context, RngState rng, bool cancelBeforeCast = false)
        => StartCore(context, rng, cancelBeforeCast, cinnabarRules: false);

    internal static OldRodFishingResult StartCore(OldRodCastContext context, RngState rng,
        bool cancelBeforeCast, bool cinnabarRules, bool firstSeviiRules = false, bool victoryRoadRules = false, bool seviiRubyRules = false, bool fourIslandRules = false, bool sixIslandRules = false, bool fiveIslandRules = false, bool ceruleanCaveRules = false, bool sevenIslandRules = false, bool goodRodRules = false, bool superRodRules = false)
    {
        ValidateContext(context);
        if (context.Rod != (superRodRules ? FishingRod.Super : goodRodRules ? FishingRod.Good : FishingRod.Old))
            throw new ArgumentException("fishing_rod_profile");
        ValidateRng(rng);
        bool hasMons = HasFishingMons(context.Map, cinnabarRules,firstSeviiRules,victoryRoadRules,seviiRubyRules,fourIslandRules,sixIslandRules,fiveIslandRules,ceruleanCaveRules,sevenIslandRules,goodRodRules,superRodRules);
        var state = new OldRodFishingState(context.Map, hasMons, RulesVersion:
            superRodRules ? SuperRodEncounterRules.RulesVersion : goodRodRules ? GoodRodEncounterRules.RulesVersion : fiveIslandRules ? FiveIslandOldRodEncounterRules.RulesVersion : sixIslandRules ? SixIslandOldRodEncounterRules.RulesVersion : fourIslandRules ? FourIslandOldRodEncounterRules.RulesVersion : seviiRubyRules ? SeviiRubyOldRodEncounterRules.RulesVersion : victoryRoadRules ? VictoryRoadOldRodEncounterRules.RulesVersion : firstSeviiRules ? FirstSeviiOldRodEncounterRules.RulesVersion : cinnabarRules ? CinnabarOldRodEncounterRules.RulesVersion : RulesVersion) { Rod = context.Rod };
        if (cancelBeforeCast) return new(state with { Status = OldRodFishingStatus.Cancelled }, rng, []);
        if (!CanCast(context)) return new(state with { Status = OldRodFishingStatus.Rejected }, rng, []);
        if (!IsAdmittedMap(context.Map, cinnabarRules,firstSeviiRules,victoryRoadRules,seviiRubyRules,fourIslandRules,sixIslandRules,fiveIslandRules,ceruleanCaveRules,sevenIslandRules,goodRodRules,superRodRules)) throw new NotSupportedException("Fishing on this map is outside the bounded Old Rod profile.");
        return new(state, rng, []);
    }

    /// <summary>
    /// Executes exactly one original Task_Fishing invocation, including its TRUE-return chain.
    /// FireRed Fishing7 jumps to Fishing10; the A reaction and repeat-round functions never run.
    /// A/B cannot cancel this task; message/animation completion must come from the presentation.
    /// </summary>
    public static OldRodFishingResult Advance(OldRodFishingState state, OldRodFrameInput input, RngState rng)
        => AdvanceCore(state, input, rng, cinnabarRules: false);

    internal static OldRodFishingResult AdvanceCore(OldRodFishingState state, OldRodFrameInput input,
        RngState rng, bool cinnabarRules, bool firstSeviiRules = false, bool victoryRoadRules = false, bool seviiRubyRules = false, bool fourIslandRules = false, bool sixIslandRules = false, bool fiveIslandRules = false, bool ceruleanCaveRules = false, bool sevenIslandRules = false, bool goodRodRules = false, bool superRodRules = false)
    {
        ValidateStateCore(state, cinnabarRules,firstSeviiRules,victoryRoadRules,seviiRubyRules,fourIslandRules,sixIslandRules,fiveIslandRules,ceruleanCaveRules,sevenIslandRules,goodRodRules,superRodRules);
        ArgumentNullException.ThrowIfNull(input);
        ValidateRng(rng);
        if (state.Status != OldRodFishingStatus.Running)
            throw new InvalidOperationException("The fishing task has already finished or did not start.");
        var traces = ImmutableArray.CreateBuilder<RandomTrace>();
        int Draw(string purpose, int bound)
        {
            var draw = Gen3Rng.Next(rng, purpose, bound);
            rng = draw.State; traces.Add(draw.Trace); return draw.Trace.Result;
        }
        switch (state.SourceStep)
        {
            case 1: // Fishing2; retain even Old Rod's mandatory Random()%1 draw.
                state = state with { MinimumRoundsRequired = 1 + Draw("fishing.minimum-rounds", MinimumRoundsBound(state.Rod)), SourceStep = 2 };
                break;
            case 2: // Fishing3 waits 60 task updates, not 60 arbitrary display frames.
                int frames = state.FrameCounter + 1;
                state = state with { FrameCounter = frames, SourceStep = frames >= 60 ? 3 : 2 };
                break;
            case 3: // Fishing4 returns TRUE, immediately invoking Fishing5 once.
                state = state with { DotsRequired = Math.Min(10, Draw("fishing.dots", 10) + 4),
                    FrameCounter = 1, NumDots = 0, SourceStep = 4 };
                break;
            case 4: // Extra 20-frame interval after the final dot is source behavior.
                int counter = state.FrameCounter + 1;
                if (counter < 20) state = state with { FrameCounter = counter };
                else if (state.NumDots >= state.DotsRequired)
                    state = state with { FrameCounter = 0, SourceStep = 5, RoundsPlayed = 1 };
                else state = state with { FrameCounter = 0, NumDots = state.NumDots + 1 };
                break;
            case 5: // Fishing6 then Fishing7, or Fishing12 then Fishing14, in this same call.
                bool bite = state.HasFishingMons && Draw("fishing.first-bite", 2) == 0;
                state = state with { SourceStep = bite ? 9 : 14 };
                break;
            case 9: // Fishing10 starts Pokemon-on-hook text; Fishing7 showed no reaction text.
                state = state with { SourceStep = 10, FrameCounter = 0 };
                break;
            case 10: // Fishing11 restores avatar, then starts encounter on its next invocation.
                if (state.FrameCounter == 0)
                {
                    if (input.MessageFinished) state = state with { FrameCounter = 1 };
                }
                else state = state with { Status = OldRodFishingStatus.EncounterReady };
                break;
            case 14: // Fishing15 waits only for no-catch animation completion.
                if (input.NoCatchAnimationFinished) state = state with { SourceStep = 15 };
                break;
            case 15: // Fishing16 consumes no RNG even when its text is still active.
                if (input.MessageFinished) state = state with { Status = OldRodFishingStatus.NotEvenANibble };
                break;
            default:
                throw new InvalidOperationException("Unreachable Old Rod fishing state.");
        }
        ValidateStateCore(state, cinnabarRules,firstSeviiRules,victoryRoadRules,seviiRubyRules,fourIslandRules,sixIslandRules,fiveIslandRules,ceruleanCaveRules,sevenIslandRules,goodRodRules,superRodRules);
        return new(state, rng, traces.ToImmutable());
    }

    public static void ValidateState(OldRodFishingState state) => ValidateStateCore(state, cinnabarRules: false);

    internal static void ValidateStateCore(OldRodFishingState state, bool cinnabarRules, bool firstSeviiRules = false, bool victoryRoadRules = false, bool seviiRubyRules = false, bool fourIslandRules = false, bool sixIslandRules = false, bool fiveIslandRules = false, bool ceruleanCaveRules = false, bool sevenIslandRules = false, bool goodRodRules = false, bool superRodRules = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Rod != (superRodRules ? FishingRod.Super : goodRodRules ? FishingRod.Good : FishingRod.Old) || state.RulesVersion != (superRodRules ? SuperRodEncounterRules.RulesVersion : goodRodRules ? GoodRodEncounterRules.RulesVersion : fiveIslandRules ? FiveIslandOldRodEncounterRules.RulesVersion : sixIslandRules ? SixIslandOldRodEncounterRules.RulesVersion : fourIslandRules ? FourIslandOldRodEncounterRules.RulesVersion : seviiRubyRules ? SeviiRubyOldRodEncounterRules.RulesVersion : victoryRoadRules ? VictoryRoadOldRodEncounterRules.RulesVersion : firstSeviiRules ? FirstSeviiOldRodEncounterRules.RulesVersion : cinnabarRules ? CinnabarOldRodEncounterRules.RulesVersion : RulesVersion)) throw new NotSupportedException("Unknown Old Rod rules version.");
        if (string.IsNullOrWhiteSpace(state.Map.Value) || !state.Map.Value.StartsWith("map.", StringComparison.Ordinal) ||
            state.HasFishingMons != HasFishingMons(state.Map, cinnabarRules,firstSeviiRules,victoryRoadRules,seviiRubyRules,fourIslandRules,sixIslandRules,fiveIslandRules,ceruleanCaveRules,sevenIslandRules,goodRodRules,superRodRules) || !Enum.IsDefined(state.Status))
            throw new ArgumentException("Fishing map or status differs from the bounded source profile.");
        if (state.Status is OldRodFishingStatus.Cancelled or OldRodFishingStatus.Rejected)
        {
            if (state.SourceStep != 1 || state.FrameCounter != 0 || state.NumDots != 0 || state.DotsRequired != 0 ||
                state.RoundsPlayed != 0 || state.MinimumRoundsRequired != 0) throw new ArgumentException("A non-started cast cannot contain fishing progress.");
            return;
        }
        if (!IsAdmittedMap(state.Map, cinnabarRules,firstSeviiRules,victoryRoadRules,seviiRubyRules,fourIslandRules,sixIslandRules,fiveIslandRules,ceruleanCaveRules,sevenIslandRules,goodRodRules,superRodRules)) throw new NotSupportedException("Fishing map is not admitted.");
        if (state.SourceStep is not (1 or 2 or 3 or 4 or 5 or 9 or 10 or 14 or 15) ||
            state.FrameCounter is < 0 or > 60 || state.NumDots is < 0 or > 10 || state.DotsRequired is < 0 or > 10 ||
            state.RoundsPlayed is < 0 or > 1 || state.MinimumRoundsRequired < 0 || state.MinimumRoundsRequired > MinimumRoundsBound(state.Rod))
            throw new ArgumentException("Invalid or unreachable source fishing counters.");
        if (state.SourceStep == 1)
        {
            if (state.FrameCounter != 0 || state.NumDots != 0 || state.DotsRequired != 0 || state.RoundsPlayed != 0 || state.MinimumRoundsRequired != 0)
                throw new ArgumentException("New cast counters differ.");
        }
        else if (state.MinimumRoundsRequired < 1 || state.MinimumRoundsRequired > MinimumRoundsBound(state.Rod))
            throw new ArgumentException("Fishing minimum rounds differs from the selected rod.");
        if (state.SourceStep is 2 or 3)
        {
            if (state.NumDots != 0 || state.DotsRequired != 0 || state.RoundsPlayed != 0 ||
                (state.SourceStep == 2 ? state.FrameCounter >= 60 : state.FrameCounter != 60)) throw new ArgumentException("Cast delay counters differ.");
        }
        if (state.SourceStep >= 4 && (state.DotsRequired < 4 || state.NumDots > state.DotsRequired))
            throw new ArgumentException("First-round dot counters differ.");
        if (state.SourceStep == 4 && (state.FrameCounter >= 20 || state.RoundsPlayed != 0)) throw new ArgumentException("Dot-round counters differ.");
        if (state.SourceStep >= 5 && (state.NumDots != state.DotsRequired || state.RoundsPlayed != 1 ||
            (state.SourceStep == 10 ? state.FrameCounter > 1 : state.FrameCounter != 0))) throw new ArgumentException("Finished-round counters differ.");
        if (state.SourceStep is 9 or 10 && !state.HasFishingMons) throw new ArgumentException("Map without fishing mons cannot bite.");
        if (state.Status == OldRodFishingStatus.EncounterReady && (state.SourceStep != 10 || state.FrameCounter != 1) ||
            state.Status == OldRodFishingStatus.NotEvenANibble && state.SourceStep != 15)
            throw new ArgumentException("Terminal fishing phase differs.");
    }

    private static bool HasFishingMons(MapId map, bool cinnabarRules, bool firstSeviiRules = false, bool victoryRoadRules = false, bool seviiRubyRules = false, bool fourIslandRules = false, bool sixIslandRules = false, bool fiveIslandRules = false, bool ceruleanCaveRules = false, bool sevenIslandRules = false, bool goodRodRules = false, bool superRodRules = false) =>
        (superRodRules ? SuperRodEncounterRules.Tables : goodRodRules ? GoodRodEncounterRules.Tables : sevenIslandRules ? SevenIslandOldRodEncounterRules.Tables : ceruleanCaveRules ? CeruleanCaveOldRodEncounterRules.Tables : fiveIslandRules ? FiveIslandOldRodEncounterRules.Tables : sixIslandRules ? SixIslandOldRodEncounterRules.Tables : fourIslandRules ? FourIslandOldRodEncounterRules.Tables : seviiRubyRules ? SeviiRubyOldRodEncounterRules.Tables : victoryRoadRules ? VictoryRoadOldRodEncounterRules.Tables : firstSeviiRules ? FirstSeviiOldRodEncounterRules.Tables : cinnabarRules ? CinnabarOldRodEncounterRules.Tables : OldRodEncounterRules.Tables).Any(t => t.Map == map);

    private static bool IsAdmittedMap(MapId map, bool cinnabarRules, bool firstSeviiRules = false, bool victoryRoadRules = false, bool seviiRubyRules = false, bool fourIslandRules = false, bool sixIslandRules = false, bool fiveIslandRules = false, bool ceruleanCaveRules = false, bool sevenIslandRules = false, bool goodRodRules = false, bool superRodRules = false) => superRodRules ? SuperRodEncounterRules.IsAdmittedMap(map) : goodRodRules ? GoodRodEncounterRules.IsAdmittedMap(map) : sevenIslandRules ? SevenIslandOldRodEncounterRules.IsAdmittedMap(map) : ceruleanCaveRules ? CeruleanCaveOldRodEncounterRules.IsAdmittedMap(map) : fiveIslandRules ? FiveIslandOldRodEncounterRules.IsAdmittedMap(map) : sixIslandRules ? SixIslandOldRodEncounterRules.IsAdmittedMap(map) : fourIslandRules ? FourIslandOldRodEncounterRules.IsAdmittedMap(map) : seviiRubyRules ? SeviiRubyOldRodEncounterRules.IsAdmittedMap(map) : victoryRoadRules ? VictoryRoadOldRodEncounterRules.IsAdmittedMap(map) : firstSeviiRules ? FirstSeviiOldRodEncounterRules.IsAdmittedMap(map) : cinnabarRules
        ? CinnabarOldRodEncounterRules.IsAdmittedMap(map) : OldRodEncounterRules.IsAdmittedMap(map);

    private static void ValidateContext(OldRodCastContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(context.Rod) || string.IsNullOrWhiteSpace(context.Map.Value) || !context.Map.Value.StartsWith("map.", StringComparison.Ordinal) ||
            context.PlayerElevation is < 0 or > 15 || context.FrontTerrain is < 0 or > 31 ||
            context.FrontMetatileBehavior is < 0 or > 511 || context.FrontCollision is < 0 or > 15)
            throw new ArgumentException("Invalid source cast context.");
    }
    private static int MinimumRoundsBound(FishingRod rod) => rod switch {
        FishingRod.Old => 1, FishingRod.Good => 3, FishingRod.Super => 6, _ => throw new ArgumentOutOfRangeException(nameof(rod)) };

    private static void ValidateRng(RngState rng)
    {
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
    }
}

using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Tutorials;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignTeachyTvBattleStage { Events, ChooseAction, ChooseMove, Bag, Party, MedicineTarget, Ending, Boundary, Finished }
public sealed record CampaignTeachyTvBattleStep(int EventIndex, WildRewardEvent? Reward = null, int? Recipient = null);
public sealed record CampaignTeachyTvBattle(string Ticket, int Lesson, SourceBattleFrame Frame,
    ImmutableArray<CampaignTeachyTvBattleStep> Steps, int StepIndex, CampaignTeachyTvBattleStage Stage,
    PokemonInstance DisplayPlayer, PokemonInstance DisplayOpponent)
{
    public ImmutableArray<SourceBattleEvent> Events { get; init; } = [];
    public CampaignBattleDisplayBatch? DisplayBatch { get; init; }
    public SourceBattleEvent? CurrentEvent => CurrentStep is {} step ? Events[step.EventIndex] : null;
    public long Revision { get; init; }
    public string WaitId => Ticket + ":battle:" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public ImmutableArray<SourcePokedudeInputCursor> ActionCursors { get; init; } = [new(), new()];
    public ImmutableArray<SourcePokedudeInputCursor> MoveCursors { get; init; } = [new(), new()];
    public ImmutableArray<SourceActionSelection> Selections { get; init; } = [];
    public SourcePokedudeVoiceoverCursor Voiceover { get; init; } = new();
    public bool VoiceoverPrinted { get; init; }
    public bool Intercepted { get; init; }
    public int SelectingSide { get; init; }
    public int MenuTicks { get; init; }
    public int SoundSequence { get; init; }
    public string? Sound { get; init; }
    public bool Interrupted { get; init; }
    public string? Boundary { get; init; }
    public ImmutableArray<RandomTrace> RandomTrace { get; init; } = [];
    public CampaignTeachyTvBattleStep? CurrentStep => Stage == CampaignTeachyTvBattleStage.Events && StepIndex < Steps.Length ? Steps[StepIndex] : null;
    public int VisibleCursor => Stage switch {
        CampaignTeachyTvBattleStage.ChooseAction => ActionCursors[0].VisibleCursor,
        CampaignTeachyTvBattleStage.ChooseMove => MoveCursors[0].VisibleCursor,
        CampaignTeachyTvBattleStage.Party => MenuTicks > 80 ? 1 : 0,
        CampaignTeachyTvBattleStage.Bag when Lesson == 1 => MenuTicks > 102 ? 1 : 0,
        CampaignTeachyTvBattleStage.Bag when Lesson == 3 => MenuTicks > 612 ? 0 : MenuTicks > 510 ? 1 : MenuTicks > 408 ? 2 : MenuTicks > 306 ? 1 : 0,
        _ => 0 };
    public string BagPocket => Lesson == 3 && MenuTicks > 204 ? "balls" : Lesson == 3 && MenuTicks > 102 ? "key-items" : "items";
    public bool ContextMenu => Stage == CampaignTeachyTvBattleStage.Party && MenuTicks > 160 ||
        Stage == CampaignTeachyTvBattleStage.Bag && MenuTicks > (Lesson == 1 ? 204 : 714);
    public int? SourceOutcome => Frame.Outcome switch { WildBattleOutcome.Won => 1, WildBattleOutcome.Lost => 2,
        WildBattleOutcome.Draw => 3, WildBattleOutcome.Escaped => 4, WildBattleOutcome.Caught => 7, _ => null };
}
public sealed record CampaignTeachyTvBattleDecision(bool Accepted, CampaignTeachyTvBattle State, string? Error = null);

/// <summary>Application owner for the source controller callbacks. Inputs acknowledge or
/// tick a current boundary; action choices and results are always obtained internally.</summary>
public static class CampaignTeachyTvBattleRules
{
    public static CampaignTeachyTvBattleDecision Start(SourceMechanicsCatalog mechanics, TeachyTvDemonstrationRequest request,
        uint trainerId, string playerName, string region, RngState rng, SourcePokedudeBadges badges)
    {
        if (request.Lesson.Battle is not {} definition || request.Lesson.SourceIndex is < 0 or > 3)
            throw new ArgumentException("teachy_tv_battle_definition");
        var party = definition.Party.Select(p => new SourcePokedudePartyDefinition(Side(p.Side), p.Level,
            new("pret:" + p.Species), p.Moves.TakeWhile(m => m != "MOVE_NONE").Select(m => new MoveId("pret:" + m)).ToImmutableArray(),
            Enum.Parse<Nature>(p.Nature[7..], true), Enum.Parse<PokemonGender>(p.Gender, true), p.FixedIv)).ToImmutableArray();
        var started = SourcePokedudeBattle.Start(mechanics, request.Lesson.SourceIndex, party, trainerId, playerName, region, rng, badges);
        if (!started.Succeeded) throw new InvalidDataException(started.Fault);
        var frame = started.State!;
        var battle = new CampaignTeachyTvBattle(request.Ticket, request.Lesson.SourceIndex, frame, Steps(started.Events), 0,
            CampaignTeachyTvBattleStage.Events, frame.Get(new(0)).Body!.Pokemon, frame.Get(new(1)).Body!.Pokemon) { RandomTrace = started.Trace, Events = started.Events,
            DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(started.EntrySlots)) };
        return new(true, Prepare(mechanics.Catalog, definition, battle));
    }

    public static CampaignTeachyTvBattleDecision Decide(SourceMechanicsCatalog mechanics, SourceBattleItemCatalog items,
        TeachyTvBattleDefinition definition, CampaignTeachyTvBattle battle, string command, string? waitId)
    {
        CampaignTeachyTvBattleDecision No(string why) => new(false, battle, why);
        if (waitId != battle.WaitId) return No("teachy_tv_battle_stale_input");
        if (battle.Stage == CampaignTeachyTvBattleStage.Finished) return No("teachy_tv_battle_finished");
        var next = battle;
        if (command == "teachy_tv_battle_cancel")
        {
            bool nested = battle.Stage is CampaignTeachyTvBattleStage.Bag or CampaignTeachyTvBattleStage.Party or CampaignTeachyTvBattleStage.MedicineTarget;
            if (!nested && battle.Frame.Outcome == WildBattleOutcome.Ongoing)
            {
                var aborted = SourcePokedudeBattle.Abort(mechanics.Catalog, battle.Frame, battle.Frame.Revision);
                if (!aborted.Succeeded) return No(aborted.Fault!);
                next = battle with { Frame = aborted.State };
            }
            return Yes(next with { Stage = CampaignTeachyTvBattleStage.Finished, Interrupted = nested || next.SourceOutcome != 3,
                Voiceover = next.Voiceover with { Pending = null } });
        }
        if (battle.Voiceover.Pending is not null)
        {
            if (command == "teachy_tv_battle_presented" && !battle.VoiceoverPrinted)
                return Yes(battle with { VoiceoverPrinted = true });
            if (command != "teachy_tv_battle_confirm" || !battle.VoiceoverPrinted) return No("teachy_tv_battle_voiceover_pending");
            return Yes(Prepare(mechanics.Catalog, definition, battle with { Voiceover = SourcePokedudeController.Acknowledge(battle.Voiceover, true, true), VoiceoverPrinted = false, Intercepted = false }));
        }
        if (command == "teachy_tv_battle_presented" && battle.Stage == CampaignTeachyTvBattleStage.Events)
        {
            next = ApplyDisplay(battle);
            next = next with { StepIndex = battle.StepIndex + 1, Intercepted = false };
        }
        else if (command == "teachy_tv_battle_tick")
        {
            if (battle.Stage == CampaignTeachyTvBattleStage.Boundary) return No(battle.Boundary!);
            if (battle.Stage == CampaignTeachyTvBattleStage.Events) return No("teachy_tv_battle_presentation_pending");
            if (battle.Stage == CampaignTeachyTvBattleStage.Ending)
                next = battle with { Stage = CampaignTeachyTvBattleStage.Finished };
            else if (battle.Stage is CampaignTeachyTvBattleStage.ChooseAction or CampaignTeachyTvBattleStage.ChooseMove)
            {
                bool move = battle.Stage == CampaignTeachyTvBattleStage.ChooseMove;
                var cursors = move ? battle.MoveCursors : battle.ActionCursors;
                var rows = (move ? definition.ChooseMove : definition.ChooseAction)
                    .Select(r => new SourcePokedudeInput(r.CursorPositions, r.Delays)).ToImmutableArray();
                // Approved completion correction: Status and Matchups get exactly one
                // final FIGHT for both controllers. Keep the authenticated source data,
                // existing move scripts and all shared battle/RNG rules unchanged.
                // There is no repeat sentinel: a fifth action still exhausts the stream.
                if (!move && battle.Lesson is 1 or 2)
                    rows = rows.Add(new([0, 0], [64, 0]));
                var input = SourcePokedudeController.Tick(rows, cursors[battle.SelectingSide], (BattleSide)battle.SelectingSide, move);
                if (!input.Succeeded) return Yes(battle with { Stage = CampaignTeachyTvBattleStage.Boundary, Boundary = input.Fault });
                cursors = cursors.SetItem(battle.SelectingSide, input.State);
                next = move ? battle with { MoveCursors = cursors } : battle with { ActionCursors = cursors };
                if (input.PlaySelectSound) next = Sound(next, "SE_SELECT");
                if (input.Selection is {} choice)
                {
                    var actor = next.Frame.Get(new(next.SelectingSide)).Occupant!.Value;
                    if (move) next = Selected(next, new(actor, SourceActionKind.Move, choice, new(next.SelectingSide ^ 1)));
                    else next = next with { Intercepted = false, MenuTicks = 0, Stage = choice switch {
                        0 => CampaignTeachyTvBattleStage.ChooseMove,
                        1 when next.SelectingSide == 0 && next.Lesson is 1 or 3 => CampaignTeachyTvBattleStage.Bag,
                        2 when next.SelectingSide == 0 && next.Lesson == 2 => CampaignTeachyTvBattleStage.Party,
                        _ => CampaignTeachyTvBattleStage.Boundary }, Boundary = choice is > 2 ? "teachy_tv_battle_action_unsupported" : null };
                }
            }
            else if (battle.Stage == CampaignTeachyTvBattleStage.Bag)
            {
                int t = battle.MenuTicks;
                next = battle with { MenuTicks = t + 1 };
                if (battle.Lesson == 1)
                {
                    if (t is 102 or 204 or 306) next = Sound(next, "SE_SELECT");
                    if (t == 306) next = next with { Stage = CampaignTeachyTvBattleStage.MedicineTarget, MenuTicks = 0, Intercepted = false };
                }
                else
                {
                    if (t is 102 or 204) next = Sound(next, "SE_BAG_POCKET");
                    if (t is 306 or 408 or 510 or 612 or 714 or 816) next = Sound(next, "SE_SELECT");
                    if (t == 816) next = Selected(next, SourcePokedudeBattle.CatchSelection(next.Frame));
                }
            }
            else if (battle.Stage == CampaignTeachyTvBattleStage.Party)
            {
                int t = battle.MenuTicks;
                next = battle with { MenuTicks = t + 1 };
                if (t is 80 or 160 or 240) next = Sound(next, "SE_SELECT");
                if (t == 240) next = Selected(next, new(next.Frame.Get(new(0)).Occupant!.Value, SourceActionKind.Switch, Incoming: new(BattleSide.Player, 1)));
            }
            else if (battle.Stage == CampaignTeachyTvBattleStage.MedicineTarget)
            {
                if (battle.MenuTicks < 80) next = battle with { MenuTicks = battle.MenuTicks + 1 };
                else
                {
                    var item = SourcePokedudeBattle.PrepareAntidote(items, mechanics.Catalog, battle.Frame, battle.Frame.Revision);
                    if (!item.Battle.Succeeded) return No(item.Battle.Fault!);
                    next = Selected(Apply(next, item.Battle), item.Selection!);
                    // Medicine text is presented before the remaining source choice and turn.
                    if (!next.Steps.IsEmpty) next = next with { Stage = CampaignTeachyTvBattleStage.Events };
                }
            }
            else return No("teachy_tv_battle_stage");
        }
        else return No("teachy_tv_battle_autoplay_only");
        return Yes(Prepare(mechanics.Catalog, definition, next));
    }

    private static CampaignTeachyTvBattle Selected(CampaignTeachyTvBattle b, SourceActionSelection selection) =>
        b with { Selections = b.Selections.Add(selection), SelectingSide = b.SelectingSide + 1,
            Stage = CampaignTeachyTvBattleStage.ChooseAction, Intercepted = false, MenuTicks = 0 };

    private static CampaignTeachyTvBattle Prepare(PokemonCatalog catalog, TeachyTvBattleDefinition definition, CampaignTeachyTvBattle b)
    {
        for (int i = 0; i < 64; i++)
        {
            if (b.Voiceover.Pending is not null || b.Stage is CampaignTeachyTvBattleStage.Boundary or CampaignTeachyTvBattleStage.Finished) return b;
            if (b.Stage == CampaignTeachyTvBattleStage.Events && b.StepIndex >= b.Steps.Length)
            {
                b = AdvanceDisplay(b, long.MaxValue) with { Steps = [], StepIndex = 0, Intercepted = false };
                if (b.Frame.Rewards?.Pending is not null) return b with { Stage = CampaignTeachyTvBattleStage.Boundary, Boundary = "teachy_tv_battle_learning_not_scripted" };
                if (b.Frame.Outcome != WildBattleOutcome.Ongoing && b.Frame.FourTurn?.Phase is null or SourceFourActionPhase.BattleEnded)
                    b = b with { Stage = CampaignTeachyTvBattleStage.Ending };
                else if (b.Frame.FourTurn is {} turn)
                {
                    var result = SourceFourActionTurns.Advance(catalog, b.Frame, new(b.Frame.Revision, b.Frame.EventSequence, b.Frame.NextAction, turn.Phase));
                    if (!result.Succeeded) return b with { Stage = CampaignTeachyTvBattleStage.Boundary, Boundary = result.Fault };
                    b = Apply(b, result); continue;
                }
                else b = b with { Stage = CampaignTeachyTvBattleStage.ChooseAction, SelectingSide = b.Selections.Length };
            }
            if (b.Stage == CampaignTeachyTvBattleStage.ChooseAction && b.SelectingSide == 2)
            {
                var result = SourceFourActionTurns.BeginActions(catalog, b.Frame, b.Frame.Revision, b.Selections);
                if (!result.Succeeded) return b with { Stage = CampaignTeachyTvBattleStage.Boundary, Boundary = result.Fault };
                b = Apply(b with { Selections = [], SelectingSide = 0 }, result); continue;
            }
            if (b.Intercepted) return b;
            var command = ControllerCommand(b);
            b = b with { Intercepted = true };
            if (command is null) return b;
            var voices = definition.Voiceovers.Select(v => new SourcePokedudeVoiceover(v.ControllerCommand, Side(v.Side), v.StringId, v.Callback, v.TextSymbol)).ToImmutableArray();
            var intercepted = SourcePokedudeController.Intercept(voices, b.Voiceover, command.Value.Command, command.Value.Side, command.Value.StringId);
            if (intercepted.Fault is {} why) return b with { Stage = CampaignTeachyTvBattleStage.Boundary, Boundary = why };
            return b with { Voiceover = intercepted.State, VoiceoverPrinted = false };
        }
        return b with { Stage = CampaignTeachyTvBattleStage.Boundary, Boundary = "teachy_tv_battle_continuation_limit" };
    }

    private static (string Command, BattleSide Side, string StringId)? ControllerCommand(CampaignTeachyTvBattle b) => b.Stage switch {
        CampaignTeachyTvBattleStage.ChooseAction => ("CONTROLLER_CHOOSEACTION", (BattleSide)b.SelectingSide, "0"),
        CampaignTeachyTvBattleStage.ChooseMove => ("CONTROLLER_CHOOSEMOVE", (BattleSide)b.SelectingSide, "0"),
        CampaignTeachyTvBattleStage.Bag => ("CONTROLLER_OPENBAG", BattleSide.Player, "0"),
        CampaignTeachyTvBattleStage.Party => ("CONTROLLER_CHOOSEPOKEMON", BattleSide.Player, "0"),
        CampaignTeachyTvBattleStage.Ending => ("CONTROLLER_ENDLINKBATTLE", BattleSide.Player, "0"),
        CampaignTeachyTvBattleStage.Events when b.CurrentStep is { Reward: WildRewardExperienceEvent } => ("CONTROLLER_PRINTSTRING", BattleSide.Player, "STRINGID_PKMNGAINEDEXP"),
        CampaignTeachyTvBattleStage.Events when b.CurrentEvent is SourceAttackStringEvent used => ("CONTROLLER_PRINTSTRING", (BattleSide)used.Actor.Value, "STRINGID_USEDMOVE"),
        CampaignTeachyTvBattleStage.Events when b.CurrentEvent is SourceActionBlockedEvent blocked && WildBattleStatus.IsSleeping(blocked.Status) => ("CONTROLLER_PRINTSTRING", (BattleSide)blocked.Side.Value, "STRINGID_PKMNFASTASLEEP"),
        _ => null };

    private static ImmutableArray<CampaignTeachyTvBattleStep> Steps(ImmutableArray<SourceBattleEvent> events)
    {
        var result = ImmutableArray.CreateBuilder<CampaignTeachyTvBattleStep>();
        for (int index = 0; index < events.Length; index++)
        {
            var e = events[index];
            if (e is SourceRewardAppliedEvent reward)
                foreach (var r in reward.Reward.Events) result.Add(new(index, r, reward.Recipient.Index));
            else if (e is not (SourceRewardLearningEvent or SourceOpponentRewardCompletedEvent or SourceEndedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent or SourceCalledPartySwitchedEvent) &&
                e is not SourceStatusChangedEvent { Reason: "sleep-countdown" or "faint" }) result.Add(new(index));
        }
        return result.ToImmutable();
    }
    private static CampaignTeachyTvBattle Apply(CampaignTeachyTvBattle b, SourceFourActionResult result) =>
        b with { DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(b.Frame.Slots)), Events = result.Events,
            Frame = result.State, Steps = Steps(result.Events), StepIndex = 0, Stage = CampaignTeachyTvBattleStage.Events,
            Intercepted = false, RandomTrace = b.RandomTrace.AddRange(result.Trace) };
    private static CampaignTeachyTvBattle AdvanceDisplay(CampaignTeachyTvBattle b, long sequence)
    {
        var before = b.DisplayBatch!;
        var after = before.AdvanceThrough(b.Events, sequence);
        for (int i = before.NextEventIndex; i < after.NextEventIndex; i++)
            if (b.Events[i] is SourceCalledPartySwitchedEvent switched)
            {
                // The hidden switch command replaces the complete active HUD identity,
                // including max HP, before any subsequent HP receipt. Use its snapshot,
                // not the final frame or a later reward already reduced in this batch.
                var pokemon = switched.Replacement!.Body.Pokemon;
                b = switched.Side.Value == 0 ? b with { DisplayPlayer = pokemon } : b with { DisplayOpponent = pokemon };
            }
        return b with { DisplayBatch = after };
    }
    private static CampaignTeachyTvBattle ApplyDisplay(CampaignTeachyTvBattle b)
    {
        // Repeated reward steps share one receipt; AdvanceThrough applies it once while
        // the visible reward level below changes only at its authored step.
        b = AdvanceDisplay(b, b.CurrentEvent!.Sequence);
        void Hp(int side, int hp) { b = side == 0 ? b with { DisplayPlayer = b.DisplayPlayer with { CurrentHp = hp } } : b with { DisplayOpponent = b.DisplayOpponent with { CurrentHp = hp } }; }
        switch (b.CurrentEvent)
        {
            case SourceHpChangedEvent e: Hp(e.Target.Value, e.HpAfter); break;
            case SourceStatusDamageEvent e: Hp(e.Side.Value, e.HpAfter); break;
            case SourceDrainEvent e: Hp(e.Actor.Value, e.HpAfter); break;
            case SourceRecoveredEvent e: Hp(e.Side.Value, e.HpAfter); break;
            case SourceMedicineAppliedEvent e when e.ActiveTarget: Hp(0, e.HpAfter); break;
            case SourcePlayerSwitchedEvent e: b = b with { DisplayPlayer = e.Replacement.Body.Pokemon }; break;
        }
        if (b.CurrentStep is { Reward: WildRewardLevelEvent, Recipient: {} recipient } && recipient == b.Frame.Get(new(0)).Member!.Value.Index)
            b = b with { DisplayPlayer = b.Frame.Party(BattleSide.Player).Members[recipient].Pokemon };
        return b;
    }
    private static CampaignTeachyTvBattle Sound(CampaignTeachyTvBattle b, string sound) => b with { SoundSequence = b.SoundSequence + 1, Sound = sound };
    private static BattleSide Side(string symbol) => symbol switch { "B_SIDE_PLAYER" => BattleSide.Player, "B_SIDE_OPPONENT" => BattleSide.Opponent, _ => throw new InvalidDataException("teachy_tv_battle_side") };
    private static CampaignTeachyTvBattleDecision Yes(CampaignTeachyTvBattle b) => new(true, b with { Revision = checked(b.Revision + 1) });
}

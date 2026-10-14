using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignMoveReminderStage { FieldScript, Party, Moves, TeachQuestion, GiveUpQuestion, ReplaceQuestion, ChooseMove, StopQuestion, Notice, Forgotten, Learned, Payment }
public sealed record CampaignMoveReminder(CampaignMoveReminderStage Stage, int PartyIndex = -1, long? InstanceId = null,
    MoveId? Move = null, int Page = 0, string? TextSymbol = null, MoveReminderLesson? Lesson = null, bool Paid = false)
{
    public ImmutableArray<MoveId> Moves { get; init; } = [];
    public ImmutableArray<string> Pages { get; init; } = [];
}

/// <summary>The original field script owns mushroom checks, party retry loops and payment.
/// These callbacks own selection and learning at its two authenticated waitstate calls.</summary>
public sealed partial class CampaignSession
{
    private const string ReminderMap = "map.two-island-house";
    private const string ReminderPath = "data/maps/TwoIsland_House/scripts.inc";
    private const string ReminderEntry = "TwoIsland_House_EventScript_MoveManiac";
    private const string ReminderPartyCallback = "TwoIsland_House_EventScript_ChooseMonToTutor";
    private const string ReminderMoveCallback = "TwoIsland_House_EventScript_ChooseMoveToTeach";
    private const string ReminderPaymentCallback = "TwoIsland_House_EventScript_ChooseMushroom";

    private bool InMoveReminderScript(CampaignState state)
    {
        if (_content.MoveReminder is not { } catalog || !ReferenceEquals(catalog.Pokemon, _content.PokemonContent?.Catalog) ||
            state.World.Player.MapId.Value != ReminderMap || state.LastTalked is not { } actor ||
            !state.Actors.TryGetValue(actor, out var npc) || !npc.Active || npc.Invisible ||
            npc.SourceLocalId != 1 || npc.GraphicsId != "OBJ_EVENT_GFX_FAT_MAN" || npc.ScriptId != ReminderEntry ||
            npc.TemplateX != 7 || npc.TemplateY != 5 || npc.Elevation != 3 || !state.Script.Locked || state.Script.Completed) return false;
        var definition = _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == actor);
        return definition is { SourceLocalId: 1, IsClone: false, ScriptId: ReminderEntry, GraphicsId: "OBJ_EVENT_GFX_FAT_MAN", X: 7, Y: 5, Elevation: 3 };
    }

    private bool AtMoveReminderWait(CampaignState state, string symbol, bool payment = false)
    {
        if (!InMoveReminderScript(state) || state.Script.Wait?.Kind != (payment ? StoryWaitKind.MenuChoice : StoryWaitKind.PartySelection) ||
            state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        if (program is null || index < 0 || index >= program.Instructions.Length) return false;
        var instruction = program.Instructions[index];
        return instruction.Source.Path == ReminderPath && instruction.Source.Symbol == symbol && (payment
            ? instruction.Op == StoryOp.Multichoice && instruction.Opcode == "multichoice" &&
              instruction.Args.SequenceEqual(new[] { "0", "0", "MULTICHOICE_MUSHROOMS", "TRUE" })
            : instruction.Op == StoryOp.WaitState && instruction.Opcode == "waitstate" && instruction.Args.IsEmpty);
    }

    private static string? ReminderSelection(ImmutableArray<PartyPokemon> party, int index, long? instanceId) =>
        MoveTutorRules.ValidateSelection(party, index, instanceId)?.Replace("move_tutor_", "move_reminder_", StringComparison.Ordinal);

    private CampaignMoveReminder ReminderMessage(CampaignMoveReminder scene, CampaignMoveReminderStage stage, string symbol,
        PartyPokemon mon, string? forgotten = null)
    {
        string raw = _content.MoveReminder!.Texts[symbol].Replace("{STR_VAR_1}", mon.Nickname, StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", scene.Move is { } move ? BattleContent.MoveNames[move.Value] : "", StringComparison.Ordinal)
            .Replace("{STR_VAR_3}", forgotten ?? "", StringComparison.Ordinal);
        raw = Regex.Replace(raw, @"\{[^}]+\}", "", RegexOptions.CultureInvariant);
        var pages = raw.Split("\\p", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\l", "\n", StringComparison.Ordinal)).ToImmutableArray();
        return scene with { Stage = stage, TextSymbol = symbol, Pages = pages, Page = 0 };
    }

    private bool TryDecideMoveReminder(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        decision = Reject("move_reminder_not_pending");
        if (state.MoveReminder is not { } scene) return command.Kind.StartsWith("move_reminder_", StringComparison.Ordinal);
        if (!InMoveReminderScript(state)) { decision = Reject("move_reminder_callback_context"); return true; }
        if (scene.Stage == CampaignMoveReminderStage.FieldScript)
        {
            if (command.Kind == "ack") return false;
            decision = Reject("move_reminder_event_active"); return true;
        }
        if (scene.Stage == CampaignMoveReminderStage.Payment)
        {
            if (!AtMoveReminderWait(state, ReminderPaymentCallback, true) || scene.Lesson is not { Succeeded: true } || scene.Paid)
            { decision = Reject("move_reminder_payment_context"); return true; }
            if (command.Kind != "move_reminder_payment" || command.Choice is not (0 or 1))
            { decision = Reject("move_reminder_payment_required"); return true; }
            var wait = state.Script.Wait!;
            var next = state with { MoveReminder = scene with { Stage = CampaignMoveReminderStage.FieldScript },
                Dialogue = null, PresentationEffects = [], PresentationTicks = 0 };
            decision = new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content, command.Choice))));
            return true;
        }
        if (!AtMoveReminderWait(state, scene.Stage == CampaignMoveReminderStage.Party ? ReminderPartyCallback : ReminderMoveCallback))
        { decision = Reject("move_reminder_callback_context"); return true; }
        if (scene.Stage == CampaignMoveReminderStage.Party)
        {
            if (command.Kind == "move_reminder_cancel")
            {
                var cancelled = state with { MoveReminder = new(CampaignMoveReminderStage.FieldScript),
                    Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", 6).SetItem("VAR_0x8005", 0) } };
                decision = new(CommandStatus.Accepted, ContinueMoveReminder(cancelled)); return true;
            }
            if (command.Kind != "move_reminder_choose") { decision = Reject("move_reminder_selection_required"); return true; }
            var error = ReminderSelection(state.Party, command.PartyIndex, command.TargetInstanceId);
            if (error is not null) { decision = Reject(error); return true; }
            var mon = state.Party[command.PartyIndex]; var candidates = MoveReminderRules.Candidates(_content.MoveReminder!, mon);
            if (candidates.Error is not null && candidates.Error != "move_reminder_egg") { decision = Reject(candidates.Error); return true; }
            var selected = state with { MoveReminder = new(CampaignMoveReminderStage.FieldScript, command.PartyIndex, mon.InstanceId) { Moves = candidates.Moves },
                TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname),
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", command.PartyIndex).SetItem("VAR_0x8005", candidates.Moves.Length) } };
            decision = new(CommandStatus.Accepted, ContinueMoveReminder(selected)); return true;
        }
        var targetError = ReminderSelection(state.Party, scene.PartyIndex, scene.InstanceId);
        if (targetError is not null) { decision = Reject(targetError); return true; }
        var pokemon = state.Party[scene.PartyIndex];
        CampaignState Message(CampaignMoveReminderStage stage, string symbol) => state with { MoveReminder = ReminderMessage(scene, stage, symbol, pokemon) };
        CampaignState Moves() => Message(CampaignMoveReminderStage.Moves, "gText_TeachWhichMoveToMon");
        CampaignState result;
        if (scene.Stage == CampaignMoveReminderStage.Moves)
        {
            if (command.Kind == "move_reminder_cancel") result = Message(CampaignMoveReminderStage.GiveUpQuestion, "gText_GiveUpTryingToTeachNewMove");
            else if (command.Kind == "move_reminder_move")
            {
                if (command.TargetInstanceId != scene.InstanceId) { decision = Reject("move_reminder_target_mismatch"); return true; }
                if (command.MoveSlot < 0 || command.MoveSlot >= scene.Moves.Length) { decision = Reject("move_reminder_move_index"); return true; }
                scene = scene with { Move = scene.Moves[command.MoveSlot] };
                result = Message(CampaignMoveReminderStage.TeachQuestion, "gText_TeachMoveQues");
            }
            else { decision = Reject("move_reminder_move_required"); return true; }
        }
        else if (scene.Stage == CampaignMoveReminderStage.ChooseMove)
        {
            if (command.Kind == "move_reminder_cancel") result = Message(CampaignMoveReminderStage.StopQuestion, "gText_StopLearningMove");
            else if (command.Kind == "move_reminder_replace")
            {
                if (command.TargetInstanceId != scene.InstanceId) { decision = Reject("move_reminder_target_mismatch"); return true; }
                if (command.MoveSlot is < 0 or > 3 || pokemon.Pokemon.Moves.Length != 4) { decision = Reject("move_reminder_invalid_slot"); return true; }
                result = PokemonMoveRules.IsHiddenMachineMove(pokemon.Pokemon.Moves[command.MoveSlot].Move)
                    ? Message(CampaignMoveReminderStage.Notice, "gText_PokeSum_HmMovesCantBeForgotten") : LearnReminderMove(state, command.MoveSlot);
            }
            else { decision = Reject("move_reminder_move_required"); return true; }
        }
        else if (command.Kind == "move_reminder_ack" && scene.Page + 1 < scene.Pages.Length)
            result = state with { MoveReminder = scene with { Page = scene.Page + 1 } };
        else if (scene.Stage is CampaignMoveReminderStage.TeachQuestion or CampaignMoveReminderStage.GiveUpQuestion or
                 CampaignMoveReminderStage.ReplaceQuestion or CampaignMoveReminderStage.StopQuestion &&
                 scene.Page + 1 == scene.Pages.Length && command.Kind is "move_reminder_choice" or "move_reminder_cancel")
        {
            int choice = command.Kind == "move_reminder_cancel" ? 0 : command.Choice ?? -1;
            if (choice is not (0 or 1)) { decision = Reject("move_reminder_choice_invalid"); return true; }
            result = scene.Stage switch
            {
                CampaignMoveReminderStage.TeachQuestion => choice == 0 ? Moves() : pokemon.Pokemon.Moves.Length == 4
                    ? Message(CampaignMoveReminderStage.ReplaceQuestion, "gText_MonIsTryingToLearnMove") : LearnReminderMove(state, null),
                CampaignMoveReminderStage.GiveUpQuestion => choice == 0 ? Moves() : FinishMoveReminder(state, false),
                CampaignMoveReminderStage.ReplaceQuestion => choice == 1 ? Message(CampaignMoveReminderStage.ChooseMove, "gText_WhichMoveShouldBeForgotten")
                    : Message(CampaignMoveReminderStage.StopQuestion, "gText_StopLearningMove"),
                _ => choice == 1 ? Moves() : Message(CampaignMoveReminderStage.ReplaceQuestion, "gText_MonIsTryingToLearnMove")
            };
        }
        else if (command.Kind == "move_reminder_ack")
        {
            result = scene.Stage switch
            {
                CampaignMoveReminderStage.Notice => Message(CampaignMoveReminderStage.ChooseMove, "gText_WhichMoveShouldBeForgotten"),
                CampaignMoveReminderStage.Forgotten => TutorSound(TutorSound(state with
                    { MoveReminder = ReminderMessage(scene, CampaignMoveReminderStage.Learned, "gText_MonForgotOldMoveAndMonLearnedNewMove", pokemon,
                        state.TextBuffers.GetValueOrDefault("STR_VAR_3")) }, "playfanfare", "MUS_LEVEL_UP"), "playfanfare", "MUS_LEVEL_UP"),
                CampaignMoveReminderStage.Learned => FinishMoveReminder(state, true),
                _ => state
            };
            if (ReferenceEquals(result, state)) { decision = Reject("move_reminder_choice_required"); return true; }
        }
        else { decision = Reject("move_reminder_command_not_allowed"); return true; }
        decision = new(CommandStatus.Accepted, result); return true;
    }

    private CampaignState LearnReminderMove(CampaignState state, int? slot)
    {
        var scene = state.MoveReminder!; var mon = state.Party[scene.PartyIndex];
        var result = MoveReminderRules.Teach(_content.MoveReminder!, _content.PokemonContent!.Catalog, mon,
            scene.Move ?? throw new InvalidDataException("move_reminder_move_missing"), slot);
        if (!result.Succeeded) throw new InvalidDataException(result.Error);
        string forgotten = slot is { } index ? BattleContent.MoveNames[mon.Pokemon.Moves[index].Move.Value] : "";
        scene = scene with { Lesson = result };
        return TutorSound(state with { Party = state.Party.SetItem(scene.PartyIndex, result.Pokemon),
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname).SetItem("STR_VAR_2", BattleContent.MoveNames[result.Move.Value]).SetItem("STR_VAR_3", forgotten),
            MoveReminder = ReminderMessage(scene, slot is null ? CampaignMoveReminderStage.Learned : CampaignMoveReminderStage.Forgotten,
                slot is null ? "gText_MonLearnedMove" : "gText_1_2_and_Poof", mon, forgotten) },
            slot is null ? "playfanfare" : "playse", slot is null ? "MUS_LEVEL_UP" : "SE_BALL_BOUNCE_1");
    }

    private CampaignState FinishMoveReminder(CampaignState state, bool learned) => ContinueMoveReminder(state with
    {
        MoveReminder = state.MoveReminder! with { Stage = CampaignMoveReminderStage.FieldScript },
        Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", learned ? 1 : 0) }
    });

    private CampaignState ContinueMoveReminder(CampaignState state)
    {
        var wait = state.Script.Wait!;
        var next = state with { Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false };
        return Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content)));
    }

    private bool TryApplyMoveReminderEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (_content.MoveReminder is null || effect.Source.Path != ReminderPath) return false;
        var args = effect.Args;
        if (effect.Opcode is not ("special" or "waitstate" or "checkitem" or "removeitem" or "multichoice")) return false;
        if (effect.Opcode == "special" && !args.Any(a => a is "ChooseMonForMoveRelearner" or "TeachMoveRelearnerMove" or "IsSelectedMonEgg")) return false;
        if (!InMoveReminderScript(state)) throw new InvalidDataException("move_reminder_source_context");
        if (effect.Opcode is "checkitem" or "removeitem")
        {
            bool tiny = args.SequenceEqual(new[] { "ITEM_TINY_MUSHROOM", "2" });
            bool big = args.SequenceEqual(new[] { "ITEM_BIG_MUSHROOM" }) || args.SequenceEqual(new[] { "ITEM_BIG_MUSHROOM", "1" });
            if (!tiny && !big) throw new InvalidDataException("move_reminder_mushroom_operands");
            var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
            if (ValidateCampaignInventory(inventory) is { } inventoryError) throw new InvalidDataException(inventoryError);
            var view = ShopInventoryView(inventory);
            if (effect.Opcode == "checkitem")
            {
                if (effect.Source.Symbol != "TwoIsland_House_EventScript_CheckPlayerHasMushrooms") throw new InvalidDataException("move_reminder_check_context");
                var options = MoveReminderRules.PaymentOptions(ShopCatalog, view);
                if (options.Error is not null) throw new InvalidDataException(options.Error);
                next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", (tiny ? options.TinyMushrooms : options.BigMushroom) ? 1 : 0) } };
                return true;
            }
            var scene = state.MoveReminder;
            if (effect.Source.Symbol != (tiny ? "TwoIsland_House_EventScript_GiveTinyMushrooms" : "TwoIsland_House_EventScript_GiveBigMushroom") ||
                scene is not { Stage: CampaignMoveReminderStage.FieldScript, Lesson.Succeeded: true, Paid: false } ||
                ReminderSelection(state.Party, scene.PartyIndex, scene.InstanceId) is not null || state.Party[scene.PartyIndex] != scene.Lesson.Pokemon ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004") != 1)
                throw new InvalidDataException("move_reminder_payment_context");
            var payment = MoveReminderRules.PayAfterLearning(ShopCatalog, view, scene.Lesson, tiny ? MoveReminderPayment.TinyMushrooms : MoveReminderPayment.BigMushroom);
            if (!payment.Succeeded) throw new InvalidDataException(payment.Error);
            next = state with { Inventory = MergeShopInventory(inventory, payment.State), MoveReminder = scene with { Paid = true },
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 1) } };
            return true;
        }
        if (effect.Opcode == "multichoice")
        {
            if (effect.Source.Symbol != ReminderPaymentCallback || !args.SequenceEqual(new[] { "0", "0", "MULTICHOICE_MUSHROOMS", "TRUE" }) ||
                state.MoveReminder is not { Stage: CampaignMoveReminderStage.FieldScript, Lesson.Succeeded: true, Paid: false } payment ||
                state.Script.Wait?.Kind != StoryWaitKind.MenuChoice || !state.Script.Flags.GetValueOrDefault("FLAG_TEMP_4"))
                throw new InvalidDataException("move_reminder_payment_menu_context");
            next = state with { MoveReminder = payment with { Stage = CampaignMoveReminderStage.Payment } }; return true;
        }
        if (effect.Opcode == "waitstate")
        {
            if (!args.IsEmpty || state.MoveReminder is not { } scene || state.Script.Wait?.Kind != StoryWaitKind.Presentation ||
                effect.Source.Symbol != (scene.Stage == CampaignMoveReminderStage.Party ? ReminderPartyCallback : ReminderMoveCallback) ||
                scene.Stage is not (CampaignMoveReminderStage.Party or CampaignMoveReminderStage.Moves))
                throw new InvalidDataException("move_reminder_wait_context");
            next = state with { Dialogue = null, SignMessage = null, PresentationTicks = 0,
                Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } }; return true;
        }
        if (args.Length != 1) throw new InvalidDataException("move_reminder_special_operands");
        if (args[0] == "ChooseMonForMoveRelearner")
        {
            if (effect.Source.Symbol != ReminderPartyCallback || state.Party.IsDefaultOrEmpty ||
                state.MoveReminder is { Lesson: not null }) throw new InvalidDataException("move_reminder_party_context");
            next = state with { MoveReminder = new(CampaignMoveReminderStage.Party) }; return true;
        }
        var selected = state.MoveReminder;
        if (selected is not { Stage: CampaignMoveReminderStage.FieldScript } ||
            ReminderSelection(state.Party, selected.PartyIndex, selected.InstanceId) is { } ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != selected.PartyIndex)
            throw new InvalidDataException("move_reminder_target_context");
        var mon = state.Party[selected.PartyIndex];
        if (args[0] == "IsSelectedMonEgg")
        {
            if (effect.Source.Symbol != ReminderPartyCallback) throw new InvalidDataException("move_reminder_egg_context");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", mon.IsEgg ? 1 : 0) } }; return true;
        }
        if (effect.Source.Symbol != ReminderMoveCallback || mon.IsEgg || selected.Moves.IsEmpty ||
            !MoveReminderRules.Candidates(_content.MoveReminder, mon).Moves.SequenceEqual(selected.Moves))
            throw new InvalidDataException("move_reminder_moves_context");
        next = state with { MoveReminder = ReminderMessage(selected, CampaignMoveReminderStage.Moves, "gText_TeachWhichMoveToMon", mon) };
        return true;
    }
}

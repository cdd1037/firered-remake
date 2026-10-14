using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Items;

namespace Game.Application;

public enum CampaignMagikarpRecordStage { FieldScript, Party }
public sealed record CampaignMagikarpRecord(CampaignMagikarpRecordStage Stage,
    int PartyIndex = -1, long? InstanceId = null, bool Compared = false, bool GiftAttempted = false);

/// <summary>The authenticated Route 12 script owns every message, branch and Net Ball gift.
/// Only ChoosePartyMon suspends for the shared party chooser. The original u16 script variable
/// is updated during CompareMagikarpSize, before the script sets its flag and attempts the gift.</summary>
public sealed partial class CampaignSession
{
    private const string MagikarpRecordMap = "map.route-12-fishing-house";
    private const string MagikarpRecordEntry = "Route12_FishingHouse_EventScript_FishingGuruBrother";
    private const string MagikarpRecordCheck = "Route12_FishingHouse_EventScript_CheckMagikarpRecord";
    private const string MagikarpRecordSign = "Route12_FishingHouse_EventScript_MagikarpRecordSign";
    private const string MagikarpRecordSignSet = "Route12_FishingHouse_EventScript_MagikarpRecordSignRecordSet";
    private const string MagikarpRecordVariable = "VAR_MAGIKARP_SIZE_RECORD";

    private bool InMagikarpRecordHouse(CampaignState state) => _content.SuperRodEnabled &&
        state.World.Player.MapId.Value == MagikarpRecordMap && state.Script.Locked && !state.Script.Completed &&
        _content.MapDefinition(state.World.Player.MapId).SourceMapSymbol == "MAP_ROUTE12_FISHING_HOUSE";

    private bool InMagikarpRecordFisher(CampaignState state)
    {
        if (!InMagikarpRecordHouse(state) || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_SUPER_ROD") ||
            state.LastTalked is not { } actor || !state.Actors.TryGetValue(actor, out var npc) || !npc.Active || npc.Invisible ||
            npc.SourceLocalId != 1 || npc.GraphicsId != "OBJ_EVENT_GFX_FISHER" || npc.ScriptId != MagikarpRecordEntry ||
            npc.TemplateX != 4 || npc.TemplateY != 4 || npc.Elevation != 3) return false;
        return _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == actor) is
            { SourceLocalId: 1, IsClone: false, GraphicsId: "OBJ_EVENT_GFX_FISHER", ScriptId: MagikarpRecordEntry, X: 4, Y: 4, Elevation: 3 };
    }

    private bool InMagikarpRecordSign(CampaignState state)
    {
        if (!InMagikarpRecordHouse(state) || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_RECORD_SETTING_MAGIKARP")) return false;
        var player = state.World.Player; var (dx, dy) = Delta(player.Facing);
        return player.X + dx == 9 && player.Y + dy == 1 &&
            _content.MapDefinition(player.MapId).BackgroundTriggers.Any(b => b.X == 9 && b.Y == 1 && b.Elevation == 0 &&
                b.ScriptId == MagikarpRecordSign && b.HiddenItem is null && b.PlayerFacing is null && !b.Underfoot);
    }

    private StoryInstruction? MagikarpRecordInstruction(CampaignState state)
    {
        if (state.Script.Stack.IsEmpty) return null;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length ? program.Instructions[index] : null;
    }

    private bool AtMagikarpRecordWait(CampaignState state)
    {
        if (!InMagikarpRecordFisher(state) || state.Script.Wait?.Kind != StoryWaitKind.PartySelection ||
            MagikarpRecordInstruction(state) is not { Op: StoryOp.WaitState, Opcode: "waitstate" } instruction ||
            instruction.Source.Path != MagikarpSizeRecordRules.ScriptPath || instruction.Source.Symbol != MagikarpRecordCheck || !instruction.Args.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId)!; int previous = frame.InstructionIndex - 2;
        return previous >= 0 && program.Instructions[previous] is { Op: StoryOp.Special, Opcode: "special" } choose &&
            choose.Source.Path == MagikarpSizeRecordRules.ScriptPath && choose.Source.Symbol == MagikarpRecordCheck &&
            choose.Args.SequenceEqual(new[] { "ChoosePartyMon" });
    }

    private static string? MagikarpRecordSelection(CampaignState state, int index, long? instanceId) =>
        MoveTutorRules.ValidateSelection(state.Party, index, instanceId)?.Replace("move_tutor_", "magikarp_record_", StringComparison.Ordinal);

    private bool TryDecideMagikarpRecord(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        decision = Reject("magikarp_record_not_pending");
        if (state.MagikarpRecord is not { } scene) return command.Kind.StartsWith("magikarp_record_", StringComparison.Ordinal);
        if (!InMagikarpRecordFisher(state)) { decision = Reject("magikarp_record_callback_context"); return true; }
        if (scene.Stage == CampaignMagikarpRecordStage.FieldScript)
        {
            if (command.Kind == "ack") return false;
            decision = Reject("magikarp_record_event_active"); return true;
        }
        if (!AtMagikarpRecordWait(state)) { decision = Reject("magikarp_record_callback_context"); return true; }
        int index = MagikarpSizeRecordRules.PartyCapacity; long? instance = null;
        if (command.Kind != "magikarp_record_cancel")
        {
            if (command.Kind != "magikarp_record_choose") { decision = Reject("magikarp_record_selection_required"); return true; }
            var error = MagikarpRecordSelection(state, command.PartyIndex, command.TargetInstanceId);
            if (error is not null) { decision = Reject(error); return true; }
            index = command.PartyIndex; instance = state.Party[index].InstanceId;
        }
        var next = state with { MagikarpRecord = new(CampaignMagikarpRecordStage.FieldScript, index, instance),
            Dialogue = null, SignMessage = null, Motions = [], PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", index) } };
        decision = new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }

    private static ushort MagikarpRecordValue(CampaignState state)
    {
        int value = state.Script.Variables.GetValueOrDefault(MagikarpRecordVariable, MagikarpSizeRecordRules.DefaultRecord);
        return value is >= ushort.MinValue and <= ushort.MaxValue ? (ushort)value : throw new InvalidDataException("magikarp_record_value_invalid");
    }

    private bool TryApplyMagikarpRecordEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (TryApplyMagikarpRecordGiftEffect(state, effect, out next)) return true;
        if (!_content.SuperRodEnabled || effect.Source.Path != MagikarpSizeRecordRules.ScriptPath ||
            effect.Opcode is not ("special" or "specialvar" or "waitstate")) return false;
        var instruction = MagikarpRecordInstruction(state);
        if (instruction is null || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode ||
            !instruction.Args.SequenceEqual(effect.Args)) throw new InvalidDataException("magikarp_record_instruction_context");
        if (effect.Opcode == "waitstate")
        {
            if (!InMagikarpRecordFisher(state) || effect.Source.Symbol != MagikarpRecordCheck || !effect.Args.IsEmpty ||
                state.MagikarpRecord?.Stage != CampaignMagikarpRecordStage.Party || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("magikarp_record_wait_context");
            next = state with { Dialogue = null, SignMessage = null, PresentationTicks = 0,
                Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
            return true;
        }
        bool returns = effect.Opcode == "specialvar";
        if (effect.Args.Length != (returns ? 2 : 1) || returns && effect.Args[0] != "VAR_RESULT")
            throw new InvalidDataException("magikarp_record_special_operands");
        string function = effect.Args[returns ? 1 : 0];
        bool sign = function == "GetMagikarpSizeRecordInfo" && effect.Source.Symbol == MagikarpRecordSignSet;
        if (returns != (function == "DoesPlayerPartyContainSpecies") ||
            !(sign ? InMagikarpRecordSign(state) : effect.Source.Symbol == MagikarpRecordCheck && InMagikarpRecordFisher(state)) ||
            state.Script.Wait?.Kind != StoryWaitKind.NativeEffect)
            throw new InvalidDataException("magikarp_record_source_context");
        switch (function)
        {
            case "QuestLog_CutRecording":
                // Source gameplay gate has already skipped the entire revisit during Quest Log playback.
                if (state.Script.QuestLogState != 0) throw new InvalidDataException("magikarp_record_questlog_context");
                return true;
            case "DoesPlayerPartyContainSpecies":
                if (state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != 129)
                    throw new InvalidDataException("magikarp_record_species_context");
                // MON_DATA_SPECIES_OR_EGG excludes eggs, but not fainted party members.
                next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT",
                    state.Party.Any(mon => !mon.IsEgg && mon.Pokemon.Species == MagikarpSizeRecordRules.MagikarpId) ? 1 : 0) } };
                return true;
            case "GetMagikarpSizeRecordInfo":
                next = state with { TextBuffers = state.TextBuffers
                    .SetItem("STR_VAR_1", _content.PokemonContent?.SpeciesNames[129] ?? throw new InvalidDataException("magikarp_record_species_name_missing"))
                    .SetItem("STR_VAR_3", MagikarpSizeRecordRules.FormatInches(MagikarpSizeRecordRules.SizeTenthsCentimeters(MagikarpRecordValue(state)))) };
                return true;
            case "ChoosePartyMon":
                if (state.Party.IsDefaultOrEmpty || state.MagikarpRecord is not null)
                    throw new InvalidDataException("magikarp_record_party_context");
                next = state with { MagikarpRecord = new(CampaignMagikarpRecordStage.Party) }; return true;
            case "CompareMagikarpSize":
                if (state.MagikarpRecord is not { Stage: CampaignMagikarpRecordStage.FieldScript, Compared: false } selected ||
                    state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != selected.PartyIndex ||
                    state.Script.Variables.GetValueOrDefault("VAR_RESULT", -1) != selected.PartyIndex ||
                    MagikarpRecordSelection(state, selected.PartyIndex, selected.InstanceId) is not null)
                    throw new InvalidDataException("magikarp_record_target_context");
                var result = MagikarpSizeRecordRules.Compare(selected.PartyIndex, state.Party[selected.PartyIndex], MagikarpRecordValue(state));
                var buffers = state.TextBuffers;
                if (result.CandidateSizeTenthsCentimeters is { } candidate && result.PreviousSizeTenthsCentimeters is { } previous)
                    buffers = buffers.SetItem("STR_VAR_2", MagikarpSizeRecordRules.FormatInches(candidate))
                        .SetItem("STR_VAR_3", MagikarpSizeRecordRules.FormatInches(previous));
                var variables = state.Script.Variables.SetItem("VAR_RESULT", (int)result.Outcome);
                // Preserve the missing/default-zero variable until the source comparison actually writes it.
                if (result.Outcome == MagikarpSizeComparisonOutcome.NewRecord) variables = variables.SetItem(MagikarpRecordVariable, result.UpdatedRecord);
                next = state with { TextBuffers = buffers, MagikarpRecord = selected with { Compared = true },
                    Script = state.Script with { Variables = variables } };
                return true;
            default: throw new InvalidDataException("magikarp_record_special_unsupported: " + function);
        }
    }

    private bool TryApplyMagikarpRecordGiftEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.SuperRodEnabled || state.MagikarpRecord is null || effect.Opcode is not
            ("additem" or "checkitemtype" or "bufferitemnameplural")) return false;
        const string reward = "Route12_FishingHouse_EventScript_NewRecordMagikarp";
        const string obtain = "data/scripts/obtain_item.inc";
        if (!InMagikarpRecordFisher(state) || state.MagikarpRecord is not { Compared: true, Stage: CampaignMagikarpRecordStage.FieldScript } selected ||
            MagikarpRecordSelection(state, selected.PartyIndex, selected.InstanceId) is not null ||
            !state.Script.Flags.GetValueOrDefault("FLAG_GOT_RECORD_SETTING_MAGIKARP") ||
            state.Party[selected.PartyIndex].IsEgg || state.Party[selected.PartyIndex].Pokemon.Species != MagikarpSizeRecordRules.MagikarpId ||
            state.Script.Wait?.Kind != StoryWaitKind.NativeEffect || effect.Source.Path != obtain)
            throw new InvalidDataException("magikarp_record_gift_context");
        var caller = state.Script.Stack.FirstOrDefault(f => f.ProgramId == reward);
        var callerProgram = _content.FindProgram(reward);
        if (caller is null || callerProgram is null || caller.InstructionIndex <= 0 || caller.InstructionIndex > callerProgram.Instructions.Length ||
            callerProgram.Instructions[caller.InstructionIndex - 1] is not { Op: StoryOp.Call, Opcode: "call" } call ||
            call.Source.Path != MagikarpSizeRecordRules.ScriptPath || call.Source.Symbol != reward || !call.Args.SequenceEqual(new[] { "Std_ObtainItem" }))
            throw new InvalidDataException("magikarp_record_gift_caller");
        var instruction = MagikarpRecordInstruction(state);
        bool add = effect.Opcode == "additem", buffer = effect.Opcode == "bufferitemnameplural";
        string[] expectedSource = add ? ["VAR_0x8000", "VAR_0x8001"] : buffer ? ["STR_VAR_2", "VAR_0x8000", "VAR_0x8001"] : ["VAR_0x8000"];
        string[] expectedValues = add ? ["6", "1"] : buffer ? ["STR_VAR_2", "6", "1"] : ["6"];
        if (instruction is not { Op: StoryOp.Special } || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode ||
            effect.Source.Symbol != (add ? "Std_ObtainItem" : "EventScript_ObtainItemMessage") ||
            !instruction.Args.SequenceEqual(expectedSource) || !effect.Args.SequenceEqual(expectedValues) ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8000", -1) != 6 || state.Script.Variables.GetValueOrDefault("VAR_0x8001", -1) != 1 ||
            add == selected.GiftAttempted)
            throw new InvalidDataException("magikarp_record_gift_operands");
        var item = ShopCatalog.Items.SingleOrDefault(i => i.SourceItemSymbol == "ITEM_NET_BALL" && i.SourceItemId == 6 &&
            i.Id.Value == "pret:ITEM_NET_BALL" && i.Pocket == "POCKET_POKE_BALLS") ?? throw new InvalidDataException("magikarp_record_net_ball_missing");
        if (buffer)
        { next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", item.Name) }; return true; }
        if (!add)
        {
            if (!_content.TryConstant("POCKET_POKE_BALLS", out int pocket)) throw new InvalidDataException("magikarp_record_ball_pocket_missing");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", pocket) } }; return true;
        }
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } error) throw new InvalidDataException(error);
        // Add calls the shared CheckSpace first; original giveitem has no separate checkitemspace.
        var result = EarlyInventoryAddRules.Add(ShopCatalog, ShopInventoryView(inventory), item.Id, 1);
        if (!result.Succeeded && result.Error is not ("inventory_destination_full" or "inventory_stack_full")) throw new InvalidDataException(result.Error);
        next = state with { Inventory = result.Succeeded ? MergeShopInventory(inventory, result.State) : inventory,
            MagikarpRecord = selected with { GiftAttempted = true },
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Succeeded ? 1 : 0) } };
        return true;
    }

}

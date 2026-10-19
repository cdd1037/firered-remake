using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Items;

namespace Game.Application;

public enum CampaignSizeRecordStage { FieldScript, Party }
public sealed record CampaignSizeRecord(CampaignSizeRecordStage Stage,
    int PartyIndex = -1, long? InstanceId = null, bool Compared = false, bool GiftAttempted = false, ushort? PendingRecord = null, bool? PreviousRecordFlag = null, bool GiftSucceeded = false);

/// <summary>Shared source-script size-record service. Selection, identity checks, chart,
/// comparison and gift delivery are common; the explicit commit policy preserves source
/// compatibility or waits for successful delivery as authorized for Water Path.</summary>
public sealed partial class CampaignSession
{
    private enum SizeRecordCommitTiming { Comparison, SuccessfulGift }
    private sealed record SizeRecordService(string Map, string MapSymbol, string Entry, string Check,
        string Sign, string SignSet, string Variable, string RecordFlag, string? RequiredFlag,
        string Graphics, int ActorX, int ActorY, string SpeciesSymbol,
        string InfoSpecial, string CompareSpecial, string Reward, string ItemSymbol, SizeRecordCommitTiming CommitTiming)
    {
        public string ScriptPath => "data/maps/" + Entry[..Entry.IndexOf("_EventScript_", StringComparison.Ordinal)] + "/scripts.inc";
        public Game.Domain.Ids.SpeciesId Species => new("pret:" + SpeciesSymbol);
    }

    // Source service declarations. The same callback, selection, sizing, gift, and UI path serves both.
    private static readonly SizeRecordService[] SizeRecordServices =
    [
        new("map.route-12-fishing-house", "MAP_ROUTE12_FISHING_HOUSE", "Route12_FishingHouse_EventScript_FishingGuruBrother",
            "Route12_FishingHouse_EventScript_CheckMagikarpRecord", "Route12_FishingHouse_EventScript_MagikarpRecordSign",
            "Route12_FishingHouse_EventScript_MagikarpRecordSignRecordSet", "VAR_MAGIKARP_SIZE_RECORD", "FLAG_GOT_RECORD_SETTING_MAGIKARP",
            "FLAG_GOT_SUPER_ROD", "OBJ_EVENT_GFX_FISHER", 4, 4, "SPECIES_MAGIKARP",
            "GetMagikarpSizeRecordInfo", "CompareMagikarpSize", "Route12_FishingHouse_EventScript_NewRecordMagikarp", "ITEM_NET_BALL", SizeRecordCommitTiming.Comparison),
        new("map.six-island-water-path-house1", "MAP_SIX_ISLAND_WATER_PATH_HOUSE1", "SixIsland_WaterPath_House1_EventScript_Beauty",
            "SixIsland_WaterPath_House1_EventScript_Beauty", "SixIsland_WaterPath_House1_EventScript_SizeRecord",
            "SixIsland_WaterPath_House1_EventScript_SizeRecordNonEmpty", "VAR_HERACROSS_SIZE_RECORD", "FLAG_GOT_NEST_BALL_FROM_WATER_PATH_HOUSE_1",
            null, "OBJ_EVENT_GFX_BEAUTY", 3, 4, "SPECIES_HERACROSS",
            "GetHeracrossSizeRecordInfo", "CompareHeracrossSize", "SixIsland_WaterPath_House1_EventScript_ShownBigHeracross", "ITEM_NEST_BALL", SizeRecordCommitTiming.SuccessfulGift)
    ];

    private int SizeRecordSpeciesNumber(SizeRecordService service) => _content.TryConstant(service.SpeciesSymbol, out int id)
        ? id : throw new InvalidDataException("size_record_species_missing");
    private int SizeRecordHeight(SizeRecordService service) => _content.SourceSpeciesHeightDecimeters(SizeRecordSpeciesNumber(service))
        ?? throw new InvalidDataException("size_record_height_missing");
    private int SizeRecordItemNumber(SizeRecordService service) => _content.TryConstant(service.ItemSymbol, out int id)
        ? id : throw new InvalidDataException("size_record_item_missing");

    private SizeRecordService? RecordService(CampaignState state) => SizeRecordServices.FirstOrDefault(s =>
        s.Map == state.World.Player.MapId.Value && _content.MapDefinition(state.World.Player.MapId).SourceMapSymbol == s.MapSymbol);

    private bool InSizeRecordHouse(CampaignState state) => RecordService(state) is not null &&
        state.Script.Locked && !state.Script.Completed;

    private bool InSizeRecordActor(CampaignState state)
    {
        if (!InSizeRecordHouse(state) || RecordService(state) is not { } service ||
            service.RequiredFlag is { } flag && !state.Script.Flags.GetValueOrDefault(flag) ||
            state.LastTalked is not { } actor || !state.Actors.TryGetValue(actor, out var npc) || !npc.Active || npc.Invisible ||
            npc.SourceLocalId != 1 || npc.GraphicsId != service.Graphics || npc.ScriptId != service.Entry ||
            npc.TemplateX != service.ActorX || npc.TemplateY != service.ActorY || npc.Elevation != 3) return false;
        var definition = _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == actor);
        return definition is { SourceLocalId: 1, IsClone: false, Elevation: 3 } && definition.GraphicsId == service.Graphics &&
            definition.ScriptId == service.Entry && definition.X == service.ActorX && definition.Y == service.ActorY;
    }

    private bool InSizeRecordSign(CampaignState state)
    {
        if (!InSizeRecordHouse(state) || RecordService(state) is not { } service || !state.Script.Flags.GetValueOrDefault(service.RecordFlag)) return false;
        var player = state.World.Player; var (dx, dy) = Delta(player.Facing);
        return _content.MapDefinition(player.MapId).BackgroundTriggers.Any(b => b.X == player.X + dx && b.Y == player.Y + dy && b.Elevation == 0 &&
                b.ScriptId == service.Sign && b.HiddenItem is null && b.PlayerFacing is null && !b.Underfoot);
    }

    private StoryInstruction? SizeRecordInstruction(CampaignState state)
    {
        if (state.Script.Stack.IsEmpty) return null;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length ? program.Instructions[index] : null;
    }

    private bool AtSizeRecordWait(CampaignState state)
    {
        if (!InSizeRecordActor(state) || state.Script.Wait?.Kind != StoryWaitKind.PartySelection ||
            SizeRecordInstruction(state) is not { Op: StoryOp.WaitState, Opcode: "waitstate" } instruction ||
            instruction.Source.Path != RecordService(state)!.ScriptPath || instruction.Source.Symbol != RecordService(state)!.Check || !instruction.Args.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId)!; int previous = frame.InstructionIndex - 2;
        return previous >= 0 && program.Instructions[previous] is { Op: StoryOp.Special, Opcode: "special" } choose &&
            choose.Source.Path == RecordService(state)!.ScriptPath && choose.Source.Symbol == RecordService(state)!.Check &&
            choose.Args.SequenceEqual(new[] { "ChoosePartyMon" });
    }

    private static string? SizeRecordSelection(CampaignState state, int index, long? instanceId) =>
        MoveTutorRules.ValidateSelection(state.Party, index, instanceId)?.Replace("move_tutor_", "size_record_", StringComparison.Ordinal);

    private bool TryDecideSizeRecord(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        if (command.Kind.StartsWith("magikarp_record_", StringComparison.Ordinal)) command = command with { Kind = command.Kind.Replace("magikarp_record_", "size_record_", StringComparison.Ordinal) };
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        decision = Reject("size_record_not_pending");
        if (state.SizeRecord is not { } scene) return command.Kind.StartsWith("size_record_", StringComparison.Ordinal);
        if (!InSizeRecordActor(state)) { decision = Reject("size_record_callback_context"); return true; }
        if (scene.Stage == CampaignSizeRecordStage.FieldScript)
        {
            if (command.Kind == "ack") return false;
            decision = Reject("size_record_event_active"); return true;
        }
        if (!AtSizeRecordWait(state)) { decision = Reject("size_record_callback_context"); return true; }
        int index = PokemonSizeRecordRules.PartyCapacity; long? instance = null;
        if (command.Kind != "size_record_cancel")
        {
            if (command.Kind != "size_record_choose") { decision = Reject("size_record_selection_required"); return true; }
            var error = SizeRecordSelection(state, command.PartyIndex, command.TargetInstanceId);
            if (error is not null) { decision = Reject(error); return true; }
            index = command.PartyIndex; instance = state.Party[index].InstanceId;
        }
        var next = state with { SizeRecord = new(CampaignSizeRecordStage.FieldScript, index, instance),
            Dialogue = null, SignMessage = null, Motions = [], PresentationTicks = 0, PresentationEffects = [], ScreenIsBlack = false,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", index) } };
        decision = new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }

    private ushort SizeRecordValue(CampaignState state)
    {
        int value = state.Script.Variables.GetValueOrDefault(RecordService(state)!.Variable, PokemonSizeRecordRules.DefaultRecord);
        return value is >= ushort.MinValue and <= ushort.MaxValue ? (ushort)value : throw new InvalidDataException("size_record_value_invalid");
    }

    private bool TryApplySizeRecordEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (TryApplySizeRecordGiftEffect(state, effect, out next)) return true;
        if (RecordService(state) is not { } service || effect.Source.Path != service.ScriptPath ||
            effect.Opcode is not ("special" or "specialvar" or "waitstate")) return false;
        var instruction = SizeRecordInstruction(state);
        if (instruction is null || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode ||
            !instruction.Args.SequenceEqual(effect.Args)) throw new InvalidDataException("size_record_instruction_context");
        if (effect.Opcode == "waitstate")
        {
            if (!InSizeRecordActor(state) || effect.Source.Symbol != RecordService(state)!.Check || !effect.Args.IsEmpty ||
                state.SizeRecord?.Stage != CampaignSizeRecordStage.Party || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("size_record_wait_context");
            next = state with { Dialogue = null, SignMessage = null, PresentationTicks = 0,
                Script = state.Script with { Wait = state.Script.Wait with { Kind = StoryWaitKind.PartySelection } } };
            return true;
        }
        bool returns = effect.Opcode == "specialvar";
        if (effect.Args.Length != (returns ? 2 : 1) || returns && effect.Args[0] != "VAR_RESULT")
            throw new InvalidDataException("size_record_special_operands");
        string function = effect.Args[returns ? 1 : 0];
        bool sign = function == service.InfoSpecial && effect.Source.Symbol == RecordService(state)!.SignSet;
        if (returns != (function == "DoesPlayerPartyContainSpecies") ||
            !(sign ? InSizeRecordSign(state) : effect.Source.Symbol == RecordService(state)!.Check && InSizeRecordActor(state)) ||
            state.Script.Wait?.Kind != StoryWaitKind.NativeEffect)
            throw new InvalidDataException("size_record_source_context");
        switch (function)
        {
            case "QuestLog_CutRecording":
                // Source gameplay gate has already skipped the entire revisit during Quest Log playback.
                if (state.Script.QuestLogState != 0) throw new InvalidDataException("size_record_questlog_context");
                return true;
            case "DoesPlayerPartyContainSpecies":
                if (state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != SizeRecordSpeciesNumber(service))
                    throw new InvalidDataException("size_record_species_context");
                // MON_DATA_SPECIES_OR_EGG excludes eggs, but not fainted party members.
                next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT",
                    state.Party.Any(mon => !mon.IsEgg && mon.Pokemon.Species == service.Species) ? 1 : 0) } };
                return true;
            case var info when info == service.InfoSpecial:
                next = state with { TextBuffers = state.TextBuffers
                    .SetItem("STR_VAR_1", SpeciesName(SizeRecordSpeciesNumber(service)))
                    .SetItem("STR_VAR_3", PokemonSizeRecordRules.FormatInches(PokemonSizeRecordRules.SizeTenthsCentimeters(SizeRecordValue(state), SizeRecordHeight(service)))) };
                return true;
            case "ChoosePartyMon":
                if (state.Party.IsDefaultOrEmpty || state.SizeRecord is not null)
                    throw new InvalidDataException("size_record_party_context");
                next = state with { SizeRecord = new(CampaignSizeRecordStage.Party) }; return true;
            case var compare when compare == service.CompareSpecial:
                if (state.SizeRecord is not { Stage: CampaignSizeRecordStage.FieldScript, Compared: false } selected ||
                    state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != selected.PartyIndex ||
                    state.Script.Variables.GetValueOrDefault("VAR_RESULT", -1) != selected.PartyIndex ||
                    SizeRecordSelection(state, selected.PartyIndex, selected.InstanceId) is not null)
                    throw new InvalidDataException("size_record_target_context");
                var result = PokemonSizeRecordRules.Compare(selected.PartyIndex, state.Party[selected.PartyIndex], SizeRecordValue(state), service.Species, SizeRecordHeight(service));
                var buffers = state.TextBuffers;
                if (result.CandidateSizeTenthsCentimeters is { } candidate && result.PreviousSizeTenthsCentimeters is { } previous)
                    buffers = buffers.SetItem("STR_VAR_2", PokemonSizeRecordRules.FormatInches(candidate))
                        .SetItem("STR_VAR_3", PokemonSizeRecordRules.FormatInches(previous));
                var variables = state.Script.Variables.SetItem("VAR_RESULT", (int)result.Outcome);
                // Preserve the missing/default-zero variable until the source comparison actually writes it.
                if (service.CommitTiming == SizeRecordCommitTiming.Comparison && result.Outcome == PokemonSizeComparisonOutcome.NewRecord) variables = variables.SetItem(RecordService(state)!.Variable, result.UpdatedRecord);
                next = state with { TextBuffers = buffers, SizeRecord = selected with { Compared = true, PendingRecord = result.UpdatedRecord,
                    PreviousRecordFlag = state.Script.Flags.TryGetValue(service.RecordFlag, out bool previousFlag) ? previousFlag : null },
                    Script = state.Script with { Variables = variables } };
                return true;
            default: throw new InvalidDataException("size_record_special_unsupported: " + function);
        }
    }

    private bool TryApplySizeRecordGiftEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (RecordService(state) is not { } service || state.SizeRecord is null || effect.Opcode is not
            ("additem" or "checkitemtype" or "bufferitemnameplural")) return false;
        string reward = service.Reward;
        const string obtain = "data/scripts/obtain_item.inc";
        if (!InSizeRecordActor(state) || state.SizeRecord is not { Compared: true, Stage: CampaignSizeRecordStage.FieldScript } selected ||
            SizeRecordSelection(state, selected.PartyIndex, selected.InstanceId) is not null ||
            service.CommitTiming == SizeRecordCommitTiming.Comparison && !state.Script.Flags.GetValueOrDefault(service.RecordFlag) ||
            state.Party[selected.PartyIndex].IsEgg || state.Party[selected.PartyIndex].Pokemon.Species != service.Species ||
            state.Script.Wait?.Kind != StoryWaitKind.NativeEffect || effect.Source.Path != obtain)
            throw new InvalidDataException("size_record_gift_context");
        var caller = state.Script.Stack.FirstOrDefault(f => f.ProgramId == reward);
        var callerProgram = _content.FindProgram(reward);
        if (caller is null || callerProgram is null || caller.InstructionIndex <= 0 || caller.InstructionIndex > callerProgram.Instructions.Length ||
            callerProgram.Instructions[caller.InstructionIndex - 1] is not { Op: StoryOp.Call, Opcode: "call" } call ||
            call.Source.Path != RecordService(state)!.ScriptPath || call.Source.Symbol != reward || !call.Args.SequenceEqual(new[] { "Std_ObtainItem" }))
            throw new InvalidDataException("size_record_gift_caller");
        var instruction = SizeRecordInstruction(state);
        bool add = effect.Opcode == "additem", buffer = effect.Opcode == "bufferitemnameplural";
        string[] expectedSource = add ? ["VAR_0x8000", "VAR_0x8001"] : buffer ? ["STR_VAR_2", "VAR_0x8000", "VAR_0x8001"] : ["VAR_0x8000"];
        string itemNumber = SizeRecordItemNumber(service).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] expectedValues = add ? [itemNumber, "1"] : buffer ? ["STR_VAR_2", itemNumber, "1"] : [itemNumber];
        if (instruction is not { Op: StoryOp.Special } || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode ||
            effect.Source.Symbol != (add ? "Std_ObtainItem" : "EventScript_ObtainItemMessage") ||
            !instruction.Args.SequenceEqual(expectedSource) || !effect.Args.SequenceEqual(expectedValues) ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8000", -1) != SizeRecordItemNumber(service) || state.Script.Variables.GetValueOrDefault("VAR_0x8001", -1) != 1 ||
            add == selected.GiftAttempted)
            throw new InvalidDataException("size_record_gift_operands");
        var item = ShopCatalog.Items.SingleOrDefault(i => i.SourceItemSymbol == service.ItemSymbol && i.SourceItemId == SizeRecordItemNumber(service) &&
            i.Id.Value == "pret:" + service.ItemSymbol && i.Pocket == "POCKET_POKE_BALLS") ?? throw new InvalidDataException("size_record_reward_missing");
        if (buffer)
        { next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", item.Name) }; return true; }
        if (!add)
        {
            if (!_content.TryConstant("POCKET_POKE_BALLS", out int pocket)) throw new InvalidDataException("size_record_ball_pocket_missing");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", pocket) } }; return true;
        }
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } error) throw new InvalidDataException(error);
        // Add calls the shared CheckSpace first; original giveitem has no separate checkitemspace.
        var result = EarlyInventoryAddRules.Add(ShopCatalog, inventory, item.Id, 1);
        if (!result.Succeeded && result.Error is not ("inventory_destination_full" or "inventory_stack_full")) throw new InvalidDataException(result.Error);
        next = state with { Inventory = result.Succeeded ? result.State : inventory,
            SizeRecord = selected with { GiftAttempted = true, GiftSucceeded = result.Succeeded },
            Script = state.Script with {
                Variables = result.Succeeded && service.CommitTiming == SizeRecordCommitTiming.SuccessfulGift
                    ? state.Script.Variables.SetItem(service.Variable, selected.PendingRecord ?? throw new InvalidDataException("size_record_pending_missing")).SetItem("VAR_RESULT", 1)
                    : state.Script.Variables.SetItem("VAR_RESULT", result.Succeeded ? 1 : 0),
                Flags = result.Succeeded && service.CommitTiming == SizeRecordCommitTiming.SuccessfulGift
                    ? state.Script.Flags.SetItem(service.RecordFlag, true) : state.Script.Flags } };
        return true;
    }

    private CampaignState PreserveUncommittedSizeRecord(CampaignState state)
    {
        if (state.SizeRecord is not { Compared: true, GiftSucceeded: false } scene ||
            RecordService(state) is not { CommitTiming: SizeRecordCommitTiming.SuccessfulGift } service) return state;
        var flags = scene.PreviousRecordFlag is { } previous
            ? state.Script.Flags.SetItem(service.RecordFlag, previous) : state.Script.Flags.Remove(service.RecordFlag);
        return state with { Script = state.Script with { Flags = flags } };
    }

}

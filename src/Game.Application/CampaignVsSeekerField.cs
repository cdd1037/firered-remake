using System.Collections.Immutable;
using System.Globalization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed record CampaignVsSeekerEffect(string Origin, string Phase, int Tick, bool Cycling,
    ImmutableArray<VsSeekerTrainer> Trainers, VsSeekerUseResult? Response, string? Message);

public sealed partial class CampaignSession
{
    private string VsSeekerText(string symbol, CampaignState state)
    {
        var template = (_content.VsSeekerTexts.TryGetValue(symbol, out var text) ? text : string.Join("\n\n", _content.TextPages(symbol)))
            .Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal).TrimEnd('$')
            .Replace(@"\p", "\n\n", StringComparison.Ordinal).Replace(@"\l", "\n", StringComparison.Ordinal).Replace(@"\n", "\n", StringComparison.Ordinal);
        return CampaignSourceText.Bind(template, token => token switch
        { "STR_VAR_1" => (100 - VsProgress(state).State.ChargeSteps).ToString(CultureInfo.InvariantCulture),
            "PLAYER" => state.PlayerName, _ => null });
    }

    private Decision<CampaignState> UseVsSeeker(CampaignState state, CampaignCommand command, bool registered = false)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!VsSeekerEnabled || (registered ? state.RegisteredItem : command.Text) != VsSeekerItem.Value)
            return No("vs_seeker_item_context");
        if (registered ? state.ItemMenu is not null : state.ItemMenu is not { Origin: "bag", View: "bag", TargetInstanceId: null })
            return No("vs_seeker_callback_context");
        if (!IsStableFieldForParty(state with { ItemMenu = null })) return No("vs_seeker_field_context");
        if (ValidateCampaignInventory(state.Inventory) is {} error) return No(error);
        if (!OwnsVsSeeker(state)) return No("vs_seeker_not_owned");
        var map = _content.MapDefinition(state.World.Player.MapId);
        var trainers = VsSeekerRules.IsMapAllowed(_content.VsSeeker!, map.SourceMapSymbol, map.MapType) ? VsTrainers(state) : [];
        var guard = VsSeekerRules.CanUse(_content.VsSeeker!, VsProgress(state).State, true,
            map.SourceMapSymbol, map.MapType, trainers, state.World.Player.X, state.World.Player.Y, VsFought(state));
        string? message = guard switch {
            VsSeekerUseGuard.CanUse => null,
            VsSeekerUseGuard.InvalidMap => "gText_OakForbidsUseOfItemHere",
            VsSeekerUseGuard.NotCharged => "VSSeeker_Text_BatteryNotChargedNeedXSteps",
            VsSeekerUseGuard.NoOneInRange => "VSSeeker_Text_NoTrainersWithinRange",
            _ => throw new InvalidDataException("vs_seeker_guard") };
        if (guard == VsSeekerUseGuard.InvalidMap && !registered)
            return new(CommandStatus.Accepted, state with { ItemMenu = state.ItemMenu! with {
                View = "message", MessageSymbol = message, ReturnView = "bag" } });
        return new(CommandStatus.Accepted, state with { ItemMenu = null,
            VsSeekerEffect = new(registered ? "registered" : "bag", message is null ? "scan" : "message",
                0, state.IsCycling, trainers, null, message is null ? null : VsSeekerText(message, state)) });
    }

    private Decision<CampaignState> DecideVsSeeker(CampaignState state, CampaignCommand command)
    {
        var effect = state.VsSeekerEffect!;
        if (command.Kind == "vs_seeker_close" && effect.Phase == "message")
            return new(CommandStatus.Accepted, state with { VsSeekerEffect = null });
        if (command.Kind != "vs_seeker_tick" || effect.Phase is not ("scan" or "wait") || command.Quantity is < 1 or > 60)
            return new(CommandStatus.Rejected, state, "vs_seeker_active");
        // Frame advancement is authoritative. Rendering may batch frames, never decide RNG or outcomes.
        int left = command.Quantity;
        while (left-- > 0 && state.VsSeekerEffect is { Phase: "scan" or "wait" } active)
        {
            int tick = active.Tick + 1;
            if (active.Phase == "scan" && tick >= (active.Cycling ? VsSeekerTiming.CyclingScanTicks : VsSeekerTiming.NormalScanTicks))
            {
                var result = VsSeekerRules.ResolveResponse(_content.VsSeeker!, VsProgress(state).State,
                    active.Trainers, state.World.Player.X, state.World.Player.Y, VsFought(state), state.World.Rng);
                var actors = state.Actors;
                foreach (var reaction in result.Reactions.Where(r => r.Response == VsSeekerTrainerResponse.Rematch))
                {
                    var actor = actors.Values.Single(a => a.SourceLocalId == reaction.LocalId);
                    actors = actors.SetItem(actor.LocalId, actor with { Facing = Game.Domain.World.Direction.South });
                }
                state = state with { Actors = actors, World = state.World with { Rng = result.Rng },
                    VsSeekerProgress = VsProgress(state) with { State = result.State },
                    Script = state.Script with { Flags = state.Script.Flags.SetItem(VsSeekerChargingFlag, result.State.Charging) },
                    VsSeekerEffect = active with { Phase = "wait", Tick = 0, Response = result } };
            }
            else if (active.Phase == "wait" && tick >= VsSeekerTiming.WaitTicks)
            {
                var response = active.Response ?? throw new InvalidDataException("vs_seeker_response_missing");
                // Original idle movement changes begin only after the player's 48-frame movement finishes.
                if (response.Response == VsSeekerResponse.FoundRematches) state = ApplyVsMovementChanges(state, response.Movements);
                state = state with { VsSeekerEffect = response.Response == VsSeekerResponse.NoResponse
                    ? active with { Phase = "message", Tick = tick, Message = VsSeekerText("VSSeeker_Text_TrainersNotReady", state) }
                    : null };
            }
            else state = state with { VsSeekerEffect = active with { Tick = tick } };
        }
        return new(CommandStatus.Accepted, state);
    }

    private bool TryApplyVsSeekerFieldEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!VsSeekerEnabled || effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal)
            ? state.Script.Variables.GetValueOrDefault(token) : int.TryParse(token, out int n) ? n
            : _content.TryConstant(token, out n) ? n : throw new InvalidDataException("vs_seeker_gift_operand:" + token);
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        var args = effect.Args;
        if (args.Length < (buffer ? 2 : 1) || Value(args[buffer ? 1 : 0]) != 362) return false;
        var source = _content.VsSeeker!.Acquisition;
        if (_content.MapDefinition(state.World.Player.MapId).SourceMapSymbol != source.SourceMap ||
            state.LastTalked is null || !state.Actors.TryGetValue(state.LastTalked, out var actor) ||
            actor.SourceLocalId != source.LocalId || actor.ScriptId != source.ScriptLabel || !actor.Active ||
            !state.Script.Locked || !state.Script.Flags.GetValueOrDefault(source.OwnershipFlag) ||
            effect.Source.Path is not ("data/event_scripts.s" or "data/scripts/obtain_item.inc"))
            throw new InvalidDataException("vs_seeker_gift_source");
        CampaignState Result(int value) => state with { Script = state.Script with {
            Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (args[0] != "STR_VAR_2" || args.Length > 2 && Value(args[2]) != 1) throw new InvalidDataException("vs_seeker_gift_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], "VS SEEKER") }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(Value("POCKET_KEY_ITEMS")); return true; }
        if (args.Length > 2 || args.Length == 2 && Value(args[1]) != 1) throw new InvalidDataException("vs_seeker_gift_quantity");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is {} error) throw new InvalidDataException(error);
        var keys = inventory.KeyItems ?? [];
        // The shared shop projection deliberately excludes unrelated service keys and
        // machines. Capacity is checked against the real pocket before that projection.
        if (OpeningInventoryRules.Quantity(keys, VsSeekerItem) != 0 || keys.Length >= ShopCatalog.QuestLimits.KeyItemSlots)
        { next = Result(0); return true; }
        var view = inventory;
        var result = effect.Opcode == "additem" ? EarlyInventoryAddRules.Add(ShopCatalog, view, VsSeekerItem, 1)
            : EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, VsSeekerItem, 1);
        if (!result.Succeeded && result.Error is not ("inventory_stack_full" or "inventory_destination_full")) throw new InvalidDataException(result.Error);
        next = Result(result.Succeeded ? 1 : 0);
        if (result.Succeeded && effect.Opcode == "additem") next = next with { Inventory = inventory with { KeyItems = keys.Add(new(VsSeekerItem, 1)) }, VsSeekerProgress = CampaignVsSeekerProgress.Empty };
        return true;
    }
}

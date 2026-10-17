using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignFieldItemService(string Id);

public sealed partial class CampaignSession
{
    private CampaignState BeginFieldItemService(CampaignState state, string program)
    {
        var matches = _content.FieldItemServices.Where(s => s.Map == state.World.Player.MapId && s.ScriptId == program).ToArray();
        if (matches.Length == 0) return state;
        if (matches.Length != 1 || state.FieldItemService is not null || state.FieldPickup is not null)
            throw new InvalidDataException("field_item_service_entry_ambiguous");
        var service = matches[0]; var map = _content.MapDefinition(service.Map);
        if (string.IsNullOrEmpty(service.Id) || service.ProgramIds.IsDefaultOrEmpty ||
            !service.ProgramIds.Contains(program) || service.ItemSymbols.IsDefaultOrEmpty ||
            service.ProgramIds.Distinct(StringComparer.Ordinal).Count() != service.ProgramIds.Length ||
            service.ItemSymbols.Distinct(StringComparer.Ordinal).Count() != service.ItemSymbols.Length ||
            service.ProgramIds.Any(p => _content.FindProgram(p) is null))
            throw new InvalidDataException("field_item_service_definition_invalid");
        if (service.SourceLocalId is { } local)
        {
            // Source transitions may relocate an actor template; identity is stable across those moves.
            var source = map.Actors.SingleOrDefault(a => a.SourceLocalId == local);
            if (source is null || source.IsClone || source.ScriptId != program || state.LastTalked is not { } id ||
                !state.Actors.TryGetValue(id, out var actor) || actor.SourceLocalId != local || actor.ScriptId != program ||
                !actor.Active || actor.Invisible)
                throw new InvalidDataException("field_item_service_actor_context");
        }
        else
        {
            var player = state.World.Player; var delta = Delta(player.Facing);
            if (service.X is not { } x || service.Y is not { } y || x != player.X + delta.Item1 || y != player.Y + delta.Item2 ||
                !map.BackgroundTriggers.Any(b => b.X == x && b.Y == y && b.ScriptId == program && b.HiddenItem is null &&
                    (b.Elevation == 0 || b.Elevation == player.Elevation)))
                throw new InvalidDataException("field_item_service_background_context");
        }
        if (ValidateCampaignInventory(state.Inventory) is { } error) throw new InvalidDataException(error);
        return state with { FieldItemService = new(service.Id) };
    }

    // Imported source scripts retain all choices, flags, order and deferred-reward branches.
    // This adapter only supplies shared inventory primitives for their authenticated call graph.
    private bool TryApplyFieldItemServiceEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool cut = effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" });
        if (state.FieldItemService is not { } context || !(cut || effect.Opcode is
            "checkplayergender" or "checkitem" or "checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal) ? state.Script.Variables.GetValueOrDefault(token) :
            int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number :
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number) ? number :
            _content.TryConstant(token, out number) ? number : throw new InvalidDataException("field_item_service_operand_unknown: " + token);
        var service = _content.FieldItemServices.SingleOrDefault(s => s.Id == context.Id);
        if (service is null || service.Map != state.World.Player.MapId ||
            !service.ProgramIds.Contains(effect.Source.Symbol) || _content.FindProgram(effect.Source.Symbol) is not { } source ||
            !source.Instructions.Any(i => i.Source == effect.Source && i.Opcode == effect.Opcode && i.Args.Length == effect.Args.Length &&
                i.Args.Select((a, index) => a.StartsWith("VAR_", StringComparison.Ordinal)
                    ? Value(a) == Value(effect.Args[index]) : a == effect.Args[index]).All(matches => matches)))
            throw new InvalidDataException("field_item_service_effect_context");
        if (cut)
        {
            if (state.Script.QuestLogState != 0) throw new InvalidDataException("field_item_service_questlog_context");
            return true;
        }
        if (!state.Script.Locked) throw new InvalidDataException("field_item_service_unlocked");
        if (effect.Opcode == "checkplayergender")
        {
            if (!effect.Args.IsEmpty || state.PlayerGender is < 0 or > 1)
                throw new InvalidDataException("field_item_service_gender_context");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender) } };
            return true;
        }
        int sourceId = Value(effect.Args[effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal) ? 1 : 0]);
        if (!service.ItemSymbols.Any(symbol => _content.TryConstant(symbol, out int value) && value == sourceId))
            throw new InvalidDataException("field_item_service_item_not_declared");
        return TryApplyScriptInventoryEffect(state, effect, out next);
    }
}

using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // These are source script primitives, not services belonging to the first NPC
    // that happened to use them. Special service handlers run first and retain
    // their transaction guards; ordinary scripts share the source behavior here.
    private bool TryApplySharedFieldScriptEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var args = effect.Args;
        int Value(string token) => int.TryParse(token, out int n) ? n :
            state.Script.Variables.TryGetValue(token, out int v) ? v :
            _content.TryConstant(token, out int c) ? c :
            throw new InvalidDataException("field_script_operand_unknown: " + token);
        switch (effect.Opcode)
        {
            case "checkplayergender":
                if (!args.IsEmpty || state.PlayerGender is < 0 or > 1)
                    throw new InvalidDataException("field_script_gender_invalid");
                next = state with { Script = state.Script with {
                    Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender) } };
                return true;
            case "copyobjectxytoperm":
                if (args.Length != 1) throw new InvalidDataException("field_script_copy_actor_operands");
                CampaignActor? actor;
                string id = args[0];
                if (id is "LOCALID_LAST_TALKED" or "VAR_LAST_TALKED")
                    actor = state.LastTalked is {} last ? state.Actors.GetValueOrDefault(last) : null;
                else if (id.StartsWith("LOCALID_", StringComparison.Ordinal))
                    actor = state.Actors.GetValueOrDefault(id);
                else
                {
                    int localId = Value(id);
                    if (localId is < 0 or > 255) throw new InvalidDataException("field_script_local_id_invalid");
                    actor = state.Actors.Values.FirstOrDefault(a => a.SourceLocalId == localId);
                }
                // TryOverrideObjectEventTemplateCoords does nothing when the live
                // object is absent. Invisible but active objects still have coordinates.
                if (actor is { Active: true })
                    next = state with { Actors = state.Actors.SetItem(actor.LocalId,
                        actor with { TemplateX = actor.X, TemplateY = actor.Y }) };
                return true;
            case "showmoneybox":
            case "hidemoneybox":
            case "updatemoneybox":
                next = ApplySourceMoneyBox(state, effect);
                return true;
            case "checkmoney":
            case "removemoney":
                if (args.Length is < 1 or > 2) throw new InvalidDataException("field_script_money_operands");
                int amount = Value(args[0]);
                if (amount < 0) throw new InvalidDataException("source_money_amount");
                if (args.Length == 2 && Value(args[1]) != 0) return true;
                next = effect.Opcode == "checkmoney" ? SourceMoneyResult(state, amount) :
                    state with { Money = Math.Max(0, state.Money - amount) };
                return true;
            default:
                return false;
        }
    }
}

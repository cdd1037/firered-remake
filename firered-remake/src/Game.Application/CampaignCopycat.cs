using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string CopycatMap = "map.saffron-city-copycats-house-2f";
    private const string CopycatSource = "data/maps/SaffronCity_CopycatsHouse_2F/scripts.inc";
    private const string CopycatEntry = "SaffronCity_CopycatsHouse_2F_EventScript_Copycat";

    private bool TryApplyCopycatItemEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool cutRecording = effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" }) && effect.Source.Path == CopycatSource;
        bool gender = effect.Opcode == "checkplayergender" && (effect.Source.Path == CopycatSource ||
            effect.Source.Path == TutorPath && effect.Source.Symbol == "EventScript_MimicTaught");
        bool itemEffect = effect.Opcode is "checkitem" or "removeitem" && !effect.Args.IsEmpty && effect.Args[0] == "ITEM_POKE_DOLL";
        if (!cutRecording && !gender && !itemEffect) return false;
        if (_content.PokeDollShopStock is null || state.World.Player.MapId.Value != CopycatMap ||
            state.LastTalked is not {} actor || !state.Actors.TryGetValue(actor, out var npc) ||
            npc.SourceLocalId != 4 || npc.ScriptId != CopycatEntry || !npc.Active || npc.Invisible)
            throw new InvalidDataException("copycat_item_context");
        if (cutRecording)
        {
            if (effect.Source.Symbol != CopycatEntry || state.Script.QuestLogState != 0)
                throw new InvalidDataException("copycat_quest_log_context");
            return true;
        }
        if (!state.Script.Locked) throw new InvalidDataException("copycat_script_unlocked");
        if (gender)
        {
            if (!effect.Args.IsEmpty || state.PlayerGender is < 0 or > 1 ||
                effect.Source.Path == CopycatSource && effect.Source.Symbol != CopycatEntry)
                throw new InvalidDataException("copycat_gender_context");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender) } };
            return true;
        }
        if (effect.Args.Length > 2 || effect.Args.Length == 2 && SeviiValue(state, effect.Args[1]) != 1)
            throw new InvalidDataException("copycat_item_quantity");
        bool removing = effect.Opcode == "removeitem";
        if (removing ? effect.Source.Path != TutorPath || effect.Source.Symbol != "EventScript_MimicTutor" ||
            !TryMoveTutorContext(state, out var tutor) || tutor.Index != 7 || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1
            : effect.Source.Path != CopycatSource || effect.Source.Symbol != CopycatEntry)
            throw new InvalidDataException("copycat_item_source");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is {} error) throw new InvalidDataException(error);
        if (!removing)
        {
            int present = OpeningInventoryRules.Quantity(inventory.BagItems, ItemIds.PokeDollId) > 0 ? 1 : 0;
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", present) } };
            return true;
        }
        var result = EarlyInventoryAddRules.Remove(ShopCatalog, inventory, ItemIds.PokeDollId, 1);
        if (!result.Succeeded) throw new InvalidDataException("copycat_success_payment:" + result.Error);
        next = state with { Inventory = result.State,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 1) } };
        return true;
    }

}

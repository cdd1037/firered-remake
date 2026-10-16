using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignLaprasGift(long InstanceId, uint Personality, SixStats Ivs);

public sealed partial class CampaignSession
{
    private const string LaprasMap = "map.silph-co-7f";
    private const string LaprasSource = "data/maps/SilphCo_7F/scripts.inc";
    private const string LaprasScript = "SilphCo_7F_EventScript_LaprasGuy";
    private const string LaprasFlag = "FLAG_GOT_LAPRAS_FROM_SILPH";
    private const string PresidentMap = "map.silph-co-11f";
    private const string PresidentSource = "data/maps/SilphCo_11F/scripts.inc";
    private const string PresidentScript = "SilphCo_11F_EventScript_President";
    private const string MasterBallFlag = "FLAG_GOT_MASTER_BALL_FROM_SILPH";
    private static readonly ItemId SilphMasterBallId = new("pret:ITEM_MASTER_BALL");

    private bool InLaprasScript(CampaignState state) => state.World.Player.MapId.Value == LaprasMap &&
        state.LastTalked is { } actor && state.Actors.TryGetValue(actor, out var value) && value.SourceLocalId == 2 && value.ScriptId == LaprasScript;
    private bool InPresidentScript(CampaignState state) => state.World.Player.MapId.Value == PresidentMap &&
        state.LastTalked is { } actor && state.Actors.TryGetValue(actor, out var value) && value.SourceLocalId == 1 && value.ScriptId == PresidentScript;

    private string? ValidateSilphGifts(CampaignState state)
    {
        bool lapras = state.Script.Flags.GetValueOrDefault(LaprasFlag), master = state.Script.Flags.GetValueOrDefault(MasterBallFlag);
        int balls = OpeningInventoryRules.Quantity(state.Inventory?.Balls ?? [], SilphMasterBallId) +
            OpeningInventoryRules.Quantity(state.Inventory?.PcItems ?? [], SilphMasterBallId) +
            OwnedPokemon(state).Count(p => p.HeldItem == "ITEM_MASTER_BALL");
        if (balls > 1 || balls > 0 && !master) return "save_silph_master_ball_reward_mismatch";
        if ((state.LaprasGift is not null) != lapras) return "save_lapras_flag_inconsistent";
        if (state.LaprasGift is not { } gift) return null;
        if (gift.InstanceId < 2 || gift.InstanceId >= state.NextPokemonId || gift.Ivs is null || gift.Ivs.Values.Any(v => v is < 0 or > 31)) return "save_lapras_origin_invalid";
        var matching = OwnedPokemon(state).Where(p => p.InstanceId == gift.InstanceId).ToArray();
        if (matching.Length > 1) return "save_lapras_instance_mismatch";
        if (!state.CaughtSpecies.Contains(131) || !state.SeenSpecies.Contains(131)) return "save_lapras_instance_mismatch";
        if (matching.Length == 1)
        {
            var mon = matching[0];
            if (mon.Pokemon.SourceSpeciesId != 131 || mon.Pokemon.Personality != gift.Personality || mon.Pokemon.Ivs != gift.Ivs ||
                mon.MetLocation != "MAPSEC_SILPH_CO" || mon.MetLevel != 25 || mon.Ball != "ITEM_POKE_BALL" ||
                !state.CaughtSpecies.Contains(131) || !state.SeenSpecies.Contains(131) ||
                state.Exploration?.Captured is not { } captures || captures.IsDefault || captures.Any(c => c is null || c.InstanceId == gift.InstanceId) ||
                state.MagikarpGift?.InstanceId == gift.InstanceId || state.EeveeGift?.InstanceId == gift.InstanceId)
                return "save_lapras_instance_mismatch";
        }
        return null;
    }

    private bool TryApplySilphGiftEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var a = effect.Args;
        if (effect.Opcode == "givemon" && !a.IsDefaultOrEmpty && a[0] is "SPECIES_LAPRAS" or "131")
        {
            if (!InLaprasScript(state) || effect.Source.Path != LaprasSource || effect.Source.Symbol != LaprasScript ||
                !a.SequenceEqual(new[] { "SPECIES_LAPRAS", "25" }) || state.LaprasGift is not null || state.Script.Flags.GetValueOrDefault(LaprasFlag))
                throw new InvalidDataException("lapras_gift_context_invalid");
            // There is deliberately no Giovanni-victory condition in the original gift script.
            var received = GiveScriptGift(state, 131, 25, "MAPSEC_SILPH_CO");
            next = received.State;
            if (received.Gift is { } mon) next = next with { LaprasGift = new(mon.InstanceId, mon.Pokemon.Personality, mon.Pokemon.Ivs) };
            return true;
        }
        if (effect.Opcode == "checkplayergender" && effect.Source.Path == PresidentSource)
        {
            if (!InPresidentScript(state) || effect.Source.Symbol != PresidentScript || !a.IsEmpty)
                throw new InvalidDataException("silph_president_gender_context_invalid");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender) } };
            return true;
        }
        if (effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        int Value(string token) => int.TryParse(token, out int n) ? n : state.Script.Variables.TryGetValue(token, out int v) ? v :
            _content.TryConstant(token, out int c) ? c : throw new InvalidDataException("silph_gift_operand_unknown: " + token);
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1) || Value(a[buffer ? 1 : 0]) != 1) return false;
        if (!InPresidentScript(state) || state.Script.Flags.GetValueOrDefault(MasterBallFlag))
            throw new InvalidDataException("silph_master_ball_context_invalid");
        var item = ShopCatalog.Items.Single(i => i.Id == SilphMasterBallId);
        if (buffer || effect.Opcode == "checkitemtype")
        {
            if (effect.Source.Path != "data/event_scripts.s" || effect.Source.Symbol != "Std_PutItemAway" ||
                state.MessagePointer != "SilphCo_11F_Text_ObtainedMasterBallFromPresident")
                throw new InvalidDataException("silph_master_ball_receipt_context_invalid");
            if (buffer)
            {
                if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) || a.Length == 3 && Value(a[2]) != 1)
                    throw new InvalidDataException("silph_master_ball_buffer_invalid");
                next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], item.Name) };
            }
            else next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", Value(item.Pocket)) } };
            return true;
        }
        if (effect.Source.Path != PresidentSource || effect.Source.Symbol != PresidentScript || a.Length > 2 ||
            (a.Length == 2 ? Value(a[1]) : 1) != 1)
            throw new InvalidDataException("silph_master_ball_reward_source_invalid");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        var error = ValidateCampaignInventory(inventory);
        if (error is not null) throw new InvalidDataException(error);
        var view = inventory;
        var result = effect.Opcode == "additem" ? EarlyInventoryAddRules.Add(ShopCatalog, view, SilphMasterBallId, 1) :
            EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, SilphMasterBallId, 1);
        if (!result.Succeeded && result.Error is not ("inventory_stack_full" or "inventory_destination_full"))
            throw new InvalidDataException(result.Error);
        next = state with { Inventory = result.Succeeded && effect.Opcode == "additem" ? result.State : inventory,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Succeeded ? 1 : 0) } };
        return true;
    }
}

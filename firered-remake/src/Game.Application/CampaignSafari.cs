using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Ids;
using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Session-only Safari counters. Original Safari Start menu does not offer saving.</summary>
public sealed record CampaignSafariVisit(int Balls, int Steps, bool Active);

public sealed partial class CampaignSession
{
    private const string SafariFlag = "FLAG_SYS_SAFARI_MODE";
    private const string SafariEntranceSource = "data/maps/FuchsiaCity_SafariZone_Entrance/scripts.inc";
    private const string SafariSharedSource = "data/scripts/safari_zone.inc";
    private const string SafariEntranceMap = "map.fuchsia-city-safari-zone-entrance";
    private static readonly ItemId GoldTeeth = new("pret:ITEM_GOLD_TEETH");

    private InventoryState WithoutSafariKeyItems(InventoryState inventory) => !_content.SafariEnabled ? inventory :
        inventory with { KeyItems = inventory.KeyItems is { } keys ? keys.Where(s => s.Item != GoldTeeth).ToImmutableArray() : null };

    private string? ValidateSafariInventory(InventoryState inventory)
    {
        var teeth = (inventory.KeyItems ?? []).Where(s => s is not null && s.Item == GoldTeeth).ToArray();
        return teeth.Length > 0 && (!_content.SafariEnabled || teeth.Length != 1 || teeth[0].Quantity != 1)
            ? "inventory_safari_teeth_invalid" : null;
    }

    private bool InSafariArea(CampaignState state) => _content.Maps.ContainsKey(state.World.Player.MapId) && _content.MapDefinition(state.World.Player.MapId)
        .SourceMapSymbol.StartsWith("MAP_SAFARI_ZONE_", StringComparison.Ordinal);

    private CampaignState GenerateSafariBattle(CampaignState state)
    {
        var encounter = state.Exploration?.EarlyEncounter ?? throw new InvalidDataException("safari_encounter_missing");
        var table = _content.LandEncounters![state.World.Player.MapId];
        var generated = GenerateCurrentLand(state,table,encounter);
        var rng = generated.GlobalRng;
        var trace = generated.RandomTrace.ToBuilder();
        // CB2_InitBattle: SetUpBattleVars -> BattleAI_HandleItemUseBeforeAISetup (four
        // simulated-damage draws), then SetWildMonHeldItem. Safari's player controller
        // supplies no combatant; its zero-speed side cannot tie this healthy target.
        for (int slot = 0; slot < 4; slot++)
        {
            var draw = Gen3Rng.Next(rng, "safari.entry.ai-setup.slot-" + slot, 16);
            rng = draw.State; trace.Add(draw.Trace);
        }
        var held = EarlyLandWildPokemonFactory.AssignHeldItem(generated.Pokemon.Species, rng, EarlyLandEncounterRules.SafariRulesVersion);
        trace.AddRange(held.RandomTrace);
        return BeginSafariBattle(state, generated.Pokemon, held.GlobalRng, trace.ToImmutable(), held.HeldItem);
    }

    private IReadOnlyDictionary<MapId, IFieldMap> SafariMovementMaps(IReadOnlyDictionary<MapId, IFieldMap> maps) =>
        !_content.SafariEnabled ? maps : maps.ToDictionary(p => p.Key, p =>
            SafariFieldMovementRules.ForFieldMovement(p.Value, EarlyLandEncounterRules.SafariRulesVersion));

    private IReadOnlyDictionary<MapId, ConnectedFieldMap> SafariConnectedMaps(IReadOnlyDictionary<MapId, ConnectedFieldMap> maps) =>
        !_content.SafariEnabled ? maps : maps.ToDictionary(p => p.Key, p =>
            SafariFieldMovementRules.ForConnectedMovement(p.Value, EarlyLandEncounterRules.SafariRulesVersion));

    private bool CanReceiveSafariPokemon(CampaignState state) => state.Party.Length < 6 ||
        state.Storage is { } storage && storage.Boxes.Any(box => box.Slots.Any(mon => mon is null));

    private string? ValidateSafariProgress(CampaignState state)
    {
        bool enabled = state.Script.Flags.GetValueOrDefault(SafariFlag);
        bool surf = state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM03");
        bool strength = state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM04");
        if (!_content.SafariEnabled)
            return state.SafariVisit is not null || state.SafariBattle is not null || state.SafariExitPending || enabled || surf || strength ? "save_safari_profile_missing" : null;
        if (state.SafariVisit is not null || state.SafariBattle is not null || state.SafariExitPending || enabled || InSafariArea(state)) return "save_safari_game_active";
        int hm03 = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], new("pret:ITEM_HM03"));
        int hm04 = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], new("pret:ITEM_HM04"));
        if (hm03 != (surf ? 1 : 0) || hm04 != (strength ? 1 : 0)) return "save_safari_hm_reward_mismatch";
        if (strength && !state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH"))
            return "save_safari_warden_teeth_missing";
        int teeth = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], GoldTeeth);
        if (teeth != (state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH") && !strength ? 1 : 0))
            return "save_safari_gold_teeth_mismatch";
        return null;
    }

    private Decision<CampaignState> DecideSafariRetire(CampaignState state)
    {
        if (!_content.SafariEnabled || state.SafariVisit is not { Active: true, Balls: > 0, Steps: > 0 } ||
            !state.Script.Flags.GetValueOrDefault(SafariFlag) || !InSafariArea(state))
            return new(CommandStatus.Rejected, state, "safari_game_not_active");
        if (!IsStableFieldForParty(state) || state.SafariExitPending || state.SafariBattle is not null ||
            state.DoubleBattle is not null || state.Fishing is not null || state.Repel is not null ||
            state.PokeFlute is not null || state.FlashEffect is not null || state.RocketSpin is not null ||
            state.TownMap is not null || state.Pc is not null)
            return new(CommandStatus.Rejected, state, "event_active");
        return new(CommandStatus.Accepted, Pump(StartStory(state, "SafariZone_EventScript_RetirePrompt")));
    }

    private CampaignState ApplySafariWalkingStep(CampaignState state)
    {
        if (state.SafariVisit is not { Active: true } visit) return state;
        if (!_content.SafariEnabled || !state.Script.Flags.GetValueOrDefault(SafariFlag) ||
            visit.Steps is < 1 or > 600 || visit.Balls is < 1 or > 30 || !InSafariArea(state))
            throw new InvalidDataException("safari_step_context");
        state = state with { SafariVisit = visit with { Steps = visit.Steps - 1 } };
        return visit.Steps == 1 ? Pump(StartStory(state, "SafariZone_EventScript_TimesUp")) : state;
    }

    private CampaignState CompleteSafariExitWarp(CampaignState state)
    {
        if (!_content.SafariEnabled || state.SafariVisit is not null || state.Script.Flags.GetValueOrDefault(SafariFlag) ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE") != 3)
            throw new InvalidDataException("safari_exit_warp_context");
        var destination = _content.MapFromSourceSymbol("MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE");
        var cell = _content.Maps[destination].GetTile(4, 1) ?? throw new InvalidDataException("safari_exit_cell");
        state = state with { SafariExitPending = false, World = state.World with
        { Player = new(destination, 4, 1, cell.Elevation, Game.Domain.World.Direction.South) } };
        return EnterMap(state, Game.Domain.World.Direction.South);
    }

    private bool TryApplySafariEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.SafariEnabled) return false;
        var a = effect.Args;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal)
            ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, out int n) ? n : _content.TryConstant(token, out n) ? n
            : throw new InvalidDataException("safari_operand: " + token);
        CampaignState Result(int value) => state with
        { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        bool entrance = effect.Source.Path == SafariEntranceSource && state.World.Player.MapId.Value == SafariEntranceMap;
        bool shared = effect.Source.Path == SafariSharedSource;
        if (shared && effect.Opcode == "setwarp")
        {
            if (effect.Source.Symbol != "SafariZone_EventScript_OutOfBallsMidBattle" || state.SafariVisit is not null ||
                !a.SequenceEqual(new[] { "MAP_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE", "4", "1" }))
                throw new InvalidDataException("safari_exit_destination");
            next = state with { SafariExitPending = true }; return true;
        }
        if (effect.Opcode == "special" && a.Length == 1 && a[0] is "EnterSafariMode" or "ExitSafariMode")
        {
            if (!entrance && !shared) throw new InvalidDataException("safari_mode_source");
            if (a[0] == "EnterSafariMode")
            {
                if (!entrance || effect.Source.Symbol != "FuchsiaCity_SafariZone_Entrance_EventScript_TryEnterSafariZone" ||
                    state.SafariVisit is not { Active: false, Balls: 30, Steps: 600 } || state.Script.Flags.GetValueOrDefault(SafariFlag))
                    throw new InvalidDataException("safari_admission_not_paid");
                next = state with { SafariVisit = state.SafariVisit with { Active = true },
                    Script = state.Script with { Flags = state.Script.Flags.SetItem(SafariFlag, true) } };
            }
            else
                next = state with { SafariVisit = null, Script = state.Script with { Flags = state.Script.Flags.SetItem(SafariFlag, false) } };
            return true;
        }
        if (entrance && effect.Opcode is "showmoneybox" or "hidemoneybox" or "updatemoneybox" or "checkmoney" or "removemoney" or "getpartysize")
        {
            if (!state.Script.Locked) throw new InvalidDataException("safari_counter_not_locked");
            switch (effect.Opcode)
            {
                case "showmoneybox":
                    if (a.Length != 2 || Value(a[0]) != 0 || Value(a[1]) != 0 || state.MoneyBox is not null)
                        throw new InvalidDataException("safari_moneybox_context");
                    next = ApplySourceMoneyBox(state, effect); break;
                case "hidemoneybox": next = ApplySourceMoneyBox(state, effect); break;
                case "updatemoneybox": next = ApplySourceMoneyBox(state, effect); break;
                case "getpartysize": next = Result(state.Party.Length); break;
                case "checkmoney":
                    if (a.Length != 1 || Value(a[0]) != 500) throw new InvalidDataException("safari_price_invalid");
                    next = SourceMoneyResult(state, 500); break;
                case "removemoney":
                    if (a.Length != 1 || Value(a[0]) != 500 || state.Money < 500 || state.SafariVisit is not null ||
                        !CanReceiveSafariPokemon(state) || state.Script.Flags.GetValueOrDefault(SafariFlag))
                        throw new InvalidDataException("safari_payment_context");
                    next = DebitSourceMoney(state, 500) with { SafariVisit = new(30, 600, false) }; break;
            }
            return true;
        }
        if (entrance && effect.Opcode == "specialvar" && a.SequenceEqual(new[] { "VAR_RESULT", "IsThereRoomInAnyBoxForMorePokemon" }))
        {
            if (state.Party.Length != 6 || state.Storage is null) throw new InvalidDataException("safari_pc_space_context");
            next = Result(CanReceiveSafariPokemon(state) ? 1 : 0); return true;
        }
        if (effect.Source.Path == "data/maps/FuchsiaCity_WardensHouse/scripts.inc" && effect.Opcode == "checkplayergender")
        {
            if (state.World.Player.MapId.Value != "map.fuchsia-city-wardens-house") throw new InvalidDataException("safari_warden_context");
            next = Result(state.PlayerGender); return true;
        }
        return TryApplySafariRewardEffect(state, effect, out next);
    }

    private bool TryApplySafariRewardEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var a = effect.Args;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal) ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, out int n) ? n : _content.TryConstant(token, out n) ? n
            : throw new InvalidDataException("safari_item_operand: " + token);
        bool buffer = effect.Opcode is "bufferitemname" or "bufferitemnameplural";
        if (effect.Opcode is not ("checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        if (a.Length < (buffer ? 2 : 1)) return false;
        int id = Value(a[buffer ? 1 : 0]);
        bool gold = id == Value("ITEM_GOLD_TEETH"), surf = id == Value("ITEM_HM03"), strength = id == Value("ITEM_HM04");
        if (!gold && !surf && !strength) return false;
        string map = state.World.Player.MapId.Value;
        bool hut = map == "map.safari-zone-secret-house" && state.LastTalked is { } hutActor &&
            state.Actors.TryGetValue(hutActor, out var attendant) && attendant.ScriptId == "SafariZone_SecretHouse_EventScript_Attendant";
        bool warden = map == "map.fuchsia-city-wardens-house" && state.LastTalked is { } wardenActor &&
            state.Actors.TryGetValue(wardenActor, out var npc) && npc.ScriptId == "FuchsiaCity_WardensHouse_EventScript_Warden";
        bool pickup = gold && state.FieldPickup is { } context && _content.FieldPickups.Any(p =>
            p.Id == context.Id && p.Item == GoldTeeth && p.MapSymbol == _content.MapDefinition(state.World.Player.MapId).SourceMapSymbol);
        if (gold ? !pickup && !warden : surf ? !hut : !warden) throw new InvalidDataException("safari_reward_actor_context");
        bool common = effect.Source.Path is "data/scripts/obtain_item.inc" or "data/event_scripts.s";
        if (!common && effect.Source.Path != (surf ? "data/maps/SafariZone_SecretHouse/scripts.inc" : "data/maps/FuchsiaCity_WardensHouse/scripts.inc"))
            throw new InvalidDataException("safari_reward_source");
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length == 3 && Value(a[2]) != 1) throw new InvalidDataException("safari_reward_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], gold ? "GOLD TEETH" : surf ? "HM03" : "HM04") };
            return true;
        }
        int result;
        if (effect.Opcode == "checkitemtype") result = Value(gold ? "POCKET_KEY_ITEMS" : "POCKET_TM_CASE");
        else
        {
            if (a.Length > 2 || a.Length == 2 && Value(a[1]) != 1) throw new InvalidDataException("safari_reward_quantity");
            var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
            if (ValidateCampaignInventory(inventory) is { } error) throw new InvalidDataException(error);
            if (gold)
            {
                var keys = inventory.KeyItems ?? [];
                int index = -1; for (int i = 0; i < keys.Length; i++) if (keys[i].Item == GoldTeeth) { index = i; break; }
                if (effect.Opcode == "removeitem")
                {
                    if (!warden || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM04")) throw new InvalidDataException("safari_teeth_exchange_context");
                    result = index >= 0 ? 1 : 0;
                    if (index >= 0) next = state with { Inventory = inventory with { KeyItems = keys.RemoveAt(index) } };
                }
                else
                {
                    bool adding = effect.Opcode == "additem";
                    bool hidden = state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH");
                    // Std_FindItem checks capacity first; its PickUpItem helper removes the actor
                    // (and sets the source hide flag) immediately before adding the Key Item.
                    if (!pickup || state.FieldPickup!.InventoryAdded || hidden != adding ||
                        effect.Source.Path != "data/scripts/obtain_item.inc" ||
                        effect.Source.Symbol != (adding ? "EventScript_PickUpItem" : "Std_FindItem") ||
                        adding && (state.LastTalked is null || state.Actors[state.LastTalked].Active))
                        throw new InvalidDataException("safari_teeth_pickup_context");
                    result = index < 0 && keys.Length < ShopCatalog.QuestLimits.KeyItemSlots ? 1 : 0;
                    if (result == 1 && effect.Opcode == "additem") next = state with
                    { Inventory = inventory with { KeyItems = keys.Add(new(GoldTeeth, 1)) }, FieldPickup = state.FieldPickup with { InventoryAdded = true } };
                }
            }
            else
            {
                var machine = new ItemId(surf ? "pret:ITEM_HM03" : "pret:ITEM_HM04");
                if (effect.Opcode == "removeitem" || state.Script.Flags.GetValueOrDefault(surf ? "FLAG_GOT_HM03" : "FLAG_GOT_HM04") ||
                    strength && !state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH"))
                    throw new InvalidDataException("safari_hm_reward_context");
                if (effect.Opcode == "checkitemspace") result = PewterMachineRules.CanAddMachine(_content.PewterMachines!, inventory, machine) ? 1 : 0;
                else
                {
                    if (OpeningInventoryRules.Quantity(inventory.Machines ?? [], machine) != 0) throw new InvalidDataException("safari_hm_duplicate");
                    var added = PewterMachineRules.AddMachine(_content.PewterMachines!, inventory, machine);
                    next = state with { Inventory = added.State }; result = added.Succeeded ? 1 : 0;
                }
            }
        }
        next = next with { Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", result) } };
        return true;
    }
}

using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private void RequireCelioEffect(CampaignState state, StoryEffect effect)
    {
        var instruction = _content.FindProgram(effect.Source.Symbol)?.Instructions.SingleOrDefault(i => i.Source == effect.Source);
        if (effect.Source.Path != RubyCenterSource || state.World.Player.MapId.Value != RubyCenterMap ||
            instruction is null || instruction.Opcode != effect.Opcode || !instruction.Args.SequenceEqual(effect.Args) ||
            state.Script.Stack.LastOrDefault() is not {} frame || frame.ProgramId != effect.Source.Symbol ||
            frame.InstructionIndex <= 0 || _content.FindProgram(frame.ProgramId)!.Instructions[frame.InstructionIndex - 1] != instruction)
            throw new InvalidDataException("celio_effect_source_context");
    }

    private bool CelioHandoffActive(CampaignState state) => state.Script.Locked &&
        state.Script.Stack.Any(f => f.ProgramId == CelioHandoffScript) && state.LastTalked == "LOCALID_CELIO" &&
        state.Actors.TryGetValue("LOCALID_CELIO", out var actor) && actor.Active &&
        actor.ScriptId == "OneIsland_PokemonCenter_1F_EventScript_Celio";

    private ImmutableSortedDictionary<string, FieldMetatileEdit> CelioMachineEdits(CampaignState state)
    {
        var result = ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty;
        foreach (string program in new[] { CelioSapphireScript, CelioMachineScript })
            foreach (var instruction in _content.FindProgram(program)!.Instructions.Where(i => i.Opcode == "setmetatile"))
            {
                var a = instruction.Args;
                int x = SeviiValue(state, a[0]), y = SeviiValue(state, a[1]), tile = SeviiValue(state, a[2]);
                var cell = _content.Maps[state.World.Player.MapId].GetTile(x, y) ?? throw new InvalidDataException("celio_machine_cell");
                if (_content.MetatileDefinition(state.World.Player.MapId, tile) is null || a.Length != 4 || a[3] != "1")
                    throw new InvalidDataException("celio_machine_tile");
                result = result.SetItem(x + ":" + y, new(x, y, tile, 3, cell.Elevation));
            }
        if (result.Count != 3) throw new InvalidDataException("celio_machine_tile_count");
        return result;
    }

    private bool TryApplyCelioEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != RubyCenterSource) return false;
        var a = effect.Args;
        if (effect.Opcode == "removeitem" && effect.Source.Symbol == CelioHandoffScript)
        {
            RequireCelioEffect(state, effect);
            if (!a.SequenceEqual(new[] { "ITEM_SAPPHIRE" }) || !CelioHandoffActive(state) || state.CelioHandoff is not null ||
                state.Roamer?.Individual is not null || state.Script.Variables.GetValueOrDefault(RubyCenterScene) != 5 ||
                !state.Script.Flags.GetValueOrDefault(FiveIslandSapphireFlag) ||
                !state.BattledTrainers.Contains("pret:TRAINER_SCIENTIST_GIDEON") || !NationalDexEnabledIn(state))
                throw new InvalidDataException("celio_sapphire_handoff_context");
            var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
            int quantity = OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], SapphireItem);
            if (quantity is < 0 or > 1) throw new InvalidDataException("celio_sapphire_quantity");
            var result = EarlyInventoryAddRules.Remove(ShopCatalog, inventory, SapphireItem, 1);
            if (!result.Succeeded && result.Error != "inventory_insufficient_quantity") throw new InvalidDataException(result.Error);
            // The source branches on RECOVERED, not possession; a prior full Key pocket may have left no item.
            next = state with { Inventory = result.Succeeded ? result.State with {
                    KeyItems = (inventory.KeyItems ?? []).RemoveAll(k => k.Item == SapphireItem) } : inventory,
                CelioHandoff = new(quantity, result.Succeeded, effect.Sequence),
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Succeeded ? 1 : 0) } };
            return true;
        }
        if (effect.Opcode == "setmetatile" && effect.Source.Symbol is CelioSapphireScript or CelioMachineScript)
        {
            RequireCelioEffect(state, effect);
            bool handoff = CelioHandoffActive(state);
            bool reload = state.Script.Stack.Any(f => f.ProgramId == "OneIsland_PokemonCenter_1F_OnLoad") && CelioCompleted(state);
            if ((!handoff && !reload) || handoff && state.CelioHandoff is not { Initialization: null })
                throw new InvalidDataException("celio_machine_context");
            var expected = CelioMachineEdits(state);
            string key = a[0] + ":" + a[1];
            if (!expected.TryGetValue(key, out var tile) || tile.SourceMetatileId != SeviiValue(state, a[2]) || a[3] != "1")
                throw new InvalidDataException("celio_machine_arguments");
            if (handoff && effect.Source.Symbol == CelioMachineScript && !state.CelioHandoff!.SapphireRedrawn)
                throw new InvalidDataException("celio_machine_effect_order");
            next = state with { MapEdits = (state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty).SetItem(key, tile),
                CelioHandoff = handoff ? state.CelioHandoff! with { SapphirePlaced = true } : state.CelioHandoff };
            return true;
        }
        if (effect.Opcode == "special" && effect.Source.Symbol == CelioHandoffScript)
        {
            RequireCelioEffect(state, effect);
            if (!CelioHandoffActive(state) || state.CelioHandoff is not {} receipt || a.Length != 1)
                throw new InvalidDataException("celio_special_context");
            if (a[0] == "DrawWholeMapView")
            {
                var expected = CelioMachineEdits(state);
                bool powered = receipt.SapphireRedrawn;
                if (!receipt.SapphirePlaced || receipt.MachineRedrawn || state.MapEdits?.Count != (powered ? 4 : 2) ||
                    !state.MapEdits.TryGetValue("16:4", out var sapphire) || sapphire != expected["16:4"] ||
                    powered && expected.Any(pair => state.MapEdits.GetValueOrDefault(pair.Key) != pair.Value))
                    throw new InvalidDataException("celio_redraw_order");
                next = state with { CelioHandoff = receipt with { SapphireRedrawn = true, MachineRedrawn = powered } };
                return true;
            }
            if (a[0] == "SetPostgameFlags")
            {
                if (!receipt.MachineRedrawn || receipt.PostgameFlagsSequence != 0 || receipt.Initialization is not null ||
                    state.Script.Variables.GetValueOrDefault(RubyCenterScene) != 5 || state.Script.Flags.GetValueOrDefault(CelioLinkFlag))
                    throw new InvalidDataException("celio_postgame_flag_order");
                // save_location.c: CHAMPION_SAVEWARP first, then GCN bits 1,2,3,15. No other story grants.
                next = state with { SpecialSaveWarpFlags = (byte)(state.SpecialSaveWarpFlags | 0x80),
                    GcnLinkFlags = (ushort)(state.GcnLinkFlags | 0x800e),
                    CelioHandoff = receipt with { PostgameFlagsSequence = effect.Sequence } };
                return true;
            }
            if (a[0] == "InitRoamer")
            {
                if (!receipt.MachineRedrawn || receipt.PostgameFlagsSequence <= receipt.RemovalSequence || receipt.Initialization is not null ||
                    state.Roamer?.Individual is not null || state.Script.Variables.GetValueOrDefault(RubyCenterScene) != 6 ||
                    !state.Script.Flags.GetValueOrDefault(CelioLinkFlag) || !state.Script.Flags.GetValueOrDefault(CelioCaveGuardFlag) ||
                    !state.Script.Flags.GetValueOrDefault(CelioLoreleiFlag) || state.SpecialSaveWarpFlags != 0x80 || state.GcnLinkFlags != 0x803f)
                    throw new InvalidDataException("celio_roamer_initialization_order");
                int starter = state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON");
                var before = state.World.Rng;
                var initialized = SourceRoamerRules.Initialize(state.Roamer ?? RoamerState.Empty, starter, before);
                next = state with { World = state.World with { Rng = initialized.Rng }, Roamer = initialized.State,
                    CelioHandoff = receipt with { InitializationSequence = effect.Sequence,
                        Initialization = new(starter, before, initialized.Rng, initialized.State, initialized.Trace) } };
                return true;
            }
        }
        return false;
    }
}

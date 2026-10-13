using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignFossilRevival(int FossilKind, int SourceItemId, int SourceSpeciesId,
    long InstanceId, uint Personality, SixStats Ivs);
public sealed record CampaignFossilMenu(string MenuId, ImmutableArray<string> Options, int Left = 0, int Top = 0);

public sealed partial class CampaignSession
{
    private const string FossilLabEntrance = "map.cinnabar-island-pokemon-lab-entrance";
    private const string FossilLabExperiment = "map.cinnabar-island-pokemon-lab-experiment-room";
    private const string FossilLabSource = "data/maps/CinnabarIsland_PokemonLab_ExperimentRoom/scripts.inc";
    private const string FossilLabEntranceSource = "data/maps/CinnabarIsland_PokemonLab_Entrance/scripts.inc";
    private const string FossilLabPrefix = "CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_";
    private const string FossilLabScientist = FossilLabPrefix + "FossilScientist";
    private const string FossilLabReviveState = "VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_REVIVE_STATE";
    private const string FossilLabWhichFossil = "VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_WHICH_FOSSIL";
    private sealed record FossilLabDefinition(int Kind, int ItemNumber, ItemId Item, string ItemSymbol,
        int Species, string SpeciesSymbol, string AcquiredFlag, string RevivedFlag, string Show, string Give, string Menu);
    private static readonly ImmutableArray<FossilLabDefinition> FossilLabDefinitions =
    [
        new(1, 357, MtMoonItemRules.HelixFossilId, "ITEM_HELIX_FOSSIL", 138, "SPECIES_OMANYTE",
            "FLAG_GOT_HELIX_FOSSIL", "FLAG_REVIVED_HELIX", "ShowHelixFossil", "GiveOmanyte", "MULTICHOICE_HELIX"),
        new(2, 358, MtMoonItemRules.DomeFossilId, "ITEM_DOME_FOSSIL", 140, "SPECIES_KABUTO",
            "FLAG_GOT_DOME_FOSSIL", "FLAG_REVIVED_DOME", "ShowDomeFossil", "GiveKabuto", "MULTICHOICE_DOME")
    ];

    private bool InFossilLabScript(CampaignState state) => _content.FossilLabEnabled &&
        state.World.Player.MapId.Value == FossilLabExperiment && state.LastTalked is { } id &&
        state.Actors.TryGetValue(id, out var actor) && actor.Active && actor.SourceLocalId == 2 &&
        actor.ScriptId == FossilLabScientist && state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") == 2;

    private static void ValidateFossilLabCatalog(ICampaignCatalog content)
    {
        if (!content.FossilLabEnabled || content.FossilLabOwnership is not { } owned ||
            content.FossilLabOpponents is not { } opponents || content.FossilLabMachines is not { } machines ||
            !ReferenceEquals(FossilLabOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(owned.Predecessor, content.FightingDojoOwnership) ||
            !ReferenceEquals(owned.Catalog, content.PokemonContent?.Catalog) ||
            !ReferenceEquals(FossilLabOpponentPokemonCatalog.For(owned), opponents) ||
            !ReferenceEquals(opponents.Predecessor, content.FightingDojoOpponents) ||
            !ReferenceEquals(FossilLabMachineCatalog.For(content.PewterMachines), machines) ||
            !ReferenceEquals(machines.Predecessor, content.FightingDojoMachines) || !ReferenceEquals(machines.Pokemon, owned))
            throw new InvalidDataException("fossil_lab_current_binding");
        // The locked region authenticates numeric IDs 0x4069/0x406A. Current
        // scripts deliberately retain symbolic variable identity, not IDs.
        var entrance = content.MapDefinition(new(FossilLabEntrance));
        var experiment = content.MapDefinition(new(FossilLabExperiment));
        if (entrance.Programs.OnTransition != "CinnabarIsland_PokemonLab_Entrance_OnTransition" ||
            !experiment.Actors.Any(a => a.SourceLocalId == 2 && a.ScriptId == FossilLabScientist))
            throw new InvalidDataException("fossil_lab_map_source");
        var transition = content.FindProgram(entrance.Programs.OnTransition);
        var ready = content.FindProgram("CinnabarIsland_PokemonLab_Entrance_EventScript_ReadyFossil");
        if (transition is null || !transition.Instructions.Any(i => i.Source.Path == FossilLabEntranceSource &&
                i.Opcode == "call_if_eq" && i.Args.SequenceEqual(new[] { FossilLabReviveState, "1", "CinnabarIsland_PokemonLab_Entrance_EventScript_ReadyFossil" })) ||
            ready is null || !ready.Instructions.Any(i => i.Source.Path == FossilLabEntranceSource &&
                i.Opcode == "setvar" && i.Args.SequenceEqual(new[] { FossilLabReviveState, "2" })))
            throw new InvalidDataException("fossil_lab_ready_source");
        foreach (var fossil in FossilLabDefinitions)
        {
            var show = content.FindProgram(FossilLabPrefix + fossil.Show);
            var give = content.FindProgram(FossilLabPrefix + fossil.Give);
            if (show is null || !show.Instructions.Any(i => i.Source.Path == FossilLabSource && i.Opcode == "removeitem" &&
                    i.Args.SequenceEqual(new[] { fossil.ItemSymbol })) ||
                give is null || !give.Instructions.Any(i => i.Source.Path == FossilLabSource && i.Opcode == "givemon" &&
                    i.Args.SequenceEqual(new[] { fossil.SpeciesSymbol, "5" })))
                throw new InvalidDataException("fossil_lab_gift_source");
        }
    }

    // These are stable-save invariants. Original scripts temporarily remove the
    // item before setting state 1, and create the gift before setting its flag.
    private string? ValidateFossilLabProgress(CampaignState state)
    {
        int phase = state.Script.Variables.GetValueOrDefault(FossilLabReviveState);
        int which = state.Script.Variables.GetValueOrDefault(FossilLabWhichFossil);
        var receipts = state.FossilRevivals ?? [];
        bool amber = state.Script.Flags.GetValueOrDefault("FLAG_GOT_OLD_AMBER") ||
            state.Script.Flags.GetValueOrDefault("FLAG_REVIVED_AMBER") || which == 3 ||
            (state.Inventory?.KeyItems ?? []).Any(s => s.Item.Value == "pret:ITEM_OLD_AMBER");
        if (amber) return "save_fossil_lab_amber_not_admitted";
        if (!_content.FossilLabEnabled)
            return phase != 0 || which != 0 || state.FossilRevivals is not null ||
                FossilLabDefinitions.Any(f => state.Script.Flags.GetValueOrDefault(f.RevivedFlag))
                ? "save_fossil_lab_profile_missing" : null;
        if (receipts.IsDefault || receipts.Length > 1 || receipts.Any(r => r is null)) return "save_fossil_lab_receipts_invalid";
        if (phase is < 0 or > 2 || which is < 0 or > 2 || phase != 0 && which == 0)
            return "save_fossil_lab_state_invalid";
        foreach (var fossil in FossilLabDefinitions)
        {
            var matching = receipts.Where(r => r.FossilKind == fossil.Kind).ToArray();
            bool revived = state.Script.Flags.GetValueOrDefault(fossil.RevivedFlag);
            if (matching.Length > 1 || revived != (matching.Length == 1)) return "save_fossil_lab_receipt_flag";
            if (phase != 0 && which == fossil.Kind && (revived || !state.Script.Flags.GetValueOrDefault(fossil.AcquiredFlag)))
                return "save_fossil_lab_pending_invalid";
        }
        // WHICH is not cleared by the original success script. Its only valid
        // idle nonzero value is the fossil for the completed receipt.
        if (phase == 0 && (receipts.IsEmpty ? which != 0 : which != receipts[0].FossilKind) ||
            phase != 0 && !receipts.IsEmpty) return "save_fossil_lab_state_receipt";
        foreach (var receipt in receipts)
        {
            var fossil = FossilLabDefinitions.SingleOrDefault(f => f.Kind == receipt.FossilKind);
            if (fossil is null || receipt.SourceItemId != fossil.ItemNumber || receipt.SourceSpeciesId != fossil.Species ||
                receipt.InstanceId < 2 || receipt.InstanceId >= state.NextPokemonId || receipt.Ivs is null ||
                !state.Script.Flags.GetValueOrDefault(fossil.AcquiredFlag)) return "save_fossil_lab_receipt_origin";
            var matching = OwnedPokemon(state).Where(p => p.InstanceId == receipt.InstanceId).ToArray();
            if (matching.Length != 1) return "save_fossil_lab_receipt_instance";
            var mon = matching[0];
            if (!EvolutionSpeciesMatchesOrigin(state, mon, fossil.Species) || mon.Pokemon.Personality != receipt.Personality ||
                mon.Pokemon.Ivs != receipt.Ivs || mon.Pokemon.OriginalTrainerId != state.TrainerId ||
                mon.OriginalTrainerName != state.PlayerName || mon.OriginalTrainerGender != state.PlayerGender ||
                mon.Language != 2 || mon.MetGame != 4 || mon.MetLocation != "MAPSEC_CINNABAR_ISLAND" ||
                mon.MetLevel != 5 || mon.Ball != "ITEM_POKE_BALL" || mon.ModernFatefulEncounter ||
                !state.CaughtSpecies.Contains(fossil.Species) || !state.SeenSpecies.Contains(fossil.Species))
                return "save_fossil_lab_receipt_instance";
        }
        return null;
    }

    private int DepositedOrRevivedFossils(CampaignState state, int kind)
    {
        if (!_content.FossilLabEnabled) return 0;
        var fossil = FossilLabDefinitions.Single(f => f.Kind == kind);
        bool pending = state.Script.Variables.GetValueOrDefault(FossilLabReviveState) is 1 or 2 &&
            state.Script.Variables.GetValueOrDefault(FossilLabWhichFossil) == kind &&
            !state.Script.Flags.GetValueOrDefault(fossil.RevivedFlag);
        return (pending ? 1 : 0) + (state.FossilRevivals ?? []).Count(r => r.FossilKind == kind);
    }

    private long? PendingFossilGiftInstanceId(CampaignState state) =>
        (state.FossilRevivals ?? []).SingleOrDefault(r => r.FossilKind == state.Script.Variables.GetValueOrDefault(FossilLabWhichFossil))?.InstanceId;

    private bool TryApplyFossilLabEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.FossilLabEnabled || effect.Source.Path != FossilLabSource) return false;
        // Validate once at the entry before any transient source mutations.
        if (effect.Opcode == "lock" && effect.Source.Symbol == FossilLabScientist)
        {
            if (!InFossilLabScript(state)) throw new InvalidDataException("fossil_lab_caller");
            var error = ValidateFossilLabProgress(state) ?? ValidateMtMoonProgress(state);
            if (error is not null) throw new InvalidDataException(error);
            return true;
        }
        if (effect.Opcode is not ("multichoice" or "bufferitemname" or "removeitem" or "givemon")) return false;
        if (!InFossilLabScript(state)) throw new InvalidDataException("fossil_lab_caller");
        var a = effect.Args;
        var fossil = effect.Opcode switch
        {
            "multichoice" => FossilLabDefinitions.SingleOrDefault(f => effect.Source.Symbol == FossilLabPrefix + (f.Kind == 1 ? "ChooseFossilHelix" : "ChooseFossilDome")),
            "givemon" => FossilLabDefinitions.SingleOrDefault(f => effect.Source.Symbol == FossilLabPrefix + f.Give),
            _ => FossilLabDefinitions.SingleOrDefault(f => effect.Source.Symbol == FossilLabPrefix + f.Show)
        };
        if (fossil is null) throw new InvalidDataException("fossil_lab_branch_not_admitted");
        if (!state.Script.Flags.GetValueOrDefault(fossil.AcquiredFlag) || state.Script.Flags.GetValueOrDefault(fossil.RevivedFlag) ||
            !(state.FossilRevivals ?? []).IsEmpty || state.Script.Flags.GetValueOrDefault("FLAG_GOT_OLD_AMBER") ||
            state.Script.Flags.GetValueOrDefault("FLAG_REVIVED_AMBER")) throw new InvalidDataException("fossil_lab_reward_context");
        int phase = state.Script.Variables.GetValueOrDefault(FossilLabReviveState);
        int which = state.Script.Variables.GetValueOrDefault(FossilLabWhichFossil);
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        var item = ShopCatalog.Items.Single(i => i.Id == fossil.Item);
        if (effect.Opcode == "givemon")
        {
            if (phase != 2 || which != fossil.Kind || OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], fossil.Item) != 0 ||
                state.Script.Variables.GetValueOrDefault("VAR_TEMP_1") != fossil.Species ||
                !a.SequenceEqual(new[] { fossil.SpeciesSymbol, "5" })) throw new InvalidDataException("fossil_lab_collection_context");
            var result = GiveScriptGift(state, fossil.Species, 5, "MAPSEC_CINNABAR_ISLAND");
            next = result.State;
            if (result.Gift is { } mon)
                next = next with { FossilRevivals = (state.FossilRevivals ?? []).Add(new(fossil.Kind, fossil.ItemNumber,
                    fossil.Species, mon.InstanceId, mon.Pokemon.Personality, mon.Pokemon.Ivs)) };
            return true;
        }
        if (phase != 0 || which != 0 || OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], fossil.Item) != 1)
            throw new InvalidDataException("fossil_lab_deposit_context");
        switch (effect.Opcode)
        {
            case "multichoice":
                if (!a.SequenceEqual(new[] { "0", "0", fossil.Menu, "FALSE" }) || state.Script.Wait?.Kind != StoryWaitKind.MenuChoice)
                    throw new InvalidDataException("fossil_lab_menu_source");
                next = state with { FossilMenu = new(fossil.Menu, [item.Name, "EXIT"]) };
                break;
            case "bufferitemname":
                if (!a.SequenceEqual(new[] { "STR_VAR_2", fossil.ItemSymbol })) throw new InvalidDataException("fossil_lab_buffer_source");
                next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", item.Name) };
                break;
            case "removeitem":
                if (!a.SequenceEqual(new[] { fossil.ItemSymbol })) throw new InvalidDataException("fossil_lab_remove_source");
                var inventoryError = ValidateCampaignInventory(inventory);
                if (inventoryError is not null) throw new InvalidDataException(inventoryError);
                var removed = EarlyInventoryAddRules.Remove(ShopCatalog, ShopInventoryView(inventory), fossil.Item, 1);
                if (!removed.Succeeded) throw new InvalidDataException(removed.Error);
                // The shop view omits unrelated quest keys. Merge only the
                // checked fossil consumption, preserving every other pocket.
                next = state with { Inventory = inventory with { KeyItems = (inventory.KeyItems ?? []).Where(k => k.Item != fossil.Item).ToImmutableArray() },
                    Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 1) } };
                break;
        }
        return true;
    }
}

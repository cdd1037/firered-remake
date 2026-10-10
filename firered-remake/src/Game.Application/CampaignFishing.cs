using Game.Domain.Encounters;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient source task and correlated renderer acknowledgements; never a save checkpoint.</summary>
public sealed record CampaignFishing(OldRodFishingState State, string WaitId,
    bool MessageFinished = false, bool NoCatchAnimationFinished = false, bool TextPrinted = false);

public sealed partial class CampaignSession
{
    public bool CanUseOldRod => _content.CityServicesEnabled && State.Fishing is null &&
        State.Inventory is {} inventory && OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], OldRodId) == 1;

    private string? ValidateFishingState(CampaignState state) => state.Fishing is not null ? "save_fishing_active" : null;

    private Decision<CampaignState> StartFieldFishing(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!_content.CityServicesEnabled || _content.OldRodEncounters is null) return No("fishing_not_enabled");
        if (command.Kind != "field_fish" || command.Text != OldRodId.Value ||
            state.ItemMenu is not { Origin: "bag", View: "bag" }) return No("fishing_bag_context_invalid");
        if (state.Fishing is not null || !IsStableFieldForParty(state with { ItemMenu = null }) ||
            state.Pc is not null || state.TownMap is not null || state.Repel is not null || state.CutEffect is not null)
            return No("fishing_event_active");
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) return No(error);
        if (OpeningInventoryRules.Quantity(state.Inventory!.KeyItems ?? [], OldRodId) != 1) return No("old_rod_missing");
        if (state.Party.IsEmpty || state.Party.All(p => p.IsEgg || p.Pokemon.CurrentHp == 0)) return No("fishing_live_party_required");
        if (_content.ConnectedFieldMaps is null) return No("fishing_map_missing");
        var position = state.World.Player;
        var maps = EditedFieldMaps(state);
        if (!maps.TryGetValue(position.MapId, out var map)) return No("fishing_map_missing");
        var (dx, dy) = Delta(position.Facing);
        int x = position.X + dx, y = position.Y + dy;
        var here = map.CellAt(position.X, position.Y); var front = map.CellAt(x, y);
        if (here is null || front is null || here.Collision != 0 || here.Occupied ||
            (here.Elevation != 15 && here.Elevation != position.Elevation)) return No("fishing_invalid_shore");
        int? terrain = state.MapEdits?.TryGetValue(x + ":" + y, out var edit) == true
            ? _content.MetatileDefinition(position.MapId, edit.SourceMetatileId)?.Terrain
            : _content.TerrainAt(position.MapId, x, y);
        if (terrain is null) return No("fishing_terrain_missing");
        // Original GetCollisionAtCoords tests impassability before elevation, then objects.
        // The admitted player has no movement range; front must remain inside this loaded map.
        int collision = front.Collision != 0 || FishingBlocksEdge(here.SourceBehavior, position.Facing) ||
            FishingBlocksEdge(front.SourceBehavior, FishingOpposite(position.Facing)) ? 2 :
            position.Elevation != 0 && front.Elevation is not (0 or 15) && front.Elevation != position.Elevation ? 3 :
            front.Occupied || VisibleActorAt(state, x, y) is not null ? 4 : 0;
        var context = new OldRodCastContext(position.MapId, true, position.Elevation, terrain.Value,
            front.SourceBehavior, collision);
        if (!OldRodFishingRules.CanCast(context)) return No("fishing_invalid_shore");
        if (!_content.OldRodEncounters.ContainsKey(position.MapId) && !OldRodEncounterRules.MapsWithoutFishingMons.Contains(position.MapId))
            return No("fishing_map_not_admitted");
        var result = OldRodFishingRules.Start(context, state.World.Rng);
        long sequence = checked(state.Script.EffectSequence + 1);
        return new(CommandStatus.Accepted, state with
        {
            Fishing = new(result.State, "fishing:" + sequence), ItemMenu = null,
            Script = state.Script with { EffectSequence = sequence },
            World = state.World with { Rng = result.GlobalRng }
        });
    }

    private Decision<CampaignState> DecideFishing(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        var fishing = state.Fishing ?? throw new InvalidDataException("fishing_state_missing");
        OldRodFishingRules.ValidateState(fishing.State);
        if (!_content.CityServicesEnabled || fishing.State.Map != state.World.Player.MapId ||
            fishing.State.Status != OldRodFishingStatus.Running || state.ItemMenu is not null ||
            state.Battle is not null || state.WildBattle is not null || state.ForestTrainerBattle is not null)
            throw new InvalidDataException("fishing_context_changed");
        if (command.Kind is "fishing_text_finished" or "ack" or "fishing_animation_finished")
        {
            if (command.Text != fishing.WaitId) return No("fishing_callback_mismatch");
            if (command.Kind == "fishing_animation_finished")
            {
                if (fishing.State.SourceStep != 14 || fishing.NoCatchAnimationFinished) return No("fishing_animation_not_pending");
                return new(CommandStatus.Accepted, state with { Fishing = fishing with { NoCatchAnimationFinished = true } });
            }
            if (state.Dialogue is null || fishing.State.SourceStep is not (10 or 14 or 15) ||
                fishing.State.SourceStep == 10 && fishing.State.FrameCounter != 0 || fishing.MessageFinished)
                return No("fishing_text_not_pending");
            if (command.Kind == "fishing_text_finished")
            {
                if (fishing.TextPrinted) return No("fishing_text_already_printed");
                return new(CommandStatus.Accepted, state with { Fishing = fishing with { TextPrinted = true } });
            }
            // Both source strings end in PAUSE_UNTIL_PRESS: printed text alone is not completion.
            if (!fishing.TextPrinted) return No("fishing_text_still_printing");
            return new(CommandStatus.Accepted, state with { Fishing = fishing with { MessageFinished = true } });
        }
        if (command.Kind != "fishing_tick") return No("fishing_active");
        var result = OldRodFishingRules.Advance(fishing.State,
            new(MessageFinished: fishing.MessageFinished, NoCatchAnimationFinished: fishing.NoCatchAnimationFinished), state.World.Rng);
        var next = state with
        {
            Fishing = fishing with { State = result.State },
            World = state.World with { Rng = result.GlobalRng }
        };
        if (fishing.State.SourceStep == 5 && result.State.SourceStep == 14)
            next = next with { Dialogue = new("gText_NotEvenANibble", _content.TextPages("gText_NotEvenANibble"), 0, false, true) };
        else if (fishing.State.SourceStep == 9 && result.State.SourceStep == 10)
            next = next with { Dialogue = new("gText_PokemonOnHook", _content.TextPages("gText_PokemonOnHook"), 0, false, true) };
        if (result.State.SourceStep == 10 && result.State.FrameCounter == 1) next = next with { Dialogue = null };
        if (result.State.Status == OldRodFishingStatus.NotEvenANibble)
            return new(CommandStatus.Accepted, next with { Fishing = null, Dialogue = null });
        if (result.State.Status == OldRodFishingStatus.EncounterReady)
            return new(CommandStatus.Accepted, BeginFishingWildBattle(next, result.State));
        return new(CommandStatus.Accepted, next);
    }

    private static Direction FishingOpposite(Direction direction) => direction switch
    { Direction.North => Direction.South, Direction.South => Direction.North, Direction.East => Direction.West, _ => Direction.East };
    private static bool FishingBlocksEdge(int behavior, Direction edge) => edge switch
    {
        Direction.East => behavior is 0x30 or 0x34 or 0x36,
        Direction.West => behavior is 0x31 or 0x35 or 0x37,
        Direction.North => behavior is 0x32 or 0x34 or 0x35,
        _ => behavior is 0x33 or 0x36 or 0x37
    };
}

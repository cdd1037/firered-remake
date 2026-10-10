using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretRocketHideoutRegion? RocketHideoutRegion { get; }
    public PretRocketHideoutPokemon? RocketHideoutPokemon { get; }
    public PretRocketHideoutItemsDocument? RocketHideoutItems { get; }
    public PretRocketHideoutPickupsDocument? RocketHideoutPickups { get; }
    public RocketHideoutTrainerCatalog? RocketHideoutTrainers { get; }
    public bool RocketHideoutEnabled => RocketHideoutRegion is not null;
    public ImmutableArray<int> RocketHideoutInitiallySetFlags => RocketHideoutRegion?.InitiallySetFlags ?? [];
    public ImmutableArray<CampaignMapMetatilePatch> RocketHideoutMetatilePatches =>
        RocketHideoutRegion?.MetatilePatches.Select(CampaignRocketPatch).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignRocketElevatorFloor> RocketHideoutElevatorFloors =>
        RocketHideoutRegion?.ElevatorFloors.Select(f => new CampaignRocketElevatorFloor(f.MenuIndex, f.Label,
            f.SourceFloorValue, f.DestinationMapId, f.X, f.Y, f.Elevation, f.DestinationWarpId)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignRocketElevatorEntrance> RocketHideoutElevatorEntrances =>
        RocketHideoutRegion?.ElevatorEntrances.Select(e => new CampaignRocketElevatorEntrance(e.SourceMapId,
            e.SourceWarpId, e.ElevatorWarpId, e.ReturnX, e.ReturnY, e.ReturnElevation)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignRocketDynamicExit> RocketHideoutDynamicExits =>
        RocketHideoutRegion?.DynamicExits.Select(e => new CampaignRocketDynamicExit(e.WarpIndex, e.X, e.Y,
            e.Elevation, e.SourceWarpId)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignMapMetatilePatch> RocketHideoutInitialPatches(
        bool entranceOpened, bool b1GuardDefeated, bool bothB4GuardsDefeated) =>
        RocketHideoutRegion?.InitialPatches(entranceOpened, b1GuardDefeated, bothB4GuardsDefeated)
            .Select(CampaignRocketPatch).ToImmutableArray() ?? [];

    private static CampaignMapMetatilePatch CampaignRocketPatch(PretRocketMetatilePatch patch) =>
        new(patch.Label, patch.MapId, patch.Cells.Select(c => new CampaignMapMetatileCell(c.X, c.Y,
            c.MetatileId, c.Collision, c.Elevation, c.RawAttributes, c.AtlasPath, c.AtlasX, c.AtlasY)).ToImmutableArray());

    private static PokemonCatalog RocketHideoutCampaignPokemon(PokemonCatalog catalog, PokemonCatalog previous)
    {
        PokemonFactory.ValidateCatalog(catalog);
        PokemonFactory.ValidateCatalog(previous);
        if (catalog.Species.Count != 99 || previous.Species.Count != 98 || catalog.Moves.Count != 230)
            throw new InvalidDataException("rocket-hideout_campaign_species_scope_differs");
        var moves = catalog.Moves;
        // Keep the authenticated Gym supplement (Shock Wave) alongside the regional catalog.
        foreach (var (id, move) in previous.Moves)
        {
            if (moves.TryGetValue(id, out var current))
            {
                if (current with { Flags = move.Flags, Source = move.Source } != move ||
                    !current.Flags.SequenceEqual(move.Flags))
                    throw new InvalidDataException("Conflicting Rocket Hideout predecessor move: " + id);
            }
            else moves = moves.Add(id, move);
        }
        if (moves.Count != 231) throw new InvalidDataException("rocket-hideout_campaign_move_scope_differs");
        var result = catalog with { Moves = moves };
        PokemonFactory.ValidateCatalog(result);
        return result;
    }

    private FieldMetatileDefinition? RocketHideoutMetatileDefinition(MapId map, int id)
    {
        var cells = RocketHideoutRegion!.MetatilePatches.Where(p => p.MapId == map)
            .SelectMany(p => p.Cells).Where(c => c.MetatileId == id).ToArray();
        if (cells.Length == 0) return null;
        if (cells.Any(c => c.RawAttributes != cells[0].RawAttributes))
            throw new InvalidDataException("rocket-hideout_conflicting_map_metatile: " + map + ":" + id);
        var attributes = PretWorldDecoder.DecodeAttributes(cells[0].RawAttributes);
        return new(id, attributes.BehaviorId, attributes.Terrain, attributes.EncounterType, attributes.LayerType);
    }

    private static IEnumerable<FieldPickupDefinition> RocketHideoutPickupDefinitions(
        PretRocketHideoutPickupsDocument pickups, EarlyShopCatalog shop, PewterMachineCatalog machines,
        PretRocketHideoutRegion region, PretStoryCompilation scripts)
    {
        foreach (var pickup in pickups.Pickups)
        {
            var item = shop.Items.SingleOrDefault(i => i.Id == pickup.Item);
            var machine = PewterMachineRules.Definition(machines, pickup.Item)?.Machine.Item;
            int itemNumber = item?.SourceItemId ?? machine?.SourceItemId ??
                throw new InvalidDataException("rocket-hideout_pickup_item_missing: " + pickup.Item);
            string symbol = item?.SourceItemSymbol ?? machine!.SourceItemSymbol;
            string name = item?.Name ?? machine!.Name;
            string pocket = item?.Pocket ?? machine!.Pocket;
            if (!scripts.TryConstant(pickup.Flag, out int flag) || flag != pickup.SourceFlagId)
                throw new InvalidDataException("rocket-hideout_pickup_flag_differs: " + pickup.Flag);
            if (!region.NewMaps.ContainsKey(PretRocketHideoutRegion.MapIdFromSourceSymbol(pickup.Map)))
                throw new InvalidDataException("rocket-hideout_pickup_map_missing: " + pickup.Map);
            bool hidden = pickup.Kind == "hidden";
            yield return new("rocket-hideout:" + pickup.Flag, pickup.Map, hidden, pickup.LocalId,
                pickup.X, pickup.Y, pickup.Elevation, hidden ? "EventScript_HiddenItemScript" : pickup.Script!,
                symbol, itemNumber, pickup.Item, name, pocket, pickup.Quantity, pickup.Flag, flag);
        }
    }
}

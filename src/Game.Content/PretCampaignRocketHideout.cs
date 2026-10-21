using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretRocketHideoutRegion RocketHideoutRegion { get; }
    public PokemonPresentationPackage? RocketHideoutPokemon { get; }
    public PretRocketHideoutItemsDocument? RocketHideoutItems { get; }
    public PretRocketHideoutPickupsDocument? RocketHideoutPickups { get; }
    public OrdinaryTrainerCatalog? RocketHideoutTrainers { get; }
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
        bool entranceOpened,bool b1GuardDefeated,bool bothB4GuardsDefeated) =>
        RocketHideoutRegion?.InitialPatches(entranceOpened, b1GuardDefeated, bothB4GuardsDefeated)
            .Select(CampaignRocketPatch).ToImmutableArray() ?? [];

    private static CampaignMapMetatilePatch CampaignRocketPatch(PretRocketMetatilePatch patch) =>
        new(patch.Label, patch.MapId, patch.Cells.Select(c => new CampaignMapMetatileCell(c.X, c.Y,
            c.MetatileId, c.Collision, c.Elevation, c.RawAttributes, c.AtlasPath, c.AtlasX, c.AtlasY)).ToImmutableArray());

    private FieldMetatileDefinition? RocketHideoutMetatileDefinition(MapId map,int id)
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
        PretRocketHideoutPickupsDocument pickups,EarlyShopCatalog shop,MachineCatalog machines,PretRocketHideoutRegion region,PretStoryCompilation scripts)
    {
        foreach (var pickup in pickups.Pickups.Where(p => p.Kind != "hidden"))
        {
            var item = shop.Items.SingleOrDefault(i => i.Id == pickup.Item);
            var machine = MachineRules.Definition(machines, pickup.Item)?.Machine.Item;
            int itemNumber = item?.SourceItemId ?? machine?.SourceItemId ??
                throw new InvalidDataException("rocket-hideout_pickup_item_missing: " + pickup.Item);
            string symbol = item?.SourceItemSymbol ?? machine!.SourceItemSymbol;
            string name = item?.Name ?? machine!.Name;
            string pocket = item?.Pocket ?? machine!.Pocket;
            if (!scripts.TryConstant(pickup.Flag, out int flag) || flag != pickup.SourceFlagId)
                throw new InvalidDataException("rocket-hideout_pickup_flag_differs: " + pickup.Flag);
            if (!region.NewMaps.ContainsKey(region.NewMaps.Values.First().ResolveMapSymbol(pickup.Map)))
                throw new InvalidDataException("rocket-hideout_pickup_map_missing: " + pickup.Map);

            yield return new("rocket-hideout:" + pickup.Flag, pickup.Map, false, pickup.LocalId,
                pickup.X, pickup.Y, pickup.Elevation, pickup.Script!,
                symbol, itemNumber, pickup.Item, name, pocket, pickup.Quantity, pickup.Flag, flag);
        }
    }
}

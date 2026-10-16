using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretRockTunnelRegion RockTunnelRegion { get; }
    public PokemonPresentationPackage? RockTunnelPokemon { get; }
    public PretRockTunnelItemsDocument? RockTunnelItems { get; }
    public PretRockTunnelEncounters? RockTunnelEncounters { get; }
    public RockTunnelTrainerCatalog? RockTunnelTrainers { get; }

    private static IEnumerable<FieldPickupDefinition> RockTunnelPickupDefinitions(
        PretRockTunnelItemsDocument items,EarlyShopCatalog shop,MachineCatalog machines,PretStoryCompilation scripts)
    {
        foreach (var pickup in items.Pickups)
        {
            var item = shop.Items.FirstOrDefault(i => i.Id == pickup.Item);
            var machine = MachineRules.Definition(machines, pickup.Item)?.Machine.Item;
            int itemNumber = item?.SourceItemId ?? machine?.SourceItemId ??
                throw new InvalidDataException("rocktunnel_pickup_item_missing: " + pickup.Item);
            string symbol = item?.SourceItemSymbol ?? machine!.SourceItemSymbol;
            string name = item?.Name ?? machine!.Name;
            string pocket = item?.Pocket ?? machine!.Pocket;
            if (!scripts.TryConstant(pickup.Flag, out int flag))
                throw new InvalidDataException("rocktunnel_pickup_flag_missing: " + pickup.Flag);
            bool hidden = pickup.Kind == "hidden";
            string map = "MAP_" + Regex.Replace(pickup.Map, "([a-z])([A-Z])", "$1_$2").ToUpperInvariant();
            yield return new("rocktunnel:" + pickup.Flag, map, hidden, pickup.LocalObjectId,
                pickup.X, pickup.Y, pickup.Elevation, hidden ? "EventScript_HiddenItemScript" : pickup.Script!,
                symbol, itemNumber, pickup.Item, name, pocket, pickup.Quantity, pickup.Flag, flag);
        }
    }
}

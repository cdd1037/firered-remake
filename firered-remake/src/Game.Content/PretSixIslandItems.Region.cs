using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public static partial class PretSixIslandItems
{
    public static SixIslandItemContent Load(PretSixIslandRegion region, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, PretStoryCompilation scripts, string? itemRoot = null)
    {
        var root = PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory);
        var document = LoadDocument(itemRoot ?? root);
        var content = LoadCatalogs(document, pokemon, previousShop, previousMachines);
        foreach (var metadata in document.Items)
        {
            var item = metadata.Item;
            if (region.Constants[item.SourceItemSymbol] != item.SourceItemId )
                throw new InvalidDataException("six_island_item_declaration");
        }
        if (region.Contracts.GetProperty("itemPickups").GetArrayLength() != 5 ||
            region.Contracts.GetProperty("hiddenItems").GetArrayLength() != 4)
            throw new InvalidDataException("six_island_pickup_contract_scope");
        foreach (var pickup in content.Pickups)
        {
            var map = region.NewMaps[PretSixIslandRegion.MapIdFromSourceSymbol(pickup.MapSymbol)];
            var row = region.Contracts.GetProperty(pickup.Hidden ? "hiddenItems" : "itemPickups").EnumerateArray().Single(r =>
                r.GetProperty("map").GetString() == pickup.MapSymbol && r.GetProperty("event").GetProperty("flag").GetString() == pickup.Flag);
            var sourceEvent = row.GetProperty("event");
            var actual = pickup.Hidden ? map.Events.GetProperty("bg_events").EnumerateArray().Single(e =>
                    e.TryGetProperty("flag", out var flag) && flag.GetString() == pickup.Flag) :
                map.Events.GetProperty("object_events")[pickup.SourceLocalId!.Value - 1];
            if (!JsonElement.DeepEquals(sourceEvent, actual) ||
                sourceEvent.GetProperty("x").GetInt32() != pickup.X || sourceEvent.GetProperty("y").GetInt32() != pickup.Y ||
                sourceEvent.GetProperty("elevation").GetInt32() != pickup.Elevation ||
                !scripts.TryConstant(pickup.Flag, out int flagId) || flagId != pickup.SourceFlagId ||
                region.Constants[pickup.ItemSymbol] != pickup.SourceItemId || pickup.Quantity != 1)
                throw new InvalidDataException("six_island_pickup_event");
            if (pickup.Hidden)
            {
                if (sourceEvent.GetProperty("item").GetString() != pickup.ItemSymbol || sourceEvent.GetProperty("quantity").GetInt32() != 1 ||
                    sourceEvent.GetProperty("underfoot").GetBoolean()) throw new InvalidDataException("six_island_hidden_pickup");
            }
            else if (sourceEvent.GetProperty("graphics_id").GetString() != "OBJ_EVENT_GFX_ITEM_BALL" ||
                sourceEvent.GetProperty("script").GetString() != pickup.Script ||
                row.GetProperty("localId").GetInt32() != pickup.SourceLocalId ||
                scripts.FindProgram(pickup.Script) is not {} program || !program.Instructions.Any(i =>
                    i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8000",pickup.ItemSymbol })))
                throw new InvalidDataException("six_island_pickup_program");
        }
        if (scripts.FindProgram("SixIsland_Mart_EventScript_Clerk") is not {} clerk ||
            !clerk.Instructions.Any(i => i.Opcode == "pokemart" && i.Args.SequenceEqual(new[] { document.Shop.Id })))
            throw new InvalidDataException("six_island_shop_program");
        return content;
    }
}

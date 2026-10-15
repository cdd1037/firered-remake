using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed record SeviiRubyMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record SeviiRubyItemsDocument(string SourceCommit, string Profile, ImmutableArray<MachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<SeviiRubyMachineRows> MachineAdditions, MachineDefinition Hm06);
public sealed record SeviiRubyItemContent(ImmutableArray<MachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    MachineCatalog Machines);

/// <summary>Authenticated Ruby keys/HM06 and eight ordinary pickups. The Ruby object executes its original bespoke script.</summary>
public static class PretSeviiRubyItems
{
    public static SeviiRubyItemContent Load(PretSeviiRubyRegion region, GamePokemonCatalog pokemon,
        EarlyShopCatalog previousShop, MachineCatalog previousMachines, PretStoryCompilation scripts, SharedMachineCatalog? shared = null)
    {
        var path = Path.Combine(Path.GetDirectoryName(region.GeneratedDirectory)!, "sevii-ruby-items", "items.json");
        var bytes = File.ReadAllBytes(path);
        var document = JsonSerializer.Deserialize<SeviiRubyItemsDocument>(bytes,
            (shared ?? SharedMachineCatalog.LoadProject(Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../..")))).CreateJsonOptions(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }))
            ?? throw new InvalidDataException("sevii_ruby_items_missing");
        if (            document.Items.Length != 5 || document.Additions.Length != 4 || document.MachineAdditions.Length != 36)
            throw new InvalidDataException("sevii_ruby_items_scope");
        return new(document.Items, Pickups(region, previousShop, scripts), previousMachines);
    }

    private static ImmutableArray<FieldPickupDefinition> Pickups(PretSeviiRubyRegion region, EarlyShopCatalog shop, PretStoryCompilation scripts)
    {
        string[] required = ["ITEM_ETHER", "ITEM_MAX_REPEL", "ITEM_CARBOS", "ITEM_ULTRA_BALL", "ITEM_FIRE_STONE", "ITEM_DIRE_HIT"];
        var missing = required.Where(symbol => !shop.Items.Any(i => i.SourceItemSymbol == symbol)).ToArray();
        if (missing.Length != 0) throw new InvalidDataException("sevii_ruby_pickup_items_missing: " + string.Join(", ", missing));
        var result = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var row in region.Contracts.GetProperty("itemPickups").EnumerateArray())
        {
            var sourceEvent = row.GetProperty("event");
            // Ruby sets FLAG_GOT_RUBY and removes itself before attempting the source giveitem_msg.
            // It must never acquire the ordinary ball recipe's success-first ordering.
            if (sourceEvent.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_RUBY") continue;
            if (sourceEvent.GetProperty("graphics_id").GetString() != "OBJ_EVENT_GFX_ITEM_BALL")
                throw new InvalidDataException("sevii_ruby_item_graphics");
            Add(row.GetProperty("map").GetString()!, sourceEvent, false, row.GetProperty("localId").GetInt32(),
                row.GetProperty("item").GetString()!, row.GetProperty("quantity").GetInt32());
        }
        foreach (var row in region.Contracts.GetProperty("hiddenItems").EnumerateArray())
        {
            var sourceEvent = row.GetProperty("event");
            if (sourceEvent.GetProperty("underfoot").GetBoolean()) throw new InvalidDataException("sevii_ruby_hidden_item_underfoot");
            Add(row.GetProperty("map").GetString()!, sourceEvent, true, null,
                sourceEvent.GetProperty("item").GetString()!, sourceEvent.GetProperty("quantity").GetInt32());
        }
        if (result.Count != 8 || result.Select(p => p.Flag).Distinct().Count() != 8)
            throw new InvalidDataException("sevii_ruby_pickup_scope");
        return result.ToImmutable();

        void Add(string symbol, JsonElement sourceEvent, bool hidden, int? localId, string itemSymbol, int quantity)
        {
            var map = region.NewMaps[PretSeviiRubyRegion.MapIdFromSourceSymbol(symbol)];
            var item = shop.Items.SingleOrDefault(i => i.SourceItemSymbol == itemSymbol)
                ?? throw new InvalidDataException("sevii_ruby_pickup_item_missing: " + itemSymbol);
            string flag = sourceEvent.GetProperty("flag").GetString()!, script = hidden ? "EventScript_HiddenItemScript" : sourceEvent.GetProperty("script").GetString()!;
            var actual = hidden ? map.Events.GetProperty("bg_events").EnumerateArray().Single(e => e.TryGetProperty("flag", out var f) && f.GetString() == flag) :
                map.Events.GetProperty("object_events")[localId!.Value - 1];
            if (!JsonElement.DeepEquals(sourceEvent, actual) || !scripts.TryConstant(flag, out int sourceFlag) ||
                region.Constants[itemSymbol] != item.SourceItemId || quantity != 1)
                throw new InvalidDataException("sevii_ruby_pickup_event");
            if (!hidden && (scripts.FindProgram(script) is not {} program || !program.Instructions.Any(i =>
                    i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8000",itemSymbol }))))
                throw new InvalidDataException("sevii_ruby_pickup_program");
            result.Add(new("sevii-ruby:" + flag, symbol, hidden, localId, sourceEvent.GetProperty("x").GetInt32(),
                sourceEvent.GetProperty("y").GetInt32(), sourceEvent.GetProperty("elevation").GetInt32(), script, itemSymbol,
                item.SourceItemId, item.Id, item.Name, item.Pocket, quantity, flag, sourceFlag));
        }
    }
}

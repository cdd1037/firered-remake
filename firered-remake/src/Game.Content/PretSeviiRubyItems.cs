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

public sealed record SeviiRubyMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SeviiRubyItemsDocument(string SourceCommit, string Profile, ImmutableArray<PewterMachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<SeviiRubyMachineRows> MachineAdditions, PewterAdditionalMachine Hm06);
public sealed record SeviiRubyItemContent(ImmutableArray<PewterMachineItem> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    SeviiRubyShopCatalog Shop, SeviiRubyMachineCatalog Machines);

/// <summary>Authenticated Ruby keys/HM06 and eight ordinary pickups. The Ruby object executes its original bespoke script.</summary>
public static class PretSeviiRubyItems
{
    public const string ItemsSha256 = "326a7f7800403a980d6cb77e066cf91b5d79fc2743520382798d79ecb794db9d";
    public static SeviiRubyItemContent Load(PretSeviiRubyRegion region, SeviiRubyOwnedPokemonCatalog pokemon,
        EarlyShopCatalog previousShop, PewterMachineCatalog previousMachines, PretStoryCompilation scripts)
    {
        var path = Path.Combine(Path.GetDirectoryName(region.GeneratedDirectory)!, "sevii-ruby-items", "items.json");
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != ItemsSha256)
            throw new InvalidDataException("sevii_ruby_items_source");
        var document = JsonSerializer.Deserialize<SeviiRubyItemsDocument>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })
            ?? throw new InvalidDataException("sevii_ruby_items_missing");
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != "firered-sevii-ruby-items-v1" ||
            document.Items.Length != 5 || document.Additions.Length != 4 || document.MachineAdditions.Length != 36)
            throw new InvalidDataException("sevii_ruby_items_scope");
        foreach (var item in document.Items)
        {
            var declaration = region.Declarations.GetProperty("itemDeclarations").EnumerateArray().Single(d => d.GetProperty("itemId").GetString() == item.Item.SourceItemSymbol);
            if (declaration.GetProperty("english").GetString() != item.Item.Name ||
                declaration.GetProperty("pocket").GetString() != item.Item.Pocket ||
                declaration.GetProperty("importance").GetInt32() != item.Item.Importance ||
                declaration.GetProperty("registrability").GetInt32() != item.Item.Registrability ||
                region.Constants[item.Item.SourceItemSymbol] != item.Item.SourceItemId ||
                region.SourceFiles.Single(f => f.Path == item.Item.Source.Path).Sha256 != item.Item.Source.Sha256)
                throw new InvalidDataException("sevii_ruby_item_declaration");
        }
        var shopProof = VictoryRoadShopCatalog.For(previousShop) ?? throw new InvalidDataException("sevii_ruby_items_shop_predecessor");
        var shop = SeviiRubyShopCatalog.Authenticate(shopProof, previousShop with {
            Profile = SeviiRubyShopCatalog.Profile, Items = previousShop.Items.AddRange(document.Additions) });
        var machineProof = VictoryRoadMachineCatalog.For(previousMachines) ?? throw new InvalidDataException("sevii_ruby_items_machine_predecessor");
        var additions = document.MachineAdditions.ToDictionary(m => m.Item);
        var machines = SeviiRubyMachineCatalog.Authenticate(machineProof, pokemon, previousMachines with {
            Profile = SeviiRubyMachineCatalog.Profile,
            Compatibility = previousMachines.Compatibility.AddRange(additions[previousMachines.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previousMachines.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).Append(document.Hm06).ToImmutableArray() });
        return new(document.Items, Pickups(region, shop.Catalog, scripts), shop, machines);
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
            var statement = row.GetProperty("script").GetProperty("lines").EnumerateArray()
                .Select(l => l.GetProperty("text").GetString()!.Trim()).Single(s => s.StartsWith("finditem ", StringComparison.Ordinal));
            var match = Regex.Match(statement, @"^finditem (ITEM_\w+)(?:,\s*(\d+))?$");
            if (!match.Success) throw new InvalidDataException("sevii_ruby_item_recipe");
            Add(row.GetProperty("map").GetString()!, sourceEvent, false, row.GetProperty("localId").GetInt32(),
                match.Groups[1].Value, match.Groups[2].Success ? int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : 1);
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
                    i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8000", itemSymbol }))))
                throw new InvalidDataException("sevii_ruby_pickup_program");
            result.Add(new("sevii-ruby:" + flag, symbol, hidden, localId, sourceEvent.GetProperty("x").GetInt32(),
                sourceEvent.GetProperty("y").GetInt32(), sourceEvent.GetProperty("elevation").GetInt32(), script, itemSymbol,
                item.SourceItemId, item.Id, item.Name, item.Pocket, quantity, flag, sourceFlag));
        }
    }
}

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class RegionalPickupDisplayTextTests
{
    [TestMethod]
    [DataRow("cerulean-cave")]
    [DataRow("power-plant")]
    [DataRow("seafoam")]
    [DataRow("lost-cave")]
    public void PickupsUseCanonicalNamesWithoutRegionalDisplayAuthority(string region)
    {
        var fixture = Load(region);
        var original = Read(fixture);
        Assert.IsFalse(original.IsDefaultOrEmpty);
        fixture = fixture with { Shop = fixture.Shop with
        {
            Items = fixture.Shop.Items.Select(row => row with
            {
                Item = row.Item with { Name = "Edited display: " + row.SourceItemSymbol }
            }).ToImmutableArray()
        }};
        foreach (bool staleName in new[] { false, true })
        {
            foreach (var row in fixture.Items["declarations"]!.AsArray())
            {
                if (staleName) row!["english"] = "Obsolete regional display name";
                else row!.AsObject().Remove("english");
            }
            var actual = Read(fixture);
            CollectionAssert.AreEqual(original.Select(row => row with
            {
                ItemName = fixture.Shop.Items.Single(item => item.Id == row.Item).Name
            }).ToArray(), actual.ToArray());
        }
    }

    [TestMethod]
    [DataRow("missing-declaration")]
    [DataRow("missing-admission")]
    [DataRow("identity")]
    [DataRow("numeric-id")]
    [DataRow("missing-item-constant")]
    [DataRow("pocket")]
    [DataRow("price")]
    public void EditedNamesDoNotRelaxItemIdentityOrBusinessFacts(string change)
    {
        var fixture = Load("seafoam");
        var first = fixture.Contracts["itemPickups"]![0]!;
        string symbol = first["item"]!.GetValue<string>();
        fixture = fixture with { Shop = fixture.Shop with
        {
            Items = fixture.Shop.Items.Select(row => row with
            {
                Item = row.Item with { Name = "Edited display name" }
            }).ToImmutableArray()
        }};
        RemoveRegionalNames(fixture);
        var item = fixture.Shop.Items.Single(row => row.SourceItemSymbol == symbol);
        switch (change)
        {
            case "missing-declaration":
                var declarations = fixture.Items["declarations"]!.AsArray();
                declarations.Remove(declarations.Single(row => row!["itemId"]!.GetValue<string>() == symbol));
                break;
            case "missing-admission":
                fixture = fixture with { Shop = fixture.Shop with { Items = fixture.Shop.Items.Remove(item) }};
                break;
            case "missing-item-constant":
                fixture = fixture with { Scripts = fixture.Scripts with { Constants = fixture.Scripts.Constants.Remove(symbol) }};
                break;
            default:
                var changed = change switch
                {
                    "identity" => item.Item with { Id = new("pret:ITEM_UNKNOWN") },
                    "numeric-id" => item.Item with { SourceItemId = item.SourceItemId + 1 },
                    "pocket" => item.Item with { Pocket = "POCKET_KEY_ITEMS" },
                    "price" => item.Item with { Price = item.Price + 1 },
                    _ => throw new ArgumentOutOfRangeException(nameof(change))
                };
                fixture = fixture with { Shop = fixture.Shop with
                {
                    Items = fixture.Shop.Items.Replace(item, item with { Item = changed })
                }};
                break;
        }
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Read(fixture));
        Assert.AreEqual("seafoam_pickup_item_" + (change == "missing-admission" ? "missing:" : "source:") + symbol,
            error.Message);
    }

    [TestMethod]
    [DataRow("quantity")]
    [DataRow("event")]
    [DataRow("missing-flag")]
    [DataRow("missing-program")]
    [DataRow("source-path")]
    [DataRow("unsupported-instruction")]
    [DataRow("item-operand")]
    [DataRow("quantity-operand")]
    [DataRow("duplicate-flag")]
    public void NameFreeDeclarationsRetainPickupAndScriptConstraints(string change)
    {
        var fixture = Load("seafoam");
        RemoveRegionalNames(fixture);
        var rows = fixture.Contracts["itemPickups"]!.AsArray();
        var first = rows[0]!;
        string label = first["event"]!["script"]!.GetValue<string>();
        string flag = first["event"]!["flag"]!.GetValue<string>();
        switch (change)
        {
            case "quantity": first["quantity"] = 2; break;
            case "event": first["event"]!["x"] = first["event"]!["x"]!.GetValue<int>() + 1; break;
            case "source-path": first["sourceRef"]!["path"] = "data/scripts/incorrect.inc"; break;
            case "duplicate-flag": rows.Add(first.DeepClone()); break;
            case "missing-flag":
                fixture = fixture with { Scripts = fixture.Scripts with { Constants = fixture.Scripts.Constants.Remove(flag) }};
                break;
            case "missing-program":
                fixture = fixture with { Scripts = fixture.Scripts with { Programs = fixture.Scripts.Programs.Remove(label) }};
                break;
            default:
                var program = fixture.Scripts.Programs[label];
                var instructions = program.Instructions;
                if (change == "unsupported-instruction")
                    instructions = instructions.SetItem(0, instructions[0] with { Op = StoryOp.Unsupported });
                else
                {
                    string variable = change == "item-operand" ? "VAR_0x8000" : "VAR_0x8001";
                    var assignment = instructions.Single(row => row.Opcode == "setorcopyvar" && row.Args[0] == variable);
                    instructions = instructions.Replace(assignment, assignment with
                    {
                        Args = assignment.Args.SetItem(1, change == "item-operand" ? "ITEM_UNKNOWN" : "2")
                    });
                }
                fixture = fixture with { Scripts = fixture.Scripts with
                {
                    Programs = fixture.Scripts.Programs.SetItem(label, program with { Instructions = instructions })
                }};
                break;
        }
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Read(fixture));
        Assert.AreEqual(change == "duplicate-flag" ? "seafoam_pickup_scope" : "seafoam_pickup_source:" + label,
            error.Message);
    }

    private static void RemoveRegionalNames(PickupFixture fixture)
    {
        foreach (var row in fixture.Items["declarations"]!.AsArray()) row!.AsObject().Remove("english");
    }

    private static ImmutableArray<FieldPickupDefinition> Read(PickupFixture fixture) =>
        PretCampaignCatalog.SourceRegionPickupDefinitions(fixture.Maps, JsonSerializer.SerializeToElement(fixture.Items),
            JsonSerializer.SerializeToElement(fixture.Contracts), fixture.ResolveMap, fixture.Region, fixture.Shop, fixture.Scripts);

    private static PickupFixture Load(string region)
    {
        var world = TestAssets.Cached(() => PretCurrentWorld.Load(TestAssets.Root));
        var scripts = TestAssets.Cached(() => PretStoryRuntime.Load(TestAssets.Root).Compilation);
        var shop = TestAssets.Cached(() => CurrentItemCatalog.Load(TestAssets.Root, SharedItemCatalog.LoadProject(TestAssets.Root)));
        PickupFixture Create(IReadOnlyDictionary<MapId, PretWorldMap> maps, JsonElement items, JsonElement contracts,
            Func<string, MapId> resolve) => new(region, maps, JsonNode.Parse(items.GetRawText())!,
                JsonNode.Parse(contracts.GetRawText())!, resolve, shop, scripts);
        switch (region)
        {
            case "cerulean-cave":
                var cave = PretCeruleanCaveRegion.Load(world);
                return Create(cave.NewMaps, cave.Items, cave.Contracts, world.Resolve);
            case "power-plant":
                var power = PretPowerPlantRegion.Load(world);
                return Create(power.NewMaps, power.Items, power.Contracts, world.Resolve);
            case "seafoam":
                var seafoam = PretSeafoamRegion.Load(world);
                return Create(seafoam.NewMaps, seafoam.Items, seafoam.Contracts, world.Resolve);
            case "lost-cave":
                var lost = PretLostCaveRegion.Load(world);
                return Create(lost.NewMaps, lost.Items, lost.Contracts, world.Resolve);
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }

    private sealed record PickupFixture(string Region, IReadOnlyDictionary<MapId, PretWorldMap> Maps,
        JsonNode Items, JsonNode Contracts, Func<string, MapId> ResolveMap, EarlyShopCatalog Shop, PretStoryCompilation Scripts);
}

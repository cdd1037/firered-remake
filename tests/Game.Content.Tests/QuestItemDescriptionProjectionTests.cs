using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class QuestItemDescriptionProjectionTests
{
    private const string BaseUiPath = "content/generated/ui/quest-item-ui/quest-item-ui.json";
    private const string TeaPath = PretRegionalTeaArt.GeneratedDirectory + "tea-art.json";
    private static readonly SourceTextPack English = SourceTextPack.Load(null);
    private static SharedItemCatalog EnglishItems => TestAssets.Cached(() => SharedItemCatalog.LoadProject(TestAssets.Root, English));
    private static GamePokemonCatalog Pokemon => TestAssets.Cached(() => SharedMachineCatalogTests.Pokemon());
    private static QuestItemUiDocument SourceUi => TestAssets.Cached(() => LoadSourceUi());

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EveryAssembledRowUsesSharedDisplayTextAndKeepsAllPresentationMetadata(bool useOverrides)
    {
        // Real base art plus the ten quest/service items outside available-items, and TM Case.
        // This deliberately does not duplicate the complete Godot regional assembly.
        var source = SourceUi;
        Assert.AreEqual(12, source.Items.Length);
        Assert.AreEqual(source.Items.Length, source.Items.Select(row => row.Id).Distinct().Count());
        var before = JsonSerializer.Serialize(source);
        using var temp = TestAssets.TemporaryOutput();
        if (useOverrides)
        {
            Write(temp.Path, "item-names", source.Items.ToDictionary(row => row.Id, row => "Edited " + row.SourceSymbol));
            Write(temp.Path, "item-descriptions", source.Items.ToDictionary(row => row.Id,
                row => "Shared " + row.SourceSymbol + @"; literal \n and trailing$"));
            Write(temp.Path, "machine-descriptions", new Dictionary<string, string>
                { [MachineRules.TmCaseId.Value] = @"Machine-owned case text; literal \n and trailing$" });
        }
        var items = SharedItemCatalog.LoadProject(TestAssets.Root, useOverrides ? SourceTextPack.Load(temp.Path) : English);
        var tmCase = TmCase(items);
        var projected = PretQuestItemUi.WithDisplayText(source, items, tmCase);
        Assert.AreEqual(source with { Items = projected.Items }, projected);
        Assert.AreEqual(before, JsonSerializer.Serialize(source));
        for (var index = 0; index < source.Items.Length; index++)
        {
            var row = source.Items[index];
            var id = new ItemId(row.Id);
            var description = id == MachineRules.TmCaseId ? tmCase.Description : items.Description(id);
            Assert.AreEqual(row with { Name = items.Definition(id).Name, Description = description },
                projected.Items[index], row.Id);
        }
        if (useOverrides)
        {
            Assert.AreNotEqual(items.Description(MachineRules.TmCaseId), tmCase.Description);
            Assert.AreEqual(@"Machine-owned case text; literal \n and trailing$",
                projected.Items.Single(row => row.Id == MachineRules.TmCaseId.Value).Description);
        }
    }

    [TestMethod]
    public void TranslatedTeaLoadsUnchangedArtThenUsesCanonicalDisplayText()
    {
        using var temp = TestAssets.TemporaryOutput();
        Write(temp.Path, "item-names", new Dictionary<string, string> { ["pret:ITEM_TEA"] = "Warm Brew" });
        Write(temp.Path, "item-descriptions", new Dictionary<string, string> { ["pret:ITEM_TEA"] = "A soothing drink." });
        var items = SharedItemCatalog.LoadProject(TestAssets.Root, SourceTextPack.Load(temp.Path));
        var tea = Tea(items);
        var original = PretRegionalTeaArt.Load(TestAssets.Root, Tea(EnglishItems));
        var translated = PretRegionalTeaArt.Load(TestAssets.Root, tea);
        Assert.AreEqual(original.Document, translated.Document);
        CollectionAssert.AreEqual(original.ReadPng(), translated.ReadPng());
        Assert.AreNotEqual(tea.Item.Name, translated.Document.Item.Name);
        Assert.AreNotEqual(tea.Description, translated.Document.Item.Description);
        Assert.AreEqual(EnglishItems.Definition(tea.Item.Id) with { Name = "Warm Brew" }, tea.Item);
        var projected = PretQuestItemUi.WithDisplayText(SourceUi, items, TmCase(items));
        var row = projected.Items.Single(entry => entry.Id == tea.Item.Id.Value);
        Assert.AreEqual(translated.Document.Item with { Name = "Warm Brew", Description = "A soothing drink." }, row);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("sourceItemId")]
    [DataRow("sourceItemSymbol")]
    [DataRow("pocket")]
    [DataRow("price")]
    [DataRow("canToss")]
    [DataRow("canSell")]
    [DataRow("canGive")]
    [DataRow("fieldUseFunction")]
    public void TeaStillRequiresItsIdentityPocketPermissionsAndFieldUse(string field)
    {
        var tea = Tea(EnglishItems);
        var item = tea.Item with { Name = "Warm Brew" };
        item = field switch
        {
            "id" => item with { Id = QuestItemRules.TownMapId },
            "sourceItemId" => item with { SourceItemId = item.SourceItemId + 1 },
            "sourceItemSymbol" => item with { SourceItemSymbol = QuestItemRules.TownMapSymbol },
            "pocket" => item with { Pocket = "POCKET_ITEMS" },
            "price" => item with { Price = 1 },
            "canToss" => item with { CanToss = true },
            "canSell" => item with { CanSell = true },
            "canGive" => item with { CanGive = true },
            "fieldUseFunction" => item with { FieldUseFunction = "FieldUseFunc_TownMap" },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        Assert.AreEqual("Tea presentation differs from the authoritative key item.",
            Assert.ThrowsExactly<InvalidDataException>(() => PretRegionalTeaArt.Load(TestAssets.Root,
                tea with { Item = item, Description = "A soothing drink." })).Message);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("sourceSymbol")]
    [DataRow("fieldUseFunction")]
    [DataRow("name")]
    [DataRow("description")]
    [DataRow("icon")]
    [DataRow("assetId")]
    [DataRow("assetTexturePath")]
    [DataRow("texturePath")]
    [DataRow("width")]
    [DataRow("height")]
    [DataRow("x")]
    [DataRow("y")]
    public void TeaArtStillRejectsChangedIdentityMissingRawTextAndLayout(string field)
    {
        using var temp = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(temp.Path, [TeaPath, PretRegionalTeaArt.GeneratedDirectory + "regional-tea.png"]);
        TestAssets.EditJson(temp.Path, TeaPath, data =>
        {
            switch (field)
            {
                case "id": data["item"]![field] = "pret:ITEM_TOWN_MAP"; break;
                case "sourceSymbol": data["item"]![field] = "ITEM_TOWN_MAP"; break;
                case "fieldUseFunction": data["item"]![field] = "FieldUseFunc_TownMap"; break;
                case "name": case "description": data["item"]![field] = " \t"; break;
                case "icon": data["item"]![field] = "another-icon"; break;
                case "assetId": data["asset"]!["id"] = "another-icon"; break;
                case "assetTexturePath": data["asset"]!["texturePath"] = "another-icon.png"; break;
                case "texturePath": data[field] = "another-icon.png"; break;
                case "width": case "height": case "x": case "y":
                    data["asset"]![field] = data["asset"]![field]!.GetValue<int>() + 1;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => PretRegionalTeaArt.Load(temp.Path,
            Tea(EnglishItems) with { Description = "A soothing drink." }));
        Assert.AreEqual(field is "id" or "sourceSymbol" or "fieldUseFunction" or "name" or "description"
            ? "Tea presentation differs from the authoritative key item." : "Tea icon layout differs.", error.Message);
    }

    private static MachineItem Tea(SharedItemCatalog items) =>
        new(items.Definition(new("pret:ITEM_TEA")), items.Description(new("pret:ITEM_TEA")));

    private static MachineItem TmCase(SharedItemCatalog items) =>
        SharedMachineCatalog.LoadProject(TestAssets.Root, items).LoadCurrent(Pokemon).TmCase;

    private static QuestItemUiDocument LoadSourceUi()
    {
        var items = EnglishItems;
        var document = PretQuestItemUi.Load(Read(BaseUiPath));
        var position = document.Assets.Single(asset => asset.Id == document.Items[0].Icon);
        void Add(QuestItemUiEntry row, QuestItemUiAsset asset) => document = document with
        {
            Items = document.Items.Add(row),
            Assets = document.Assets.Add(asset with { X = position.X, Y = position.Y })
        };
        void AddItem(MachineItem value, ItemIcon icon, string assetId) => Add(
            new(value.Item.Id.Value, value.Item.SourceItemSymbol, value.Item.Name, value.Description,
                assetId, value.Item.FieldUseFunction),
            new(assetId, icon.PngPath, icon.Width, icon.Height, position.X, position.Y));
        var oldMan = PretOldManArt.Load(Read("content/generated/sprites/pokemon/old-man/old-man-art.json"));
        var tvIcon = oldMan.Assets.Single(asset => asset.Id == oldMan.TeachyTv.Icon);
        Add(oldMan.TeachyTv, new(tvIcon.Id, tvIcon.TexturePath, tvIcon.Width, tvIcon.Height, position.X, position.Y));
        var map = PretTownMap.Load(TestAssets.Root, items);
        var mapIcon = map.Asset(map.ItemIconAssetId);
        Add(new(map.Item.Id.Value, map.Item.SourceItemSymbol, map.Item.Name, map.ItemDescription,
                mapIcon.Id, map.Item.FieldUseFunction),
            new(mapIcon.Id, mapIcon.TexturePath, mapIcon.Width, mapIcon.Height, position.X, position.Y));
        foreach (var path in new[]
        {
            "content/generated/maps/regions/city-services/items.json",
            "content/generated/maps/regions/good-rod-service/items.json",
            "content/generated/maps/regions/super-rod-service/items.json",
            "content/generated/data/services/game-corner-coins/items.json"
        })
        {
            var service = ContentJson.ParseStrict<PretCityServiceItemsDocument>(Read(path), items.CreateJsonOptions());
            foreach (var value in service.Items)
                AddItem(value, service.Icons.Single(icon => icon.Item == value.Item.Id), "city-service-" + value.Item.SourceItemSymbol);
        }
        var tea = PretRegionalTeaArt.Load(TestAssets.Root, Tea(items));
        Add(tea.Document.Item, tea.Document.Asset);
        var caseIcon = ItemIconAssets.LoadProject(TestAssets.Root,
            "content/generated/data/machine-learning/pewter.json", MachineRules.TmCaseId).Single();
        AddItem(TmCase(items), caseIcon, "machine-tm-case");
        return document;
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(TestAssets.Root, relative));

    private static void Write(string root, string group, Dictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.Combine(root, "source"));
        File.WriteAllText(Path.Combine(root, "source", group + ".json"), JsonSerializer.Serialize(values));
    }
}

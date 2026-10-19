using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SourceNameTextPackTests
{
    private static readonly SourceTextPack English = SourceTextPack.Load(null);
    private static PokemonCatalog Pokemon => TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root).Catalog);
    private static GamePokemonCatalog GamePokemon => TestAssets.Cached(() => SharedMachineCatalogTests.Pokemon());

    [TestMethod]
    public void SparsePokedexOverridesPreserveUnchangedCategoriesAndGameplayMetadata()
    {
        using var temp = TestAssets.TemporaryOutput();
        Write(temp.Path, "species-names", "1", "Leaf Friend");
        Write(temp.Path, "species-categories", "1", "Sprout");
        Write(temp.Path, "species-descriptions", "1", "A plant grows\non its back.");
        Write(temp.Path, "pokedex-ui", "gText_SearchNoColor", "FIND");
        var original = PretPokedexMenu.Load(TestAssets.Root, English);
        var localized = PretPokedexMenu.Load(TestAssets.Root, SourceTextPack.Load(temp.Path));
        var expected = original.Entries[1] with { Name = "Leaf Friend", Category = "Sprout",
            DisplayCategory = "Sprout", Description = "A plant grows\non its back." };
        Assert.AreEqual(expected, localized.Entries[1]);
        Assert.AreEqual(expected, localized.NationalEntries[1]);
        Assert.AreEqual(expected, localized.Document.Entries.Single(entry => entry.SourceSpeciesId == 1));
        Assert.AreEqual(original.Entries[7], localized.Entries[7]);
        Assert.AreEqual(original.Document.Rules, localized.Document.Rules);
        CollectionAssert.AreEqual(original.Document.Orders.Alphabetical.ToArray(), localized.Document.Orders.Alphabetical.ToArray());
        Assert.AreEqual("FIND", localized.UiTexts["gText_SearchNoColor"]);
        Assert.AreEqual(original.UiTexts["gText_Cry"], localized.UiTexts["gText_Cry"]);
        Assert.AreEqual(expected.Name, localized.BuildNumericalList([1], [1], new(true)).Rows.Single().DisplayName);
    }

    [TestMethod]
    [DataRow("sourceId")]
    [DataRow("nationalNumber")]
    [DataRow("height")]
    [DataRow("weight")]
    [DataRow("type1")]
    [DataRow("type2")]
    public void RegionalDexRuleComparisonIgnoresDisplayTextButRejectsChangedFacts(string field)
    {
        var dex = PretPokedexMenu.Load(TestAssets.Root, English);
        var display = dex.Entries[1] with { Name = "Leaf Friend", Category = "Sprout", DisplayCategory = "Sprout",
            Description = "Edited description.", HeightDisplay = "Edited height", WeightDisplay = "Edited weight",
            HabitatTitle = "Edited habitat", Portrait = new("regional-portrait.png", 64, 64), Footprint = null };
        Assert.IsTrue(dex.HasSameEntryRules(display));
        var changed = field switch
        {
            "sourceId" => display with { SourceSpeciesId = 9999 },
            "nationalNumber" => display with { NationalNumber = 2 },
            "height" => display with { HeightDecimeters = display.HeightDecimeters + 1 },
            "weight" => display with { WeightHectograms = display.WeightHectograms + 1 },
            "type1" => display with { Type1 = "TYPE_FIRE" },
            "type2" => display with { Type2 = "TYPE_FIRE" },
            _ => throw new ArgumentException("Unknown dex rule field.")
        };
        Assert.IsFalse(dex.HasSameEntryRules(changed));
    }

    [TestMethod]
    public void SparseNamesUseExistingIdsAndLeaveRulesAndLiteralParametersAlone()
    {
        using var temp = TestAssets.TemporaryOutput();
        Write(temp.Path, "item-names", "pret:ITEM_POTION", "Healing Bottle");
        Write(temp.Path, "move-names", "pret:MOVE_TACKLE", "Body Bump");
        Write(temp.Path, "type-names", "TYPE_NORMAL", "Ordinary");
        Write(temp.Path, "ability-names", "ABILITY_OVERGROW", "Verdant Power");
        Write(temp.Path, "nature-names", "0", "literal {DYNAMIC 0x01} {LV_2}");
        Write(temp.Path, "display-labels", "EggName", "Hatchling");
        var pack = SourceTextPack.Load(temp.Path);
        var items = SharedItemCatalog.LoadProject(TestAssets.Root, pack);
        var originalItems = SharedItemCatalog.LoadProject(TestAssets.Root, English);
        var names = SourceDisplayNames.LoadProject(TestAssets.Root, items, Pokemon);
        Assert.AreEqual("Healing Bottle", names.ItemName("ITEM_POTION"));
        Assert.AreEqual(originalItems.Definition(new("pret:ITEM_POTION")) with { Name = "Healing Bottle" }, items.Definition(new("pret:ITEM_POTION")));
        Assert.AreEqual("Ordinary", names.TypeName("TYPE_NORMAL"));
        Assert.AreEqual("Verdant Power", names.AbilityName("ABILITY_OVERGROW"));
        Assert.AreEqual("Hatchling", names.EggName);
        Assert.AreEqual("NONE", names.HeldItemName("ITEM_NONE"));
        Assert.AreEqual(100, StatRules.ApplyNature(100, Nature.Hardy, Stat.Attack));
        var originalMoves = PretMetronomeMoveCatalog.Load(TestAssets.Root, English);
        var moves = PretMetronomeMoveCatalog.Load(TestAssets.Root, pack);
        Assert.AreEqual("Body Bump", moves.MoveNames[new("pret:MOVE_TACKLE")]);
        foreach (var (id, definition) in moves.Moves)
            Assert.IsTrue(PretMetronomeMoveCatalog.DefinitionsEqual(originalMoves.Moves[id], definition));
        var summary = SummaryPresentation.Load(TestAssets.Root, pack);
        Assert.AreEqual("literal {DYNAMIC 0x01} {LV_2} nature.\nMet in PALLET TOWN at Lv 5.",
            summary.TrainerMemo(new(names.NatureName(Nature.Hardy), "MAPSEC_PALLET_TOWN", 5, 4, false, true)));
    }

    [TestMethod]
    public void SummaryProseOverridesRetainSourceParametersAndPortraitBehavior()
    {
        using var temp = TestAssets.TemporaryOutput();
        Write(temp.Path, "summary-moves", "MOVE_TACKLE", "A direct body attack.");
        Write(temp.Path, "summary-abilities", "ABILITY_OVERGROW", "Boosts plant attacks.");
        Write(temp.Path, "summary-locations", "MAPSEC_PALLET_TOWN", "First Town");
        Write(temp.Path, "summary-memos", "Met", "{DYNAMIC 0x00} temperament.\nFound in {DYNAMIC 0x02} at {LV_2} {DYNAMIC 0x01}.");
        var original = SummaryPresentation.Load(TestAssets.Root, English);
        var localized = SummaryPresentation.Load(TestAssets.Root, SourceTextPack.Load(temp.Path));
        Assert.AreEqual("A direct body attack.", localized.Moves["MOVE_TACKLE"]);
        Assert.AreEqual("Boosts plant attacks.", localized.Abilities["ABILITY_OVERGROW"]);
        Assert.AreEqual("HARDY temperament.\nFound in First Town at Lv 5.", localized.TrainerMemo(new("HARDY", "MAPSEC_PALLET_TOWN", 5, 4, false, true)));
        Assert.AreEqual(original.Moves["MOVE_GROWL"], localized.Moves["MOVE_GROWL"]);
        Assert.AreEqual(original.FlipPortrait("pret:SPECIES_BULBASAUR"), localized.FlipPortrait("pret:SPECIES_BULBASAUR"));
        Assert.AreEqual(original.PortraitOffset(2, 100, 100, false, 0, false), localized.PortraitOffset(2, 100, 100, false, 0, false));
    }

    [TestMethod]
    public void ItemDescriptionsShareAnExplicitPackWithoutContaminatingRootCache()
    {
        var selected = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var selectedName = selected.Definition(MachineRules.Tm39Id).Machine.Item.Name;
        using var first = TestAssets.TemporaryOutput();
        using var second = TestAssets.TemporaryOutput();
        Write(first.Path, "item-names", "pret:ITEM_TM39", "First Disc");
        Write(first.Path, "item-descriptions", "pret:ITEM_POTION", "Restores some health.");
        Write(first.Path, "machine-descriptions", "pret:ITEM_TM39", "First machine instructions.");
        Write(second.Path, "item-names", "pret:ITEM_TM39", "Second Disc");
        Write(second.Path, "machine-descriptions", "pret:ITEM_TM39", "Second machine instructions.");
        var firstPack = SourceTextPack.Load(first.Path);
        // A loaded pack is a snapshot; later file edits do not mutate an existing composition.
        Write(first.Path, "item-names", "pret:ITEM_TM39", "Edited after load");
        var firstItems = SharedItemCatalog.LoadProject(TestAssets.Root, firstPack);
        var secondItems = SharedItemCatalog.LoadProject(TestAssets.Root, SourceTextPack.Load(second.Path));
        var firstMachines = SharedMachineCatalog.LoadProject(TestAssets.Root, firstItems);
        var secondMachines = SharedMachineCatalog.LoadProject(TestAssets.Root, secondItems);
        var machine = firstMachines.Definition(MachineRules.Tm39Id);
        Assert.AreEqual("First Disc", machine.Machine.Item.Name);
        Assert.AreEqual("First machine instructions.", machine.Machine.Description);
        Assert.AreEqual("Second Disc", secondMachines.Definition(MachineRules.Tm39Id).Machine.Item.Name);
        Assert.AreEqual("Second machine instructions.", secondMachines.Definition(MachineRules.Tm39Id).Machine.Description);
        Assert.AreSame(firstItems.Definition(MachineRules.Tm39Id), machine.Machine.Item);
        Assert.AreEqual(selectedName, selected.Definition(MachineRules.Tm39Id).Machine.Item.Name);
        Assert.AreSame(selected, SharedMachineCatalog.LoadProject(TestAssets.Root));
        var original = CurrentItemCatalog.Load(TestAssets.Root, SharedItemCatalog.LoadProject(TestAssets.Root, English));
        var localized = CurrentItemCatalog.Load(TestAssets.Root, firstItems);
        var potion = localized.Items.Single(item => item.Id.Value == "pret:ITEM_POTION");
        var originalPotion = original.Items.Single(item => item.Id == potion.Id);
        Assert.AreEqual("Restores some health.", potion.Description);
        Assert.AreEqual(JsonSerializer.Serialize(originalPotion), JsonSerializer.Serialize(potion with { Description = originalPotion.Description }));
        Assert.AreEqual(original.Items.Single(item => item.Id.Value == "pret:ITEM_ANTIDOTE").Description,
            localized.Items.Single(item => item.Id.Value == "pret:ITEM_ANTIDOTE").Description);
        Assert.AreEqual(selected.Definition(MachineRules.Tm39Id).SourceMoveId, machine.SourceMoveId);
        Assert.AreEqual(selected.Definition(MachineRules.Tm39Id).MovePp, machine.MovePp);
    }

    [TestMethod]
    [DataRow("species-names", "99999")]
    [DataRow("item-names", "pret:ITEM_MISSING")]
    [DataRow("move-names", "pret:MOVE_MISSING")]
    [DataRow("type-names", "TYPE_MISSING")]
    [DataRow("ability-names", "ABILITY_MISSING")]
    [DataRow("nature-names", "25")]
    [DataRow("display-labels", "MissingLabel")]
    [DataRow("summary-moves", "MOVE_MISSING")]
    [DataRow("summary-abilities", "ABILITY_MISSING")]
    [DataRow("summary-locations", "MAPSEC_MISSING")]
    [DataRow("item-descriptions", "pret:ITEM_TM39")]
    [DataRow("machine-descriptions", "pret:ITEM_POTION")]
    public void UnknownIdsAndWrongDescriptionCatalogsFailAtTheOwningLoader(string group, string id)
    {
        using var temp = TestAssets.TemporaryOutput();
        Write(temp.Path, group, id, "Replacement");
        var pack = SourceTextPack.Load(temp.Path);
        Assert.ThrowsExactly<InvalidDataException>(() => Load(group, pack));
    }

    [TestMethod]
    [DataRow("pokedex-ui", "gText_Cry", "PLAY")]
    [DataRow("summary-memos", "Met", "Found at {DYNAMIC 0x02}.")]
    [DataRow("summary-memos", "Met", "{UNKNOWN_CONTROL}")]
    public void PresentationTemplatesRejectMissingOrUnknownControls(string group, string id, string value)
    {
        using var temp = TestAssets.TemporaryOutput();
        Write(temp.Path, group, id, value);
        Assert.ThrowsExactly<InvalidDataException>(() => Load(group, SourceTextPack.Load(temp.Path)));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    public void IslandPickupsProjectCanonicalNamesWithoutChangingTheirAdmission(int island)
    {
        using var temp = TestAssets.TemporaryOutput();
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, IslandPath(island))))!;
        var names = source["pickups"]!.AsArray().Select(row => row!["item"]!["value"]!.GetValue<string>())
            .Distinct().ToDictionary(id => id, _ => "Shared Pickup Name");
        Write(temp.Path, "item-names", names);
        var shared = SharedMachineCatalog.LoadProject(TestAssets.Root,
            SharedItemCatalog.LoadProject(TestAssets.Root, SourceTextPack.Load(temp.Path)));
        var shop = CurrentItemCatalog.Load(TestAssets.Root, shared.Items);
        var machines = shared.LoadCurrent(GamePokemon);
        var pickups = IslandPickups(island, TestAssets.Root, shop, machines, shared);
        Assert.IsTrue(pickups.Length > 0);
        foreach (var pickup in pickups)
            Assert.AreEqual("Shared Pickup Name", pickup.ItemName);
        TestAssets.CopyFiles(temp.Path, [IslandPath(island)]);
        TestAssets.EditJson(temp.Path, IslandPath(island), json => json["pickups"]![0]!["sourceItemId"] = 9999);
        Assert.ThrowsExactly<InvalidDataException>(() => IslandPickups(island, temp.Path, shop, machines, shared));
    }

    private static ImmutableArray<FieldPickupDefinition> IslandPickups(int island, string root,
        EarlyShopCatalog shop, MachineCatalog machines, SharedMachineCatalog shared) => island switch
    {
        4 => PretFourIslandItems.LoadCatalogs(root, GamePokemon, shop, machines, shared).Pickups,
        5 => PretFiveIslandItems.LoadCatalogs(root, GamePokemon, shop, machines, shared).Pickups,
        6 => PretSixIslandItems.LoadCatalogs(root, GamePokemon, shop, machines, shared).Pickups,
        _ => throw new ArgumentOutOfRangeException(nameof(island))
    };
    private static string IslandPath(int island) => island switch
    {
        4 => "content/generated/data/items/four-island.json",
        5 => "content/generated/data/regions/five-island/items.json",
        6 => "content/generated/data/items/six-island.json",
        _ => throw new ArgumentOutOfRangeException(nameof(island))
    };
    private static void Load(string group, SourceTextPack pack)
    {
        if (group.StartsWith("species-", StringComparison.Ordinal) || group == "pokedex-ui")
            _ = PretPokedexMenu.Load(TestAssets.Root, pack);
        else if (group.StartsWith("summary-", StringComparison.Ordinal))
            _ = SummaryPresentation.Load(TestAssets.Root, pack);
        else if (group == "move-names") _ = PretMetronomeMoveCatalog.Load(TestAssets.Root, pack);
        else
        {
            var items = SharedItemCatalog.LoadProject(TestAssets.Root, pack);
            if (group == "item-descriptions") _ = CurrentItemCatalog.Load(TestAssets.Root, items);
            else if (group == "machine-descriptions") _ = SharedMachineCatalog.LoadProject(TestAssets.Root, items);
            else _ = SourceDisplayNames.LoadProject(TestAssets.Root, items, Pokemon);
        }
    }
    private static void Write(string root, string group, string id, string value) =>
        Write(root, group, new Dictionary<string, string> { [id] = value });
    private static void Write(string root, string group, Dictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.Combine(root, "source"));
        File.WriteAllText(Path.Combine(root, "source", group + ".json"), JsonSerializer.Serialize(values));
    }
}

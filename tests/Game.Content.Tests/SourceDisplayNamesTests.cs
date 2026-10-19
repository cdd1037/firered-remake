using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SourceDisplayNamesTests
{
    private static SharedItemCatalog Items => TestAssets.Cached(() => SharedItemCatalog.LoadProject(TestAssets.Root));
    private static PokemonCatalog Pokemon => TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root).Catalog);
    private static SourceDisplayNames Names => TestAssets.Cached(() => SourceDisplayNames.LoadProject(TestAssets.Root, Items, Pokemon));

    [TestMethod]
    [DataRow("type", "TYPE_NORMAL", "NORMAL")]
    [DataRow("type", "TYPE_FIGHTING", "FIGHT")]
    [DataRow("type", "TYPE_ELECTRIC", "ELECTR")]
    [DataRow("type", "TYPE_PSYCHIC", "PSYCHC")]
    [DataRow("type", "TYPE_MYSTERY", "???")]
    [DataRow("ability", "ABILITY_NONE", "-------")]
    [DataRow("ability", "ABILITY_COMPOUND_EYES", "COMPOUNDEYES")]
    [DataRow("ability", "ABILITY_LIGHTNING_ROD", "LIGHTNINGROD")]
    public void NamesRetainSourceSpellings(string kind, string id, string expected) =>
        Assert.AreEqual(expected, kind == "type" ? Names.TypeName(id) : Names.AbilityName(id));

    [TestMethod]
    [DataRow("ITEM_POKE_BALL")]
    [DataRow("ITEM_FOCUS_BAND")]
    [DataRow("ITEM_CHOICE_BAND")]
    [DataRow("ITEM_ORAN_BERRY")]
    [DataRow("ITEM_SS_TICKET")]
    public void ItemNamesUseCanonicalDefinitions(string symbol)
    {
        Assert.AreEqual(Items.Definition(new("pret:" + symbol)).Name, Names.ItemName(symbol));
        Assert.AreEqual(Names.ItemName(symbol), Names.HeldItemName(symbol));
    }

    [TestMethod]
    public void NoHeldItemIsASummaryLabelAndNeverARealItem()
    {
        Assert.AreEqual("NONE", Names.HeldItemName("ITEM_NONE"));
        Assert.ThrowsExactly<InvalidDataException>(() => Names.ItemName("ITEM_NONE"));
        Assert.ThrowsExactly<InvalidDataException>(() => Names.ItemName("ITEM_MISSING"));
        Assert.ThrowsExactly<InvalidDataException>(() => Names.AbilityName("ABILITY_MISSING"));
        Assert.ThrowsExactly<InvalidDataException>(() => Names.TypeName("TYPE_MISSING"));
    }

    [TestMethod]
    public void DisplayEditsRemainLiteralAndDoNotChangeGameplayIdentity()
    {
        using var temp = TestAssets.TemporaryOutput();
        const string itemPath = "content/generated/data/items.json";
        TestAssets.CopyFiles(temp.Path, [SourceDisplayNames.RelativePath, itemPath]);
        TestAssets.EditJson(temp.Path, SourceDisplayNames.RelativePath, json =>
        {
            json["types"]!["TYPE_GRASS"] = "leaf {literal}";
            json["abilities"]!["ABILITY_OVERGROW"] = "growth {literal}";
            json["noHeldItem"] = "empty {literal}";
        });
        TestAssets.EditJson(temp.Path, itemPath, json =>
            json["items"]!.AsArray().Single(item => item!["sourceItemSymbol"]!.GetValue<string>() == "ITEM_POKE_BALL")!["name"] = "ball {literal}");
        var items = SharedItemCatalog.LoadProject(temp.Path);
        var names = SourceDisplayNames.LoadProject(temp.Path, items, Pokemon);
        Assert.AreEqual("leaf {literal}", names.TypeName("TYPE_GRASS"));
        Assert.AreEqual("growth {literal}", names.AbilityName("ABILITY_OVERGROW"));
        Assert.AreEqual("empty {literal}", names.HeldItemName("ITEM_NONE"));
        Assert.AreEqual("ball {literal}", names.ItemName("ITEM_POKE_BALL"));
        var original = Items.Definition(new("pret:ITEM_POKE_BALL"));
        Assert.AreEqual(original with { Name = "ball {literal}" }, items.Definition(original.Id));
        Assert.IsTrue(Pokemon.Species.Values.Any(s => s.Ability1 == "ABILITY_OVERGROW" && s.Type1 == "TYPE_GRASS"));
    }

    [TestMethod]
    [DataRow("empty-types")]
    [DataRow("empty-abilities")]
    [DataRow("blank-type")]
    [DataRow("blank-ability")]
    [DataRow("blank-none")]
    [DataRow("missing-type")]
    [DataRow("missing-ability")]
    [DataRow("bad-symbol")]
    public void InvalidNamesAndMissingReferencedIdsFailAtLoad(string mutation)
    {
        using var temp = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(temp.Path, [SourceDisplayNames.RelativePath]);
        TestAssets.EditJson(temp.Path, SourceDisplayNames.RelativePath, json =>
        {
            switch (mutation)
            {
                case "empty-types": json["types"]!.AsObject().Clear(); break;
                case "empty-abilities": json["abilities"]!.AsObject().Clear(); break;
                case "blank-type": json["types"]!["TYPE_GRASS"] = " "; break;
                case "blank-ability": json["abilities"]!["ABILITY_OVERGROW"] = " "; break;
                case "blank-none": json["noHeldItem"] = " "; break;
                case "missing-type": json["types"]!.AsObject().Remove("TYPE_GRASS"); break;
                case "missing-ability": json["abilities"]!.AsObject().Remove("ABILITY_OVERGROW"); break;
                case "bad-symbol": json["types"]!["grass"] = "GRASS"; break;
                default: Assert.Fail("Unrecognized mutation."); break;
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => SourceDisplayNames.LoadProject(temp.Path, Items, Pokemon));
    }
}

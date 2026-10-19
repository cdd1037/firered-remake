using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

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
    [DataRow("nature", "0", "HARDY")]
    [DataRow("nature", "3", "ADAMANT")]
    [DataRow("nature", "24", "QUIRKY")]
    public void NamesRetainSourceSpellings(string kind, string id, string expected) =>
        Assert.AreEqual(expected, kind switch
        {
            "type" => Names.TypeName(id),
            "ability" => Names.AbilityName(id),
            "nature" => Names.NatureName((Nature)int.Parse(id)),
            _ => throw new ArgumentException("Unknown display-name kind.")
        });

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
        Assert.ThrowsExactly<InvalidDataException>(() => Names.NatureName((Nature)(-1)));
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
            json["natures"]!["0"] = "nature {DYNAMIC 0x01} {LV_2}";
            json["natures"]!["3"] = "nature {DYNAMIC 0x01} {LV_2}";
        });
        TestAssets.EditJson(temp.Path, itemPath, json =>
            json["items"]!.AsArray().Single(item => item!["sourceItemSymbol"]!.GetValue<string>() == "ITEM_POKE_BALL")!["name"] = "ball {literal}");
        var items = SharedItemCatalog.LoadProject(temp.Path);
        var names = SourceDisplayNames.LoadProject(temp.Path, items, Pokemon);
        Assert.AreEqual("leaf {literal}", names.TypeName("TYPE_GRASS"));
        Assert.AreEqual("growth {literal}", names.AbilityName("ABILITY_OVERGROW"));
        Assert.AreEqual("empty {literal}", names.HeldItemName("ITEM_NONE"));
        Assert.AreEqual("nature {DYNAMIC 0x01} {LV_2}", names.NatureName(Nature.Hardy));
        Assert.AreEqual(names.NatureName(Nature.Hardy), names.NatureName(Nature.Adamant));
        Assert.AreEqual(100, StatRules.ApplyNature(100, Nature.Hardy, Stat.Attack));
        Assert.AreEqual(110, StatRules.ApplyNature(100, Nature.Adamant, Stat.Attack));
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
    [DataRow("empty-natures")]
    [DataRow("blank-nature")]
    [DataRow("missing-nature")]
    [DataRow("named-nature-id")]
    [DataRow("negative-nature-id")]
    [DataRow("out-of-range-nature-id")]
    [DataRow("noncanonical-nature-id")]
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
                case "empty-natures": json["natures"]!.AsObject().Clear(); break;
                case "blank-nature": json["natures"]!["0"] = " "; break;
                case "missing-nature": json["natures"]!.AsObject().Remove("0"); break;
                case "named-nature-id":
                case "negative-nature-id":
                case "out-of-range-nature-id":
                case "noncanonical-nature-id":
                    json["natures"]!.AsObject().Remove("0");
                    json["natures"]![mutation switch
                    {
                        "named-nature-id" => "Hardy", "negative-nature-id" => "-1",
                        "out-of-range-nature-id" => "25", _ => "00"
                    }] = "valid display name";
                    break;
                default: Assert.Fail("Unrecognized mutation."); break;
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => SourceDisplayNames.LoadProject(temp.Path, Items, Pokemon));
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingNatureTableAndDuplicateNumericIdentityFailSchema(bool duplicate)
    {
        using var temp = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(temp.Path, [SourceDisplayNames.RelativePath]);
        string path = Path.Combine(temp.Path, SourceDisplayNames.RelativePath);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        if (!duplicate) json.AsObject().Remove("natures");
        string text = json.ToJsonString();
        if (duplicate) text = text.Replace("\"natures\":{", "\"natures\":{\"0\":\"duplicate\",", StringComparison.Ordinal);
        File.WriteAllText(path, text);
        Assert.ThrowsExactly<ContentValidationException>(() => SourceDisplayNames.LoadProject(temp.Path, Items, Pokemon));
    }
}

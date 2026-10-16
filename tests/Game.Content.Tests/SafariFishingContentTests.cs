using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SafariFishingContentTests
{
    private static string Json => File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/maps/regions/safari/fishing.json"));

    [TestMethod]
    public void FourSafariAreasHaveEveryOriginalRodSlotAndDragonairHeldItem()
    {
        var catalog = PretSafariFishing.Parse(Json);
        Assert.AreEqual(12, catalog.Tables.Count);
        foreach (string name in new[] { "center", "east", "north", "west" })
        {
            var map = new MapId("map.safari-zone-" + name);
            var old = catalog.Table(FishingRod.Old, map);
            Assert.IsTrue(old.Slots.All(s => s.Species.Value == "pret:SPECIES_MAGIKARP" && s.MinimumLevel == 5 && s.MaximumLevel == 5));
            CollectionAssert.AreEqual(new[] { 70, 30 }, old.Slots.Select(s => s.Weight).ToArray());
            var good = catalog.Table(FishingRod.Good, map);
            CollectionAssert.AreEqual(new[] { "pret:SPECIES_GOLDEEN", "pret:SPECIES_MAGIKARP", "pret:SPECIES_POLIWAG" }, good.Slots.Select(s => s.Species.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 60, 20, 20 }, good.Slots.Select(s => s.Weight).ToArray());
            Assert.IsTrue(good.Slots.All(s => s.MinimumLevel == 5 && s.MaximumLevel == 15));
            var super = catalog.Table(FishingRod.Super, map);
            CollectionAssert.AreEqual(new[] { "pret:SPECIES_GOLDEEN", "pret:SPECIES_SEAKING", "pret:SPECIES_DRATINI", "pret:SPECIES_PSYDUCK", "pret:SPECIES_DRAGONAIR" }, super.Slots.Select(s => s.Species.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 40, 40, 15, 4, 1 }, super.Slots.Select(s => s.Weight).ToArray());
            CollectionAssert.AreEqual(new byte[] { 15, 20, 15, 15, 25 }, super.Slots.Select(s => s.MinimumLevel).ToArray());
            CollectionAssert.AreEqual(new byte[] { 25, 30, 25, 35, 35 }, super.Slots.Select(s => s.MaximumLevel).ToArray());
        }
        Assert.AreEqual("ITEM_DRAGON_FANG", catalog.HeldItems[new("pret:SPECIES_DRAGONAIR")].Rare);
    }

    [TestMethod]
    public void LoaderAcceptsLegalDefinitionEditsAndRejectsInvalidData()
    {
        var document = JsonNode.Parse(Json)!.AsObject();
        document["tables"]![0]!["table"]!["slots"]![0]!["maximumLevel"] = 6;
        Assert.AreEqual((byte)6, PretSafariFishing.Parse(document.ToJsonString()).Table(FishingRod.Old, new("map.safari-zone-center")).Slots[0].MaximumLevel);
        document["tables"]![0]!["table"]!["slots"]![0]!["weight"] = 71;
        Assert.Throws<InvalidDataException>(() => PretSafariFishing.Parse(document.ToJsonString()));
    }
}

using System.Text.Json;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class MtEmberSummitEncounterContentTests
{
    [TestMethod]
    public void SummitPathLandRowsMatchEveryPinnedFireRedSlotAndWeight()
    {
        using var wild = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Source, "src/data/wild_encounters.json")));
        var group = wild.RootElement.GetProperty("wild_encounter_groups").EnumerateArray()
            .Single(g => g.GetProperty("label").GetString() == "gWildMonHeaders");
        var weights = group.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("type").GetString() == "land_mons")
            .GetProperty("encounter_rates").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        Assert.AreEqual(3, MtEmberSummitEncounterTables.Land.Length);
        for (int floor = 1; floor <= 3; floor++)
        {
            var table = MtEmberSummitEncounterTables.Land.Single(t => t.Map.Value == $"map.mt-ember-summit-path-{floor}f");
            var original = group.GetProperty("encounters").EnumerateArray().Single(e =>
                e.GetProperty("map").GetString() == $"MAP_MT_EMBER_SUMMIT_PATH_{floor}F" &&
                e.GetProperty("base_label").GetString() == $"sMtEmberSummitPath{floor}F_FireRed").GetProperty("land_mons");
            Assert.AreEqual(original.GetProperty("encounter_rate").GetInt32(), table.RawEncounterRate);
            var slots = original.GetProperty("mons").EnumerateArray().ToArray();
            Assert.AreEqual(slots.Length, table.Slots.Length);
            Assert.AreEqual(12, slots.Length);
            for (int index = 0; index < slots.Length; index++)
            {
                var actual = table.Slots[index];
                Assert.AreEqual(index, actual.Index);
                Assert.AreEqual("pret:" + slots[index].GetProperty("species").GetString(), actual.Species.Value);
                Assert.AreEqual(slots[index].GetProperty("min_level").GetInt32(), (int)actual.MinimumLevel);
                Assert.AreEqual(slots[index].GetProperty("max_level").GetInt32(), (int)actual.MaximumLevel);
                Assert.AreEqual(weights[index], actual.Weight);
            }
            Assert.AreSame(table, SourceTableEncounterRegistry.Land.Single(t => t.Map == table.Map));
        }
    }

    [TestMethod]
    public void SummitSecondFloorUsesExistingGlobalRockSmashRows()
    {
        var map = new MapId("map.mt-ember-summit-path-2f");
        var rocks = PretFieldRockSmash.Load(TestAssets.Root);
        Assert.AreEqual(9, rocks.Rocks.Count(r => r.Map == map));
        var table = rocks.EncountersFor(map)!;
        Assert.AreEqual(50, table.RawEncounterRate);
        CollectionAssert.AreEqual(new[] { "SPECIES_GEODUDE", "SPECIES_GEODUDE", "SPECIES_GEODUDE", "SPECIES_GRAVELER", "SPECIES_GRAVELER" },
            table.Slots.Select(s => s.Species.Value[5..]).ToArray());
        CollectionAssert.AreEqual(new byte[] { 5, 10, 15, 25, 30 }, table.Slots.Select(s => s.MinimumLevel).ToArray());
        CollectionAssert.AreEqual(new byte[] { 20, 20, 30, 40, 40 }, table.Slots.Select(s => s.MaximumLevel).ToArray());
        CollectionAssert.AreEqual(new[] { 60, 30, 5, 4, 1 }, table.Slots.Select(s => s.Weight).ToArray());
    }
}

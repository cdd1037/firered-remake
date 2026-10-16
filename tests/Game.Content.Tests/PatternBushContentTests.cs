using System.Text.Json;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PatternBushContentTests
{
    private static readonly Lazy<PretCampaignCatalog> Current = new(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static readonly MapId Bush = new("map.six-island-pattern-bush");
    private static readonly MapId Green = new("map.six-island-green-path");
    private static readonly MapId Water = new("map.six-island-water-path");

    [TestMethod]
    public void FireRedBushTablePreservesEverySourceSpeciesLevelAndWeightedSlot()
    {
        var table = Current.Value.LandEncounters![Bush];
        Assert.AreEqual(21, table.RawEncounterRate);
        CollectionAssert.AreEqual(new[] { "SPINARAK", "KAKUNA", "SPINARAK", "CATERPIE", "WEEDLE", "HERACROSS",
            "METAPOD", "HERACROSS", "LEDYBA", "HERACROSS", "LEDYBA", "HERACROSS" },
            table.Slots.Select(slot => slot.Species.Value[13..]).ToArray());
        CollectionAssert.AreEqual(new[] { 9, 9, 14, 6, 6, 15, 9, 20, 9, 25, 14, 30 },
            table.Slots.Select(slot => (int)slot.MinimumLevel).ToArray());
        Assert.IsTrue(table.Slots.All(slot => slot.MinimumLevel == slot.MaximumLevel));
        CollectionAssert.AreEqual(new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 },
            table.Slots.Select(slot => slot.Weight).ToArray());
        var distribution = Enumerable.Range(0, 100).Select(roll => table.Slots[EarlyLandEncounterRules.ChooseSlot(roll)])
            .GroupBy(slot => slot.Species.Value).ToDictionary(group => group.Key, group => group.Count());
        Assert.AreEqual(30, distribution["pret:SPECIES_SPINARAK"]);
        Assert.AreEqual(20, distribution["pret:SPECIES_KAKUNA"]);
        Assert.AreEqual(10, distribution["pret:SPECIES_CATERPIE"]);
        Assert.AreEqual(10, distribution["pret:SPECIES_WEEDLE"]);
        Assert.AreEqual(20, distribution["pret:SPECIES_HERACROSS"]);
        Assert.AreEqual(5, distribution["pret:SPECIES_METAPOD"]);
        Assert.AreEqual(5, distribution["pret:SPECIES_LEDYBA"]);
    }

    [TestMethod]
    public void OriginalConnectionAndBothWarpGroupsHaveLoadedPhysicalDestinations()
    {
        var content = Current.Value;
        var green = content.Maps[Green]; var bush = content.Maps[Bush];
        Assert.AreEqual((72, 20), (green.Width, green.Height));
        Assert.AreEqual((60, 32), (bush.Width, bush.Height));
        Assert.IsTrue(content.ConnectedFieldMaps![Water].Connections.Any(c => c.Direction == Direction.West && c.DestinationMapId == Green && c.Offset == 0));
        Assert.IsTrue(content.ConnectedFieldMaps[Green].Connections.Any(c => c.Direction == Direction.East && c.DestinationMapId == Water && c.Offset == 0));
        CollectionAssert.AreEqual(new[] { 63, 64, 45, 46 }, green.FieldWarps.Select(w => w.X).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 1, 4, 4 }, green.FieldWarps.Select(w => w.DestinationWarpId).ToArray());
        CollectionAssert.AreEqual(new[] { 54, 55, 56, 3, 4, 5 }, bush.FieldWarps.Select(w => w.X).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 0, 1, 2, 2, 3 }, bush.FieldWarps.Select(w => w.DestinationWarpId).ToArray());
        foreach (var map in new[] { green, bush })
            foreach (var warp in map.FieldWarps)
            {
                var target = content.Maps[warp.DestinationMapId].FieldWarps[warp.DestinationWarpId];
                Assert.AreEqual(map.Id, target.DestinationMapId);
                Assert.IsFalse(content.Maps[warp.DestinationMapId].GetTile(target.X, target.Y)!.Blocked);
            }
    }
    [TestMethod]
    public void NewTrainerClassAndPortraitTuplesHaveExactReusableArtwork()
    {
        var available = new Dictionary<(string Class, string Pic, int SourcePic), (string Name, JsonElement Art)>();
        foreach (var region in new[] { "seven-island", "six-island" })
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
                $"content/generated/sprites/pokemon/{region}/manifest.json")));
            var manifest = document.RootElement;
            foreach (var row in manifest.GetProperty("trainers").EnumerateArray())
            {
                var art = manifest.GetProperty("portraits").EnumerateArray().Single(a =>
                    a.GetProperty("id").GetString() == row.GetProperty("portrait").GetString());
                available.TryAdd((row.GetProperty("trainerClass").GetString()!, row.GetProperty("trainerPic").GetString()!,
                    art.GetProperty("sourceTrainerPicId").GetInt32()), (row.GetProperty("className").GetString()!, art.Clone()));
            }
        }
        var tower = PretTrainerTowerPresentation.Load(TestAssets.Root);
        foreach (var row in tower.GetProperty("trainerBindings").EnumerateArray())
        {
            var art = tower.GetProperty("trainerPortraits").EnumerateArray().Single(a =>
                a.GetProperty("id").GetString() == row.GetProperty("portrait").GetString());
            available.TryAdd((row.GetProperty("trainerClass").GetString()!, row.GetProperty("trainerPic").GetString()!,
                row.GetProperty("sourceTrainerPicId").GetInt32()), (row.GetProperty("className").GetString()!, art.Clone()));
        }
        var trainers = Current.Value.SixIslandTrainers!;
        var ids = trainers.BattlesByScript.Where(pair => pair.Key.StartsWith("SixIsland_PatternBush_", StringComparison.Ordinal) ||
            pair.Key.StartsWith("SixIsland_GreenPath_", StringComparison.Ordinal)).Select(pair => pair.Value.Trainer).Distinct().ToArray();
        Assert.AreEqual(13, ids.Length);
        foreach (var id in ids)
        {
            var trainer = trainers.Trainers[id];
            Assert.IsTrue(available.TryGetValue((trainer.TrainerClass, trainer.TrainerPic, trainer.SourceTrainerPic), out var presentation), id.Value);
            Assert.IsFalse(string.IsNullOrWhiteSpace(presentation.Name));
            Assert.AreEqual(trainer.SourceTrainerPic, presentation.Art.GetProperty("sourceTrainerPicId").GetInt32());
            Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, presentation.Art.GetProperty("texturePath").GetString()!)));
            Assert.IsTrue(presentation.Art.GetProperty("width").GetInt32() > 0);
            Assert.IsTrue(presentation.Art.GetProperty("height").GetInt32() > 0);
        }
    }

    [TestMethod]
    public void GreenPathHasSourceSurfAndEveryRodTable()
    {
        var content = Current.Value;
        var surf = content.WaterEncounters![Green];
        Assert.AreEqual(2, surf.RawEncounterRate);
        CollectionAssert.AreEqual(new[] { "TENTACOOL", "TENTACOOL", "TENTACOOL", "TENTACRUEL", "TENTACRUEL" },
            surf.Slots.Select(slot => slot.Species.Value[13..]).ToArray());
        CollectionAssert.AreEqual(new[] { 60, 30, 5, 4, 1 }, surf.Slots.Select(slot => slot.Weight).ToArray());
        Assert.AreEqual(2, content.OldRodEncounters![Green].Slots.Length);
        Assert.AreEqual(3, content.GoodRodEncounters![Green].Slots.Length);
        Assert.AreEqual(5, content.SuperRodEncounters![Green].Slots.Length);
        Assert.IsTrue(content.OldRodEncounters[Green].Slots.All(slot => slot.Species == new SpeciesId("pret:SPECIES_MAGIKARP")));
        Assert.IsTrue(content.SuperRodEncounters[Green].Slots.Any(slot => slot.Species == new SpeciesId("pret:SPECIES_QWILFISH")));
    }

}

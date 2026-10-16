using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class TreasureBeachTests
{
    private static readonly MapId Beach = new("map.one-island-treasure-beach");
    private static readonly MapId Island = new("map.one-island");
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));

    [TestMethod]
    public void SourceGeometryActorsAndReciprocalConnectionAreMounted()
    {
        var content = Content;
        var map = content.Maps[Beach];
        Assert.AreEqual((24, 40), (map.Width, map.Height));
        Assert.IsTrue(map.FieldWarps.IsEmpty);
        Assert.AreEqual(new ConnectedFieldConnection(Direction.North, 0, Island),
            content.ConnectedFieldMaps![Beach].Connections.Single());
        Assert.IsTrue(content.ConnectedFieldMaps[Island].Connections.Contains(new(Direction.South, 0, Beach)));
        var definition = content.MapDefinition(Beach);
        Assert.AreEqual("MAPSEC_TREASURE_BEACH", definition.RegionMapSection);
        Assert.AreEqual("MUS_ROUTE3", definition.Music);
        Assert.AreEqual(2, definition.Actors.Length);
        var amara = definition.Actors.Single(a => a.ScriptId == "OneIsland_TreasureBeach_EventScript_Amara");
        Assert.AreEqual((8, 11, 1, 1), (amara.X, amara.Y, amara.Elevation, amara.TrainerSight));
        Assert.AreEqual("OBJ_EVENT_GFX_SWIMMER_F_WATER", amara.GraphicsId);
        Assert.AreEqual("TRAINER_TYPE_NORMAL", amara.TrainerType);
        var boy = definition.Actors.Single(a => a.ScriptId == "OneIsland_TreasureBeach_EventScript_Boy");
        Assert.AreEqual((13, 20, 3), (boy.X, boy.Y, boy.Elevation));
        Assert.IsNotNull(content.FindProgram(boy.ScriptId));
        Assert.IsNotNull(content.FindProgram(amara.ScriptId));
        var trainer = content.FirstSeviiTrainerParties!.Trainers[new("pret:TRAINER_SWIMMER_FEMALE_AMARA")];
        CollectionAssert.AreEqual(new[] { "pret:SPECIES_SEEL", "pret:SPECIES_SEEL", "pret:SPECIES_DEWGONG" },
            trainer.Party.Select(p => p.Species.Value).ToArray());
        Assert.IsTrue(trainer.Party.All(p => p.Level == 36 && p.SourceIv == 0 && p.HeldItem == "ITEM_NONE"));
    }

    [TestMethod]
    public void EightSourceHiddenPickupsUseTheSharedRenewableFlags()
    {
        var content = Content;
        var pickups = content.FieldPickups.Where(p => p.MapSymbol == "MAP_ONE_ISLAND_TREASURE_BEACH").ToArray();
        var expected = new[] { (8, 20, "ITEM_STARDUST"), (13, 27, "ITEM_STARDUST"),
            (11, 31, "ITEM_PEARL"), (9, 34, "ITEM_PEARL"), (15, 22, "ITEM_ULTRA_BALL"),
            (16, 33, "ITEM_ULTRA_BALL"), (15, 29, "ITEM_STAR_PIECE"), (8, 27, "ITEM_BIG_PEARL") };
        CollectionAssert.AreEquivalent(expected, pickups.Select(p => (p.X, p.Y, p.ItemSymbol)).ToArray());
        Assert.IsTrue(pickups.All(p => p.Hidden && p.Elevation == 3 && p.Quantity == 1 &&
            RenewableHiddenItemRules.AllFlags.Contains(p.Flag)));
        var triggers = content.MapDefinition(Beach).BackgroundTriggers;
        Assert.AreEqual(8, triggers.Length);
        foreach (var pickup in pickups)
        {
            var trigger = triggers.Single(t => t.X == pickup.X && t.Y == pickup.Y);
            Assert.AreEqual(pickup.Flag, trigger.HiddenFlag);
            Assert.AreEqual(pickup.ItemSymbol, trigger.HiddenItem);
            Assert.IsFalse(trigger.Underfoot);
        }
    }

    [TestMethod]
    public void LandAndSurfTablesKeepFireRedSpeciesLevelsWeightsAndRates()
    {
        var land = Content.LandEncounters![Beach];
        Assert.AreEqual(21, land.RawEncounterRate);
        CollectionAssert.AreEqual(new[] { "SPEAROW", "TANGELA", "SPEAROW", "TANGELA", "FEAROW", "MEOWTH",
            "FEAROW", "PSYDUCK", "FEAROW", "PERSIAN", "FEAROW", "PERSIAN" },
            land.Slots.Select(s => s.Species.Value[13..]).ToArray());
        CollectionAssert.AreEqual(new[] { 32, 33, 31, 35, 36, 31, 38, 31, 40, 37, 40, 40 },
            land.Slots.Select(s => (int)s.MinimumLevel).ToArray());
        Assert.IsTrue(land.Slots.All(s => s.MinimumLevel == s.MaximumLevel));
        CollectionAssert.AreEqual(new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 },
            land.Slots.Select(s => s.Weight).ToArray());
        var water = Content.WaterEncounters![Beach];
        Assert.AreEqual(2, water.RawEncounterRate);
        CollectionAssert.AreEqual(new[] { "TENTACOOL", "TENTACOOL", "TENTACOOL", "TENTACRUEL", "TENTACRUEL" },
            water.Slots.Select(s => s.Species.Value[13..]).ToArray());
        CollectionAssert.AreEqual(new[] { (5, 20, 60), (20, 35, 30), (35, 40, 5), (35, 40, 4), (35, 40, 1) },
            water.Slots.Select(s => ((int)s.MinimumLevel, (int)s.MaximumLevel, s.Weight)).ToArray());
    }

    [TestMethod]
    [DataRow(FishingRod.Old)]
    [DataRow(FishingRod.Good)]
    [DataRow(FishingRod.Super)]
    public void EveryRodUsesTheMountedGeneratedSourceTable(FishingRod rod)
    {
        var profile = Content.FishingTables;
        Assert.IsTrue(profile.HasTable(rod, Beach));
        var table = profile.Table(rod, Beach);
        Assert.AreEqual(20, table.RawEncounterRate);
        var expected = rod switch {
            FishingRod.Old => new[] { ("MAGIKARP", 5, 5, 70), ("MAGIKARP", 5, 5, 30) },
            FishingRod.Good => new[] { ("HORSEA", 5, 15, 60), ("MAGIKARP", 5, 15, 20), ("HORSEA", 5, 15, 20) },
            _ => new[] { ("HORSEA", 15, 25, 40), ("HORSEA", 15, 25, 40), ("GYARADOS", 15, 25, 15),
                ("SEADRA", 25, 35, 4), ("PSYDUCK", 25, 35, 1) }
        };
        CollectionAssert.AreEqual(expected, table.Slots.Select(s =>
            (s.Species.Value[13..], (int)s.MinimumLevel, (int)s.MaximumLevel, s.Weight)).ToArray());
    }
    [TestMethod]
    public void GeneratedWaterHeaderRejectsMissingDuplicateAndMalformedTables()
    {
        string json = File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/regions/first-sevii/encounters.json"));
        var headers = JsonNode.Parse(json)!["admitted"]!.AsArray();
        var beach = headers.Single(h => h!["map"]!.GetValue<string>() == "MAP_ONE_ISLAND_TREASURE_BEACH")!;
        JsonArray OnlyBeach() => new(beach.DeepClone());
        void Reject(JsonArray rows)
        {
            using var document = JsonDocument.Parse(rows.ToJsonString());
            Assert.ThrowsExactly<ArgumentException>(() =>
                PretWildEncounterTables.Water(document.RootElement, PretFirstSeviiRegion.MapIdFromSourceSymbol));
        }
        Reject([]);
        Reject(new(beach.DeepClone(), beach.DeepClone()));
        var changed = OnlyBeach();
        changed[0]!["water_mons"]!["mons"]!.AsArray().RemoveAt(4);
        Reject(changed);
        changed = OnlyBeach();
        changed[0]!["water_mons"]!["encounter_rate"] = 0;
        Reject(changed);
        changed = OnlyBeach();
        changed[0]!["water_mons"]!["mons"]![0]!["max_level"] = 101;
        Reject(changed);
    }

}

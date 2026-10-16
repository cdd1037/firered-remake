using System.Text.Json;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class OutcastIslandContentTests
{
    private static readonly MapId Green = new("map.six-island-green-path");
    private static readonly MapId Outcast = new("map.six-island-outcast-island");
    private static readonly MapId Cave = new("map.six-island-altering-cave");
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));

    [TestMethod]
    public void OriginalNorthConnectionCaveWarpAndFieldRestrictionsAreMounted()
    {
        var content = Content;
        var outcast = content.Maps[Outcast]; var cave = content.Maps[Cave];
        Assert.AreEqual((24, 80), (outcast.Width, outcast.Height));
        Assert.AreEqual((32, 24), (cave.Width, cave.Height));
        Assert.IsTrue(content.ConnectedFieldMaps![Green].Connections.Contains(new(Direction.North, 0, Outcast)));
        Assert.AreEqual(new ConnectedFieldConnection(Direction.South, 0, Green),
            content.ConnectedFieldMaps[Outcast].Connections.Single());
        Assert.IsTrue(content.ConnectedFieldMaps[Cave].Connections.IsEmpty);
        var entrance = outcast.FieldWarps.Single(); var exit = cave.FieldWarps.Single();
        Assert.AreEqual((7, 21, Cave, 0), (entrance.X, entrance.Y, entrance.DestinationMapId, entrance.DestinationWarpId));
        Assert.AreEqual((18, 22, Outcast, 0), (exit.X, exit.Y, exit.DestinationMapId, exit.DestinationWarpId));
        Assert.IsFalse(outcast.GetTile(entrance.X, entrance.Y)!.Blocked);
        Assert.IsFalse(cave.GetTile(exit.X, exit.Y)!.Blocked);
        var outdoor = content.MapDefinition(Outcast); var underground = content.MapDefinition(Cave);
        Assert.AreEqual(("MAPSEC_OUTCAST_ISLAND", "MUS_SEVII_ROUTE", "MAP_TYPE_ROUTE"),
            (outdoor.RegionMapSection, outdoor.Music, outdoor.MapType));
        Assert.AreEqual(("MAPSEC_ALTERING_CAVE", "MUS_SEVII_CAVE", "MAP_TYPE_UNDERGROUND"),
            (underground.RegionMapSection, underground.Music, underground.MapType));
        Assert.IsFalse(underground.RequiresFlash);
        Assert.IsTrue(underground.Actors.IsEmpty);
        Assert.IsTrue(FieldFlySourceRules.MapTypeAllowsFly(outdoor.MapType));
        Assert.IsFalse(FieldFlySourceRules.MapTypeAllowsFly(underground.MapType));
        Assert.IsFalse(content.EscapeRoutes.Exits.ContainsKey(Outcast));
        CollectionAssert.AreEqual(new[] { new EscapeRopeDestination(Outcast, 7, 22) }, content.EscapeRoutes.Exits[Cave].ToArray());
        Assert.IsNotNull(content.FindProgram("SixIsland_AlteringCave_OnTransition"));
    }

    [TestMethod]
    [DataRow("map.six-island-outcast-island")]
    [DataRow("map.six-island-altering-cave")]
    public void EverySourceCellResolvesItsExactSharedMetatileDefinition(string mapName)
    {
        var content = Content; var map = content.SourceMaps[new(mapName)];
        Assert.AreEqual(map.Width * map.Height, map.Cells.Length);
        Assert.IsFalse(map.BorderCells.IsEmpty);
        foreach (var cell in map.Cells.Concat(map.BorderCells))
        {
            var definition = content.MetatileDefinition(map.Id, cell.MetatileId);
            Assert.IsNotNull(definition, $"{mapName} cell {cell.X},{cell.Y} metatile {cell.MetatileId}");
            Assert.AreEqual(new FieldMetatileDefinition(cell.MetatileId, cell.BehaviorId, cell.Terrain,
                cell.EncounterType, cell.LayerType), definition,
                $"{mapName} cell {cell.X},{cell.Y} must retain source behavior, terrain, encounters and layers");
        }
    }

    [TestMethod]
    public void SixSourceTrainerActorsKeepFiveBattlesAndSharedSiblingDouble()
    {
        var content = Content;
        var actors = content.MapDefinition(Outcast).Actors;
        var expected = new[] {
            ("Rocket", 9, 24, 3, "TRAINER_TEAM_ROCKET_GRUNT_46"),
            ("Tylor", 12, 15, 3, "TRAINER_FISHERMAN_TYLOR"),
            ("Mymo", 13, 34, 1, "TRAINER_SWIMMER_MALE_MYMO"),
            ("Nicole", 14, 61, 1, "TRAINER_SWIMMER_FEMALE_NICOLE"),
            ("Ava", 10, 44, 1, "TRAINER_SIS_AND_BRO_AVA_GEB"),
            ("Geb", 11, 44, 1, "TRAINER_SIS_AND_BRO_AVA_GEB") };
        Assert.AreEqual(6, actors.Count(a => a.TrainerType == "TRAINER_TYPE_NORMAL"));
        var trainers = content.SixIslandTrainers!;
        foreach (var (name, x, y, elevation, id) in expected)
        {
            string script = "SixIsland_OutcastIsland_EventScript_" + name;
            var actor = actors.Single(a => a.ScriptId == script);
            Assert.AreEqual((x, y, elevation, 1), (actor.X, actor.Y, actor.Elevation, actor.TrainerSight));
            var battle = trainers.BattlesByScript[script];
            Assert.AreEqual("pret:" + id, battle.Trainer.Value);
            Assert.AreEqual(name is "Ava" or "Geb", trainers.Trainers[battle.Trainer].DoubleBattle);
            Assert.AreEqual(name is "Ava" or "Geb" ? 4 : 0, battle.Mode);
            Assert.IsNotNull(content.FindProgram(script));
        }
        Assert.AreEqual("FLAG_HIDE_FIVE_ISLAND_ROCKETS", actors.Single(a => a.ScriptId.EndsWith("_Rocket", StringComparison.Ordinal)).HideFlag);
        Assert.AreEqual(5, expected.Select(e => e.Item5).Distinct().Count());
    }

    [TestMethod]
    public void EveryOutcastTrainerReusesAnExactClassPortraitTupleWithRealArtwork()
    {
        var available = new Dictionary<(string Class, string Pic, int SourcePic), (string Name, JsonElement Art)>();
        foreach (string region in new[] { "five-island", "six-island", "sevii-ruby" })
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
                $"content/generated/sprites/pokemon/{region}/manifest.json")));
            var manifest = document.RootElement;
            foreach (var row in manifest.GetProperty("trainers").EnumerateArray())
            {
                var art = manifest.GetProperty("portraits").EnumerateArray().Single(p =>
                    p.GetProperty("id").GetString() == row.GetProperty("portrait").GetString());
                available.TryAdd((row.GetProperty("trainerClass").GetString()!, row.GetProperty("trainerPic").GetString()!,
                    art.GetProperty("sourceTrainerPicId").GetInt32()), (row.GetProperty("className").GetString()!, art.Clone()));
            }
        }
        var tower = PretTrainerTowerPresentation.Load(TestAssets.Root);
        foreach (var row in tower.GetProperty("trainerBindings").EnumerateArray())
        {
            var art = tower.GetProperty("trainerPortraits").EnumerateArray().Single(p =>
                p.GetProperty("id").GetString() == row.GetProperty("portrait").GetString());
            available.TryAdd((row.GetProperty("trainerClass").GetString()!, row.GetProperty("trainerPic").GetString()!,
                row.GetProperty("sourceTrainerPicId").GetInt32()), (row.GetProperty("className").GetString()!, art.Clone()));
        }
        var trainers = Content.SixIslandTrainers!;
        var ids = trainers.BattlesByScript.Where(p => p.Key.StartsWith("SixIsland_OutcastIsland_", StringComparison.Ordinal))
            .Select(p => p.Value.Trainer).Distinct().ToArray();
        Assert.AreEqual(5, ids.Length);
        foreach (var id in ids)
        {
            var trainer = trainers.Trainers[id];
            Assert.IsTrue(available.TryGetValue((trainer.TrainerClass, trainer.TrainerPic, trainer.SourceTrainerPic), out var presentation), id.Value);
            Assert.IsFalse(string.IsNullOrWhiteSpace(presentation.Name));
            Assert.IsTrue(presentation.Art.GetProperty("width").GetInt32() > 0);
            Assert.IsTrue(presentation.Art.GetProperty("height").GetInt32() > 0);
            Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, presentation.Art.GetProperty("texturePath").GetString()!)));
        }
    }

    [TestMethod]
    public void PPUpAndBothHiddenItemsRetainOriginalCoordinatesPocketsAndFlags()
    {
        var pickups = Content.FieldPickups.Where(p => p.MapSymbol == "MAP_SIX_ISLAND_OUTCAST_ISLAND").ToArray();
        var expected = new[] {
            (11, 16, "ITEM_PP_UP", false, "POCKET_ITEMS", "FLAG_HIDE_SIX_ISLAND_OUTCAST_ISLAND_PP_UP"),
            (16, 23, "ITEM_STAR_PIECE", true, "POCKET_ITEMS", "FLAG_HIDDEN_ITEM_SIX_ISLAND_OUTCAST_ISLAND_STAR_PIECE"),
            (6, 24, "ITEM_NET_BALL", true, "POCKET_POKE_BALLS", "FLAG_HIDDEN_ITEM_SIX_ISLAND_OUTCAST_ISLAND_NET_BALL") };
        CollectionAssert.AreEquivalent(expected, pickups.Select(p => (p.X, p.Y, p.ItemSymbol, p.Hidden, p.Pocket, p.Flag)).ToArray());
        Assert.IsTrue(pickups.All(p => p.Quantity == 1 && p.Elevation == 3));
        foreach (var pickup in pickups.Where(p => p.Hidden))
        {
            var trigger = Content.MapDefinition(Outcast).BackgroundTriggers.Single(t => t.X == pickup.X && t.Y == pickup.Y);
            Assert.AreEqual(pickup.ItemSymbol, trigger.HiddenItem);
            Assert.AreEqual(pickup.Flag, trigger.HiddenFlag);
            Assert.IsFalse(trigger.Underfoot);
        }
    }

    [TestMethod]
    public void AlteringCaveUsesOnlyTheDefaultFireRedZubatTable()
    {
        var table = Content.LandEncounters![Cave];
        Assert.AreEqual(5, table.RawEncounterRate);
        Assert.IsTrue(table.Slots.All(s => s.Species == new SpeciesId("pret:SPECIES_ZUBAT")));
        CollectionAssert.AreEqual(new[] { 10, 12, 8, 14, 10, 12, 16, 6, 8, 14, 8, 14 },
            table.Slots.Select(s => (int)s.MinimumLevel).ToArray());
        Assert.IsTrue(table.Slots.All(s => s.MinimumLevel == s.MaximumLevel));
        CollectionAssert.AreEqual(new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 },
            table.Slots.Select(s => s.Weight).ToArray());
        Assert.IsFalse(Content.LandEncounters.ContainsKey(Outcast));
        Assert.IsFalse(Content.WaterEncounters!.ContainsKey(Cave));
        foreach (var rod in new[] { FishingRod.Old, FishingRod.Good, FishingRod.Super })
            Assert.IsFalse(Content.FishingTables.HasTable(rod, Cave));
    }

    [TestMethod]
    public void OutcastSurfTableKeepsAllFireRedSpeciesLevelsAndWeights()
    {
        var table = Content.WaterEncounters![Outcast];
        Assert.AreEqual(2, table.RawEncounterRate);
        CollectionAssert.AreEqual(new[] { "TENTACOOL", "TENTACOOL", "TENTACOOL", "TENTACRUEL", "TENTACRUEL" },
            table.Slots.Select(s => s.Species.Value[13..]).ToArray());
        CollectionAssert.AreEqual(new[] { (5, 20, 60), (20, 35, 30), (35, 40, 5), (35, 40, 4), (35, 40, 1) },
            table.Slots.Select(s => ((int)s.MinimumLevel, (int)s.MaximumLevel, s.Weight)).ToArray());
    }

    [TestMethod]
    [DataRow(FishingRod.Old)]
    [DataRow(FishingRod.Good)]
    [DataRow(FishingRod.Super)]
    public void OutcastEveryRodKeepsExactFireRedSourceSlots(FishingRod rod)
    {
        var table = Content.FishingTables.Table(rod, Outcast);
        Assert.AreEqual(20, table.RawEncounterRate);
        var expected = rod switch {
            FishingRod.Old => new[] { ("MAGIKARP", 5, 5, 70), ("MAGIKARP", 5, 5, 30) },
            FishingRod.Good => new[] { ("HORSEA", 5, 15, 60), ("MAGIKARP", 5, 15, 20), ("HORSEA", 5, 15, 20) },
            _ => new[] { ("HORSEA", 15, 25, 40), ("QWILFISH", 15, 25, 40), ("GYARADOS", 15, 25, 15),
                ("SEADRA", 25, 35, 4), ("PSYDUCK", 25, 35, 1) }
        };
        CollectionAssert.AreEqual(expected, table.Slots.Select(s =>
            (s.Species.Value[13..], (int)s.MinimumLevel, (int)s.MaximumLevel, s.Weight)).ToArray());
    }
}

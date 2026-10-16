using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class MtEmberSummitContentTests
{
    private static readonly Lazy<PretCampaignCatalog> Current = new(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static MapId Map(string suffix) => new("map.mt-ember-" + suffix);

    [TestMethod]
    public void SummitRouteHasOriginalDimensionsAndReciprocalPhysicalWarps()
    {
        var region = PretSeviiRubyRegion.Load(PretCurrentWorld.Load(TestAssets.Root));
        foreach (var (name, width, height) in new[] { ("summit-path-1f", 15, 18), ("summit-path-2f", 48, 46),
            ("summit-path-3f", 15, 11), ("summit", 19, 22) })
        {
            var map = region.NewMaps[Map(name)];
            Assert.AreEqual((width, height), (map.Width, map.Height));
            foreach (var warp in map.FieldWarps)
            {
                var destination = region.NewMaps[warp.DestinationMapId];
                var reverse = destination.FieldWarps[warp.DestinationWarpId];
                Assert.AreEqual(map.Id, reverse.DestinationMapId);
                Assert.AreEqual(warp.Index, reverse.DestinationWarpId);
                Assert.IsFalse(map.GetTile(warp.X, warp.Y)!.Blocked);
                Assert.IsFalse(destination.GetTile(reverse.X, reverse.Y)!.Blocked);
            }
            foreach (var cell in map.Cells)
            {
                var definition = region.MetatileDefinition(map.Id, cell.MetatileId)!;
                Assert.AreEqual(cell.BehaviorId, definition.Behavior);
                Assert.AreEqual(cell.EncounterType, definition.EncounterType);
            }
        }
        var exterior = region.NewMaps[Map("exterior")];
        Assert.AreEqual(Map("summit-path-1f"), exterior.FieldWarps[2].DestinationMapId);
        Assert.AreEqual(Map("summit-path-3f"), exterior.FieldWarps[3].DestinationMapId);
        Assert.AreEqual(Map("summit"), exterior.FieldWarps[4].DestinationMapId);
        Assert.AreEqual((29, 7), (exterior.FieldWarps[4].X, exterior.FieldWarps[4].Y));
        Assert.AreEqual(Map("summit-path-2f"), region.NewMaps[Map("summit-path-1f")].FieldWarps[1].DestinationMapId);
        Assert.AreEqual(Map("summit-path-3f"), region.NewMaps[Map("summit-path-2f")].FieldWarps[1].DestinationMapId);
    }

    [TestMethod]
    public void SummitActorsUseSharedStrengthAndRockSmashDefinitions()
    {
        var content = Current.Value;
        var summit = content.MapDefinition(Map("summit"));
        var moltres = summit.Actors.Single(actor => actor.SourceLocalId == 1);
        Assert.AreEqual((9, 6, 3), (moltres.X, moltres.Y, moltres.Elevation));
        Assert.AreEqual("OBJ_EVENT_GFX_MOLTRES", moltres.GraphicsId);
        Assert.AreEqual("FLAG_HIDE_MOLTRES", moltres.HideFlag);
        Assert.AreEqual("MtEmber_Summit_EventScript_Moltres", moltres.ScriptId);
        var boulders = summit.Actors.Where(actor => actor.GraphicsId == FieldHmSourceRules.BoulderGraphics).ToArray();
        CollectionAssert.AreEqual(new[] { (2, 10, 12), (3, 9, 12), (4, 8, 11), (5, 8, 10) },
            boulders.Select(actor => (actor.SourceLocalId, actor.X, actor.Y)).ToArray());
        Assert.IsTrue(boulders.All(actor => actor.ScriptId == "EventScript_StrengthBoulder" && actor.HideFlag == "0" &&
            actor.Elevation == 3 && actor.MovementType == "MOVEMENT_TYPE_LOOK_AROUND"));
        var cave = content.MapDefinition(Map("summit-path-2f"));
        var rocks = cave.Actors.Where(actor => actor.GraphicsId == FieldRockSmashSourceRules.Graphics).ToArray();
        CollectionAssert.AreEqual(Enumerable.Range(0x12, 9).Select(value => $"FLAG_TEMP_{value:X}").ToArray(),
            rocks.Select(actor => actor.HideFlag).ToArray());
        foreach (var rock in rocks)
        {
            var source = content.FieldRockSmash!.SourceFor(cave, rock);
            Assert.IsNotNull(source);
            Assert.AreEqual(rock.SourceLocalId, source.SourceLocalId);
            Assert.AreEqual(3, rock.Elevation);
        }
    }

    [TestMethod]
    public void MoltresOverworldPreservesSourceFrameGeometry()
    {
        var sprite = PretOverworldTextures.Load(Path.Combine(TestAssets.Root, "content/generated"))["OBJ_EVENT_GFX_MOLTRES"];
        Assert.AreEqual((32, 32, 9), (sprite.FrameWidth, sprite.FrameHeight, sprite.FrameCount));
        Assert.AreEqual((16, 24), (sprite.AnchorX, sprite.AnchorY));
        Assert.AreEqual("pokemon.png", sprite.TexturePath);
    }

    [TestMethod]
    [DataRow("dest_map", "MAP_MT_EMBER_SUMMIT")]
    [DataRow("dest_warp_id", "999")]
    public void InvalidSummitWarpIsRejectedWithoutHistoricalRegionCounts(string field, string value)
    {
        var root = TestAssets.Output();
        var source = Path.Combine(TestAssets.Root, "content/generated");
        try
        {
            var required = Directory.EnumerateFiles(Path.Combine(source, "maps/regions/sevii-ruby"), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(source, path)).Concat(["data/constants.json"]).Concat(CurrentWorldTestFixture.RelativeFiles()).Distinct();
            foreach (var relative in required)
            {
                var target = Path.Combine(root, "content/generated", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(source, relative), target);
            }
            _ = PretSeviiRubyRegion.Load(PretCurrentWorld.Load(root));
            var path = Path.Combine(root, "content/generated/maps/regions/sevii-ruby/mt-ember-summit-path-2f.json");
            var document = JsonNode.Parse(File.ReadAllText(path))!;
            document["events"]!["warp_events"]![0]![field] = value;
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretSeviiRubyRegion.Load(PretCurrentWorld.Load(root)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

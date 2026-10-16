using System.Text.Json;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class WaterPathHouseContentTests
{
    private static readonly MapId Water = new("map.six-island-water-path");
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));

    [TestMethod]
    [DataRow(1, 5, 13, 3, 7)]
    [DataRow(2, 11, 19, 4, 7)]
    public void SourceDoorsMountBothPhysicalDirectionsAndIndoorRestrictions(int number, int outsideX, int outsideY, int insideX, int insideY)
    {
        var content = Content; var id = new MapId($"map.six-island-water-path-house{number}");
        var room = content.Maps[id]; var outside = content.Maps[Water];
        Assert.AreEqual((11, 9), (room.Width, room.Height));
        var entrance = outside.FieldWarps[number - 1]; var exit = room.FieldWarps.Single();
        Assert.AreEqual((outsideX, outsideY, id, 0),
            (entrance.X, entrance.Y, entrance.DestinationMapId, entrance.DestinationWarpId));
        Assert.AreEqual((insideX, insideY, Water, number - 1),
            (exit.X, exit.Y, exit.DestinationMapId, exit.DestinationWarpId));
        Assert.IsFalse(outside.GetTile(outsideX, outsideY + 1)!.Blocked, "walkable approach below source door");
        Assert.AreEqual(0x69, content.SourceMaps[Water].Cells.Single(c => c.X == outsideX && c.Y == outsideY).BehaviorId);
        Assert.IsFalse(room.GetTile(insideX, insideY)!.Blocked);
        Assert.IsTrue(content.ConnectedFieldMaps![id].Connections.IsEmpty);
        var definition = content.MapDefinition(id);
        Assert.AreEqual(("MAP_TYPE_INDOOR", "MAPSEC_WATER_PATH", "MUS_FUCHSIA"),
            (definition.MapType, definition.RegionMapSection, definition.Music));
        Assert.IsFalse(FieldFlySourceRules.MapTypeAllowsFly(definition.MapType));
        Assert.IsFalse(content.EscapeRoutes.Exits.ContainsKey(id));
        var source = content.SourceMaps[id];
        foreach (var flag in new[] { "allow_cycling", "allow_escaping", "allow_running", "requires_flash" })
            Assert.IsFalse(source.Events.GetProperty(flag).GetBoolean(), flag);
        foreach (var cell in source.Cells.Concat(source.BorderCells))
            Assert.AreEqual(new FieldMetatileDefinition(cell.MetatileId, cell.BehaviorId, cell.Terrain,
                cell.EncounterType, cell.LayerType), content.MetatileDefinition(id, cell.MetatileId));
    }

    [TestMethod]
    [DataRow(1, "Beauty", "OBJ_EVENT_GFX_BEAUTY", 3, 4, "MOVEMENT_TYPE_FACE_DOWN")]
    [DataRow(2, "Man", "OBJ_EVENT_GFX_MAN", 7, 4, "MOVEMENT_TYPE_FACE_UP_AND_LEFT")]
    public void ExactSourceActorsHaveSharedPresentationAndExecutableScripts(int number, string name, string graphics, int x, int y, string movement)
    {
        var content = Content; var id = new MapId($"map.six-island-water-path-house{number}");
        var actor = content.MapDefinition(id).Actors.Single();
        Assert.AreEqual((1, graphics, x, y, 3, movement, "TRAINER_TYPE_NONE"),
            (actor.SourceLocalId, actor.GraphicsId, actor.X, actor.Y, actor.Elevation, actor.MovementType, actor.TrainerType));
        Assert.AreEqual($"SixIsland_WaterPath_House{number}_EventScript_{name}", actor.ScriptId);
        var program = content.FindProgram(actor.ScriptId);
        Assert.IsNotNull(program);
        Assert.IsFalse(program.Instructions.Any(i => i.Op == StoryOp.Unsupported));
        var presentation = PretCampaignMapPresentation.Create(content)[id];
        Assert.AreEqual(content.SixIslandRegion!.NpcSourcePath, presentation.NpcSourcePath);
        Assert.IsTrue(presentation.UsesLocalBorder);
        using var shared = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
            "content/generated/sprites/overworld/catalog.json")));
        var art = shared.RootElement.GetProperty(graphics);
        Assert.IsTrue(art.GetProperty("frameCount").GetInt32() > 0);
        Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, "content/generated/sprites/overworld",
            art.GetProperty("texturePath").GetString()!)));
    }

    [TestMethod]
    public void House1ChartAndHouse2DialogueKeepSeparateSourceBindings()
    {
        var content = Content;
        var chart = content.MapDefinition(new("map.six-island-water-path-house1")).BackgroundTriggers.Single();
        Assert.AreEqual((9, 1, 0, "SixIsland_WaterPath_House1_EventScript_SizeRecord"),
            (chart.X, chart.Y, chart.Elevation, chart.ScriptId));
        Assert.IsNull(chart.PlayerFacing);
        Assert.IsNotNull(content.FindProgram(chart.ScriptId));
        Assert.IsTrue(content.MapDefinition(new("map.six-island-water-path-house2")).BackgroundTriggers.IsEmpty);
        var man = content.FindProgram("SixIsland_WaterPath_House2_EventScript_Man")!;
        Assert.AreEqual(2, man.Instructions.Length);
        Assert.AreEqual(StoryOp.Dialog, man.Instructions[0].Op);
        CollectionAssert.AreEqual(new[] { "SixIsland_WaterPath_House2_Text_MyNeighborMeasuredMe", "MSGBOX_NPC" },
            man.Instructions[0].Args.ToArray());
        Assert.AreEqual(StoryOp.End, man.Instructions[1].Op);
    }
}

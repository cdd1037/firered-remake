using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class OrdinaryInteriorBatchContentTests
{
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));

    [TestMethod]
    [DataRow("ViridianCity_School", "map.viridian-school")]
    [DataRow("FuchsiaCity_House1", "map.fuchsia-city-house-1")]
    [DataRow("ThreeIsland_House2", "map.three-island-house2")]
    [DataRow("ViridianCity_House", "map.viridian-house")]
    [DataRow("VermilionCity_House3", "map.vermilion-city-house-3")]
    [DataRow("SaffronCity_House", "map.saffron-city-house")]
    [DataRow("OneIsland_House1", "map.one-island-house1")]
    [DataRow("OneIsland_House2", "map.one-island-house2")]
    [DataRow("ThreeIsland_House1", "map.three-island-house1")]
    [DataRow("ThreeIsland_House3", "map.three-island-house3")]
    [DataRow("ThreeIsland_House4", "map.three-island-house4")]
    [DataRow("ThreeIsland_House5", "map.three-island-house5")]
    [DataRow("FiveIsland_House1", "map.five-island-house1")]
    [DataRow("FiveIsland_House2", "map.five-island-house2")]
    [DataRow("SixIsland_House", "map.six-island-house")]
    public void ImportedRoomPreservesPinnedEventsActorsDoorsScriptsAndPresentation(string sourceName, string stableId)
    {
        var content = Content;
        var id = new MapId(stableId);
        var sourceDirectory = Path.Combine(TestAssets.Source, "data/maps", sourceName);
        using var original = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(sourceDirectory, "map.json")));
        var raw = original.RootElement;
        var room = content.SchoolHouseInteriors.NewMaps[id];
        var definition = content.MapDefinition(id);
        Assert.IsTrue(JsonElement.DeepEquals(raw, room.Events), sourceName + ": complete source event parity");
        Assert.AreEqual(raw.GetProperty("id").GetString(), definition.SourceMapSymbol);
        Assert.AreEqual(raw.GetProperty("music").GetString(), definition.Music);
        Assert.AreEqual(raw.GetProperty("region_map_section").GetString(), definition.RegionMapSection);
        Assert.AreEqual("MAP_TYPE_INDOOR", definition.MapType);
        Assert.IsTrue(content.ConnectedFieldMaps![id].Connections.IsEmpty);
        Assert.IsFalse(FieldFlySourceRules.MapTypeAllowsFly(definition.MapType));
        Assert.IsFalse(content.EscapeRoutes.Exits.ContainsKey(id));
        foreach (string key in new[] { "allow_running", "allow_cycling", "allow_escaping", "requires_flash" })
            Assert.IsFalse(room.Events.GetProperty(key).GetBoolean(), key);

        var objects = raw.GetProperty("object_events").EnumerateArray().ToArray();
        Assert.AreEqual(objects.Length, definition.Actors.Length);
        Assert.AreEqual(raw.GetProperty("bg_events").GetArrayLength(), definition.BackgroundTriggers.Length);
        for (int index = 0; index < objects.Length; index++)
        {
            var expected = objects[index];
            var actor = definition.Actors.Single(a => a.SourceLocalId == index + 1);
            Assert.AreEqual((expected.GetProperty("graphics_id").GetString(), expected.GetProperty("x").GetInt32(),
                    expected.GetProperty("y").GetInt32(), expected.GetProperty("elevation").GetInt32(),
                    expected.GetProperty("movement_type").GetString(), expected.GetProperty("script").GetString(),
                    expected.GetProperty("flag").GetString()),
                (actor.GraphicsId, actor.X, actor.Y, actor.Elevation, actor.MovementType, actor.ScriptId, actor.HideFlag));
        }
        foreach (var exit in room.FieldWarps)
        {
            var exterior = content.Maps[exit.DestinationMapId];
            var entrance = exterior.FieldWarps[exit.DestinationWarpId];
            Assert.AreEqual(id, entrance.DestinationMapId, "each source exit returns to the same exterior door");
            var arrival = room.FieldWarps[entrance.DestinationWarpId];
            Assert.AreEqual(exit.DestinationMapId, arrival.DestinationMapId);
            Assert.IsFalse(exterior.GetTile(entrance.X, entrance.Y + 1)!.Blocked);
            Assert.IsFalse(room.GetTile(exit.X, exit.Y)!.Blocked);
        }

        string sourceScripts = File.ReadAllText(Path.Combine(sourceDirectory, "scripts.inc"));
        var programNames = Regex.Matches(sourceScripts, @"(?m)^(\w+_EventScript_\w+)::")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.IsTrue(programNames.Length > 0, "the test must cover actual source interactions");
        foreach (string script in programNames)
        {
            var program = content.FindProgram(script);
            Assert.IsNotNull(program, script);
            Assert.IsFalse(program.Instructions.Any(i => i.Op == StoryOp.Unsupported), script);
        }
        foreach (Match match in Regex.Matches(sourceScripts, @"\bmsgbox\s+(\w+)"))
            Assert.IsTrue(content.TextPages(match.Groups[1].Value).Any(page => !string.IsNullOrWhiteSpace(page)), match.Value);
        foreach (var actor in definition.Actors.Where(a => a.ScriptId is not ("0" or "0x0")))
            Assert.IsTrue(programNames.Contains(actor.ScriptId), actor.ScriptId);
        foreach (var sign in definition.BackgroundTriggers)
        {
            // Source map events may bind shared scripts such as the school's Pokémon Journal.
            var program = content.FindProgram(sign.ScriptId);
            Assert.IsNotNull(program, sign.ScriptId);
            Assert.IsFalse(program.Instructions.Any(i => i.Op == StoryOp.Unsupported), sign.ScriptId);
        }

        var presentation = PretCampaignMapPresentation.Create(content)[id];
        Assert.IsTrue(presentation.OverworldCatalogPath.EndsWith("sprites/overworld/catalog.json", StringComparison.Ordinal));
        Assert.IsTrue(presentation.UsesLocalBorder);
        using var art = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
            "content/generated/sprites/overworld/catalog.json")));
        foreach (string graphics in definition.Actors.Select(a => a.GraphicsId).Distinct())
        {
            var entry = art.RootElement.GetProperty(graphics);
            Assert.IsTrue(entry.GetProperty("frameCount").GetInt32() > 0);
            Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, "content/generated/sprites/overworld",
                entry.GetProperty("texturePath").GetString()!)), graphics);
        }
    }

    [TestMethod]
    public void LostelleVisibilityGenderSpecialAndRescueBranchRemainSourceSelected()
    {
        var content = Content;
        var lostelle = content.MapDefinition(new("map.three-island-house1")).Actors.Single();
        Assert.AreEqual("FLAG_HIDE_LOSTELLE_IN_HER_HOME", lostelle.HideFlag);
        var talk = content.FindProgram(lostelle.ScriptId)!;
        Assert.IsTrue(talk.Instructions.Any(i => i.Op == StoryOp.Special &&
            i.Args.SequenceEqual(new[] { "BufferBigGuyOrBigGirlString" })));
        var girl = content.FindProgram("ThreeIsland_House5_EventScript_LittleGirl")!;
        Assert.IsTrue(girl.Instructions.Any(i => i.Op == StoryOp.FlagGoto && i.Args.SequenceEqual(new[] {
            "FLAG_RESCUED_LOSTELLE", "ThreeIsland_House5_EventScript_LittleGirlLostelleFound" })));
    }
}

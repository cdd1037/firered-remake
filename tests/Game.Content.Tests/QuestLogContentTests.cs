using System.Text.Json;
using Game.Content;
using Game.Domain.World;
namespace FireRed.Tests.Content;
[TestClass]
public sealed class QuestLogContentTests
{
    [TestMethod]
    [DataRow("quest-log", "ArrivedInLocation", "Reached {STR_VAR_1}.")]
    [DataRow("quest-log-map-names", "map.pallet-town", "START TOWN")]
    [DataRow("quest-log-departures", "QL_LOCATION_OAKS_LAB/place", "RESEARCH ROOM")]
    [DataRow("quest-log-departures", "QL_LOCATION_OAKS_LAB/region", "START TOWN")]
    public void SparseProjectionChangesOnlyOwnedDisplayFields(string group, string id, string text)
    {
        var original = PretQuestLogSource.Load(TestAssets.Root, SourceTextPack.Load(null));
        using var output = TestAssets.TemporaryOutput();
        Write(output.Path, group, id, text);
        var display = PretQuestLogSource.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        CollectionAssert.AreEqual(original.EventSizes.OrderBy(pair => pair.Key).ToArray(), display.EventSizes.OrderBy(pair => pair.Key).ToArray());
        CollectionAssert.AreEqual(original.ArrivalFlags.ToArray(), display.ArrivalFlags.ToArray());
        CollectionAssert.AreEqual(original.DisabledMaps.ToArray(), display.DisabledMaps.ToArray());
        CollectionAssert.AreEqual(original.StoryItems.ToArray(), display.StoryItems.ToArray());
        CollectionAssert.AreEqual(original.WindowPixels.ToArray(), display.WindowPixels.ToArray());
        Assert.AreEqual(original.Texts["RestedAtHome"], display.Texts["RestedAtHome"]);
        Assert.AreEqual(original.MapNames["map.viridian-city"], display.MapNames["map.viridian-city"]);
        if (group == "quest-log") Assert.AreEqual("Reached PALLET TOWN.", display.Expand("ArrivedInLocation", "RED", ["PALLET TOWN"]));
        else if (group == "quest-log-map-names") Assert.AreEqual(text, display.MapName(id));
        else Assert.AreEqual(text, id.EndsWith("/place", StringComparison.Ordinal) ? display.Departures[1].Place : display.Departures[1].RegionName);
        foreach (var (before, after) in original.Departures.Zip(display.Departures))
            Assert.AreEqual(before, after with { Place = before.Place, RegionName = before.RegionName });
    }

    [TestMethod]
    [DataRow("quest-log", "Unknown", "Text")]
    [DataRow("quest-log-map-names", "map.missing", "Text")]
    [DataRow("quest-log-departures", "QL_LOCATION_UNKNOWN/place", "Text")]
    [DataRow("quest-log", "ArrivedInLocation", "Missing argument.")]
    [DataRow("quest-log", "ArrivedInLocation", "Reached {UNKNOWN}.")]
    [DataRow("quest-log-map-names", "map.pallet-town", "")]
    [DataRow("quest-log-departures", "QL_LOCATION_OAKS_LAB/place", "{PLAYER}")]
    public void UnknownIdsEmptyNamesAndChangedTemplateControlsFailAtTheLoader(string group, string id, string text)
    {
        using var output = TestAssets.TemporaryOutput();
        Write(output.Path, group, id, text);
        Assert.ThrowsExactly<InvalidDataException>(() => PretQuestLogSource.Load(TestAssets.Root, SourceTextPack.Load(output.Path)));
    }

    [TestMethod]
    [DataRow("{STR_VAR_1}", "{PLAYER}\\n$", "{DYNAMIC 0x00}")]
    [DataRow("RED", "{STR_VAR_2}", "literal")]
    public void QuestBindingIsSinglePassAndKeepsActualSourceLineBreaks(string player, string first, string second)
    {
        var source = PretQuestLogSource.Load(TestAssets.Root, SourceTextPack.Load(null));
        Assert.AreEqual(player + " switched the POKéMON\n" + first + " with " + second + ".",
            source.Expand("SwitchMon1WithMon2", player, [first, second]));
        Assert.ThrowsExactly<InvalidDataException>(() => source.Expand("SwitchMon1WithMon2", player, [first]));
        Assert.ThrowsExactly<InvalidDataException>(() => (source with { Texts = source.Texts.SetItem("ArrivedInLocation", "{UNKNOWN}") })
            .Expand("ArrivedInLocation", player, [first]));
    }

    private static void Write(string root, string group, string id, string value)
    {
        Directory.CreateDirectory(Path.Combine(root, "source"));
        File.WriteAllText(Path.Combine(root, "source", group + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = value }));
    }

    [TestMethod]
    public void OriginalTextsMapNamesAndEventBudgetAreAvailableWithoutRawSource()
    {
        var source=PretQuestLogSource.Load(TestAssets.Root);
        Assert.AreEqual("Previously on your quest…",source.Texts["PreviouslyOnYourQuest"]);
        Assert.AreEqual("PALLET TOWN",source.MapName("map.pallet-town"));
        Assert.AreEqual("Arrived in PALLET TOWN.",source.Expand("ArrivedInLocation","RED",["PALLET TOWN"]));
        Assert.AreEqual(8,source.EventSizes[2]);Assert.AreEqual(4,source.EventSizes[11]);
        Assert.AreEqual(14,source.EventSizes[37]);Assert.AreEqual(2,source.EventSizes[39]);
        Assert.IsTrue(source.DisabledMaps.Contains("map.trainer-tower-lobby"));
        Assert.IsTrue(source.ArrivalFlags.Contains("FLAG_WORLD_MAP_VIRIDIAN_CITY"));
        Assert.IsTrue(source.Departures.Any(d=>d.Inside=="map.players-house-1f"&&d.Outside=="map.pallet-town")||source.Departures.Any(d=>d.TextKey=="RestedAtHome"));
        Assert.ThrowsExactly<InvalidDataException>(()=>source.Expand("ArrivedInLocation","RED",[]));
    }
}

using Game.Content;
using Game.Domain.World;
namespace FireRed.Tests.Content;
[TestClass]
public sealed class QuestLogContentTests
{
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

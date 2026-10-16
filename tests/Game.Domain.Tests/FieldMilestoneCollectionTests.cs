using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class FieldMilestoneCollectionTests
{
    private static FieldMilestoneCollection Collection => new(new MapId("map.house"), "Query",
    [new("GAME_STAT_ENTERED_HOF", "VAR_HOF", "VAR_HOF_AWARD", 1, false, true, [1,40,100,200]),
     new("GAME_STAT_HATCHED_EGGS", "VAR_EGGS", "VAR_EGG_AWARD", 2, true, true, [1,100,200,300]),
     new("GAME_STAT_LINK_BATTLE_WINS", "VAR_LINK", "VAR_LINK_AWARD", 4, false, false, [1,20,50,100])]);

    [TestMethod]
    public void SourceNativeCountConversionsAndUnavailableLinkArePreserved()
    {
        var stats = ImmutableSortedDictionary<string,int>.Empty.Add("GAME_STAT_ENTERED_HOF",65536)
            .Add("GAME_STAT_HATCHED_EGGS",65536).Add("GAME_STAT_LINK_BATTLE_WINS",1000);
        var result = Collection.Query(ImmutableSortedDictionary<string,int>.Empty,stats,"VAR_RESULT");
        Assert.AreEqual(0,result["VAR_HOF"]);
        Assert.AreEqual(65535,result["VAR_EGGS"]);
        Assert.AreEqual(0,result["VAR_LINK"]);
        Assert.AreEqual(2,result["VAR_RESULT"]);
        Assert.IsFalse(result.ContainsKey("VAR_EGG_AWARD"),"query must not award a sticker");
    }

    [TestMethod]
    public void MissingAndZeroStatsHaveNoBragCategories()
    {
        Assert.AreEqual(0,Collection.Query(ImmutableSortedDictionary<string,int>.Empty,null,"VAR_RESULT")["VAR_RESULT"]);
    }
}

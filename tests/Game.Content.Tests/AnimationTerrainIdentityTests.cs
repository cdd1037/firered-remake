using Game.Content;
using System.Text.Json;
namespace FireRed.Tests.Content;
[TestClass,TestCategory("Game")]
public sealed class AnimationTerrainIdentityTests
{
    [TestMethod]
    public void GymAndLeaderRetainDistinctGeneratedSourceTerrainIds()
    {
        var terrain=PretGymBattleTerrain.Load(TestAssets.Root);
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"content/generated/battle/terrain/gym/gym-battle-terrain.json")));
        var source=document.RootElement.GetProperty("variants").EnumerateArray().ToDictionary(v=>v.GetProperty("sceneTexture").GetString()!,v=>v.GetProperty("terrainId").GetInt32());
        Assert.AreEqual(source["gym-scene"],terrain.GymSourceTerrainId);Assert.AreEqual(source["leader-scene"],terrain.LeaderSourceTerrainId);Assert.AreNotEqual(terrain.GymSourceTerrainId,terrain.LeaderSourceTerrainId);
    }
}

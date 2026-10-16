using System.Reflection;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedRegionBoundaryTests
{
    [TestMethod]
    public void LeagueAndSevenIslandLoadBusinessDataWithoutUnusedRulesReports()
    {
        foreach (string family in new[] { "pokemon-league", "league-rematch", "seven-island" })
            Assert.IsFalse(File.Exists(Path.Combine(TestAssets.Root, "content/generated/data/regions", family, "rules.json")));

        var league = PretPokemonLeagueRules.LoadDocument(TestAssets.Root);
        Assert.AreEqual(7, league.Trainers.Length);
        Assert.AreEqual(7, league.Battles.Length);
        var rematch = PretLeagueRematchRules.LoadDocument(TestAssets.Root);
        Assert.AreEqual(7, rematch.Trainers.Length);
        Assert.AreEqual(7, rematch.Battles.Length);
        var seven = PretSevenIslandRules.LoadDocument(TestAssets.Root);
        Assert.AreEqual(18, seven.Trainers.Length);
        Assert.AreEqual(20, seven.Battles.Length);
        Assert.AreEqual(13, PretSevenIslandRules.LoadItemsDocument(TestAssets.Root).Pickups.Length);
        Assert.AreEqual(11, PretSevenIslandRules.LoadEncounterDeclarations(TestAssets.Root).GetProperty("encounters").GetArrayLength());
    }

    [TestMethod]
    public void SeafoamLoadsTrainerBusinessDataWithoutUnusedRulesReport()
    {
        Assert.IsFalse(File.Exists(Path.Combine(TestAssets.Root, "content/generated/data/regions/seafoam/rules.json")));
        var document = PretSeafoamRules.LoadDocument(TestAssets.Root);
        Assert.AreEqual(21, document.Trainers.Length);
        Assert.AreEqual(22, document.Battles.Length);
        Assert.IsNotNull(PretSeafoamRules.LoadTrainers(TestAssets.Root));
    }

    [TestMethod]
    public void MtMoonLoadsBusinessDefinitionsWithoutDuplicateSharedCatalogs()
    {
        var region = PretMtMoonRegion.Load(PretCurrentWorld.Load(TestAssets.Root));
        Assert.AreEqual(3, region.NewMaps.Count);
        Assert.AreEqual(12, PretMtMoonTrainers.Load(TestAssets.Root).Trainers.Count);
        Assert.AreEqual(3, region.Encounters.GetArrayLength());
        var items = PretMtMoonItems.LoadProject(TestAssets.Root);
        Assert.AreEqual(18, items.Pickups.Count(p => p.Map != "Route4"));
        Assert.AreEqual(2, items.Fossils.Length);
        foreach (string obsolete in new[] { "dependencies.json", "npc-source.json", "mtmoon-catalog.json" })
            Assert.IsFalse(File.Exists(Path.Combine(region.GeneratedDirectory, obsolete)));

    }

    [TestMethod]
    public void RegionAndServiceWrappersLoadFromGeneratedDataWithoutSourceTrees()
    {
        string root = TestAssets.Output();
        string generated = Path.Combine(root, "content/generated");
        string source = Path.Combine(TestAssets.Root, "content/generated");
        Directory.CreateDirectory(generated);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(generated, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        try
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/sources")));
            Type[] loaders =
            [
            typeof(PretVermilionGymRegion),
            typeof(PretSsAnneRegion),
            typeof(PretViridianRegion),
            typeof(PretTrainerTowerRegion),
            typeof(PretFuchsiaRouteRegion),
            typeof(PretCeruleanCaveRegion),
            typeof(PretMtMoonRegion),
            typeof(PretRoute3Region),
            typeof(PretCeladonApproachRegion),
            typeof(PretRocketHideoutRegion),
            typeof(PretBillRegion),
            typeof(PretMoveReminderHouse),
            typeof(PretFossilLabRegion),
            typeof(PretFuchsiaGymRegion),
            typeof(PretPokemonTowerRegion),
            typeof(PretRoute16FlyHouse),
            typeof(PretRoute22Region),
            typeof(PretSaffronGymRegion),
            typeof(PretCopycatHouse),
            typeof(PretCeladonGymRegion),
            typeof(PretFightingDojoRegion),
            typeof(PretForestRegion),
            typeof(PretCeruleanRegion),
            typeof(PretRockTunnelRegion),
            typeof(PretSafariRegion),
            typeof(PretPowerPlantRegion),
            typeof(PretPewterRegion),
            typeof(PretSnorlaxRegion),
            typeof(PretVermilionRegion),
            typeof(PretRoute11Region),
            typeof(PretMoveDeleterHouse),
            typeof(PretSchoolHouseInteriors),
            typeof(PretCityServices),
            typeof(PretPokemonStorageSource),
            typeof(PretMagikarpContract),
            typeof(PretRegionalGiftContract),
            ];
            var world = PretCurrentWorld.Load(root);
            var failures = new List<string>();
            foreach (Type type in loaders)
            {
                var method = type.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)!;
                object?[] arguments = method.GetParameters().Select((p, i) => i == 0 ? (p.ParameterType == typeof(PretCurrentWorld) ? (object)world : root) : p.DefaultValue).ToArray();
                try { Assert.IsNotNull(method.Invoke(null, arguments), type.Name); }
                catch (Exception error)
                {
                    var cause = error is TargetInvocationException invocation ? invocation.InnerException ?? error : error;
                    failures.Add(type.Name + ": " + cause);
                }
            }
            Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
        }
        finally { Directory.Delete(root, true); }
    }
}

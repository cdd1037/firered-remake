using System.Reflection;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedRegionBoundaryTests
{
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
            var failures = new List<string>();
            foreach (Type type in loaders)
            {
                var method = type.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)!;
                object?[] arguments = method.GetParameters().Select((p, i) => i == 0 ? (object)root : p.DefaultValue).ToArray();
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

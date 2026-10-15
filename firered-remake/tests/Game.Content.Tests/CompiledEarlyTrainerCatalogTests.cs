using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CompiledEarlyTrainerCatalogTests
{
    // Representative original parties, not a second copy of the complete trainer catalog.
    [TestMethod]
    [DataRow("forest", "BUG_CATCHER_RICK", "RICK", new[] { "WEEDLE", "CATERPIE" }, new[] { 6, 6 })]
    [DataRow("pewter", "LEADER_BROCK", "BROCK", new[] { "GEODUDE", "ONIX" }, new[] { 12, 14 })]
    [DataRow("route22", "RIVAL_ROUTE22_EARLY_SQUIRTLE", "TERRY", new[] { "PIDGEY", "SQUIRTLE" }, new[] { 9, 9 })]
    [DataRow("route3", "LASS_SALLY", "SALLY", new[] { "RATTATA", "NIDORAN_F" }, new[] { 10, 10 })]
    [DataRow("mtmoon", "SUPER_NERD_MIGUEL", "MIGUEL", new[] { "GRIMER", "VOLTORB", "KOFFING" }, new[] { 12, 12, 12 })]
    public void CompiledCatalogLoadsNamedPartyInOrderWithoutRawInputs(string region, string id, string name,
        string[] species, int[] levels)
    {
        WithCatalog(region, (root, _) =>
        {
            var trainer = LoadTrainers(root, region)[new("pret:TRAINER_" + id)];
            Assert.AreEqual(name, trainer.SourceTrainerName);
            CollectionAssert.AreEqual(species.Select(s => "pret:SPECIES_" + s).ToArray(), trainer.Party.Select(p => p.Species.Value).ToArray());
            CollectionAssert.AreEqual(levels, trainer.Party.Select(p => p.Level).ToArray());
            Assert.AreSame(SharedTrainerCatalog.LoadProject(root).Definition(trainer.Id), trainer);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        });
    }

    [TestMethod]
    [DataRow("forest")]
    [DataRow("pewter")]
    [DataRow("route22")]
    [DataRow("route3")]
    [DataRow("mtmoon")]
    public void CompiledCatalogStillRejectsInvalidGameplayCallbacks(string region)
    {
        WithCatalog(region, (root, path) =>
        {
            var document = JsonNode.Parse(File.ReadAllText(path))!;
            document["battles"]![0]!["callback"] = "InvalidCallback";
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<NotSupportedException>(() => LoadTrainers(root, region));
        });
    }

    [TestMethod]
    [DataRow("forest")]
    [DataRow("pewter")]
    [DataRow("route22")]
    [DataRow("route3")]
    [DataRow("mtmoon")]
    public void CompiledCatalogRejectsWrongSchemaAndUnboundBattleReference(string region)
    {
        WithCatalog(region, (root, path) =>
        {
            var document = JsonNode.Parse(File.ReadAllText(path))!;
            document["schemaVersion"] = 2;
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => LoadTrainers(root, region));
            document["schemaVersion"] = 1;
            document["trainers"]!.AsArray().RemoveAt(0);
            File.WriteAllText(path, document.ToJsonString());
            var error = Assert.ThrowsExactly<ArgumentException>(() => LoadTrainers(root, region));
            Assert.AreEqual("trainer_catalog_battle_reference", error.Message);
        });
    }

    private static void WithCatalog(string region, Action<string, string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-compiled-trainers-" + Guid.NewGuid().ToString("N"));
        string relative = region switch {
            "forest" => PretForestTrainers.RelativePath, "pewter" => PretPewterTrainers.RelativePath,
            "route22" => PretRoute22Trainers.RelativePath, "route3" => PretRoute3Trainers.RelativePath,
            "mtmoon" => PretMtMoonTrainers.RelativePath, _ => throw new ArgumentOutOfRangeException(nameof(region))
        };
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, relative), path);
            string shared = Path.Combine(root, SharedTrainerCatalog.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(shared)!);
            File.Copy(Path.Combine(TestAssets.Root, SharedTrainerCatalog.RelativePath), shared);
            action(root, path);
        }
        finally { Directory.Delete(root, true); }
    }

    private static ImmutableDictionary<TrainerId, TrainerDefinition> LoadTrainers(string root, string region) => region switch {
        "forest" => PretForestTrainers.Load(root).Trainers,
        "pewter" => PretPewterTrainers.Load(root).Trainers,
        "route22" => PretRoute22Trainers.Load(root).Trainers,
        "route3" => PretRoute3Trainers.Load(root).Trainers,
        "mtmoon" => PretMtMoonTrainers.Load(root).Trainers,
        _ => throw new ArgumentOutOfRangeException(nameof(region))
    };
}

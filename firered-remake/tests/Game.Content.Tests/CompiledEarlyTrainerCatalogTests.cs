using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CompiledEarlyTrainerCatalogTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Hashes are the complete sorted trainer and battle arrays serialized from the
    // pre-migration loaders. They include names, bytes, moves, money, and source lines.
    [TestMethod]
    [DataRow("forest", "725d171f7d3b3c064572abd9d70df4ac3f46ad950aa57e0373497823f74e123b")]
    [DataRow("pewter", "12fcbc07b817118c2bf7600d60c91423b03b66f87879c425dc0a4bfff584e53a")]
    [DataRow("route22", "0a8fa2bc057ec1be06531e9689460ed7b049e9b6ec678b4d3d696e871d2c7221")]
    [DataRow("route3", "5dfc6987d463f13dfcd7f20edd80af14039098e2add6b276201ab96ec1051fba")]
    [DataRow("mtmoon", "7189f2981517502ba4c83162415f4635089868bf3d781fde09e9abfb3ae334ff")]
    public void CompiledCatalogAlonePreservesEveryBaselineDomainField(string region, string expectedHash)
    {
        WithCatalog(region, (root, _) =>
        {
            var json = LoadDomainArrays(root, region);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            Assert.AreEqual(expectedHash, hash);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.AreEqual(1, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
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
            Assert.ThrowsExactly<NotSupportedException>(() => LoadDomainArrays(root, region));
        });
    }

    [TestMethod]
    [DataRow("forest")]
    [DataRow("pewter")]
    [DataRow("route22")]
    [DataRow("route3")]
    [DataRow("mtmoon")]
    public void CompiledCatalogRejectsWrongSchemaAndTruncatedRoster(string region)
    {
        WithCatalog(region, (root, path) =>
        {
            var document = JsonNode.Parse(File.ReadAllText(path))!;
            document["schemaVersion"] = 2;
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => LoadDomainArrays(root, region));
            document["schemaVersion"] = 1;
            document["trainers"]!.AsArray().RemoveAt(0);
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => LoadDomainArrays(root, region));
        });
    }

    private static void WithCatalog(string region, Action<string, string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-compiled-trainers-" + Guid.NewGuid().ToString("N"));
        string relative = $"content/generated/{region}-trainers/trainers.json";
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, relative), path);
            action(root, path);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string LoadDomainArrays(string root, string region)
    {
        switch (region)
        {
            case "forest":
                var forest = PretForestTrainers.Load(root);
                return JsonSerializer.Serialize(new { trainers = forest.Trainers.Values.OrderBy(t => t.Id.Value), battles = forest.Battles.Values.OrderBy(b => b.Trainer.Value) }, JsonOptions);
            case "pewter":
                var pewter = PretPewterTrainers.Load(root);
                return JsonSerializer.Serialize(new { trainers = pewter.Trainers.Values.OrderBy(t => t.Id.Value), battles = pewter.Battles.Values.OrderBy(b => b.Trainer.Value) }, JsonOptions);
            case "route22":
                var route22 = PretRoute22Trainers.Load(root);
                return JsonSerializer.Serialize(new { trainers = route22.Trainers.Values.OrderBy(t => t.Id.Value), battles = route22.Battles.Values.OrderBy(b => b.Trainer.Value) }, JsonOptions);
            case "route3":
                var route3 = PretRoute3Trainers.Load(root);
                return JsonSerializer.Serialize(new { trainers = route3.Trainers.Values.OrderBy(t => t.Id.Value), battles = route3.Battles.Values.OrderBy(b => b.Trainer.Value) }, JsonOptions);
            case "mtmoon":
                var mtmoon = PretMtMoonTrainers.Load(root);
                return JsonSerializer.Serialize(new { trainers = mtmoon.Trainers.Values.OrderBy(t => t.Id.Value), battles = mtmoon.Battles.Values.OrderBy(b => b.Trainer.Value) }, JsonOptions);
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }
}

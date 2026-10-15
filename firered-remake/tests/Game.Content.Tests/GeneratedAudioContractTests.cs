using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedAudioContractTests
{
    [TestMethod]
    public void SynthManifestsContainOnlyPlaybackBindings()
    {
        string root = Path.Combine(TestAssets.Root, "content/generated");
        int manifests = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            var document = json.RootElement;
            if (document.ValueKind != JsonValueKind.Object || !document.TryGetProperty("cues", out var cues) ||
                !document.TryGetProperty("schemaVersion", out var version)) continue;
            manifests++;
            Assert.AreEqual(1, version.GetInt32(), path);
            Assert.IsFalse(document.TryGetProperty("sourceCommit", out _), path);
            Assert.IsTrue(cues.GetArrayLength() > 0, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in cues.EnumerateArray()) ValidateCue(cue, ids, path);
            if (document.TryGetProperty("inheritedCues", out var inherited))
                foreach (var cue in inherited.EnumerateArray()) ValidateCue(cue, ids, path);
        }
        Assert.IsTrue(manifests > 0, "No runtime synth manifests were checked.");
        _ = PretTrainerTowerAudio.LoadSynthManifest(TestAssets.Root);
    }

    private static void ValidateCue(JsonElement cue, HashSet<string> ids, string path)
    {
        string[] required = ["id", "sourceSymbol", "sourcePlayer", "priority", "sourceCounter", "songAddress", "species", "looping"];
        foreach (string field in required) Assert.IsTrue(cue.TryGetProperty(field, out _), path + ": " + field);
        foreach (var field in cue.EnumerateObject())
            Assert.IsTrue(required.Contains(field.Name) || field.Name == "cryMode", path + ": " + field.Name);
        Assert.IsTrue(ids.Add(cue.GetProperty("id").GetString()!), path + ": duplicate cue");
        Assert.IsTrue(cue.GetProperty("sourcePlayer").GetInt32() is >= 0 and <= 4, path);
        Assert.IsTrue(cue.GetProperty("priority").GetInt32() is >= 0 and <= 255, path);
        if (cue.GetProperty("sourceCounter").ValueKind != JsonValueKind.Null)
            Assert.IsTrue(cue.GetProperty("sourceCounter").GetInt32() >= 0, path);
        if (cue.GetProperty("species").GetInt32() > 0)
        {
            Assert.AreEqual(4, cue.GetProperty("sourcePlayer").GetInt32(), path);
            Assert.AreEqual(0u, cue.GetProperty("songAddress").GetUInt32(), path);
            Assert.IsFalse(cue.GetProperty("looping").GetBoolean(), path);
        }
        else Assert.IsTrue(cue.GetProperty("songAddress").GetUInt32() > 0, path);
    }
}

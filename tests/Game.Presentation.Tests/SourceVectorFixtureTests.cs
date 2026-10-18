using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceVectorFixtureTests
{
    [TestMethod]
    public void MissingGeneratedDataReportsTheExplicitCommandWithoutRunningIt()
    {
        using var temp = TestAssets.TemporaryOutput();
        string path = Path.Combine(temp.Path, "missing.json");
        const string command = "python reference.py --output missing.json";
        var error = Assert.ThrowsExactly<FileNotFoundException>(() => TestAssets.GeneratedFile(path, command));
        StringAssert.Contains(error.Message, command);
        StringAssert.Contains(error.Message, path);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void ExplicitFullDirectoryUsesItsDataAndNeverFallsBack()
    {
        using var temp = TestAssets.TemporaryOutput();
        string? before = Environment.GetEnvironmentVariable("FIRERED_SOURCE_VECTOR_DIR");
        try
        {
            Environment.SetEnvironmentVariable("FIRERED_SOURCE_VECTOR_DIR", temp.Path);
            File.WriteAllText(Path.Combine(temp.Path, "probe.json"), "{\"profile\":\"full\",\"value\":17}");
            using var doc = SourceVectorFixture.Read("probe.json", "generate probe");
            Assert.AreEqual(17, doc.RootElement.GetProperty("value").GetInt32());
            // This file normally exists locally, but an explicit full directory is authoritative.
            var error = Assert.ThrowsExactly<FileNotFoundException>(() => SourceVectorFixture.Read("social-four-source-vectors.json", "generate social"));
            StringAssert.Contains(error.Message, temp.Path);
            StringAssert.Contains(error.Message, "generate social");
            File.WriteAllText(Path.Combine(temp.Path, "invalid.json"), "{");
            Assert.Throws<JsonException>(() => SourceVectorFixture.Read("invalid.json", "generate invalid"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRERED_SOURCE_VECTOR_DIR", before);
        }
    }
}

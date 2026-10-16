using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Growth;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedResourceFileTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("/absolute")]
    [DataRow("C:/absolute")]
    [DataRow("folder\\file")]
    [DataRow("folder//file")]
    [DataRow("../escape")]
    [DataRow("folder/../escape")]
    [DataRow("./file")]
    public void RejectsUnsafeRelativePaths(string path) =>
        Assert.ThrowsExactly<InvalidDataException>(() => GeneratedResourceFile.SafePath(TestAssets.Root, path));

    [TestMethod]
    public void ReadsOrdinaryFilesAndRejectsFileDirectoryAndAncestorLinks()
    {
        var root = TestAssets.Output();
        try
        {
            var directory = Path.Combine(root, "actual");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "file.json"), "{}");
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("{}"), GeneratedResourceFile.Read(root, "actual/file.json"));
            File.CreateSymbolicLink(Path.Combine(root, "file-link.json"), Path.Combine(directory, "file.json"));
            Directory.CreateSymbolicLink(Path.Combine(root, "directory-link"), directory);
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedResourceFile.Read(root, "file-link.json"));
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedResourceFile.Read(root, "directory-link/file.json"));
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedResourceFile.Read(Path.Combine(root, "directory-link"), "file.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("{\"name\":\"first\",\"name\":\"second\"}")]
    [DataRow("{\"name\":\"first\",\"nested\":{\"x\":1,\"x\":2}}")]
    [DataRow("null")]
    public void StrictByteReaderRejectsDuplicateKeysAndNull(string json) =>
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<JsonElement>(Encoding.UTF8.GetBytes(json), ContentJson.CreateOptions()));

    [TestMethod]
    public void StrictByteReaderEnforcesSizeDepthSchemaAndExactEnumGrammar()
    {
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<JsonElement>(new byte[ContentJson.MaximumJsonBytes + 1], ContentJson.CreateOptions()));
        var nested = new string('[', 65) + "0" + new string(']', 65);
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<JsonElement>(Encoding.UTF8.GetBytes(nested), ContentJson.CreateOptions()));
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var name = JsonNamingPolicy.CamelCase.ConvertName(GrowthRate.MediumFast.ToString());
        Assert.AreEqual(GrowthRate.MediumFast, ContentJson.ParseStrict<GrowthRate>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(name)), options));
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<GrowthRate>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(name.ToUpperInvariant())), options));
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<GrowthRate>(Encoding.UTF8.GetBytes("0"), options));
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<NameDocument>(Encoding.UTF8.GetBytes("{\"name\":\"x\",\"unknown\":true}"), options));
        Assert.ThrowsExactly<ContentValidationException>(() => ContentJson.ParseStrict<NameDocument>(Encoding.UTF8.GetBytes("{}"), options));
    }

    private sealed record NameDocument(string Name);
}

using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedStoryRuntimeTests
{
    [TestMethod]
    public void StoryRunsFromGeneratedArtifactWithoutRawSources()
    {
        var root = TestAssets.Output();
        var relative = "content/generated/story/runtime.json";
        var target = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(TestAssets.Root, relative), target, true);
        var story = PretStoryRuntime.Load(root);
        Assert.AreEqual(4099, story.Compilation.Programs.Count);
        Assert.AreEqual(4097, story.TextBank.Count);
        Assert.AreEqual(366, story.Compilation.MapHeaders.Count);
        Assert.AreEqual(403, story.Compilation.Movements.Count);
        Assert.AreEqual(6885, story.Compilation.Constants.Count);
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        Assert.IsFalse(typeof(PretStoryRuntime).Assembly.GetTypes().Any(t => t.Name == "PretStoryCompiler"));
    }

    [TestMethod]
    public void StoryPreservesFallthroughMenusControlCodesAndTypedDeclarations()
    {
        var story = PretStoryRuntime.Load(TestAssets.Root);
        var compiled = story.Compilation;
        CollectionAssert.AreEqual(new[] { "releaseall", "end" },
            compiled.FindProgram("EventScript_UseSurf")!.Instructions.TakeLast(2).Select(i => i.Opcode).ToArray());
        CollectionAssert.AreEqual(new[] { "lockall", "msgbox", "releaseall", "end" },
            compiled.FindProgram("EventScript_CantUseWaterfall")!.Instructions.Select(i => i.Opcode).ToArray());
        Assert.AreEqual("return", compiled.FindProgram("FourIsland_EventScript_TrySetDayCareManPos")!.Instructions.Last().Opcode);
        Assert.AreEqual(1, compiled.Menus.Count);
        Assert.IsTrue(story.TextBank.Values.Any(text => text.Contains(@"\p", StringComparison.Ordinal)));
        Assert.IsTrue(story.TextBank.Values.Any(text => text.Contains("{PLAYER}", StringComparison.Ordinal)));
        Assert.IsTrue(story.TextBank.Values.Any(text => text.Contains("{PLAY_BGM", StringComparison.Ordinal)));
        Assert.IsTrue(compiled.AssemblyData.Values.SelectMany(rows => rows).Any(row => row.Directive == ".2byte" && row.Operands.Contains("ITEM_ULTRA_BALL")));
        var roundtrip = JsonSerializer.Deserialize<PretStoryRuntime>(JsonSerializer.Serialize(story, PretStoryRuntime.JsonOptions), PretStoryRuntime.JsonOptions)!;
        Assert.AreEqual(story.Compilation.Programs.Count, roundtrip.Compilation.Programs.Count);
        Assert.AreEqual(story.TextBank["sText_GotchaPkmnCaught2"], roundtrip.TextBank["sText_GotchaPkmnCaught2"]);
    }
    [TestMethod]
    public void CompactProgramsPreserveEveryInstructionAndSparseFallthroughSymbol()
    {
        var story = PretStoryRuntime.Load(TestAssets.Root);
        var json = JsonSerializer.Serialize(story, PretStoryRuntime.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var programs = document.RootElement.GetProperty("Compilation").GetProperty("Programs");
        int instructions = 0, overrides = 0, mixedPrograms = 0;
        foreach (var entry in programs.EnumerateObject())
        {
            var runtime = story.Compilation.FindProgram(entry.Name)!;
            var encoded = entry.Value;
            Assert.AreEqual(runtime.Id, encoded.GetProperty("Id").GetString());
            int index = 0, symbols = 0;
            foreach (var instruction in encoded.GetProperty("Instructions").EnumerateArray())
            {
                var original = runtime.Instructions[index++];
                Assert.IsFalse(instruction.TryGetProperty("Source", out _));
                Assert.IsFalse(instruction.TryGetProperty("SourcePath", out _));
                Assert.AreEqual(original.Source.Path, encoded.GetProperty("SourcePath").GetString());
                Assert.AreEqual(original.Source.Line, instruction.GetProperty("Line").GetInt32());
                var symbol = instruction.TryGetProperty("SourceSymbol", out var sourceSymbol)
                    ? sourceSymbol.GetString() : runtime.Id;
                Assert.AreEqual(original.Source.Symbol, symbol);
                if (symbol != runtime.Id) { overrides++; symbols++; }
                instructions++;
            }
            Assert.AreEqual(runtime.Instructions.Length, index);
            if (symbols != 0) mixedPrograms++;
        }
        Assert.AreEqual(20728, instructions);
        Assert.AreEqual(9, overrides);
        Assert.AreEqual(5, mixedPrograms);
        var roundtrip = JsonSerializer.Deserialize<PretStoryRuntime>(json, PretStoryRuntime.JsonOptions)!;
        // Default serialization exposes the complete runtime model, not the compact wire representation.
        Assert.IsTrue(System.Text.Json.Nodes.JsonNode.DeepEquals(
            JsonSerializer.SerializeToNode(story), JsonSerializer.SerializeToNode(roundtrip)));
        Assert.AreEqual("EventScript_UseSurfRelease", roundtrip.Compilation.FindProgram("EventScript_UseSurf")!.Instructions[^2].Source.Symbol);
        Assert.AreEqual("EventScript_UseSurfEnd", roundtrip.Compilation.FindProgram("EventScript_UseSurf")!.Instructions[^1].Source.Symbol);
        Assert.AreEqual("EventScript_EndWaterfall", roundtrip.Compilation.FindProgram("EventScript_CantUseWaterfall")!.Instructions[^1].Source.Symbol);
        Assert.AreEqual("FourIsland_EventScript_EndSetDayCareManPos", roundtrip.Compilation.FindProgram("FourIsland_EventScript_TrySetDayCareManPos")!.Instructions[^1].Source.Symbol);
    }

    [TestMethod]
    public void CompactProgramsRejectLegacyAndIncompleteSourceMetadata()
    {
        const string valid = """{"Id":"event","SourcePath":"scripts.inc","Instructions":[{"Op":0,"Opcode":"end","Args":[],"Line":7}]}""";
        Assert.AreEqual(new StorySource("scripts.inc", "event", 7),
            JsonSerializer.Deserialize<StoryProgram>(valid, PretStoryRuntime.JsonOptions)!.Instructions[0].Source);
        foreach (var invalid in new[]
        {
            valid.Replace(",\"Line\":7", ""),
            valid.Replace("\"Line\":7", "\"Line\":0"),
            valid.Replace("\"SourcePath\":\"scripts.inc\",", ""),
            valid.Replace("\"Line\":7", "\"Source\":{\"Path\":\"scripts.inc\",\"Symbol\":\"event\",\"Line\":7}"),
        })
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<StoryProgram>(invalid, PretStoryRuntime.JsonOptions));
        var root = TestAssets.Output();
        var target = Path.Combine(root, "content/generated/story/runtime.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var story = PretStoryRuntime.Load(TestAssets.Root);
        File.WriteAllText(target, JsonSerializer.Serialize(story with { SchemaVersion = 1 }, PretStoryRuntime.JsonOptions));
        Assert.ThrowsExactly<InvalidDataException>(() => PretStoryRuntime.Load(root));
    }
}

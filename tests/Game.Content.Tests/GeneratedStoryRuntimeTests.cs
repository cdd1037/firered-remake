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
        var relative = "content/generated/data/story.json";
        var target = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(TestAssets.Root, relative), target, true);
        var story = PretStoryRuntime.Load(root);
        using var artifact = JsonDocument.Parse(File.ReadAllBytes(target));
        Assert.IsFalse(artifact.RootElement.TryGetProperty("StarterSpeciesNames", out _));
        Assert.IsFalse(artifact.RootElement.TryGetProperty("MoveNames", out _));
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(story, PretStoryRuntime.JsonOptions));
        Assert.IsFalse(serialized.RootElement.TryGetProperty("StarterSpeciesNames", out _));
        Assert.IsFalse(serialized.RootElement.TryGetProperty("MoveNames", out _));
        var compilation = artifact.RootElement.GetProperty("Compilation");
        static void SameKeys(JsonElement encoded, IEnumerable<string> loaded)
        {
            var expected = encoded.EnumerateObject().Select(entry => entry.Name).ToArray();
            Assert.IsTrue(expected.Length > 0, "The generated collection must not be empty.");
            CollectionAssert.AreEquivalent(expected, loaded.ToArray());
        }
        SameKeys(compilation.GetProperty("Programs"), story.Compilation.Programs.Keys);
        SameKeys(artifact.RootElement.GetProperty("TextBank"), story.TextBank.Keys);
        SameKeys(compilation.GetProperty("MapHeaders"), story.Compilation.MapHeaders.Keys);
        SameKeys(compilation.GetProperty("Movements"), story.Compilation.Movements.Keys);
        SameKeys(compilation.GetProperty("Constants"), story.Compilation.Constants.Keys);
        SameKeys(compilation.GetProperty("Menus"), story.Compilation.Menus.Keys);
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
        // These semantic examples remain required; newly imported source menus are
        // covered by the artifact-key equality above and their service tests.
        CollectionAssert.IsSubsetOf(new[]
        {
            "MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD",
            "MULTICHOICE_TRAINER_TOWER_MODE",
            "MULTICHOICE_YES_NO_INFO",
        }, compiled.Menus.Keys.ToArray());
        var whiteboard = compiled.FindMenu("MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD")!;
        Assert.AreEqual(15, whiteboard.SourceId);
        CollectionAssert.AreEqual(new[] { "SLP", "PSN", "PAR", "BRN", "FRZ", "EXIT" },
            whiteboard.Options.Select(option => option.Text).ToArray());
        var towerMode = compiled.FindMenu("MULTICHOICE_TRAINER_TOWER_MODE")!;
        Assert.AreEqual(64, towerMode.SourceId);
        CollectionAssert.AreEqual(new[] { "SINGLE", "DOUBLE", "KNOCKOUT", "MIXED", "EXIT" },
            towerMode.Options.Select(option => option.Text).ToArray());
        var yesNoInfo = compiled.FindMenu("MULTICHOICE_YES_NO_INFO")!;
        Assert.AreEqual(16, yesNoInfo.SourceId);
        CollectionAssert.AreEqual(new[] { "YES", "NO", "INFO" },
            yesNoInfo.Options.Select(option => option.Text).ToArray());
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
        // Compare coverage to the actual input artifact, not a historical content-size snapshot.
        using var input = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/data/story.json")));
        var sourcePrograms = input.RootElement.GetProperty("Compilation").GetProperty("Programs").EnumerateObject().ToArray();
        Assert.AreEqual(sourcePrograms.Sum(program => program.Value.GetProperty("Instructions").GetArrayLength()), instructions);
        static int SymbolOverrides(JsonProperty program) => program.Value.GetProperty("Instructions").EnumerateArray()
            .Count(instruction => instruction.TryGetProperty("SourceSymbol", out var symbol) && symbol.GetString() != program.Name);
        Assert.AreEqual(sourcePrograms.Sum(SymbolOverrides), overrides);
        Assert.AreEqual(sourcePrograms.Count(program => SymbolOverrides(program) != 0), mixedPrograms);
        Assert.IsTrue(instructions > 0 && overrides > 0 && mixedPrograms > 0);
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
        var target = Path.Combine(root, "content/generated/data/story.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var story = PretStoryRuntime.Load(TestAssets.Root);
        File.WriteAllText(target, JsonSerializer.Serialize(story with { SchemaVersion = 1 }, PretStoryRuntime.JsonOptions));
        Assert.ThrowsExactly<InvalidDataException>(() => PretStoryRuntime.Load(root));
    }
}

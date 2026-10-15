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
        var roundtrip = JsonSerializer.Deserialize<PretStoryRuntime>(JsonSerializer.Serialize(story))!;
        Assert.AreEqual(story.Compilation.Programs.Count, roundtrip.Compilation.Programs.Count);
        Assert.AreEqual(story.TextBank["sText_GotchaPkmnCaught2"], roundtrip.TextBank["sText_GotchaPkmnCaught2"]);
    }
}

using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class StoryMenuContentTests
{
    [TestMethod]
    public void RuntimeLoadsImportedMenusWithoutRawSources()
    {
        using var temp = TestAssets.TemporaryOutput();
        var directory = Path.Combine(temp.Path, "content/generated/data");
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(TestAssets.Root, "content/generated/data/story.json"), Path.Combine(directory, "story.json"));
        var runtime = PretStoryRuntime.Load(temp.Path);
        var menu = runtime.Compilation.FindMenu("MULTICHOICE_YES_NO_INFO")!;
        CollectionAssert.AreEqual(new[] { "YES", "NO", "INFO" }, menu.Options.Select(o => o.Text).ToArray());
        Assert.AreEqual(5, runtime.Compilation.FindMenu("MULTICHOICE_TRAINER_TOWER_MODE")!.Options.Length);
        Assert.AreEqual(6, runtime.Compilation.FindMenu("MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD")!.Options.Length);
        var instruction = runtime.Compilation.FindProgram("TrainerTower_Lobby_EventScript_AskEnterChallenge")!.Instructions.Single(i => i.Opcode == "multichoice");
        Assert.AreSame(menu, StoryMenuRules.Resolve(instruction, runtime.Compilation)!.Definition);
        Assert.IsNull(runtime.Compilation.FindMenu("MULTICHOICE_YES_NO")); // Nurse remains native this batch.
    }
    [TestMethod]
    public void InvalidImportedMenuFailsAtLoad()
    {
        var runtime = PretStoryRuntime.Load(TestAssets.Root);
        const string id = "MULTICHOICE_YES_NO_INFO";
        using var temp = TestAssets.TemporaryOutput();
        var directory = Path.Combine(temp.Path, "content/generated/data"); Directory.CreateDirectory(directory);
        foreach (var invalid in new[] { runtime.Compilation.Menus[id] with { Options = [] }, runtime.Compilation.Menus[id] with { SourceId = 255 } })
        {
            var broken = runtime with { Compilation = runtime.Compilation with { Menus = runtime.Compilation.Menus.SetItem(id, invalid) } };
            File.WriteAllText(Path.Combine(directory, "story.json"), JsonSerializer.Serialize(broken, PretStoryRuntime.JsonOptions));
            Assert.Throws<InvalidDataException>(() => PretStoryRuntime.Load(temp.Path));
        }
    }
}

using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class BadgeTopicListMenuContentTests
{
    [TestMethod]
    public void GeneratedOnlyBadgeMenuIncludesOriginalNineRowsAndSuspendableFourRowLayout()
    {
        using var temp = TestAssets.TemporaryOutput();
        var directory = Path.Combine(temp.Path, "content/generated/data"); Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(TestAssets.Root, "content/generated/data/story.json"), Path.Combine(directory, "story.json"));
        var story = PretStoryRuntime.Load(temp.Path).Compilation;
        var menu = story.FindListMenu(0)!;
        Assert.AreEqual("LISTMENU_BADGES", menu.Id);
        Assert.AreEqual(new StoryListMenuLayout(1, 1, 4, 7, true), menu.ListLayout);
        CollectionAssert.AreEqual(new[] { "BOULDERBADGE", "CASCADEBADGE", "THUNDERBADGE", "RAINBOWBADGE", "SOULBADGE",
            "MARSHBADGE", "VOLCANOBADGE", "EARTHBADGE", "EXIT" }, menu.Options.Select(o => o.Text).ToArray());
        Assert.IsNull(story.FindListMenu(1), "Silph native adapter remains independent");
        Assert.IsTrue(story.FindProgram("CeruleanCity_House1_EventScript_BadgeGuy")!.Instructions
            .Any(i => i.Opcode == "special" && i.Args.SequenceEqual(new[] { "ListMenu" })));
    }

    [TestMethod]
    public void InvalidListLayoutsAndDuplicateIdsFailAtLoad()
    {
        var runtime = PretStoryRuntime.Load(TestAssets.Root);
        var menu = runtime.Compilation.FindListMenu(0)!;
        using var temp = TestAssets.TemporaryOutput();
        var directory = Path.Combine(temp.Path, "content/generated/data"); Directory.CreateDirectory(directory);
        foreach (var layout in new[] { menu.ListLayout! with { VisibleRows = 0 }, menu.ListLayout! with { VisibleRows = 10 },
            menu.ListLayout! with { HeightTiles = 1 }, menu.ListLayout! with { Top = 17 } })
        {
            var broken = runtime with { Compilation = runtime.Compilation with { Menus = runtime.Compilation.Menus.SetItem(menu.Id,
                menu with { ListLayout = layout }) } };
            File.WriteAllText(Path.Combine(directory, "story.json"), JsonSerializer.Serialize(broken, PretStoryRuntime.JsonOptions));
            Assert.Throws<InvalidDataException>(() => PretStoryRuntime.Load(temp.Path));
        }
        var duplicateId = "LISTMENU_DUPLICATE";
        var duplicate = runtime with { Compilation = runtime.Compilation with {
            Menus = runtime.Compilation.Menus.Add(duplicateId, menu with { Id = duplicateId }),
            Constants = runtime.Compilation.Constants.Add(duplicateId, menu.SourceId) } };
        File.WriteAllText(Path.Combine(directory, "story.json"), JsonSerializer.Serialize(duplicate, PretStoryRuntime.JsonOptions));
        Assert.Throws<InvalidDataException>(() => PretStoryRuntime.Load(temp.Path));
    }
}

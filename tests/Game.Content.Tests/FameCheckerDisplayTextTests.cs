using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class FameCheckerDisplayTextTests
{
    private static readonly SourceTextPack English = SourceTextPack.Load(null);
    private static readonly PretFameChecker Original = PretFameChecker.Load(TestAssets.Root);
    private static readonly PretStoryRuntime Story = PretStoryRuntime.Load(TestAssets.Root);
    private static readonly SharedTrainerCatalog Trainers = SharedTrainerCatalog.LoadProject(TestAssets.Root);

    [TestMethod]
    public void AllFameTextUsesExistingAuthoritiesWithoutChangingTheEnglishBaseline()
    {
        var display = Project(English).Catalog;
        CollectionAssert.AreEqual(Original.Catalog.Texts.Select(text => text.EncodedText).ToArray(),
            display.Texts.Select(text => text.EncodedText).ToArray());
        Assert.AreEqual(Original.Catalog.CollectionEvents, display.CollectionEvents);
        foreach (var (before, after) in Original.Catalog.Persons.Zip(display.Persons))
        {
            Assert.AreEqual(before.Id, after.Id);
            Assert.AreEqual(before.TrainerSymbol, after.TrainerSymbol);
            CollectionAssert.AreEqual(before.Facts.Select(fact => fact.IconSymbol).ToArray(),
                after.Facts.Select(fact => fact.IconSymbol).ToArray());
        }
    }

    [TestMethod]
    [DataRow("battle", "gFameCheckerOakName", "OAK", "MENTOR")]
    [DataRow("story", "gFameCheckerFlavorText_ProfOak0", "What does this person do?", "Who is this person here?")]
    [DataRow("trainer-names", "pret:TRAINER_LEADER_BROCK", "BROCK", "STONE")]
    public void SharedSymbolOrTrainerOverrideReachesActualFactAndMenuConsumers(string group, string id, string before, string after)
    {
        using var output = TestAssets.TemporaryOutput();
        string original = group == "story" ? Story.TextBank[id] : before;
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source", group + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = original.Replace(before, after, StringComparison.Ordinal) }));
        var display = Project(SourceTextPack.Load(output.Path)).Catalog;
        var views = FameCheckerRules.CreateView(display,
            Enumerable.Range(0, FameCheckerRules.PersonCount).ToDictionary(i => i, _ => new FameEntry(2, FameCheckerRules.AllFactsMask)), false);
        Assert.AreEqual(FameCheckerRules.PersonCount, views.Length);
        if (group == "story")
        {
            var fact = views[0].Facts[0].Fact!;
            StringAssert.Contains(FameCheckerTextPages.Parse(fact.Text, "{RIVAL}$", "BLUE")[0].Text, after);
            Assert.AreEqual(Original.Catalog.Persons[0].Facts[0].Text.ControlCodes, fact.Text.ControlCodes);
        }
        else
        {
            var person = views[group == "battle" ? 0 : 2].Person;
            Assert.AreEqual(after, FameCheckerTextPages.Plain(person.MenuName, "RED", "BLUE"));
        }
        Assert.AreEqual(Original.Catalog.CollectionEvents, display.CollectionEvents);
    }

    private static PretFameChecker Project(SourceTextPack pack) => Original.WithDisplayText(
        PretBattleText.Load(TestAssets.Root, pack), pack.Apply("story", Story.TextBank, preserveControls: true),
        SourceTrainerDisplayNames.LoadProject(TestAssets.Root, Trainers, pack));
}

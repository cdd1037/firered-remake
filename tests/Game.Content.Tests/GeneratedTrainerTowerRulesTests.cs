using System.Text.Json;
using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedTrainerTowerRulesTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NamesAndSpeechProjectByTemplateAndWordIdentityWithoutChangingSourceRules(bool translated)
    {
        var original = PretTrainerTowerRules.Load(TestAssets.Root);
        string originalJson = JsonSerializer.Serialize(original.Data);
        var floor = original.Template(original.Course(TrainerTowerChallengeKind.Single).Floors[0].TemplateSymbol);
        var trainer = floor.Trainers[0];
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        if (translated)
        {
            Write("trainer-tower-names", floor.Symbol + "/0", "DISPLAY");
            Write("move-names", "pret:MOVE_EARTHQUAKE", "GROUND SHAKE");
            var word = trainer.SpeechBefore.First(word => word.Value != SourceEasyChatRules.BlankWord && word.Value / 512 is >= 1 and <= 16);
            Write("easy-chat-words", word.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "GREETING");
        }
        var pack = SourceTextPack.Load(output.Path);
        var words = PretMailComposer.Load(TestAssets.Root, pack).WithDisplayNames(
            PretPokedexMenu.Load(TestAssets.Root, pack), PretMetronomeMoveCatalog.Load(TestAssets.Root, pack));
        var display = PretTrainerTowerRules.WithDisplayText(original, words, pack);
        Assert.AreEqual(originalJson, JsonSerializer.Serialize(original.Data));
        Assert.AreEqual(original.Courses, display.Courses);
        Assert.AreEqual(translated ? "DISPLAY" : trainer.Name,
            display.DescribeBattle(TrainerTowerChallengeKind.Single, 0, 0, 50).Members[0].Trainer.Name);
        foreach (var (beforeFloor, afterFloor) in original.Templates.Zip(display.Templates))
        {
            Assert.AreEqual(beforeFloor, afterFloor with { Trainers = beforeFloor.Trainers });
            foreach (var (beforeTrainer, afterTrainer) in beforeFloor.Trainers.Zip(afterFloor.Trainers))
            {
                Assert.AreEqual(beforeTrainer, afterTrainer with { Name = beforeTrainer.Name,
                    SpeechBefore = beforeTrainer.SpeechBefore, SpeechWin = beforeTrainer.SpeechWin,
                    SpeechLose = beforeTrainer.SpeechLose, SpeechAfter = beforeTrainer.SpeechAfter });
                foreach (var (before, after) in Speech(beforeTrainer).Zip(Speech(afterTrainer)))
                {
                    Assert.AreEqual(before.Value, after.Value); Assert.AreEqual(before.Symbol, after.Symbol);
                    Assert.AreEqual(SourceEasyChatRules.WordText(words, (ushort)before.Value), after.Text);
                    if (!translated) Assert.AreEqual(before.Text, after.Text);
                }
            }
        }
        void Write(string group, string id, string value) => File.WriteAllText(
            Path.Combine(output.Path, "source", group + ".json"), JsonSerializer.Serialize(new Dictionary<string, string> { [id] = value }));
    }

    [TestMethod]
    [DataRow("missing/0", "NAME")]
    [DataRow("sTrainerTowerFloor_Single_1/0", "")]
    [DataRow("sTrainerTowerFloor_Single_1/0", "{PLAYER}")]
    public void TowerNamesRejectUnknownTemplateAndInvalidDisplayText(string id, string text)
    {
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source/trainer-tower-names.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = text }));
        Assert.ThrowsExactly<InvalidDataException>(() => PretTrainerTowerRules.WithDisplayText(
            PretTrainerTowerRules.Load(TestAssets.Root), PretMailComposer.Load(TestAssets.Root, SourceTextPack.Load(null)).Catalog,
            SourceTextPack.Load(output.Path)));
    }

    private static IEnumerable<TrainerTowerWord> Speech(TrainerTowerTrainerTemplate trainer) =>
        trainer.SpeechBefore.Concat(trainer.SpeechWin).Concat(trainer.SpeechLose).Concat(trainer.SpeechAfter);

    [TestMethod]
    public void TowerCoursesAndPrizesLoadWithoutImporterMetadata()
    {
        var tower = PretTrainerTowerRules.Load(TestAssets.Root);
        var items = PretTrainerTowerRules.LoadItemsDocument(TestAssets.Root);
        Assert.IsFalse(tower.Courses.IsEmpty);
        Assert.IsFalse(tower.Templates.IsEmpty);
        Assert.AreEqual(tower.Courses.Length, items.Prizes.Length);
        foreach (var prize in items.Prizes)
        {
            var course = tower.Course(prize.Course);
            Assert.AreEqual("pret:" + course.PrizeItem, prize.Item.Value);
            Assert.AreEqual(course.SourcePrizeItemId, prize.SourceItemId);
            Assert.AreEqual(1, prize.Quantity);
        }
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/regions/trainer-tower/catalog.json")));
        Assert.IsFalse(document.RootElement.TryGetProperty("metadata", out _));
    }
}

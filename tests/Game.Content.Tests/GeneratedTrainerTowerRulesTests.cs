using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedTrainerTowerRulesTests
{
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

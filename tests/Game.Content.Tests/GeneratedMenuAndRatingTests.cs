using Game.Content;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedMenuAndRatingTests
{
    [TestMethod]
    public void MenusRatingsAndCenterLoadWithGeneratedResourcesOnly()
    {
        var root = TestAssets.Output();
        try
        {
            string[] files =
            [
                "ui/main-menu/main-menu.json",
                "data/services/oak-rating/oak-rating.json",
                "data/constants.json",
                "data/story.json"
            ];
            var generated = Path.Combine(TestAssets.Root, "content/generated");
            var packages = new[] { "maps/regions/viridian-center", "maps/regions/opening" };
            var required = files.Concat(CurrentWorldTestFixture.RelativeFiles()).Concat(packages.SelectMany(package =>
                Directory.EnumerateFiles(Path.Combine(generated, package), "*", SearchOption.AllDirectories)
                    .Select(file => Path.GetRelativePath(generated, file)))).Distinct();
            foreach (var relative in required)
            {
                var target = Path.Combine(root, "content/generated", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(generated, relative), target);
            }

            var menu = PretMainMenuSource.Load(root);
            var window = menu.Windows[PretMainMenuSource.ContinueWindow];
            Assert.AreEqual((24, 8, 192, 80), (window.PixelX, window.PixelY, window.PixelWidth, window.PixelHeight));
            Assert.AreEqual((240, 160), (menu.Rules.LogicalWidth, menu.Rules.LogicalHeight));
            Assert.IsTrue(menu.Rules.EmptySaveEntersNewGameDirectly);
            Assert.IsFalse(menu.Rules.NavigationWraps);

            var rating = PretOakRating.Load(root);
            Assert.AreEqual("PokedexRating_Text_LessThan10", rating.SelectKantoRating(0, false).TextSymbol);
            Assert.IsTrue(rating.SelectKantoRating(150, false).KantoComplete);
            Assert.IsFalse(rating.SelectKantoRating(150, true).KantoComplete);
            Assert.IsTrue(rating.SelectKantoRating(151, true).KantoComplete);
            var regional = rating.EvaluateNationalNumbers([1, 2, 151, 152], [1, 151], new(true));
            Assert.AreEqual(new OakRatingCounts(3, 2), regional.Kanto);
            Assert.IsNull(regional.National);
            var national = rating.EvaluateNationalNumbers(Enumerable.Range(1, 386), Enumerable.Range(1, 386), new(true, true, 185, 25176));
            Assert.IsTrue(national.NationalComplete);
            Assert.IsTrue(national.ShouldSetOakSawDexCompletion);

            var center = PretViridianCenter.Load(PretCurrentWorld.Load(root));
            Assert.AreEqual(new MapId(PretViridianCenter.StableMapId), center.Map.Id);
            Assert.AreEqual((15, 10), (center.Map.Width, center.Map.Height));
            Assert.AreEqual(2, center.Constants["HEAL_LOCATION_VIRIDIAN_CITY"]);
            Assert.AreEqual(1, center.Constants["LOCALID_VIRIDIAN_NURSE"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(center.SourceUiTexts["gText_PlayerScurriedToCenter"]));
            Assert.IsTrue(PretCurrentWorld.Load(root).Maps.ContainsKey(new MapId("map.pallet-town")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
        }
        finally { Directory.Delete(root, true); }
    }
}

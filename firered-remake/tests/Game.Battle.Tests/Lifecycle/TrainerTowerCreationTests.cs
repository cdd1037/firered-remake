using FireRed.Tests;
using Game.Content;
using Game.Domain.Randomness;
using Game.Domain.Trainers;

namespace FireRed.Tests.Battle.Lifecycle;

[TestClass, TestCategory("Game")]
public sealed class TrainerTowerCreationTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(50)]
    [DataRow(100)]
    public void EverySelectedTowerMemberPreservesFixedIdentityWithoutRandomDraws(int level)
    {
        var mechanics = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var game = PretGamePokemonCatalog.LoadContent(
            File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)), canonical, mechanics);
        var tower = PretTrainerTowerRules.Load(TestAssets.Root);
        var rng = new RngState(42);
        var count = 0;
        foreach (var course in tower.Courses)
        foreach (var floor in course.Floors)
        for (var trainer = 0; trainer < (floor.Format == TrainerTowerBattleFormat.Knockout ? 3 : 1); trainer++)
        {
            var descriptor = tower.DescribeBattle(course.Kind, floor.Index, trainer, level);
            var made = tower.CreateBattle(game.TrainerTowerOpponents, course.Kind, floor.Index, trainer, level, rng);
            Assert.AreEqual(rng, made.Rng);
            Assert.IsTrue(made.Trace.IsEmpty);
            Assert.AreEqual(descriptor.Members.Length, made.Party.Length);
            for (var index = 0; index < made.Party.Length; index++)
            {
                var expected = descriptor.Members[index].Template;
                var actual = made.Party[index].Pokemon;
                Assert.AreEqual(expected.Species, actual.Species);
                Assert.AreEqual(expected.Personality, actual.Personality);
                Assert.AreEqual(expected.OriginalTrainerId, actual.OriginalTrainerId);
                Assert.AreEqual(expected.AbilitySlot, actual.AbilitySlot);
                Assert.AreEqual(expected.AbilityId, actual.AbilityId);
                Assert.AreEqual(expected.Ivs, actual.Ivs);
                Assert.AreEqual(expected.Evs, actual.Evs);
                Assert.AreEqual(level, actual.Level);
                CollectionAssert.AreEqual(expected.Moves.ToArray(), actual.Moves.Select(m => m.Move).ToArray());
                count++;
            }
        }
        Assert.AreEqual(74, count);
    }
}

using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class PokemonAdmissionParityTests
{
    [TestMethod]
    public void CurrentCompositionUsesExactlyTheExplicitAdmissionCatalog()
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonical, TestAssets.Root);
        var trades = PretNpcTrades.Load(TestAssets.Root, mechanics.Catalog).Trades;
        var expected = PretGamePokemonCatalog.Load(TestAssets.Root, canonical, mechanics, trades);
        var campaign = PretCampaignCatalog.LoadCurrent(TestAssets.Root);
        Check(expected.Catalog, campaign.PokemonContent!.Catalog);
        Check(expected.Opponents.Catalog, campaign.VsSeekerOpponents!.Catalog);
        Check(expected.TrainerTowerOpponents.Catalog, campaign.TrainerTowerOpponents.Catalog);
        Assert.IsTrue(campaign.BreedingMetadata!.Species.Keys.ToHashSet().SetEquals(expected.Catalog.Species.Keys));
        Assert.AreEqual(9, campaign.NpcTrades.Definitions.Count);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, campaign.PokemonContent.StarterChoices.Select(x => x.Index).ToArray());
        CollectionAssert.AreEquivalent(new[] { 1, 4, 7 }, campaign.PokemonContent.StarterChoices.Select(x => x.SourceSpeciesId).ToArray());
        Assert.IsTrue(campaign.PokemonContent.StarterChoices.All(x => x.SourceSpeciesId != x.RivalSourceSpeciesId &&
            !string.IsNullOrWhiteSpace(x.BallId) && !string.IsNullOrWhiteSpace(x.BallHideFlag) &&
            !string.IsNullOrWhiteSpace(x.RivalBallId) && !string.IsNullOrWhiteSpace(x.RivalBallHideFlag)));
        Assert.IsTrue(campaign.PokemonContent.AllowSleepStatus);
        Assert.IsTrue(campaign.PokemonContent.AllowFreezeStatus);
        Assert.IsTrue(campaign.PokemonContent.AllowToxicStatus);

        static void Check(PokemonCatalog expected, PokemonCatalog actual)
        {
            Assert.IsTrue(expected.Species.Keys.ToHashSet().SetEquals(actual.Species.Keys));
            foreach (var (id, definition) in expected.Species)
                Assert.AreEqual(JsonSerializer.Serialize(definition), JsonSerializer.Serialize(actual.Species[id]), id.Value);
            Assert.IsTrue(expected.Moves.Keys.ToHashSet().SetEquals(actual.Moves.Keys));
            foreach (var (id, definition) in expected.Moves)
                Assert.AreEqual(JsonSerializer.Serialize(definition), JsonSerializer.Serialize(actual.Moves[id]), id.Value);
        }
    }
}

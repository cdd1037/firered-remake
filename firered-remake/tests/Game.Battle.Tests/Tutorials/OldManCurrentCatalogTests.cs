using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Tutorials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Battle.Tutorials;

[TestClass, TestCategory("Game")]
public sealed class OldManCurrentCatalogTests
{
    [TestMethod]
    public void CurrentOwnedCatalogPreservesForcedCatchRandomDrawsAndPlayer()
    {
        var owned = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!.Pokemon;
        var pokemon = SourcePokemonFactory.Create(owned.Mechanics,
            new(new("pret:SPECIES_CHARMANDER"), 5, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var player = new PartyPokemon(1, pokemon, "CHARMANDER", "RED", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        var result = OldManTutorialRules.Start(owned, player, new(0));
        // Independent Gen III LCG sequence: two male attempts, two IV draws, four AI
        // draws, held item, turn number, then rejected slot 2 and accepted slot 0.
        Assert.AreEqual(new RngState(2440580858, 18), result.Rng);
        Assert.AreEqual(2, result.GenerationAttempts);
        Assert.AreEqual(3805056578u, result.Weedle.OriginalTrainerId);
        Assert.AreEqual(1742450629u, result.Weedle.Personality);
        CollectionAssert.AreEqual(new[] { 86, 95, 94, 87 }, result.SimulatedAiRng.ToArray());
        CollectionAssert.AreEqual(new[] { 2, 0 }, result.EnemyMoveSlotAttempts.ToArray());
        Assert.AreEqual(20, result.HeldItemRoll);
        Assert.AreEqual((ushort)52135, result.RandomTurnNumber);
        Assert.AreEqual(pokemon, player.Pokemon);
        Assert.AreEqual(result.Rng, OldManTutorialRules.Finish(result).Rng);
    }
}

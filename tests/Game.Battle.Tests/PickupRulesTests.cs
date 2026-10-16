using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Battle;

[TestClass, TestCategory("Game")]
public sealed class PickupRulesTests
{
    private static GamePokemonCatalog Catalog =>
        TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!.Pokemon;

    private static PartyPokemon Member(long id, string species = "MEOWTH", uint personality = 0)
    {
        var pokemon = SourcePokemonFactory.Create(Catalog.Mechanics,
            new(new("pret:SPECIES_" + species), 20, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        return new(id, pokemon, species, "RED", 0, 2, "MAPSEC_ROUTE_5", 20, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }

    [TestMethod]
    public void PickupUsesPartyOrderAndExactRngIncludingFaintedMembers()
    {
        var first = Member(1);
        first = first with { Pokemon = first.Pokemon with { CurrentHp = 0 } };
        ImmutableArray<PartyPokemon> party = [first, Member(2) with { HeldItem = "ITEM_ORAN_BERRY" }, Member(3, "PIDGEY"), Member(4)];
        var result = VermilionPickupRules.Apply(Catalog, party, new(0), WildBattleOutcome.Won);
        Assert.AreEqual(new RngState(1383151765, 3), result.Rng);
        CollectionAssert.AreEqual(new[] { "vermilion.pickup.party-0.chance", "vermilion.pickup.party-0.item", "vermilion.pickup.party-3.chance" }, result.Trace.Select(t => t.Purpose).ToArray());
        Assert.AreEqual(new VermilionPickupAward(0, 1, "ITEM_PERSIM_BERRY"), result.Awards.Single());
        Assert.AreEqual(first with { HeldItem = "ITEM_PERSIM_BERRY" }, result.Party[0]);
        CollectionAssert.AreEqual(party.Skip(1).ToArray(), result.Party.Skip(1).ToArray());
        Assert.AreEqual("ITEM_NONE", party[0].HeldItem, "Input state remains unchanged.");
    }

    [TestMethod]
    public void NonVictoryDoesNotAwardOrConsumeRngAndOngoingIsRejected()
    {
        ImmutableArray<PartyPokemon> party = [Member(1)];
        var rng = new RngState(0, 7);
        foreach (var outcome in new[] { WildBattleOutcome.Lost, WildBattleOutcome.Draw, WildBattleOutcome.Escaped, WildBattleOutcome.Caught })
        {
            var result = VermilionPickupRules.Apply(Catalog, party, rng, outcome);
            Assert.AreEqual(party, result.Party);
            Assert.AreEqual(rng, result.Rng);
            Assert.IsTrue(result.Trace.IsEmpty && result.Awards.IsEmpty);
        }
        Assert.Throws<ArgumentException>(() => VermilionPickupRules.Apply(Catalog, party, rng, WildBattleOutcome.Ongoing));
        Assert.Throws<ArgumentException>(() => VermilionPickupRules.Apply(Catalog, [party[0], party[0]], rng, WildBattleOutcome.Won));
        Assert.Throws<NotSupportedException>(() => VermilionPickupRules.Apply(Catalog, party, rng with { AlgorithmId = "unknown" }, WildBattleOutcome.Won));
    }

    [TestMethod]
    public void EggWithPickupAbilityDoesNotConsumeRng()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!;
        var parents = new FourIslandDayCareState(new(Member(1), 7), new(Member(2, personality: 255), 19), 123, 51);
        var egg = SourceEggRules.Receive(content, Catalog.Catalog.BreedingData!, parents, 3,
            new(42, "RED", 0), "MAPSEC_FOUR_ISLAND", 4, new(0)).Egg;
        Assert.IsTrue(egg.IsEgg);
        Assert.AreEqual("ABILITY_PICKUP", egg.Pokemon.AbilityId);
        var result = VermilionPickupRules.Apply(Catalog, [egg], new(0), WildBattleOutcome.Won);
        Assert.AreEqual(new RngState(0), result.Rng);
        Assert.AreEqual(egg, result.Party.Single());
        Assert.IsTrue(result.Trace.IsEmpty && result.Awards.IsEmpty);
    }

    [TestMethod]
    public void ItemRollRetainsEveryOriginalProbabilityBucket()
    {
        string[] items = ["ITEM_ORAN_BERRY", "ITEM_CHERI_BERRY", "ITEM_CHESTO_BERRY", "ITEM_PECHA_BERRY", "ITEM_RAWST_BERRY", "ITEM_ASPEAR_BERRY", "ITEM_PERSIM_BERRY", "ITEM_TM10", "ITEM_PP_UP", "ITEM_RARE_CANDY", "ITEM_NUGGET", "ITEM_SPELON_BERRY", "ITEM_PAMTRE_BERRY", "ITEM_WATMEL_BERRY", "ITEM_DURIN_BERRY", "ITEM_BELUE_BERRY"];
        int[] counts = [15, 10, 10, 10, 10, 10, 10, 5, 5, 5, 5, 1, 1, 1, 1, 1];
        var expected = items.SelectMany((item, index) => Enumerable.Repeat(item, counts[index])).ToArray();
        CollectionAssert.AreEqual(expected, Enumerable.Range(0, 100).Select(VermilionPickupRules.ItemForRoll).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => VermilionPickupRules.ItemForRoll(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => VermilionPickupRules.ItemForRoll(100));
    }
}

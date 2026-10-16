using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class DaisyGroomingAndKantoPokedexTests
{
    [TestMethod]
    public void MassageUsesThreeInEveryBandAndCapsAt255()
    {
        foreach (var before in new[] { 0, 98, 99, 100, 198, 199, 200, 252, 253, 254, 255 })
        {
            var pokemon = Mon(before);
            var after = DaisyGroomingRules.Apply(pokemon, "MAPSEC_PALLET_TOWN");
            Assert.AreEqual(Math.Min(255, before + 3), after.Pokemon.Friendship);
            Assert.AreEqual(pokemon, after with { Pokemon = after.Pokemon with { Friendship = before } });
        }
    }

    [TestMethod]
    public void MassageScalesBaseBeforeAddingBallAndLocationBonuses()
    {
        foreach (bool bell in new[] { false, true })
        foreach (bool luxury in new[] { false, true })
        foreach (bool sameLocation in new[] { false, true })
        {
            var pokemon = Mon(100) with
            {
                HeldItem = bell ? "ITEM_SOOTHE_BELL" : "ITEM_NONE",
                Ball = luxury ? "ITEM_LUXURY_BALL" : "ITEM_POKE_BALL",
                MetLocation = sameLocation ? "MAPSEC_PALLET_TOWN" : "MAPSEC_ROUTE_1"
            };
            Assert.AreEqual(100 + (bell ? 4 : 3) + (luxury ? 1 : 0) + (sameLocation ? 1 : 0),
                DaisyGroomingRules.Apply(pokemon, "MAPSEC_PALLET_TOWN").Pokemon.Friendship);
        }
        var nearCap = Mon(253) with { HeldItem = "ITEM_SOOTHE_BELL", Ball = "ITEM_LUXURY_BALL", MetLocation = "MAPSEC_PALLET_TOWN" };
        Assert.AreEqual(255, DaisyGroomingRules.Apply(nearCap, "MAPSEC_PALLET_TOWN").Pokemon.Friendship);
    }

    [TestMethod]
    public void MassageLeavesEggCyclesUntouchedButIncludesFaintedPokemon()
    {
        var egg = Mon(10) with { IsEgg = true };
        Assert.AreSame(egg, DaisyGroomingRules.Apply(egg, "MAPSEC_PALLET_TOWN"));
        var fainted = Mon(70);
        fainted = fainted with { Pokemon = fainted.Pokemon with { CurrentHp = 0 } };
        var groomed = DaisyGroomingRules.Apply(fainted, "MAPSEC_PALLET_TOWN");
        Assert.AreEqual(73, groomed.Pokemon.Friendship);
        Assert.AreEqual(0, groomed.Pokemon.CurrentHp);
    }

    [TestMethod]
    public void KantoCompletionRequiresEveryCaughtEntryExceptMew()
    {
        int[] all = Enumerable.Range(1, 150).ToArray();
        Assert.IsTrue(KantoPokedexRules.HasAllKantoMons(all));
        Assert.IsTrue(KantoPokedexRules.HasAllKantoMons(all.Concat([151, 152, 386])));
        Assert.IsFalse(KantoPokedexRules.HasAllKantoMons([]));
        Assert.IsFalse(KantoPokedexRules.HasAllKantoMons(Enumerable.Repeat(1, 150)));
        foreach (int missing in all)
            Assert.IsFalse(KantoPokedexRules.HasAllKantoMons(all.Where(n => n != missing).Concat([0, 151, 152, 386])), $"Missing caught flag {missing}");
    }

    private static PartyPokemon Mon(int friendship) => new(1,
        new(new("pret:SPECIES_BULBASAUR"), 1, 5, 135, 1, 0, Nature.Hardy,
            SixStats.Zero, SixStats.Zero, new(20, 10, 10, 10, 10, 10), 20,
            PokemonGender.Male, 0, "ABILITY_OVERGROW", friendship, []),
        "BULBASAUR", "RED", 0, 2, "MAPSEC_ROUTE_1", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
}

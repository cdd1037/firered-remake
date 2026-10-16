using Game.Application;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.FieldItems;

[TestClass, TestCategory("Game")]
public sealed class FieldPoisonRulesTests
{
    private static PartyPokemon Member(uint status, int hp = 2, int friendship = 205)
    {
        var catalog = CurrentCampaignFixture.Catalog.GamePokemon.Catalog;
        var made = PokemonFactory.Create(catalog, new(new("pret:SPECIES_BULBASAUR"), 5, 1,
            IvPolicy.Fixed(10), PersonalityPolicy.Fixed(7)), new(1)).Pokemon;
        return new(1, made with { CurrentHp = hp, Friendship = friendship }, "BULBASAUR", "RED", 0,
            2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE") { Status = status };
    }

    [TestMethod]
    [DataRow(8u)]
    [DataRow(128u)]
    public void BothPoisonFormsKeepFiveStepCadenceAndOneHpDamage(uint status)
    {
        var member = Member(status);
        var waiting = CampaignFieldPoisonRules.Walk(3, [member]);
        Assert.AreEqual(FieldPoisonEffect.None, waiting.Effect);
        Assert.AreEqual(2, waiting.Party[0].Pokemon.CurrentHp);
        var tick = CampaignFieldPoisonRules.Walk(waiting.Counter, waiting.Party);
        Assert.AreEqual(0, tick.Counter);
        Assert.AreEqual(FieldPoisonEffect.Poisoned, tick.Effect);
        Assert.AreEqual(1, tick.Party[0].Pokemon.CurrentHp);
        var fainted = CampaignFieldPoisonRules.Walk(4, tick.Party);
        Assert.AreEqual(FieldPoisonEffect.Fainted, fainted.Effect);
        Assert.IsFalse(CampaignFieldPoisonRules.HasUsablePokemon(fainted.Party));
        var resolved = CampaignFieldPoisonRules.ResolveFaint(fainted.Party[0]);
        Assert.AreEqual(0u, resolved.Status);
        Assert.AreEqual(195, resolved.Pokemon.Friendship);
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    [DataRow(5u)]
    [DataRow(16u)]
    [DataRow(32u)]
    [DataRow(64u)]
    public void OtherPersistentStatusesDoNotCauseFieldDamage(uint status)
    {
        var member = Member(status);
        var tick = CampaignFieldPoisonRules.Walk(4, [member]);
        Assert.AreEqual(FieldPoisonEffect.None, tick.Effect);
        Assert.AreEqual(member, tick.Party[0]);
        Assert.IsTrue(CampaignFieldPoisonRules.HasUsablePokemon(tick.Party));
    }

    [TestMethod]
    public void InvalidStatusAndCounterStillReject()
    {
        foreach (uint invalid in new uint[] { 6, 7, 256 })
            Assert.ThrowsExactly<NotSupportedException>(() => CampaignFieldPoisonRules.Walk(4, [Member(invalid)]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignFieldPoisonRules.Walk(5, [Member(8)]));
    }
}

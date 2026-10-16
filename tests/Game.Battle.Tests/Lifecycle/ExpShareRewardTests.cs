using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Battle.Lifecycle;

[TestClass, TestCategory("Game")]
public sealed class ExpShareRewardTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void HeldShareRewardsLivingNonparticipantWithoutChangingTheBattle(bool holderParticipated, bool holderFainted)
    {
        var mechanics = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        PokemonInstance Mon(string species) => SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_" + species), 20, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var participant = Mon("PIDGEY");
        var holder = Mon("RATTATA");
        if (holderFainted) holder = holder with { CurrentHp = 0 };
        var defeated = Mon("SPEAROW") with { CurrentHp = 0 };
        var party = new SourceBattleParty(BattleSide.Player, [new(participant, "ITEM_NONE"), new(holder, "ITEM_EXP_SHARE")]);
        var metadata = Enumerable.Range(0, 2).Select(index => new SourceRewardMetadata(new(BattleSide.Player, index),
            "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray();
        ImmutableArray<PartyMemberKey> participants = holderParticipated ? [new(BattleSide.Player, 0), new(BattleSide.Player, 1)] : [new(BattleSide.Player, 0)];
        var result = SourceVictoryRewards.Calculate(mechanics, party, metadata,
            new(new(new(1), new(BattleSide.Opponent, 0), 1), new(defeated), participants),
            new(SourceRewardBattleKind.OrdinaryWild, 42, "RED", "MAPSEC_ROUTE_1"));
        int baseExperience = mechanics.Catalog.Species[defeated.Species].ExperienceYield * defeated.Level / 7;
        int half = baseExperience / 2;
        Assert.AreEqual(holderFainted ? 1 : 2, result.Members.Length);
        Assert.AreEqual(holderFainted ? baseExperience : holderParticipated ? half / 2 : half,
            result.Members.Single(member => member.Key.Index == 0).Reward.ExperienceGained);
        if (!holderFainted)
        {
            var reward = result.Members.Single(member => member.Key.Index == 1).Reward;
            Assert.AreEqual(half + (holderParticipated ? half / 2 : 0), reward.ExperienceGained);
            Assert.AreEqual(mechanics.Catalog.Species[defeated.Species].EvYield, reward.EvsGained);
        }
        Assert.AreSame(holder, party.Members[1].Pokemon, "The calculation does not mutate the persistent party.");
    }
}

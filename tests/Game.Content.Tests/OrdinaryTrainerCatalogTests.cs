using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class OrdinaryTrainerCatalogTests
{
    private static OrdinaryTrainerCatalog Gym() => PretCeladonGymTrainers.Load(
        TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root)), TestAssets.Root);

    [TestMethod]
    public void PartyIdentityDoesNotCollapseDistinctSourceOccurrences()
    {
        var gym = Gym();
        var battle = gym.Battles[0];
        var other = battle with { Source = battle.Source with { StartLine = battle.Source.StartLine + 1 } };
        var catalog = new OrdinaryTrainerCatalog(gym.Trainers, gym.Battles.Add(other), gym.TracePrefix);
        Assert.AreEqual(gym.Battles.Length + 1, catalog.Battles.Length);
        Assert.AreSame(gym.Trainers, catalog.Trainers);
        Assert.AreEqual(2, catalog.Battles.Count(row => row.Trainer == battle.Trainer));
        // Runtime composition must still resolve one unambiguous primary actor occurrence.
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers, gym.Battles.Add(battle), gym.TracePrefix));
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers, gym.Battles.RemoveAt(0), gym.TracePrefix));
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers, [], gym.TracePrefix));
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers, default, gym.TracePrefix));
    }

    [TestMethod]
    public void SharedOrdinaryCatalogRejectsSpecialModesAndBrokenReferences()
    {
        var gym = Gym();
        var battle = gym.Battles[0];
        CeruleanTrainerBattleDefinition[] invalid = [
            battle with { Trainer = new("pret:TRAINER_MISSING") },
            battle with { Mode = 4 }, battle with { Mode = 9 },
            battle with { RivalFlags = 3 }, battle with { BattleTypeFlags = 24 },
            battle with { VictoryText = "UnexpectedVictory" },
            battle with { Callback = "UnexpectedCallback" },
            battle with { EntryScript = "UnexpectedEntry" }
        ];
        foreach (var changed in invalid)
            Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers, gym.Battles.SetItem(0, changed), gym.TracePrefix));
        var trainer = gym.Trainers[battle.Trainer];
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers.SetItem(trainer.Id,
            trainer with { DoubleBattle = true }), gym.Battles, gym.TracePrefix));
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(gym.Trainers, gym.Battles, ""));
        Assert.ThrowsExactly<ArgumentException>(() => new OrdinaryTrainerCatalog(
            ImmutableDictionary<TrainerId, TrainerDefinition>.Empty, gym.Battles, gym.TracePrefix));
    }
}

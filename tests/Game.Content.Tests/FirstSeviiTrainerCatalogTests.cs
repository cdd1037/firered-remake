using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class FirstSeviiTrainerCatalogTests
{
    [TestMethod]
    public void MembershipAndOccurrenceKeysReplaceHistoricalRosterGates()
    {
        var catalog = PretFirstSeviiRules.LoadTrainers(TestAssets.Root);
        var amara = new TrainerId("pret:TRAINER_SWIMMER_FEMALE_AMARA");
        var battle = catalog.BattlesBySource.Single(pair => pair.Value.Trainer == amara);
        var subset = new FirstSeviiTrainerCatalog(
            catalog.Trainers.RemoveRange(catalog.Trainers.Keys.Where(id => id != amara)),
            catalog.BattlesBySource.RemoveRange(catalog.BattlesBySource.Keys.Where(key => key != battle.Key)));
        FirstSeviiTrainerPartyFactory.ValidateCatalog(subset);
        Assert.ThrowsExactly<ArgumentException>(() => FirstSeviiTrainerPartyFactory.ValidateCatalog(
            subset with { Trainers = subset.Trainers.Remove(amara) }));
        Assert.ThrowsExactly<ArgumentException>(() => FirstSeviiTrainerPartyFactory.ValidateCatalog(
            subset with { BattlesBySource = subset.BattlesBySource.Remove(battle.Key)
                .Add(new(battle.Key.ScriptEntry, battle.Key.SourceLine + 1), battle.Value) }));
        Assert.ThrowsExactly<NotSupportedException>(() => subset.Battle(battle.Key.ScriptEntry, battle.Key.SourceLine + 1));
    }
}

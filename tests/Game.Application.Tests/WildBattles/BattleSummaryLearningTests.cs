using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class BattleSummaryLearningTests : WildBattleFixture
{
    [TestMethod]
    public void BattleSummaryReadsKeepAuthorityRngTurnAndOwnedMoveSlotsUnchanged()
    {
        var lead = Owned("PIDGEY", 20, 101, "GROWL", "TACKLE");
        var reserve = Owned("RATTATA", 20, 102, "TACKLE");
        var session = Controlled([lead, reserve], Owned("PIDGEY", 4, 201, "GROWL"));
        var state = session.State; var revision = session.Revision;
        string before = CanonicalJson.Hash(state);
        var descriptions = SummaryPresentation.Load(TestAssets.Root);
        for (int index = 0; index < state.Party.Length; index++)
        {
            var member = state.WildBattle!.Frame.Party(BattleSide.Player).Members[index];
            Assert.AreEqual(state.Party[index].Pokemon, member.Pokemon);
            for (int page = 0; page < 3; page++)
                _ = PokemonSummaryRules.SeekPartyIndex(state.Party, index, (PokemonSummaryPage)page, 1);
            foreach (var move in member.Pokemon.Moves)
            {
                Assert.IsNotNull(catalog.Moves[move.Move]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(descriptions.Moves[move.Move.Value.Replace("pret:", "")]));
            }
        }
        var attempt = session.Execute("summary-cannot-reorder-battle", revision,
            new("party_summary_swap", PartyIndex: 0, MoveSlot: 0, Choice: 1) { TargetInstanceId = lead.InstanceId });
        Assert.AreEqual(CommandStatus.Rejected, attempt.Status);
        Assert.AreSame(state, session.State); Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
    }

    [TestMethod]
    public void LearningCandidateCancelAndStaleCommandsPreserveRewardAndKnownMoves()
    {
        var lead = Owned("BULBASAUR", 19, 101, "TACKLE", "GROWL", "LEECH_SEED", "VINE_WHIP");
        var species = catalog.Species[lead.Pokemon.Species];
        var candidate = species.LevelUpMoves.First(m => m.Level > 19 && m.Level < 40 && !lead.Pokemon.Moves.Any(x => x.Move == m.Move));
        lead = Owned("BULBASAUR", candidate.Level - 1, 101, "TACKLE", "GROWL", "LEECH_SEED", "VINE_WHIP");
        lead = lead with { Pokemon = lead.Pokemon with { Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, candidate.Level) - 1 } };
        var enemy = Owned("PIDGEY", 2, 201, "GROWL");
        enemy = enemy with { Pokemon = enemy.Pokemon with { CurrentHp = 1 } };
        var session = Controlled([lead], enemy);
        Send(session, new("battle_move", MoveSlot: 0)); Drain(session, stopLearning: true);
        var state = session.State; long revision = session.Revision;
        var known = state.Party[0].Pokemon.Moves; uint xp = state.Party[0].Pokemon.Experience;
        Assert.AreEqual(candidate.Move, state.WildBattle!.PendingMoves[0].Move);
        foreach (var (rev, slot) in new[] { (revision, 4), (revision - 1, 0) })
        {
            var rejected = session.Execute("summary-learning-invalid:" + slot, rev, new("wild_learn", MoveSlot: slot));
            Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
            Assert.AreSame(state, session.State); Assert.AreEqual(revision, session.Revision);
        }
        // The view maps the new-move row and Back to -1; row 4 is never sent as a replacement.
        Send(session, new("wild_learn", MoveSlot: -1));
        Assert.AreEqual(state.World.Rng, session.State.World.Rng);
        Assert.AreEqual(xp, session.State.Party[0].Pokemon.Experience);
        CollectionAssert.AreEqual(known.ToArray(), session.State.Party[0].Pokemon.Moves.ToArray());
    }
}

using System.Reflection;
using Game.Domain.Items;
using Game.Application;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Randomness;
using Game.Domain.Tutorials;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class TeachyTvCanonicalReceiptTests
{
    [TestMethod]
    public void PublicMatchupsHiddenSwitchRefreshesVisibleIdentityBeforeDamageAndRewards()
    {
        var content = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(content);
        var initial = session.State;
        initial = initial with { RegisteredItem = QuestItemRules.TeachyTvId.Value,
            Inventory = initial.Inventory! with { KeyItems = (initial.Inventory!.KeyItems ?? []).Add(new(QuestItemRules.TeachyTvId, 1)) } };
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(initial, session.RulesVersion, content.ContentVersion, session.Revision));
        PartyMemberKey? previousMember = null;
        int switches = 0, playerMoves = 0, playerDamage = 0, rewardSteps = 0;
        void CheckReturnedDisplay()
        {
            Assert.AreEqual(initial.Party, session.State.Party);
            Assert.AreEqual(initial.Inventory, session.State.Inventory);
            if (session.State.TeachyTv?.Battle is not {} battle) return;
            Assert.AreNotEqual(CampaignTeachyTvBattleStage.Boundary, battle.Stage, battle.Boundary);
            var display = battle.DisplayBatch!.Display; var member = display.Members[0];
            var expected = display.Bodies[0].Pokemon;
            var projected = session.TeachyTvBattlePresentation!;
            if (previousMember is {} previous && previous != member)
            {
                switches++;
                Assert.AreEqual(new PartyMemberKey(BattleSide.Player, 0), previous);
                Assert.AreEqual(new PartyMemberKey(BattleSide.Player, 1), member);
                Console.WriteLine($"teachy_hidden_switch revision={battle.Revision} event={battle.CurrentEvent?.GetType().Name} " +
                    $"expected={expected.Species.Value}/{expected.Level}/{expected.CurrentHp}/{expected.Stats.MaxHp} " +
                    $"visible={battle.DisplayPlayer.Species.Value}/{battle.DisplayPlayer.Level}/{battle.DisplayPlayer.CurrentHp}/{battle.DisplayPlayer.Stats.MaxHp}");
                Assert.AreEqual("pret:SPECIES_BUTTERFREE", expected.Species.Value);
                Assert.AreEqual(expected.CurrentHp, battle.DisplayPlayer.CurrentHp,
                    "The first returned state after the hidden switch owns the incoming HP, not outgoing Poliwag HP.");
            }
            previousMember = member;
            Assert.AreEqual(expected.Species, battle.DisplayPlayer.Species);
            Assert.AreEqual(expected.Level, battle.DisplayPlayer.Level);
            Assert.AreEqual(expected.Stats, battle.DisplayPlayer.Stats);
            Assert.AreEqual(expected.Stats.MaxHp, battle.DisplayPlayer.Stats.MaxHp);
            Assert.IsTrue(battle.DisplayPlayer.CurrentHp >= 0 && battle.DisplayPlayer.CurrentHp <= battle.DisplayPlayer.Stats.MaxHp,
                "Every returned visible Pokemon must have a drawable HP range.");
            Assert.AreEqual(battle.DisplayPlayer, projected.DisplayPlayer);
            Assert.AreEqual(member.Index, projected.ActivePartyIndex);
            Assert.IsFalse(battle.Steps.Any(step => battle.Events[step.EventIndex] is SourceCalledPartySwitchedEvent),
                "The canonical switch remains hidden, without an added presentation acknowledgement.");
            if (switches > 0)
            {
                Assert.AreEqual(new PartyMemberKey(BattleSide.Player, 1), member);
                Assert.AreEqual("pret:SPECIES_BUTTERFREE", projected.DisplayPlayer.Species.Value);
            }
        }
        void Send(CampaignCommand command)
        {
            var result = session.Execute("teachy-switch:" + session.Revision, session.Revision, command);
            Assert.AreEqual(CommandStatus.Accepted, result.Status, command.Kind + ": " + result.Error);
            CheckReturnedDisplay();
        }
        Send(new("use_registered"));
        Send(new("teachy_tv_navigate", Choice: 2, Text: session.State.TeachyTv!.Playback.WaitId));
        Send(new("teachy_tv_choose", Text: session.State.TeachyTv!.Playback.WaitId));
        for (int n = 0; n < 100 && session.State.TeachyTv!.Battle is null; n++)
        {
            var instruction = session.CurrentTeachyTvInstruction!;
            Send(new(instruction.Kind switch {
                TeachyTvInstructionKind.Demonstration => "teachy_tv_begin_demo",
                TeachyTvInstructionKind.Confirm => "teachy_tv_confirm", _ => "teachy_tv_presented" },
                Text: session.State.TeachyTv.Playback.WaitId));
        }
        Assert.IsNotNull(session.State.TeachyTv!.Battle);
        for (int n = 0; n < 30000 && session.State.TeachyTv?.Battle is {} battle; n++)
        {
            string command = battle.Voiceover.Pending is not null
                ? battle.VoiceoverPrinted ? "teachy_tv_battle_confirm" : "teachy_tv_battle_presented"
                : battle.Stage == CampaignTeachyTvBattleStage.Events ? "teachy_tv_battle_presented" : "teachy_tv_battle_tick";
            bool acknowledges = command == "teachy_tv_battle_presented" && battle.Voiceover.Pending is null;
            var damage = switches > 0 && acknowledges && battle.CurrentEvent is SourceHpChangedEvent { Target.Value: 0 } hp ? hp : null;
            if (damage is not null) Assert.AreEqual(damage.HpBefore, battle.DisplayPlayer.CurrentHp);
            if (switches > 0 && acknowledges && battle.CurrentEvent is SourceMoveUsedEvent { Actor.Value: 0 }) playerMoves++;
            if (switches > 0 && acknowledges && battle.CurrentStep?.Reward is not null) rewardSteps++;
            Send(new(command, Text: battle.WaitId));
            if (damage is not null)
            {
                playerDamage++;
                var after = session.State.TeachyTv!.Battle!;
                Assert.AreEqual(damage.HpAfter, after.DisplayPlayer.CurrentHp);
                Assert.AreEqual(battle.DisplayPlayer.Stats.MaxHp, after.DisplayPlayer.Stats.MaxHp);
            }
        }
        Assert.AreEqual(1, switches, "The public demonstration must cross the Poliwag-to-Butterfree hidden switch.");
        Assert.IsTrue(playerMoves > 0); Assert.IsTrue(playerDamage > 0);
        Assert.AreEqual(4, rewardSteps, "Both participants retain their authored EXP and EV substeps.");
        Assert.IsNull(session.State.TeachyTv?.Battle, "The complete public lesson must return from the demonstration.");
        Assert.AreEqual(initial.Party, session.State.Party); Assert.AreEqual(initial.Inventory, session.State.Inventory);
        Assert.IsTrue(initial.SeenSpecies.SetEquals(session.State.SeenSpecies));
        Assert.IsTrue(initial.CaughtSpecies.SetEquals(session.State.CaughtSpecies));
        Assert.AreEqual(initial.Money, session.State.Money);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void PublicSessionOwnsAllFourLessonsAndRestoresCampaignOwnership(int lessonIndex)
    {
        var content = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(content);
        var initial = session.State;
        initial = initial with { RegisteredItem = QuestItemRules.TeachyTvId.Value,
            Inventory = initial.Inventory! with { KeyItems = (initial.Inventory!.KeyItems ?? []).Add(new(QuestItemRules.TeachyTvId, 1)) } };
        // A controlled new-game diagnostic fixture. Every entry and continuation below
        // goes through public commands; no battle frame or completion result is injected.
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(initial, session.RulesVersion, content.ContentVersion, session.Revision));
        void Send(CampaignCommand command)
        {
            var result = session.Execute("teachy-public:" + session.Revision, session.Revision, command);
            Assert.AreEqual(CommandStatus.Accepted, result.Status, command.Kind + ": " + result.Error);
        }
        Send(new("use_registered"));
        Send(new("teachy_tv_navigate", Choice: lessonIndex, Text: session.State.TeachyTv!.Playback.WaitId));
        Send(new("teachy_tv_choose", Text: session.State.TeachyTv!.Playback.WaitId));
        for (int n = 0; n < 100 && session.State.TeachyTv!.Battle is null; n++)
        {
            var instruction = session.CurrentTeachyTvInstruction!;
            Send(new(instruction.Kind switch {
                TeachyTvInstructionKind.Demonstration => "teachy_tv_begin_demo",
                TeachyTvInstructionKind.Confirm => "teachy_tv_confirm", _ => "teachy_tv_presented" },
                Text: session.State.TeachyTv.Playback.WaitId));
        }
        Assert.IsNotNull(session.State.TeachyTv!.Battle);
        int presented = 0;
        for (int n = 0; n < 30000 && session.State.TeachyTv?.Battle is {} battle; n++)
        {
            Assert.AreNotEqual(CampaignTeachyTvBattleStage.Boundary, battle.Stage, battle.Boundary);
            string command = battle.Voiceover.Pending is not null
                ? battle.VoiceoverPrinted ? "teachy_tv_battle_confirm" : "teachy_tv_battle_presented"
                : battle.Stage == CampaignTeachyTvBattleStage.Events ? "teachy_tv_battle_presented" : "teachy_tv_battle_tick";
            if (command == "teachy_tv_battle_presented") presented++;
            Send(new(command, Text: battle.WaitId));
        }
        Assert.IsTrue(presented > 0);
        Assert.IsNull(session.State.TeachyTv?.Battle, "source demonstration must finish, not stop at a boundary");
        Assert.AreEqual(initial.Party, session.State.Party);
        Assert.AreEqual(initial.Inventory, session.State.Inventory);
        Assert.IsTrue(initial.SeenSpecies.SetEquals(session.State.SeenSpecies));
        Assert.IsTrue(initial.CaughtSpecies.SetEquals(session.State.CaughtSpecies));
        Assert.AreEqual(initial.Money, session.State.Money);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void AuthoredStepsRetainCanonicalIdentityAndOnlyPresentedAcknowledges(int lessonIndex)
    {
        var content = CurrentCampaignFixture.Catalog;
        var mechanics = content.PokemonContent!.Pokemon.Mechanics;
        var lesson = content.TeachyTv!.Lessons[lessonIndex];
        var start = CampaignTeachyTvBattleRules.Start(mechanics, new("canonical-teachy", lesson),
            12345, "RED", "MAPSEC_PALLET_TOWN", new RngState(0x12345678), new());
        Assert.IsTrue(start.Accepted, start.Error);
        var battle = start.State;
        Assert.IsNotNull(battle.DisplayBatch);
        Assert.AreEqual(0, battle.DisplayBatch.NextEventIndex, "entry has not been presented");
        int acknowledgements = 0, rewards = 0;
        for (int budget = 0; budget < 30000 && battle.Stage != CampaignTeachyTvBattleStage.Finished; budget++)
        {
            Assert.AreNotEqual(CampaignTeachyTvBattleStage.Boundary, battle.Stage, battle.Boundary);
            Assert.IsTrue(battle.Events.Zip(battle.Events.Skip(1)).All(pair => pair.First.Sequence < pair.Second.Sequence));
            foreach (var step in battle.Steps) Assert.IsTrue(step.EventIndex >= 0 && step.EventIndex < battle.Events.Length);
            if (battle.CurrentStep is {} current) Assert.AreSame(battle.Events[current.EventIndex], battle.CurrentEvent);
            string command = battle.Voiceover.Pending is not null
                ? battle.VoiceoverPrinted ? "teachy_tv_battle_confirm" : "teachy_tv_battle_presented"
                : battle.Stage == CampaignTeachyTvBattleStage.Events ? "teachy_tv_battle_presented" : "teachy_tv_battle_tick";
            var before = battle;
            var result = CampaignTeachyTvBattleRules.Decide(mechanics, content.PresetDoubleItems!, lesson.Battle!, battle, command, battle.WaitId);
            Assert.IsTrue(result.Accepted, result.Error);
            battle = result.State;
            if (before.CurrentStep is {} stepBefore && before.Voiceover.Pending is null)
            {
                acknowledgements++;
                if (stepBefore.Reward is not null) rewards++;
                // The executor may publish a next batch after the final step; within a batch
                // each repeated reward subpage advances only the presentation step.
                if (before.Events == battle.Events && !battle.Steps.IsEmpty)
                {
                    Assert.AreEqual(before.StepIndex + 1, battle.StepIndex);
                    Assert.AreEqual(before.Frame.Rng, battle.Frame.Rng);
                    Assert.AreEqual(CanonicalJson.Hash(before.Frame), CanonicalJson.Hash(battle.Frame));
                    Assert.AreEqual(CanonicalJson.Hash(before.DisplayBatch!.AdvanceThrough(before.Events, before.CurrentEvent!.Sequence)), CanonicalJson.Hash(battle.DisplayBatch));
                }
            }
            if (before.Voiceover.Pending is not null && !before.VoiceoverPrinted)
            {
                Assert.AreEqual(before.StepIndex, battle.StepIndex);
                Assert.AreEqual(before.DisplayBatch, battle.DisplayBatch);
                Assert.AreEqual(before.Frame.Rng, battle.Frame.Rng);
            }
        }
        Assert.AreEqual(CampaignTeachyTvBattleStage.Finished, battle.Stage, "nonempty bounded tutorial completion");
        Assert.IsTrue(acknowledgements > 0);
        if (lessonIndex == 0) Assert.IsTrue(rewards > 0);
    }
}

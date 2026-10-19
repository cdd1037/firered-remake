using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Application.WildBattles;
[TestClass, TestCategory("Game")]
public sealed class WildBattleFlowTests : WildBattleFixture
{

    [TestMethod]
    public void public_ordinary_entry_frame_authority_KO_rewards_once_current_save()
    {
        var session = StartField();
        var b = session.State.WildBattle!;
        var frame = b.Frame!;
        Assert.IsTrue(typeof(CampaignWildBattle).GetField("_legacyState", BindingFlags.Instance | BindingFlags.NonPublic)is null, "canonical instance stores no legacy snapshot");
        Assert.IsTrue(validator.ValidatePlayerSave(session.State) == "save_event_active", "active canonical battle save refusal");
        Assert.IsTrue(typeof(CampaignWildBattle).GetProperty("State")!.SetMethod is null && typeof(CampaignWildBattle).GetProperty("PendingMoves")!.SetMethod is null, "combat and learning views are read-only");
        bool guarded = false;
        try
        {
            _ = b with
            {
                Frame = null !
            };
        }
        catch (InvalidDataException)
        {
            guarded = true;
        }

        Assert.IsTrue(guarded, "canonical frame cannot be removed");
        string before = CanonicalJson.Hash(frame);
        _ = b.State;
        _ = b.State;
        Assert.IsTrue(CanonicalJson.Hash(b.Frame) == before, "read projection consumes no RNG or mutation");
        var xp = session.State.Party.Sum(p => (long)p.Pokemon.Experience);
        long nextId = session.State.Exploration!.NextWildBattleId;
        int slot = Enumerable.Range(0, b.State.Player.Pokemon.Moves.Length).Where(i => b.State.Player.Pokemon.Moves[i].CurrentPp > 0).OrderByDescending(i => catalog.Moves[b.State.Player.Pokemon.Moves[i].Move].Power).First();
        Send(session, new("battle_move", MoveSlot: slot));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is null, "ordinary KO completes entire modal lifecycle");
        Assert.IsTrue(session.State.Exploration!.NextWildBattleId == nextId && session.State.Exploration.Battles.Count(h => h.Id == b.Id) == 1 && session.State.Exploration.Battles[^1].Outcome == WildBattleOutcome.Won, "one terminal history");
        Assert.IsTrue(session.State.Party.Sum(p => (long)p.Pokemon.Experience) - xp == session.State.Exploration.Battles[^1].ExperienceGained, "exactly once canonical EXP credited");
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void voluntary_switch_and_medicine_retain_real_party_ownership()
    {
        var a = Owned("PIDGEY", 20, 101, "GROWL");
        var b = Owned("RATTATA", 30, 102, "TACKLE");
        b = b with
        {
            Pokemon = b.Pokemon with
            {
                CurrentHp = b.Pokemon.Stats.MaxHp - 10
            }
        };
        var enemy = Owned("PIDGEY", 4, 201, "GROWL");
        var session = Controlled([a, b], enemy);
        Send(session, new("battle_switch", PartyIndex: 1));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle!.ActivePartyIndex == 1 && session.State.WildBattle.Frame!.Get(new(0)).Member!.Value.Index == 1, "switch updates canonical active identity");
        Assert.IsTrue(session.State.Party[0].Pokemon.Personality == a.Pokemon.Personality && session.State.Party[1].Pokemon.Personality == b.Pokemon.Personality, "switch retains permanent identities");
        Send(session, new("battle_medicine", Text: "pret:ITEM_POTION", PartyIndex: 1) { TargetInstanceId = b.InstanceId });
        Drain(session);
        Assert.IsTrue(session.State.Party[1].Pokemon.CurrentHp == b.Pokemon.Stats.MaxHp && !session.State.Inventory!.BagItems.Any(s => s.Item.Value == "pret:ITEM_POTION"), "prepared medicine applies and pays once before same canonical queue");
        Send(session, new("battle_move", MoveSlot: 0));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is null, "switch then KO complete");
    }

    [TestMethod]
    public void medicine_rejects_invalid_target_and_slot_without_consuming_turn_or_item()
    {
        var player = Owned("PIDGEY", 20, 101, "GROWL");
        var session = Controlled([player], Owned("PIDGEY", 4, 201, "GROWL"));
        var valid = new CampaignCommand("battle_medicine", Text: "pret:ITEM_POTION") { TargetInstanceId = player.InstanceId };
        foreach (var (command, error) in new[] {
            (valid with { TargetInstanceId = 999 }, "battle_medicine_target_identity"),
            (valid with { PartyIndex = 1 }, "battle_medicine_target_identity"),
            (valid with { Text = "" }, "battle_medicine_item_invalid"),
            (valid with { MoveSlot = -2 }, "source_item_move_slot") })
        {
            var before = session.State; long revision = session.Revision;
            var rejected = session.Execute("invalid-medicine:" + commands++, revision, command);
            Assert.AreEqual(CommandStatus.Rejected, rejected.Status); Assert.AreEqual(error, rejected.Error);
            Assert.AreSame(before, session.State); Assert.AreEqual(revision, session.Revision);
        }
    }

    [TestMethod]
    [DataRow("ETHER", false)]
    [DataRow("LEPPA_BERRY", true)]
    public void medicine_PP_selection_restores_reserve_move_and_consumes_correct_pocket(string symbol, bool berry)
    {
        var reserve = Owned("RATTATA", 20, 102, "GROWL", "TACKLE");
        reserve = reserve with { Pokemon = reserve.Pokemon with { Moves = reserve.Pokemon.Moves.SetItem(1,
            reserve.Pokemon.Moves[1] with { CurrentPp = 0 }) } };
        var session = Controlled([Owned("PIDGEY", 20, 101, "GROWL"), reserve], Owned("PIDGEY", 4, 201, "GROWL"));
        var item = new ItemId("pret:ITEM_" + symbol); var inventory = session.State.Inventory!;
        var stacks = (berry ? inventory.Berries ?? [] : inventory.BagItems).RemoveAll(stack => stack.Item == item).Add(new(item, 2));
        var state = session.State with { Inventory = berry ? inventory with { Berries = stacks } : inventory with { BagItems = stacks } };
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
        var command = new CampaignCommand("battle_medicine", Text: item.Value, PartyIndex: 1) { TargetInstanceId = reserve.InstanceId };
        var rejected = session.Execute("missing-PP-slot", session.Revision, command);
        Assert.AreEqual("source_item_move_slot", rejected.Error); Assert.AreSame(state, session.State);
        Send(session, command with { MoveSlot = 1 }); Drain(session);
        Assert.AreEqual(10, session.State.Party[1].Pokemon.Moves[1].CurrentPp);
        Assert.AreEqual(reserve.Pokemon.Moves[0], session.State.Party[1].Pokemon.Moves[0]);
        Assert.AreEqual(1, (berry ? session.State.Inventory!.Berries!.Value : session.State.Inventory!.BagItems).Single(stack => stack.Item == item).Quantity);
        Assert.AreEqual(0, session.State.WildBattle!.ActivePartyIndex);
    }

    [TestMethod]
    public void faint_replacement_preserves_continuation_and_charges_friendship_once()
    {
        var a = Owned("PIDGEY", 3, 101, "GROWL");
        a = a with
        {
            Pokemon = a.Pokemon with
            {
                CurrentHp = 1
            }
        };
        var b = Owned("RATTATA", 70, 102, "TACKLE");
        var enemy = Owned("PIDGEY", 30, 201, "TACKLE");
        var session = Controlled([a, b], enemy);
        Send(session, new("battle_move", MoveSlot: 0));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is { Replacement: CampaignReplacementStage.UseNext }, "actual faint exposes replacement modal");
        var frame = session.State.WildBattle!.Frame!;
        var rng = frame.Rng;
        int friendship = session.State.Party[0].Pokemon.Friendship;
        Send(session, new("battle_next", Choice: 1));
        Assert.IsTrue(session.State.WildBattle!.Frame == frame && session.State.World.Rng == rng, "party menu selection advances no battle/RNG");
        Send(session, new("battle_switch", PartyIndex: 1));
        Drain(session);
        Assert.IsTrue(session.State.Party[0].Pokemon.Friendship == friendship && friendship == a.Pokemon.Friendship - 1, "faint friendship once across continuation");
        Assert.IsTrue(session.State.WildBattle!.Frame!.Get(new(0)).Member!.Value.Index == 1, "replacement installs actual reserve");
        Send(session, new("battle_move", MoveSlot: 0));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is null, "replacement resumed through terminal KO");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EXP_learning_pause_uses_frame_cursor_and_never_reapplies_reward(bool translated)
    {
        content = GrowthResultTextFixture.WithTranslations(content, translated);
        var a = Owned("BULBASAUR", 19, 101, "TACKLE", "GROWL", "LEECH_SEED", "VINE_WHIP");
        var species = catalog.Species[a.Pokemon.Species];
        var target = species.LevelUpMoves.First(m => m.Level > 19 && m.Level < 40 && !a.Pokemon.Moves.Any(x => x.Move == m.Move));
        a = Owned("BULBASAUR", target.Level - 1, 101, "TACKLE", "GROWL", "LEECH_SEED", "VINE_WHIP");
        a = a with
        {
            Pokemon = a.Pokemon with
            {
                Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, target.Level) - 1
            }
        };
        var enemy = Owned("PIDGEY", 2, 201, "GROWL");
        enemy = enemy with
        {
            Pokemon = enemy.Pokemon with
            {
                CurrentHp = 1
            }
        };
        var session = Controlled([a], enemy);
        Send(session, new("battle_move", MoveSlot: 0));
        Drain(session, stopLearning: true);
        var b = session.State.WildBattle!;
        Assert.IsTrue(b.Frame!.Rewards?.Pending is not null && b.PendingMoves[0].Move == target.Move, "canonical learning cursor exposed");
        long xp = session.State.Party[0].Pokemon.Experience;
        var rewardFrame = b.Frame;
        var rejected = session.Execute("wrong-learning", session.Revision, new("wild_learn", MoveSlot: 4));
        Assert.IsTrue(rejected.Status == CommandStatus.Rejected && ReferenceEquals(session.State.WildBattle!.Frame, rewardFrame), "bad learning choice leaves canonical state identical");
        Send(session, new("wild_learn", MoveSlot: 0));
        Assert.IsTrue(session.State.WildBattle!.SourceEvents.OfType<SourceRewardLearningEvent>().Any(e =>
            e.Outcome == SourceRewardLearningOutcome.Replaced && e.Candidate.Move == target.Move), "Actual replacement receipt");
        GrowthResultTextFixture.AssertPages(content, session.State.WildBattle!.Pages.Select(page => page.Text),
            "sText_PkmnLearnedMove", ("B_BUFF1", content.PokemonDisplayName(session.State.Party[0])),
            ("B_BUFF2", content.BattleContent.MoveNames[target.Move.Value]));
        Assert.IsTrue(session.State.WildBattle.Pages.All(page => page.Reward is null && page.PartyIndex == 0),
            "Manual-learning pages keep their original reward/recipient metadata.");
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is null && session.State.Party[0].Pokemon.Experience == xp, "learning resumes without duplicate EXP");
        Assert.IsTrue(session.State.Party[0].Pokemon.Moves[0].Move == target.Move, "chosen learning persisted");
    }

    [TestMethod]
    public void Transform_retains_permanent_identity_and_PP_through_escape()
    {
        var ditto = Owned("DITTO", 50, 101, "TRANSFORM");
        var enemy = Owned("PIDGEY", 20, 201, "GROWL");
        var session = Controlled([ditto], enemy);
        Send(session, new("battle_move", MoveSlot: 0));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle!.Frame!.Get(new(0)).Body!.Transform is not null, "actual shared Transform recipe executed");
        Assert.IsTrue(session.State.Party[0].Pokemon.Species == ditto.Pokemon.Species && session.State.Party[0].Pokemon.Personality == ditto.Pokemon.Personality, "Transform preserves permanent species/personality");
        for (int i = 0; i < 10 && session.State.WildBattle is not null; i++)
        {
            Send(session, new("battle_menu", Text: "run"));
            Drain(session);
        }

        Assert.IsTrue(session.State.WildBattle is null && session.State.Party[0].Pokemon.Species == ditto.Pokemon.Species && session.State.Party[0].Pokemon.Moves[0].Move == ditto.Pokemon.Moves[0].Move && session.State.Party[0].Pokemon.Moves[0].CurrentPp == ditto.Pokemon.Moves[0].CurrentPp - 1, "escape persists original Transform PP exactly once");
    }

    [TestMethod]
    public void Poke_Flute_uses_canonical_item_action_and_keeps_reusable_key_item()
    {
        var a = Owned("PIDGEY", 20, 101, "GROWL")with
        {
            Status = 3
        };
        var enemy = Owned("PIDGEY", 4, 201, "GROWL")with
        {
            Status = 3
        };
        var session = Controlled([a], enemy);
        int before = session.State.Inventory!.KeyItems!.Value.Single(i => i.Item.Value == "pret:ITEM_POKE_FLUTE").Quantity;
        Send(session, new("battle_poke_flute", Text: "pret:ITEM_POKE_FLUTE"));
        Drain(session);
        Assert.IsTrue(session.State.Party[0].Status == 0 && session.State.WildBattle!.Frame!.Party(BattleSide.Opponent).Members[0].Status == 0, "flute wakes actual canonical parties");
        Assert.IsTrue(session.State.Inventory!.KeyItems!.Value.Single(i => i.Item.Value == "pret:ITEM_POKE_FLUTE").Quantity == before, "reusable flute retained");
        Send(session, new("battle_menu", Text: "run"));
        Drain(session);
        Assert.IsTrue(session.State.WildBattle is null, "flute lifecycle completes through escape");
    }
}

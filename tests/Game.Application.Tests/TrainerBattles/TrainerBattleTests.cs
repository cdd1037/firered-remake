using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using System.Security.Cryptography;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Application.TrainerBattles;
[TestClass, TestCategory("Game")]
public sealed class TrainerBattleTests
{
    private global::Game.Content.PretCampaignCatalog current = default !;
    private global::Game.Application.CampaignSession owner = default !;
    private byte[] sourceBytes = default !;
    private global::Game.Application.LoadedSave<global::Game.Application.CampaignState> checkpoint = default !;
    private global::Game.Application.CampaignState currentState = default !;
    private int sequence = default !;
    private int replacements = default !;
    private int shiftChoices = default !;
    private int learningChoices = default !;
    private global::Game.Application.CampaignState roseState = default !;
    [TestInitialize]
    public void SetUp()
    {
        current = CurrentCampaignFixture.Catalog;
        owner = CampaignSession.CreateCurrent(current);
        sourceBytes = File.ReadAllBytes(TestAssets.Checkpoint);
        checkpoint = CurrentCampaignFixture.Checkpoint;
        currentState = checkpoint.State;
        currentState = currentState with
        {
            Inventory = currentState.Inventory!with
            {
                BagItems = currentState.Inventory!.BagItems.RemoveAll(x => x.Item.Value == "pret:ITEM_REVIVE").Add(new(new("pret:ITEM_REVIVE"), 5))
            }
        };
        var validationError = owner.ValidatePlayerSave(currentState);
        Assert.IsNull(validationError, "current fixture strict validity: " + validationError);
        Assert.IsTrue(Environment.Version.Major == 10, "actual runtime10");
        sequence = 0;
        replacements = 0;
        shiftChoices = 0;
        learningChoices = 0;
        roseState = Place(current, owner, Heal(currentState), "SixIsland_WaterPath_EventScript_Rose");
    }

    private object? Call(CampaignSession owner, string method, params object? [] values)
    {
        try
        {
            var target = typeof(CampaignSession).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name == method && m.GetParameters().Length >= values.Length && m.GetParameters().Skip(values.Length).All(p => p.IsOptional));
            return target.Invoke(owner, values.Concat(target.GetParameters().Skip(values.Length).Select(p => p.DefaultValue)).ToArray());
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException!;
        }
    }

    private CampaignState Heal(CampaignState state) => state with
    {
        Party = state.Party.Select(p => p with { Status = 0, Pokemon = p.Pokemon with { CurrentHp = p.Pokemon.Stats.MaxHp, Moves = p.Pokemon.Moves.Select(m => m with { CurrentPp = m.MaximumPp }).ToImmutableArray() } }).ToImmutableArray()
    };
    private CampaignState Place(ICampaignCatalog content, CampaignSession session, CampaignState state, string script)
    {
        var map = content.Maps.Keys.Single(map => content.MapDefinition(map).Actors.Any(a => a.ScriptId == script));
        var actor = content.MapDefinition(map).Actors.Single(a => a.ScriptId == script);
        var field = content.ConnectedFieldMaps![map];
        var options = new[]
        {
            (X: actor.X, Y: actor.Y + 1, Face: Direction.North),
            (X: actor.X + 1, Y: actor.Y, Face: Direction.West),
            (X: actor.X, Y: actor.Y - 1, Face: Direction.South),
            (X: actor.X - 1, Y: actor.Y, Face: Direction.East)
        };
        var target = options.First(p => field.CellAt(p.X, p.Y)is { Collision: 0, Elevation: 3 } && !content.MapDefinition(map).Actors.Any(a => a.X == p.X && a.Y == p.Y));
        return (CampaignState)Call(session, "Pump", Call(session, "EnterMap", state with { World = state.World with { Player = new(map, target.X, target.Y, 3, target.Face) }, IsSurfing = false }, null))!;
    }

    private void Send(CampaignSession session, CampaignCommand command)
    {
        if (command.Kind == "battle_switch" && session.State.ForestTrainerBattle?.Frame.FourTurn?.Phase == SourceFourActionPhase.ReplacementsPending)
            replacements++;
        if (command.Kind == "trainer_shift")
            shiftChoices++;
        if (command.Kind == "wild_learn")
            learningChoices++;
        var result = session.Execute("canonical-trainer:" + sequence, session.Revision, command);
        sequence++;
        Assert.IsTrue(result.Status == CommandStatus.Accepted, command.Kind + ": " + result.Error);
    }

    private void Drain(CampaignSession session, ICampaignCatalog content, bool acceptShift = true, bool learnSourceMove = false)
    {
        for (int budget = 0; budget < 500; budget++)
        {
            var state = session.State;
            if (state.ForestTrainerBattle is { } trainer)
            {
                var p = trainer.Presentation;
                if (trainer.Stage == CampaignForestTrainerStage.Intro || p.Phase == CampaignBattlePhase.Events)
                {
                    Send(session, new("ack"));
                    continue;
                }

                if (p.Phase == CampaignBattlePhase.Results)
                {
                    if (!p.PendingMoves.IsEmpty && p.PageIndex + 1 >= p.Pages.Length && learnSourceMove)
                    {
                        var pending = p.PendingMoves[0]; int recipient = p.RewardPartyIndex;
                        int slot = Enumerable.Range(0, state.Party[recipient].Pokemon.Moves.Length)
                            .First(i => !PokemonMoveRules.IsHiddenMachineMove(state.Party[recipient].Pokemon.Moves[i].Move));
                        Send(session, new("wild_learn", MoveSlot: slot));
                        Assert.IsTrue(session.State.ForestTrainerBattle!.Presentation.Events.OfType<SourceRewardLearningEvent>().Any(e =>
                            e.Outcome == SourceRewardLearningOutcome.Replaced && e.Candidate.Move == pending.Move), "Actual trainer replacement receipt");
                        var pages = session.State.ForestTrainerBattle!.Presentation.Pages
                            .Where(page => page.Reward is null && page.PartyIndex == recipient);
                        GrowthResultTextFixture.AssertPages(current, pages.Select(page => page.Text), "sText_PkmnLearnedMove",
                            ("B_BUFF1", current.PokemonDisplayName(session.State.Party[recipient])),
                            ("B_BUFF2", current.BattleContent.MoveNames[pending.Move.Value]));
                    }
                    else Send(session, !p.PendingMoves.IsEmpty && p.PageIndex + 1 >= p.Pages.Length ? new("wild_learn", MoveSlot: -1) : new("ack"));
                    continue;
                }

                if (trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice)
                {
                    if (!acceptShift)
                    {
                        Send(session, new("trainer_shift", Choice: 0));
                        continue;
                    }

                    Send(session, new("trainer_shift", Choice: 1));
                    Send(session, new("battle_switch", PartyIndex: Enumerable.Range(0, state.Party.Length).First(i => i != p.ActivePartyIndex && state.Party[i].Pokemon.CurrentHp > 0)));
                    continue;
                }

                if (trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingPlayerReplacement)
                {
                    Send(session, new("battle_switch", PartyIndex: Enumerable.Range(0, state.Party.Length).First(i => state.Party[i].Pokemon.CurrentHp > 0)));
                    continue;
                }

                var mechanics = content.OpponentPokemon?.Mechanics.Catalog ?? content.OpponentPokemon!.Mechanics.Catalog;
                int Score(int slot)
                {
                    if (slot < 0)
                        return 0;
                    var move = mechanics.Moves[trainer.Combat.Battle.Player.Pokemon.Moves[slot].Move];
                    if (move.Power == 0)
                        return 0;
                    // Selection heuristic only; actual damage belongs to the battle engine.
                    return move.Power;
                }

                var slots = Enumerable.Range(-1, trainer.Combat.Battle.Player.Pokemon.Moves.Length + 1).Where(i => SourceWildBattlePolicy.ValidateMoveSelection(mechanics, trainer.Frame, i)is null);
                Send(session, new("battle_move", MoveSlot: slots.OrderByDescending(Score).First()));
                continue;
            }

            if (state.Evolution is not null)
            {
                Send(session, new("evolution_choice", Choice: 1));
                continue;
            }

            if (state.Script.Wait is { } wait)
            {
                Send(session, wait.Kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") : new("ack", Choice: wait.Kind == StoryWaitKind.Choice ? 1 : null));
                continue;
            }

            if (state.Dialogue is not null)
            {
                Send(session, new("ack"));
                continue;
            }

            return;
        }

        throw new InvalidDataException("bounded battle drain exceeded");
    }

    private void SaveCheck(string key, CampaignSession session, ICampaignCatalog content)
    {
        var validationError = session.ValidatePlayerSave(session.State);
        Assert.IsNull(validationError, key + " final strict save validity: " + validationError);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(CanonicalJson.Hash(continued.State) == CanonicalJson.Hash(session.State), key + " exact current Continue");
        Console.WriteLine("Completed " + key);
    }

    [TestMethod]
    public void OneCanonicalSwitchRetainsOutgoingIdentityAndIsAppliedOnlyOnce()
    {
        var session = CampaignSession.RestoreCurrent(current, roseState, checkpoint.Generation);
        Send(session, new("interact"));
        while (session.State.ForestTrainerBattle!.Presentation.Phase != CampaignBattlePhase.ChooseMove)
            Send(session, new("ack"));
        var before = session.State.ForestTrainerBattle!.Presentation; var outgoing = before.DisplayBatch!.Display.Bodies[0];
        Send(session, new("battle_switch", PartyIndex: 1));
        var switched = session.State.ForestTrainerBattle!.Presentation;
        for (int n = 0; n < 32 && switched.Events[switched.EventIndex] is not SourceCalledPartySwitchedEvent; n++)
        {
            Send(session, new("ack")); switched = session.State.ForestTrainerBattle!.Presentation;
        }
        var receipt = switched.Events[switched.EventIndex] as SourceCalledPartySwitchedEvent;
        Assert.IsNotNull(receipt); Assert.IsNotNull(switched.SwitchDisplay); Assert.IsNotNull(receipt.Replacement);
        Assert.AreEqual(outgoing, switched.SwitchDisplay.Previous);
        Assert.AreEqual(receipt.Replacement.Body, switched.SwitchDisplay.Replacement);
        Assert.AreEqual(new PartyMemberKey(BattleSide.Player, 1), switched.DisplayBatch!.Display.Members[0]);
        Assert.AreEqual(switched.EventIndex + 1, switched.DisplayBatch.NextEventIndex);
        Assert.AreEqual(1, switched.Events.OfType<SourceCalledPartySwitchedEvent>().Count());
        var advance = typeof(CampaignSession).GetMethod("AdvanceDisplayThrough", BindingFlags.Static | BindingFlags.NonPublic)!;
        var repeated = (CampaignWildBattle)advance.Invoke(null, [switched, receipt.Sequence])!;
        Assert.AreSame(switched.SwitchDisplay, repeated.SwitchDisplay);
        Assert.AreEqual(switched.EventIndex, repeated.EventIndex);
        Assert.AreEqual(switched.DisplayBatch, repeated.DisplayBatch);
        Assert.AreEqual(switched.Frame, repeated.Frame); Assert.AreEqual(switched.RandomTrace, repeated.RandomTrace);
        long sequence = receipt.Sequence;
        Send(session, new("ack"));
        var next = session.State.ForestTrainerBattle!.Presentation;
        Assert.IsFalse(next.Events.ElementAtOrDefault(next.EventIndex)?.Sequence == sequence,
            "One acknowledgement advances the canonical receipt, not a synthetic second switch event.");
    }

    [TestMethod]
    public void RoseWinAndShift()
    {
        var rose = CampaignSession.RestoreCurrent(current, roseState, checkpoint.Generation);
        Send(rose, new("interact"));
        Assert.IsTrue(rose.State.ForestTrainerBattle?.Trainer.Value == "pret:TRAINER_AROMA_LADY_ROSE", "authentic Rose public entry");
        var entry = rose.State.ForestTrainerBattle!;
        Assert.IsTrue(entry.Frame.TrainerEncounter is not null && entry.Frame.Party(BattleSide.Player).Members.Length == 6, "canonical full party authority");
        Assert.IsTrue(entry.Presentation.RandomTrace.Take(4).Select(t => t.Purpose).SequenceEqual(Enumerable.Range(0, 4).Select(i => $"six-island-trainer.entry.ai-setup.slot-{i}")), "original four setup draws first");
        while (rose.State.ForestTrainerBattle!.Presentation.Phase != CampaignBattlePhase.ChooseMove)
            Send(rose, new("ack"));
        var before = CanonicalJson.Hash(rose.State);
        var rejected = rose.Execute("invalid-item", rose.Revision, new("battle_medicine", Text: "pret:ITEM_REVIVE", PartyIndex: 1) { TargetInstanceId = rose.State.Party[1].InstanceId });
        Assert.IsTrue(rejected.Status == CommandStatus.Rejected && CanonicalJson.Hash(rose.State) == before, "failed item selection atomic");
        Drain(rose, current);
        Assert.IsTrue(rose.State.ForestTrainerHistory!.Value.Last()is { Outcome: WildBattleOutcome.Won, ExperienceGained: > 0, MoneyGained: > 0 }, "Rose win EXP prize");
        Assert.IsTrue(rose.State.Party.Sum(p => (long)p.Pokemon.Experience) - roseState.Party.Sum(p => (long)p.Pokemon.Experience) == rose.State.ForestTrainerHistory!.Value.Last().ExperienceGained, "party EXP delta equals single shared reward total");
        SaveCheck("rose-win-shift", rose, current);
        Assert.IsTrue(shiftChoices > 0, "public Shift choice exercised");
    }

    [TestMethod]
    public void RoseReviveAndReplacement()
    {
        var faintState = roseState with
        {
            Party = roseState.Party.SetItem(0, roseState.Party[0] with { Status = WildBattleStatus.Poison, Pokemon = roseState.Party[0].Pokemon with { CurrentHp = 1 } }).SetItem(1, roseState.Party[1] with { Pokemon = roseState.Party[1].Pokemon with { CurrentHp = 0 } })
        };
        var faint = CampaignSession.RestoreCurrent(current, faintState, checkpoint.Generation);
        Send(faint, new("interact"));
        Assert.IsTrue(faint.State.Party[1].Pokemon.Friendship == faintState.Party[1].Pokemon.Friendship, "already-fainted reserve does not acquire a new friendship penalty at entry");
        while (faint.State.ForestTrainerBattle!.Presentation.Phase != CampaignBattlePhase.ChooseMove)
            Send(faint, new("ack"));
        int revives = faint.State.Inventory!.BagItems.Single(x => x.Item.Value == "pret:ITEM_REVIVE").Quantity;
        Send(faint, new("battle_medicine", Text: "pret:ITEM_REVIVE", PartyIndex: 1) { TargetInstanceId = faint.State.Party[1].InstanceId });
        Assert.IsTrue(faint.State.Party[1].Pokemon.CurrentHp > 0 && faint.State.Inventory!.BagItems.Single(x => x.Item.Value == "pret:ITEM_REVIVE").Quantity == revives - 1, "reserve Revive paid and applied once");
        Drain(faint, current);
        SaveCheck("rose-revive-replacement", faint, current);
        Assert.IsTrue(replacements > 0, "public fainted player replacement exercised");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RoseSourceLevelLearning(bool translated)
    {
        current = GrowthResultTextFixture.WithTranslations(current, translated);
        int learnerIndex = Enumerable.Range(0, roseState.Party.Length).Single(i => roseState.Party[i].Pokemon.Species.Value == "pret:SPECIES_ARCANINE");
        var learner = roseState.Party[learnerIndex];
        var pokemon = learner.Pokemon;
        var species = current.PokemonContent!.Catalog.Species[pokemon.Species];
        var candidate = species.LevelUpMoves.First(m => m.Level > pokemon.Level && !pokemon.Moves.Any(s => s.Move == m.Move));
        int targetLevel = candidate.Level, level = targetLevel - 1;
        var basis = species.BaseStats;
        int Stat(int b, int iv, int ev, Game.Domain.Rules.Stat s) => Game.Domain.Rules.StatRules.CalculateOther(b, iv, ev, level, pokemon.Nature, s);
        var stats = new BattleStats(Game.Domain.Rules.StatRules.CalculateHp(basis.Hp, pokemon.Ivs.Hp, pokemon.Evs.Hp, level), Stat(basis.Attack, pokemon.Ivs.Attack, pokemon.Evs.Attack, Game.Domain.Rules.Stat.Attack), Stat(basis.Defense, pokemon.Ivs.Defense, pokemon.Evs.Defense, Game.Domain.Rules.Stat.Defense), Stat(basis.SpecialAttack, pokemon.Ivs.SpecialAttack, pokemon.Evs.SpecialAttack, Game.Domain.Rules.Stat.SpecialAttack), Stat(basis.SpecialDefense, pokemon.Ivs.SpecialDefense, pokemon.Evs.SpecialDefense, Game.Domain.Rules.Stat.SpecialDefense), Stat(basis.Speed, pokemon.Ivs.Speed, pokemon.Evs.Speed, Game.Domain.Rules.Stat.Speed));
        learner = learner with
        {
            Pokemon = pokemon with
            {
                Level = level,
                Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, targetLevel) - 1,
                StatCalculationEvs = null,
                Stats = stats,
                CurrentHp = stats.MaxHp
            }
        };
        var learningState = roseState with
        {
            Party = roseState.Party.SetItem(0, learner).SetItem(learnerIndex, roseState.Party[0])
        };
        var learning = CampaignSession.RestoreCurrent(current, learningState, checkpoint.Generation);
        Send(learning, new("interact"));
        Drain(learning, current, learnSourceMove: true);
        Assert.IsTrue(learning.State.Party[0].Pokemon.Level == targetLevel, "actual EXP reaches next source learning level");
        Assert.IsTrue(learningChoices > 0, "actual source learning cursor answered once");
        Assert.IsTrue(learning.State.Party.Sum(p => (long)p.Pokemon.Experience) - learningState.Party.Sum(p => (long)p.Pokemon.Experience) == learning.State.ForestTrainerHistory!.Value.Last().ExperienceGained, "learning continuation does not duplicate EXP");
        SaveCheck("rose-level-learning", learning, current);
    }

    [TestMethod]
    public void GideonSourceCallbackAndReentry()
    {
        var gideonState = Heal(currentState);
        bool sapphireAlreadyRecovered = gideonState.Script.Flags.GetValueOrDefault("FLAG_RECOVERED_SAPPHIRE");
        int oldHistory = (gideonState.ForestTrainerHistory ?? []).Length;
        var gideon = CampaignSession.RestoreCurrent(current, Place(current, owner, gideonState, "FiveIsland_RocketWarehouse_EventScript_Gideon"), checkpoint.Generation);
        Send(gideon, new("interact"));
        Assert.IsTrue(sapphireAlreadyRecovered ? gideon.State.ForestTrainerBattle is null : gideon.State.ForestTrainerBattle?.Trainer.Value == "pret:TRAINER_SCIENTIST_GIDEON", "Gideon actual source entry or completed re-entry");
        Drain(gideon, current, false);
        Console.WriteLine("Gideon outcome " + gideon.State.ForestTrainerHistory!.Value.Last().Outcome + " callback=" + gideon.State.Script.Flags.GetValueOrDefault("FLAG_RECOVERED_SAPPHIRE"));
        Assert.IsTrue(gideon.State.Script.Flags.GetValueOrDefault("FLAG_RECOVERED_SAPPHIRE"), "Gideon actual source callback recovered Sapphire");
        SaveCheck("gideon-source-callback", gideon, current);
        if (sapphireAlreadyRecovered)
            Assert.IsTrue((gideon.State.ForestTrainerHistory ?? []).Length == oldHistory, "completed Gideon re-entry awards no second battle or EXP");
        Assert.IsTrue(sourceBytes.AsSpan().SequenceEqual(File.ReadAllBytes(TestAssets.Checkpoint)), "input checkpoint remains unchanged");
    }
}

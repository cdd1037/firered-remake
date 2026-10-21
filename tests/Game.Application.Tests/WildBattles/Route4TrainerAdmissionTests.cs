using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace FireRed.Tests.Application.WildBattles;

/// <summary>Controlled Route4 placement, not evidence of natural route completion.</summary>
[TestClass]
public sealed class Route4TrainerAdmissionTests
{
    private readonly PretCampaignCatalog content = CurrentCampaignFixture.Catalog;
    private static void Send(CampaignSession session, CampaignCommand command)
    {
        var result = session.Execute("route4-controlled:" + session.Revision, session.Revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
    }
    private const string Script = "Route4_EventScript_Crissy";
    private const string Trainer = "pret:TRAINER_LASS_CRISSY";

    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    [TestMethod]
    public void EveryCompiledOrdinaryCallHasOneRegisteredOccurrence()
    {
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var content = CurrentCampaignFixture.Catalog;
        var compilation = (PretStoryCompilation)typeof(PretCampaignCatalog).GetField("_scripts", members)!.GetValue(content)!;
        var calls = compilation.Programs.Values.SelectMany(p => p.Instructions)
            .Where(i => i.Op == StoryOp.TrainerBattle && i.Opcode is "trainerbattle_single" or "trainerbattle_no_intro").ToArray();
        Assert.IsTrue(calls.Length > 0);
        var session = CampaignSession.CreateCurrent(content);
        var registry = typeof(CampaignSession).GetProperty("TrainerOccurrences", members)!.GetValue(session)!;
        var dictionary = registry.GetType().GetProperty("ByIdentity", members)!.GetValue(registry)!;
        var registered = ((System.Collections.IEnumerable)dictionary).Cast<object>().Select(row => {
            var occurrence = row.GetType().GetProperty("Value")!.GetValue(row)!;
            return (StoryInstruction)occurrence.GetType().GetProperty("Instruction", members)!.GetValue(occurrence)!;
        }).Where(i => i.Opcode is "trainerbattle_single" or "trainerbattle_no_intro").ToArray();
        CollectionAssert.AreEquivalent(calls, registered);
    }

    [TestMethod]
    [DataRow("call")]
    [DataRow("post")]
    [DataRow("autoclose")]
    [DataRow("mode")]
    [DataRow("source")]
    public void ActualCompiledCrissyInstructionMutationsAreRejected(string mutation)
    {
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var original = CurrentCampaignFixture.Catalog;
        var program = original.FindProgram(Script)!;
        int index = Enumerable.Range(0, program.Instructions.Length).Single(i =>
            program.Instructions[i].Opcode == (mutation is "post" or "autoclose" ? "msgbox" : "trainerbattle_single"));
        var instruction = program.Instructions[index];
        var changed = mutation switch {
            "call" => instruction with { Args = instruction.Args.SetItem(0, "TRAINER_LASS_SALLY") },
            "post" => instruction with { Args = instruction.Args.SetItem(0, "Route4_Text_CrissyIntro") },
            "autoclose" => instruction with { Args = instruction.Args.SetItem(1, "MSGBOX_NPC") },
            "mode" => instruction with { Opcode = "trainerbattle_rematch" },
            "source" => instruction with { Source = instruction.Source with { Path = "data/maps/Route3/scripts.inc" } },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var copy = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", members)!.Invoke(original, null)!;
        var field = typeof(PretCampaignCatalog).GetField("_scripts", members)!;
        var compilation = (PretStoryCompilation)field.GetValue(original)!;
        field.SetValue(copy, compilation with { Programs = compilation.Programs.SetItem(Script,
            program with { Instructions = program.Instructions.SetItem(index, changed) }) });
        Assert.ThrowsExactly<InvalidDataException>(() => CampaignSession.CreateCurrent(copy));
    }

    [TestMethod]
    public void ActualRoute4ActorTalkStartsRegisteredBattleAndSettlesIntoPostDialogue()
    {
        var session = CampaignSession.CreateCurrent(content);
        var map = new MapId("map.route-4");
        var actor = content.MapDefinition(map).Actors.Single(a => a.ScriptId == Script);
        Assert.AreEqual(75, actor.X);
        Assert.AreEqual(3, actor.Y);
        var definition = content.OrdinaryTrainerCatalogs.SelectMany(c => c.Battles).Single(b => b.ScriptEntry == Script);
        Assert.AreEqual(Trainer, definition.Trainer.Value);
        Assert.AreEqual("Route4_Text_CrissyPostBattle", definition.PostBattleText);
        var call = content.FindProgram(Script)!.Instructions.Single(i => i.Op == StoryOp.TrainerBattle);
        CollectionAssert.AreEqual(new[] { "TRAINER_LASS_CRISSY", "Route4_Text_CrissyIntro", "Route4_Text_CrissyDefeat" }, call.Args.ToArray());
        var state = session.State;
        // Explicit source-created test party, not a captured/starter history or a player save.
        var made = SourcePokemonFactory.Create(content.GamePokemon.Mechanics,
            new(new("pret:SPECIES_CHARMELEON"), 50, state.TrainerId, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new RngState(42));
        var move = content.GamePokemon.Mechanics.Catalog.Moves[new("pret:MOVE_FLAMETHROWER")];
        var pokemon = made.Pokemon with { Moves = [new(move.Id, move.Pp, move.Pp)] };
        state = state with { Party = [new(1, pokemon, null, state.PlayerName, state.PlayerGender,
            2, "MAPSEC_ROUTE_4", 50, 4, "ITEM_POKE_BALL", "ITEM_NONE")], NextPokemonId = 2 };
        Assert.IsFalse((state.ForestTrainerHistory ?? []).Any(h => h.TrainerId == Trainer), "Fixture must not invent prior Crissy history.");
        state = state with {
            World = state.World with { Player = new(map, actor.X, actor.Y + 1, actor.Elevation, Direction.North) },
            BattledTrainers = state.BattledTrainers.Remove(Trainer),
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables, EffectSequence = state.Script.EffectSequence },
            Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null
        };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session, (CampaignState)pump.Invoke(session, [state])!);
        Send(session, new("interact"));
        Assert.IsNotNull(session.State.ForestTrainerBattle);
        Assert.AreEqual(definition.Trainer, session.State.ForestTrainerBattle.Trainer);
        Assert.AreEqual(Script, session.State.ForestTrainerBattle.SourceScriptEntry);
        Assert.AreEqual(call.Source.Line, session.State.ForestTrainerBattle.SourceScriptLine);
        for (int budget = 0; budget < 400 && session.State.ForestTrainerBattle is { } battle; budget++)
        {
            if (battle.Stage == CampaignForestTrainerStage.Intro || battle.Presentation.Phase == CampaignBattlePhase.Events)
                Send(session, new("ack"));
            else if (battle.Presentation.Phase == CampaignBattlePhase.Results)
                Send(session, !battle.Presentation.PendingMoves.IsEmpty && battle.Presentation.PageIndex + 1 >= battle.Presentation.Pages.Length
                    ? new("wild_learn", MoveSlot: -1) : new("ack"));
            else if (battle.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice)
                Send(session, new("trainer_shift", Choice: 0));
            else Send(session, new("battle_move", MoveSlot: 0));
        }
        Assert.IsNull(session.State.ForestTrainerBattle, "Bounded real battle must finish.");
        Assert.IsTrue(session.State.BattledTrainers.Contains(Trainer));
        Assert.AreEqual(1, (session.State.ForestTrainerHistory ?? []).Count(h => h.TrainerId == Trainer));
        // Normal single-battle callback ends; source post speech is reached by talking again.
        Assert.IsNull(session.State.Dialogue);
        Assert.IsTrue(session.State.Script.Completed && !session.State.Script.Locked);
        var money = session.State.Money;
        var history = session.State.ForestTrainerHistory;
        Send(session, new("interact"));
        Assert.IsNull(session.State.ForestTrainerBattle);
        Assert.AreEqual(definition.PostBattleText, session.State.Dialogue?.TextSymbol);
        for (int budget = 0; budget < 30 && (session.State.Dialogue is not null || !session.State.Script.Completed); budget++)
            Send(session, new("ack"));
        Assert.AreEqual(money, session.State.Money);
        Assert.AreEqual(history, session.State.ForestTrainerHistory);
        Assert.IsTrue(session.State.Script.Completed && !session.State.Script.Locked);
    }
}

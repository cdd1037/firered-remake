using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class RegisteredTrainerOccurrenceTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static T Property<T>(object target, string name) =>
        (T)(target.GetType().GetProperty(name, InstanceMembers)?.GetValue(target)
            ?? throw new InvalidOperationException("Missing registry property: " + name));

    private static object[] Rows(object collection) => ((IEnumerable)collection).Cast<object>().ToArray();

    private static Dictionary<TrainerId, object> TrainerEntries(object dictionary) =>
        Rows(dictionary).ToDictionary(entry => Property<TrainerId>(entry, "Key"), entry => Property<object>(entry, "Value"));

    private static object Invoke(CampaignSession session, string method, params object?[] arguments)
    {
        try
        {
            return typeof(CampaignSession).GetMethod(method, InstanceMembers)!.Invoke(session, arguments)!;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static ForestTrainerBattleDefinition Definition(object occurrence) =>
        Property<ForestTrainerBattleDefinition>(Property<object>(occurrence, "Source"), "Definition");

    private static StoryInstruction Instruction(object occurrence) => Property<StoryInstruction>(occurrence, "Instruction");

    private static object[] Sources(object binding) => Rows(Property<object>(Property<object>(binding, "Scripts"), "Sources"));

    [TestMethod]
    public void CurrentCatalogBindsEveryAdmittedOrdinaryTrainerExactlyOnce()
    {
        var content = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(content);
        var bindings = TrainerEntries(Property<object>(session, "OrdinaryTrainerBindings"));
        var registry = Property<object>(session, "TrainerOccurrences");
        var byTrainer = TrainerEntries(Property<object>(registry, "ByTrainer"));
        var primary = TrainerEntries(Property<object>(registry, "Primary"));
        var admitted = bindings.Where(entry => Sources(entry.Value).Length != 0).Select(entry => entry.Key).ToArray();
        Assert.IsTrue(admitted.Length > 0, "The current catalog must exercise an ordinary roster.");
        CollectionAssert.AreEquivalent(admitted, byTrainer.Keys.ToArray());
        CollectionAssert.AreEquivalent(admitted, primary.Keys.ToArray());

        var identities = new HashSet<(TrainerId Trainer, string Script, int Line)>();
        foreach (var (trainer, rows) in byTrainer)
        {
            var occurrences = Rows(rows);
            var declared = Sources(bindings[trainer]).Select(source => Property<ForestTrainerBattleDefinition>(source, "Definition"))
                .Where(source => source.Trainer == trainer && source.Mode != 4).ToArray();
            Assert.AreEqual(declared.Length, occurrences.Length, trainer.Value);
            Assert.IsTrue(occurrences.Any(row => ReferenceEquals(row, primary[trainer])), "Primary must be an actual registered occurrence.");
            foreach (var occurrence in occurrences)
            {
                var source = Definition(occurrence);
                var instruction = Instruction(occurrence);
                Assert.AreEqual(trainer, source.Trainer);
                Assert.IsTrue(identities.Add((trainer, instruction.Source.Symbol, instruction.Source.Line)), "Each call site binds once.");
                Assert.AreEqual(StoryOp.TrainerBattle, instruction.Op);
                Assert.AreEqual(source.Source.Path, instruction.Source.Path);
                Assert.AreEqual(source.ScriptEntry, instruction.Source.Symbol);
                Assert.IsTrue(content.FindProgram(source.ScriptEntry)!.Instructions.Contains(instruction));
                Assert.IsTrue(content.Maps.ContainsKey(Property<MapId>(occurrence, "Map")), "Every occurrence resolves a loaded map.");
            }
        }
        Assert.AreEqual(identities.Count, Property<int>(Property<object>(registry, "ByIdentity"), "Count"));

        // Miguel uses the older ForestTrainerBattleDefinition shape but is still no-intro.
        var miguel = primary[new("pret:TRAINER_SUPER_NERD_MIGUEL")];
        Assert.AreEqual("trainerbattle_no_intro", Instruction(miguel).Opcode);
        Assert.AreEqual(2, Instruction(miguel).Args.Length);
        var rocket = Rows(byTrainer[new("pret:TRAINER_TEAM_ROCKET_GRUNT_5")]);
        Assert.AreEqual(2, rocket.Length, "Direct conversation and coordinate trigger are distinct declared calls.");
        Assert.AreEqual("CeruleanCity_EventScript_Grunt", Definition(primary[new("pret:TRAINER_TEAM_ROCKET_GRUNT_5")]).ScriptEntry);
    }

    [TestMethod]
    public void ExactCompiledOccurrenceRejectsForgedOpcodeArgumentsAndLocation()
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var rows = TrainerEntries(Property<object>(Property<object>(session, "TrainerOccurrences"), "ByTrainer"));
        foreach (var (trainer, occurrences) in rows)
        foreach (var occurrence in Rows(occurrences))
        {
            var instruction = Instruction(occurrence);
            var effect = new StoryEffect(1, instruction.Opcode, instruction.Args, instruction.Source);
            Assert.AreSame(occurrence, Invoke(session, "RequireTrainerOccurrence", trainer, effect));
        }

        var selected = rows.SelectMany(entry => Rows(entry.Value)).First(row => Instruction(row).Opcode == "trainerbattle_single");
        var call = Instruction(selected);
        var id = Definition(selected).Trainer;
        var original = new StoryEffect(1, call.Opcode, call.Args, call.Source);
        StoryEffect[] forged =
        [
            original with { Opcode = "trainerbattle_no_intro" },
            original with { Args = original.Args.SetItem(0, "TRAINER_NOT_REGISTERED") },
            original with { Args = original.Args.SetItem(1, "Text_NotTheDeclaredIntro") },
            original with { Args = original.Args.Add("UNDECLARED_ARGUMENT") },
            original with { Source = original.Source with { Path = "data/maps/Other/scripts.inc" } },
            original with { Source = original.Source with { Symbol = original.Source.Symbol + "_Other" } },
            original with { Source = original.Source with { Line = original.Source.Line + 1 } }
        ];
        foreach (var effect in forged)
            Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "RequireTrainerOccurrence", id, effect));
        Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "RequireTrainerOccurrence", new TrainerId("pret:TRAINER_NOT_REGISTERED"), original));

        var sharedProgram = rows.SelectMany(entry => Rows(entry.Value)).GroupBy(row => Instruction(row).Source.Symbol)
            .First(group => group.Select(row => Definition(row).Trainer).Distinct().Count() > 1).ToArray();
        var first = Instruction(sharedProgram[0]);
        var second = Instruction(sharedProgram[1]);
        Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "RequireTrainerOccurrence", Definition(sharedProgram[0]).Trainer,
            new StoryEffect(1, first.Opcode, first.Args, first.Source with { Line = second.Source.Line })));
    }

    [TestMethod]
    [DataRow("TRAINER_LEADER_ERIKA")]
    [DataRow("TRAINER_LEADER_KOGA")]
    [DataRow("TRAINER_LEADER_SABRINA")]
    public void MigratedGymOccurrenceRetainsActorMapCallbackAndMusic(string symbol)
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var primary = TrainerEntries(Property<object>(Property<object>(session, "TrainerOccurrences"), "Primary"));
        var occurrence = primary[new("pret:" + symbol)];
        var link = Rows(Property<object>(occurrence, "Actors")).Single();
        var map = Property<MapId>(link, "Map");
        var localId = Property<string>(link, "LocalId");
        var sourceId = Property<int>(link, "SourceLocalId");
        var script = Property<string>(link, "Script");
        var actor = new CampaignActor(localId, sourceId, "unused", 0, 0, 0, Direction.South,
            "unused", script, "", true, false, 0, 0);
        var state = session.State with {
            World = session.State.World with { Player = session.State.World.Player with { MapId = map } },
            LastTalked = localId, Actors = session.State.Actors.SetItem(localId, actor),
            Script = session.State.Script with { Wait = new(StoryWaitKind.Battle, "gym-guard-test"),
                Variables = session.State.Script.Variables.SetItem("VAR_LAST_TALKED", sourceId) }
        };
        Invoke(session, "RequireTrainerActorOccurrence", state, occurrence);
        CampaignState[] forged = [
            state with { World = state.World with { Player = state.World.Player with { MapId = new("map.pallet-town") } } },
            state with { LastTalked = "unregistered-actor" },
            state with { Actors = state.Actors.SetItem(localId, actor with { Active = false }) },
            state with { Actors = state.Actors.SetItem(localId, actor with { SourceLocalId = sourceId + 1 }) },
            state with { Actors = state.Actors.SetItem(localId, actor with { ScriptId = script + "_Other" }) },
            state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_LAST_TALKED", sourceId + 1) } }
        ];
        foreach (var candidate in forged)
            Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "RequireTrainerActorOccurrence", candidate, occurrence));
        var instruction = Instruction(occurrence);
        Assert.AreEqual(1, Definition(occurrence).Mode);
        Assert.AreEqual("NO_MUSIC", instruction.Args[4]);
        var original = new StoryEffect(1, instruction.Opcode, instruction.Args, instruction.Source);
        foreach (int index in new[] { 1, 2, 3, 4 })
            Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "RequireTrainerOccurrence", Definition(occurrence).Trainer,
                original with { Args = original.Args.SetItem(index, "NotTheDeclaredArgument") }));
    }

    [TestMethod]
    public void SettledHistoryUsesTrainerBusinessIdentityAcrossEquivalentCallSites()
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var registry = Property<object>(session, "TrainerOccurrences");
        var rows = TrainerEntries(Property<object>(registry, "ByTrainer"));
        var bindings = TrainerEntries(Property<object>(session, "OrdinaryTrainerBindings"));
        var equivalent = rows.Where(entry =>
        {
            var modes = Rows(entry.Value).Select(row => Definition(row).Mode).ToArray();
            return modes.Contains(2) && modes.Contains(3);
        }).ToArray();
        Assert.IsTrue(equivalent.Length > 0, "The generated catalog must include normal/quiz equivalents.");

        foreach (var (trainer, occurrences) in equivalent)
        {
            var history = new CampaignForestTrainerHistory("forest-trainer-battle:1", trainer.Value,
                WildBattleOutcome.Won, 0, 0, [0]);
            Assert.IsTrue((bool)Invoke(session, "RegisteredTrainerHistoryMatches", history), "Legacy null coordinates retain the same trainer/party identity.");
            foreach (var occurrence in Rows(occurrences))
            {
                var instruction = Instruction(occurrence);
                Assert.IsTrue((bool)Invoke(session, "RegisteredTrainerHistoryMatches", history with
                { SourceScriptEntry = instruction.Source.Symbol, SourceScriptLine = instruction.Source.Line }));
            }
            Assert.IsTrue((bool)Invoke(session, "RegisteredTrainerHistoryMatches", history with
            { SourceScriptEntry = "diagnostic-only:old-call", SourceScriptLine = -1 }), "Diagnostic coordinates are not settled-battle authority.");
            Assert.IsTrue((bool)Invoke(session, "RegisteredTrainerHistoryMatches", history with
            { SourceScriptEntry = "diagnostic-only:old-call" }), "Partial legacy diagnostic metadata does not change business identity.");
        }

        var unknown = new CampaignForestTrainerHistory("forest-trainer-battle:1", "pret:TRAINER_NOT_REGISTERED",
            WildBattleOutcome.Won, 0, 0, [0]);
        Assert.IsFalse((bool)Invoke(session, "RegisteredTrainerHistoryMatches", unknown));
        var rematchOnly = bindings.Where(entry => Sources(entry.Value).Length == 0).Select(entry => entry.Key).ToArray();
        Assert.IsTrue(rematchOnly.Length > 0, "Current Vs. Seeker supplies parties without ordinary occurrences.");
        foreach (var trainer in rematchOnly)
            Assert.IsFalse((bool)Invoke(session, "RegisteredTrainerHistoryMatches", unknown with { TrainerId = trainer.Value }));
    }

    [TestMethod]
    [DataRow("CeladonCity_Gym_")]
    [DataRow("FuchsiaCity_Gym_")]
    [DataRow("SaffronCity_Gym_")]
    [DataRow("Route19_")]
    [DataRow("SaffronCity_Dojo_")]
    public void CompositionRejectsMutatedModeAndBattleFlags(string scriptPrefix)
    {
        foreach (var mode in new[] { 1, 2, 3, 4, 9 })
            RejectMutation(mode, 8);
        foreach (uint flags in new uint[] { 0, 9, 24 })
            RejectMutation(0, flags);

        void RejectMutation(int mode, uint flags)
        {
            var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
            var bindings = TrainerEntries(Property<object>(session, "OrdinaryTrainerBindings"));
            var source = bindings.Values.SelectMany(Sources).First(candidate =>
            {
                var definition = Property<ForestTrainerBattleDefinition>(candidate, "Definition");
                return definition.Mode == 0 && definition.ScriptEntry.StartsWith(scriptPrefix, StringComparison.Ordinal);
            });
            var original = Property<ForestTrainerBattleDefinition>(source, "Definition");
            // Mutate the session-local projected row, retaining the exact compiled macro and args.
            source.GetType().GetProperty("Definition", InstanceMembers)!.SetValue(source,
                original with { Mode = mode, BattleTypeFlags = flags });
            var error = Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "BindTrainerOccurrences"));
            StringAssert.StartsWith(error.Message, "trainer_occurrence_semantics_mismatch:");
        }
    }

    [TestMethod]
    public void CompositionRejectsMissingProgramsInsteadOfRegionalScriptAllowlists()
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var bindings = TrainerEntries(Property<object>(session, "OrdinaryTrainerBindings"));
        var source = bindings.Values.SelectMany(Sources).First(candidate =>
            Property<ForestTrainerBattleDefinition>(candidate, "Definition").Mode == 0);
        var original = Property<ForestTrainerBattleDefinition>(source, "Definition");
        source.GetType().GetProperty("Definition", InstanceMembers)!.SetValue(source,
            original with { ScriptEntry = "MissingMap_EventScript_MissingTrainer" });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "BindTrainerOccurrences"));
        StringAssert.StartsWith(error.Message, "trainer_occurrence_program_missing:");
    }

    [TestMethod]
    [DataRow("ViridianForest_EventScript_Rick")]
    [DataRow("Route3_EventScript_Ben")]
    [DataRow("CeladonCity_Gym_EventScript_Tamia")]
    public void CompositionAuthenticatesDeclaredPostTextAgainstActualProgram(string script)
    {
        foreach (var replacement in new[] { "", " ", "Text_NotDeclaredByTheProgram" })
        {
            var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
            var bindings = TrainerEntries(Property<object>(session, "OrdinaryTrainerBindings"));
            var source = bindings.Values.SelectMany(Sources).First(candidate =>
                Property<ForestTrainerBattleDefinition>(candidate, "Definition").ScriptEntry == script);
            var original = Property<ForestTrainerBattleDefinition>(source, "Definition");
            source.GetType().GetProperty("Definition", InstanceMembers)!.SetValue(source,
                original with { PostBattleText = replacement });
            var error = Assert.ThrowsExactly<InvalidDataException>(() => Invoke(session, "BindTrainerOccurrences"));
            StringAssert.StartsWith(error.Message, "trainer_post_dialogue_binding:");
        }
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("duplicate")]
    [DataRow("substituted-text")]
    [DataRow("wrong-autoclose-mode")]
    [DataRow("wrong-op")]
    [DataRow("wrong-opcode")]
    [DataRow("extra-argument")]
    [DataRow("before-battle")]
    public void CompositionRejectsMalformedActualPostDialogue(string mutation)
    {
        const string script = "ViridianForest_EventScript_Rick";
        var original = CurrentCampaignFixture.Catalog;
        var program = original.FindProgram(script)!;
        var index = Enumerable.Range(0, program.Instructions.Length)
            .Single(i => program.Instructions[i].Opcode == "msgbox");
        var dialogue = program.Instructions[index];
        var instructions = mutation switch
        {
            "missing" => program.Instructions.RemoveAt(index),
            "duplicate" => program.Instructions.Insert(index + 1, dialogue),
            "substituted-text" => program.Instructions.SetItem(index,
                dialogue with { Args = dialogue.Args.SetItem(0, "ViridianForest_Text_RickIntro") }),
            "wrong-autoclose-mode" => program.Instructions.SetItem(index,
                dialogue with { Args = dialogue.Args.SetItem(1, "MSGBOX_NPC") }),
            "wrong-op" => program.Instructions.SetItem(index, dialogue with { Op = StoryOp.Message }),
            "wrong-opcode" => program.Instructions.SetItem(index, dialogue with { Opcode = "message" }),
            "extra-argument" => program.Instructions.SetItem(index,
                dialogue with { Args = dialogue.Args.Add("UNDECLARED_ARGUMENT") }),
            "before-battle" => program.Instructions.RemoveAt(index).Insert(0, dialogue),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        // The immutable script override is private to this composition. Other tests retain
        // the original generated programs, definitions and source assets.
        var copy = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", InstanceMembers)!.Invoke(original, null)!;
        var field = typeof(PretCampaignCatalog).GetField("_scripts", InstanceMembers)!;
        var compilation = (PretStoryCompilation)field.GetValue(original)!;
        field.SetValue(copy, compilation with {
            Programs = compilation.Programs.SetItem(script, program with { Instructions = instructions })
        });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => CampaignSession.CreateCurrent(copy));
        StringAssert.StartsWith(error.Message, "trainer_post_dialogue_binding:");
    }

    [TestMethod]
    public void CallbackModesFollowCompiledMacroMusicSemantics()
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var primary = TrainerEntries(Property<object>(Property<object>(session, "TrainerOccurrences"), "Primary"));
        Assert.AreEqual(2, Definition(primary[new("pret:TRAINER_GENTLEMAN_TUCKER")]).Mode);
        Assert.AreEqual(1, Definition(primary[new("pret:TRAINER_LEADER_LT_SURGE")]).Mode);
        foreach (string symbol in new[] { "TRAINER_TEAM_ROCKET_ADMIN", "TRAINER_TEAM_ROCKET_ADMIN_2", "TRAINER_SCIENTIST_GIDEON" })
            Assert.AreEqual(2, Definition(primary[new("pret:" + symbol)]).Mode);
    }

    [TestMethod]
    public void NewCurrentGameRoundtripsAfterRegistryComposition()
    {
        var content = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(content, new(0x11223344));
        _ = Property<object>(session, "TrainerOccurrences");
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var saved = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, saved.State, saved.Generation);
        _ = Property<object>(restored, "TrainerOccurrences");
        Assert.AreEqual(session.Revision, restored.Revision);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(restored.State));
        var movement = restored.Execute("registered-trainer-new-game", restored.Revision, new("move", Direction.East));
        Assert.AreEqual(CommandStatus.Accepted, movement.Status, movement.Error);
        Assert.IsNull(restored.ValidatePlayerSave(restored.State));
    }
}

using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Application.Story;

[TestClass]
public sealed class SourceDoubleTrainerCompilerTests
{
    [TestMethod]
    [DataRow("data/scripts/trainers.inc")]
    [DataRow("data/maps/AnotherMap/scripts.inc")]
    public void DoubleMacrosCompileByShapeWithoutMapOrTrainerNameLists(string path)
    {
        const string script = "AnotherMap_EventScript_Pair";
        const string first = "trainerbattle_double TRAINER_SOURCE_DEFINED_PAIR, PairIntro, PairDefeat, PairNotEnoughMons";
        const string rematch = "trainerbattle_rematch_double TRAINER_SOURCE_DEFINED_PAIR, PairRematchIntro, PairDefeat, PairNotEnoughMons";
        var source = new PretSourceScriptFile(path,
            $"{script}::\n\t{first}\n\tend\n{script}Rematch::\n\t{rematch}\n\tend\n",
            new(SourceKind.Pret, path, script, "pret/pokefirered", "037335f4c725d7c9aecdac87066f2002b4bd7e14"));
        var compiled = PretStoryCompiler.Compile([source], includePresetDoubleOpcodes: true);
        Assert.AreEqual(0, compiled.UnsupportedInstructions.Length);
        foreach (string name in new[] { script, script + "Rematch" })
        {
            var instruction = compiled.FindProgram(name)!.Instructions[0];
            Assert.AreEqual(StoryOp.TrainerBattle, instruction.Op);
            Assert.AreEqual(4, instruction.Args.Length);
            Assert.AreEqual("TRAINER_SOURCE_DEFINED_PAIR", instruction.Args[0]);
            Assert.AreEqual(path, instruction.Source.Path);
            Assert.AreEqual(name, instruction.Source.Symbol);
        }
        Assert.AreEqual(StoryOp.Unsupported, PretStoryCompiler.Compile([source]).FindProgram(script)!.Instructions[0].Op);
        Assert.Throws<ContentValidationException>(() => PretStoryCompiler.Compile([
            source with { Text = source.Text.Replace(", PairNotEnoughMons", "", StringComparison.Ordinal) }
        ], includePresetDoubleOpcodes: true));
    }
}

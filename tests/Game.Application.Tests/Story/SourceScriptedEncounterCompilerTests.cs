using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Events;

namespace FireRed.Tests.Application.Story;

[TestClass]
public sealed class SourceScriptedEncounterCompilerTests
{
    private const string Path = "data/maps/MtEmber_Summit/scripts.inc";
    private const string Script = "MtEmber_Summit_EventScript_Moltres";
    private const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    private static PretSourceScriptFile Source(string? text = null) => new(Path,
        text ?? File.ReadAllText(System.IO.Path.Combine(TestAssets.Source, Path)),
        new(SourceKind.Pret, Path, "MAP_MT_EMBER_SUMMIT", "pret/pokefirered", Commit));

    [TestMethod]
    public void PinnedLegendaryScriptCompilesThroughSharedDefinitionWithoutARegionSwitch()
    {
        var source = Source();
        var compiled = PretStoryCompiler.Compile([source]);
        Assert.AreEqual(0, compiled.UnsupportedInstructions.Length);
        var definition = SourceScriptedEncounterRegistry.Definitions.Single(d => d.ScriptId == Script);
        var instructions = compiled.FindProgram(Script)!.Instructions;
        var set = instructions.Single(i => i.Opcode == "setwildbattle");
        Assert.AreEqual(StoryOp.Special, set.Op);
        CollectionAssert.AreEqual(new[] { definition.Species.Value[5..], definition.Level.ToString() }, set.Args.ToArray());
        var battle = instructions.Single(i => i.Opcode == "special" && i.Args[0] == "StartLegendaryBattle");
        Assert.AreEqual(StoryOp.TrainerBattle, battle.Op);
        Assert.IsTrue(instructions.IndexOf(instructions.Single(i => i.Op == StoryOp.WaitButtonPress)) < instructions.IndexOf(battle));
        foreach (var instruction in instructions)
        {
            Assert.AreEqual(Path, instruction.Source.Path);
            Assert.AreEqual(Script, instruction.Source.Symbol);
            StringAssert.StartsWith(source.Text.Replace("\r\n", "\n").Split('\n')[instruction.Source.Line - 1].Trim(), instruction.Opcode);
        }
        Assert.IsNotNull(compiled.FindProgram(definition.ResumeScript));
        Assert.IsNotNull(compiled.FindProgram(definition.OutcomeScript));
    }

    [TestMethod]
    public void FixedEncounterRequiresItsExactSourceIdentityAndCaller()
    {
        var source = Source();
        PretSourceScriptFile[] forged = [
            source with { Path = "data/maps/Elsewhere/scripts.inc" },
            source with { SourceRef = source.SourceRef with { Path = "data/maps/Elsewhere/scripts.inc" } },
            source with { SourceRef = source.SourceRef with { Kind = SourceKind.OriginalFixture } },
            source with { SourceRef = source.SourceRef with { Repository = "other/repository" } },
            source with { SourceRef = source.SourceRef with { Commit = new string('0', 40) } },
            source with { Text = source.Text.Replace(Script + "::", "MtEmber_Summit_EventScript_Other::", StringComparison.Ordinal) }
        ];
        foreach (var file in forged)
        {
            var compiled = PretStoryCompiler.Compile([file]);
            var entries = compiled.Programs.Values.SelectMany(p => p.Instructions).Where(i =>
                i.Opcode == "setwildbattle" || i.Opcode == "special" && i.Args[0] == "StartLegendaryBattle").ToArray();
            Assert.AreEqual(2, entries.Length);
            Assert.IsTrue(entries.All(i => i.Op == StoryOp.Unsupported));
        }
    }

    [TestMethod]
    public void FixedEncounterRejectsChangedSpeciesLevelAndBattleKind()
    {
        var source = Source();
        foreach (var replacement in new[] { "SPECIES_MOLTRES, 51", "SPECIES_ZAPDOS, 50", "SPECIES_MOLTRES" })
            Assert.Throws<InvalidDataException>(() => PretStoryCompiler.Compile([
                source with { Text = source.Text.Replace("SPECIES_MOLTRES, 50", replacement, StringComparison.Ordinal) }]));
        var changed = source with { Text = source.Text.Replace("special StartLegendaryBattle", "dowildbattle", StringComparison.Ordinal) };
        Assert.AreEqual(StoryOp.Unsupported, PretStoryCompiler.Compile([changed]).FindProgram(Script)!.Instructions
            .Single(i => i.Opcode == "dowildbattle").Op);
    }
}

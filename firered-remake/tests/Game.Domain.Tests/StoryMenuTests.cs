using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class StoryMenuTests
{
    private const string MenuId = "MULTICHOICE_YES_NO_INFO";
    private static readonly StoryMenuDefinition ThreeOptions = new(MenuId, 7,
        [new("Text_Yes", "Yes"), new("Text_No", "No"), new("Text_Info", "Info")], 5);

    [TestMethod]
    [DataRow("data/maps/TrainerTower_Lobby/scripts.inc", "Original", "0", "0")]
    [DataRow("data/maps/MyNewTown/scripts.inc", "NewNpc", "11", "6")]
    public void DefinedMenuThirdChoiceFollowsScriptRegardlessOfLocation(string path, string symbol, string left, string top)
    {
        var catalog = Catalog(Menu("multichoice", [left, top, MenuId, "FALSE"], path, symbol));
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        Assert.AreEqual(TransitionStatus.Accepted, start.Status);
        Assert.AreEqual(StoryWaitKind.MenuChoice, start.State.Wait!.Kind);
        Assert.AreEqual(path, start.Effects.Single().Source.Path);
        var result = StoryRules.Continue(start.State, start.State.Wait.CorrelationId, catalog, 2);
        Assert.AreEqual(TransitionStatus.Accepted, result.Status);
        Assert.AreEqual(2, result.State.Variables["VAR_RESULT"]);
        Assert.AreEqual(2, result.State.Variables["VAR_CHOSEN"]);
        Assert.IsTrue(result.State.Completed);
    }

    [TestMethod]
    [DataRow("FALSE", true)]
    [DataRow("TRUE", false)]
    [DataRow("0", true)]
    [DataRow("1", false)]
    [DataRow("2", true)]
    [DataRow("3", false)]
    public void CancellationUsesInstructionFlags(string flags, bool allowed)
    {
        var catalog = Catalog(Menu("multichoice", ["0", "0", MenuId, flags]));
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        Assert.AreEqual(TransitionStatus.Accepted, start.Status);
        var result = StoryRules.Continue(start.State, start.State.Wait!.CorrelationId, catalog, 127);
        Assert.AreEqual(allowed ? TransitionStatus.Accepted : TransitionStatus.Rejected, result.Status);
        if (allowed) Assert.AreEqual(127, result.State.Variables["VAR_CHOSEN"]);
        else Assert.AreSame(start.State, result.State);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    [DataRow(126)]
    [DataRow(128)]
    [DataRow(null)]
    public void InvalidAnswersDoNotAdvanceOrEmitEffects(int? choice)
    {
        var catalog = Catalog(Menu("multichoice", ["0", "0", MenuId, "FALSE"]));
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        var result = StoryRules.Continue(start.State, start.State.Wait!.CorrelationId, catalog, choice);
        Assert.AreEqual(TransitionStatus.Rejected, result.Status);
        Assert.AreEqual("event_menu_choice_required", result.Diagnostic!.Code);
        Assert.AreSame(start.State, result.State);
        Assert.IsTrue(result.Effects.IsEmpty);
    }

    [TestMethod]
    public void DefaultCursorDoesNotLimitAvailableChoices()
    {
        var instruction = Menu("multichoicedefault", ["0x0B", "6", MenuId, "2", "TRUE"]);
        var catalog = Catalog(instruction);
        var menu = StoryMenuRules.Resolve(instruction, catalog)!;
        Assert.AreEqual(2, menu.DefaultChoice);
        Assert.AreEqual(11, menu.Left);
        Assert.IsTrue(menu.IgnoreB);
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        var result = StoryRules.Continue(start.State, start.State.Wait!.CorrelationId, catalog, 0);
        Assert.AreEqual(TransitionStatus.Accepted, result.Status);
        Assert.AreEqual(0, result.State.Variables["VAR_CHOSEN"]);
    }

    [TestMethod]
    public void InvalidDefinitionsAndOperandsFaultBeforeMenuIsPresented()
    {
        var valid = Menu("multichoice", ["0", "0", MenuId, "FALSE"]);
        var cases = new[]
        {
            Catalog(valid, ThreeOptions with { Options = [] }),
            Catalog(valid, ThreeOptions with { Options = default }),
            Catalog(valid, ThreeOptions with { Options = [new("Text", " ")] }),
            Catalog(valid, ThreeOptions with { Id = "another_menu" }),
            Catalog(valid, ThreeOptions with { SourceId = 8 }),
            Catalog(valid with { Args = ["-1", "0", MenuId, "FALSE"] }),
            Catalog(valid with { Args = ["0", "256", MenuId, "FALSE"] }),
            Catalog(valid with { Args = ["0", "0", MenuId, "UNKNOWN_FLAG"] }),
            Catalog(valid with { Args = ["0", "0", MenuId] }),
            Catalog(Menu("multichoicedefault", ["0", "0", MenuId, "3", "FALSE"])),
            Catalog(Menu("multichoicedefault", ["0", "0", MenuId, "-1", "FALSE"]))
        };
        foreach (var catalog in cases)
        {
            var original = StoryState.Empty;
            var result = StoryRules.Start(original, "menu", catalog);
            Assert.AreEqual(TransitionStatus.Fault, result.Status);
            Assert.AreEqual("event_menu_invalid", result.Diagnostic!.Code);
            Assert.AreSame(original, result.State);
            Assert.IsTrue(result.Effects.IsEmpty);
        }
    }

    [TestMethod]
    public void DefinitionOwnsItsRangeEvenAtHistoricalThreeChoiceLocation()
    {
        var instruction = Menu("multichoice", ["0", "0", MenuId, "FALSE"], "data/maps/TrainerTower_Lobby/scripts.inc");
        var catalog = Catalog(instruction, ThreeOptions with { Options = ThreeOptions.Options[..2] });
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        var result = StoryRules.Continue(start.State, start.State.Wait!.CorrelationId, catalog, 2);
        Assert.AreEqual(TransitionStatus.Rejected, result.Status);
    }

    [TestMethod]
    public void NativeTowerAndFerryMenusWithoutDefinitionsKeepTheirExistingDispatch()
    {
        var tower = Menu("multichoice", ["0", "0", MenuId, "FALSE"], "data/maps/TrainerTower_Lobby/scripts.inc");
        var ferry = new StoryInstruction(StoryOp.WaitState, "waitstate", [],
            new("data/scripts/seagallop.inc", "EventScript_SeviiDestinationsPage1", 1));
        foreach (var (instruction, choice) in new[] { (tower, 2), (ferry, 5) })
        {
            var catalog = Catalog(instruction);
            catalog.Definition = null;
            var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
            // The native ferry workflow replaces its presentation wait with its menu callback.
            var waiting = start.State with { Wait = start.State.Wait! with { Kind = StoryWaitKind.MenuChoice } };
            var result = StoryRules.Continue(waiting, waiting.Wait!.CorrelationId, catalog, choice);
            Assert.AreEqual(TransitionStatus.Accepted, result.Status);
            Assert.AreEqual(choice, result.State.Variables["VAR_CHOSEN"]);
        }
    }

    [TestMethod]
    public void MissingDefinitionDoesNotGloballyWidenUnknownMenu()
    {
        var catalog = Catalog(Menu("multichoice", ["0", "0", MenuId, "FALSE"]));
        catalog.Definition = null;
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        var result = StoryRules.Continue(start.State, start.State.Wait!.CorrelationId, catalog, 2);
        Assert.AreEqual(TransitionStatus.Rejected, result.Status);
    }

    [TestMethod]
    public void GridKeepsItsOwnColumnValidationAndCancellation()
    {
        var instruction = Menu("multichoicegrid", ["0", "0", MenuId, "3", "TRUE"]);
        var catalog = Catalog(instruction);
        Assert.IsNull(StoryMenuRules.Resolve(instruction, catalog));
        var start = StoryRules.Start(StoryState.Empty, "menu", catalog);
        Assert.AreEqual(TransitionStatus.Accepted, start.Status);
        var result = StoryRules.Continue(start.State, start.State.Wait!.CorrelationId, catalog, 2);
        Assert.AreEqual(TransitionStatus.Accepted, result.Status);
        result = StoryRules.Continue(start.State, start.State.Wait.CorrelationId, catalog, 127);
        Assert.AreEqual(TransitionStatus.Rejected, result.Status);
        var invalid = Catalog(instruction with { Args = ["0", "0", MenuId, "2", "FALSE"] });
        Assert.AreEqual("event_grid_menu_invalid", StoryRules.Start(StoryState.Empty, "menu", invalid).Diagnostic!.Code);
    }

    private static StoryInstruction Menu(string opcode, ImmutableArray<string> args,
        string path = "data/maps/MyNewTown/scripts.inc", string symbol = "NewNpc") =>
        new(StoryOp.Multichoice, opcode, args, new(path, symbol, 1));

    private static MenuCatalog Catalog(StoryInstruction instruction, StoryMenuDefinition? definition = null) =>
        new(new("menu", [instruction,
            new(StoryOp.CopyVar, "copyvar", ["VAR_CHOSEN", "VAR_RESULT"], instruction.Source),
            new(StoryOp.End, "end", [], instruction.Source)]), definition ?? ThreeOptions);

    private sealed class MenuCatalog(StoryProgram program, StoryMenuDefinition? definition) : IStoryCatalog
    {
        public StoryMenuDefinition? Definition { get; set; } = definition;
        public StoryProgram? FindProgram(string id) => id == program.Id ? program : null;
        public StoryMenuDefinition? FindMenu(string id) => id == MenuId ? Definition : null;
        public bool TryConstant(string symbol, out int value)
        {
            value = symbol switch { MenuId => 7, "FALSE" => 0, "TRUE" => 1, "SCR_MENU_CANCEL" => 127, _ => -1 };
            return value >= 0;
        }
    }
}

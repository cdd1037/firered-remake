using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class StoryListMenuTests
{
    private static StoryInstruction Instruction(StoryOp op, string opcode, params string[] args) =>
        new(op, opcode, [.. args], new("data/maps/UnrelatedRoom/scripts.inc", "menu", 1));

    private sealed class Catalog : IStoryCatalog
    {
        public StoryMenuDefinition Definition { get; set; } = new("LISTMENU_TOPICS", 73,
            [new("A", "First"), new("B", "Second"), new("C", "Exit")], 6)
        { ListLayout = new(1, 1, 2, 4, true) };
        public bool Enabled { get; set; } = true;
        public StoryProgram Program { get; set; } = new("menu", [
            Instruction(StoryOp.SetVar, "setvar", "VAR_0x8004", "73"),
            Instruction(StoryOp.Special, "special", "ListMenu"),
            Instruction(StoryOp.WaitState, "waitstate"),
            Instruction(StoryOp.Dialog, "msgbox", "TOPIC"),
            Instruction(StoryOp.Special, "special", "ReturnToListMenu"),
            Instruction(StoryOp.WaitState, "waitstate"),
            Instruction(StoryOp.End, "end")]);
        public StoryProgram? FindProgram(string id) => id == "menu" ? Program : null;
        public StoryMenuDefinition? FindMenu(string id) => Enabled && id == Definition.Id ? Definition : null;
        public StoryMenuDefinition? FindListMenu(int sourceId) => Enabled && sourceId == 73 ? Definition : null;
        public bool TryConstant(string symbol, out int value) { value = 0; return false; }
    }

    private static StoryTransition Next(StoryTransition current, Catalog catalog, int? choice = null) =>
        StoryRules.Continue(current.State, current.State.Wait!.CorrelationId, catalog, choice);

    [TestMethod]
    public void CatalogListUsesOriginalSpecialWaitCallbackAndResumesSelectedTopicAtAnyMap()
    {
        var catalog = new Catalog();
        var opened = StoryRules.Start(StoryState.Empty, "menu", catalog);
        Assert.AreEqual(StoryWaitKind.NativeEffect, opened.State.Wait!.Kind);
        Assert.AreEqual("ListMenu", opened.Effects.Single().Args.Single());
        var menu = Next(opened, catalog);
        Assert.AreEqual(StoryWaitKind.MenuChoice, menu.State.Wait!.Kind);
        Assert.AreEqual(0, StoryMenuRules.Pending(menu.State, catalog)!.DefaultChoice);
        var topic = Next(menu, catalog, 1);
        Assert.AreEqual(StoryWaitKind.Dialog, topic.State.Wait!.Kind);
        Assert.IsNull(StoryMenuRules.Pending(topic.State, catalog));
        Assert.AreEqual(1, StoryListMenuRules.Display(topic.State, catalog)!.DefaultChoice);
        Assert.IsTrue(topic.State.ListMenu!.Suspended);
        var returned = Next(Next(topic, catalog), catalog);
        Assert.AreEqual(StoryWaitKind.MenuChoice, returned.State.Wait!.Kind);
        Assert.AreEqual(1, StoryMenuRules.Pending(returned.State, catalog)!.DefaultChoice);
        var done = Next(returned, catalog, 2);
        Assert.IsTrue(done.State.Completed);
        Assert.IsNull(done.State.ListMenu);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(127)]
    public void ExitAndBRemoveWindowBeforeFollowingScript(int choice)
    {
        var catalog = new Catalog();
        var menu = Next(StoryRules.Start(StoryState.Empty, "menu", catalog), catalog);
        var result = Next(menu, catalog, choice);
        Assert.AreEqual(choice, result.State.Variables["VAR_RESULT"]);
        Assert.IsNull(result.State.ListMenu);
        Assert.IsNull(StoryListMenuRules.Display(result.State, catalog));
    }

    [TestMethod]
    [DataRow(-1)] [DataRow(3)] [DataRow(126)] [DataRow(128)] [DataRow(null)]
    public void InvalidListAnswersAreAtomic(int? choice)
    {
        var catalog = new Catalog();
        var menu = Next(StoryRules.Start(StoryState.Empty, "menu", catalog), catalog);
        var result = Next(menu, catalog, choice);
        Assert.AreEqual(TransitionStatus.Rejected, result.Status);
        Assert.AreSame(menu.State, result.State);
        Assert.IsTrue(result.Effects.IsEmpty);
    }

    [TestMethod]
    public void UnselectedNativeListStaysOutsideGenericAdapter()
    {
        var catalog = new Catalog { Enabled = false };
        var opened = StoryRules.Start(StoryState.Empty, "menu", catalog);
        Assert.IsNull(opened.State.ListMenu);
        var callback = Next(opened, catalog);
        Assert.AreEqual(StoryWaitKind.Presentation, callback.State.Wait!.Kind);
        Assert.IsNull(StoryMenuRules.Pending(callback.State, catalog));
    }
}

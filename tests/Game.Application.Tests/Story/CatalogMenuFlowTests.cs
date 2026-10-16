using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;

namespace FireRed.Tests.Application.Story;

[TestClass, TestCategory("Game")]
public sealed class CatalogMenuFlowTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static CampaignState Basis => CurrentCampaignFixture.Checkpoint.State with
    { Script = StoryState.Empty, Dialogue = null, PresentationEffects = [], Motions = [], EntryPrograms = [] };
    private static CampaignState Call(CampaignSession session, string method, params object[] args) =>
        (CampaignState)typeof(CampaignSession).GetMethod(method, Private)!.Invoke(session, args)!;
    private static void Install(CampaignSession session, PretCampaignCatalog catalog, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, catalog.ContentVersion));
    private static CampaignSession Open(PretCampaignCatalog catalog, CampaignState basis, string program)
    {
        var session = CampaignSession.CreateCurrent(catalog);
        Install(session, catalog, basis);
        var entered = Call(session, "StartStory", basis, program); // Includes real all-choice preflight.
        Install(session, catalog, Call(session, "Pump", entered));
        return session;
    }
    private static void Ack(CampaignSession session, int? choice = null)
    {
        var result = session.Execute("menu:" + session.Revision, session.Revision, new("ack", Choice: choice));
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
    }
    private static PretCampaignCatalog WithProgram(StoryProgram program)
    {
        // Isolated immutable script override; never mutate the shared catalog used by other tests.
        var original = CurrentCampaignFixture.Catalog;
        var copy = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        var field = typeof(PretCampaignCatalog).GetField("_scripts", Private)!;
        var scripts = (PretStoryCompilation)field.GetValue(original)!;
        field.SetValue(copy, scripts with { Programs = scripts.Programs.SetItem(program.Id, program) });
        return copy;
    }
    private static StoryProgram Ordinary(string path, string left = "11")
    {
        var source = new StorySource(path, "TestMenu", 1);
        return new("TestMenu", [
            new(StoryOp.Multichoice, "multichoice", [left, "2", "MULTICHOICE_YES_NO_INFO", "FALSE"], source),
            new(StoryOp.CopyVar, "copyvar", ["VAR_MENU_TEST", "VAR_RESULT"], source),
            new(StoryOp.End, "end", [], source)]);
    }

    [TestMethod]
    [DataRow("data/maps/MyNewTown/scripts.inc", "11")]
    [DataRow("data/maps/AnotherTown/scripts.inc", "3")]
    public void OrdinaryMenuAtAnotherLocationHasReachableThirdChoice(string path, string left)
    {
        var catalog = WithProgram(Ordinary(path, left));
        var session = Open(catalog, Basis, "TestMenu");
        Assert.AreEqual(3, session.SourceMenu!.Definition.Options.Length);
        Assert.AreEqual(int.Parse(left), session.SourceMenu.Left);
        var before = session.State;
        var invalid = session.Execute("invalid", session.Revision, new("ack", Choice: 3));
        Assert.AreNotEqual(CommandStatus.Accepted, invalid.Status);
        Assert.AreSame(before, session.State);
        Ack(session, 2);
        Assert.AreEqual(2, session.State.Script.Variables["VAR_MENU_TEST"]);
        Assert.IsNull(session.SourceMenu);
    }

    [TestMethod]
    public void RealTowerInfoReturnsToCatalogMenuAndYesOpensFiveChoices()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var basis = Basis;
        basis = basis with { World = basis.World with { Player = basis.World.Player with { MapId = new("map.trainer-tower-lobby"), X = 9, Y = 7 } } };
        var session = Open(catalog, basis, "TrainerTower_Lobby_EventScript_AskEnterChallenge");
        for (int i = 0; i < 20 && session.SourceMenu is null; i++) Ack(session);
        Assert.AreEqual(3, session.SourceMenu!.Definition.Options.Length);
        Assert.IsNull(session.State.TrainerTowerMenu, "No duplicated ordinary-menu state");
        Ack(session, 2);
        Assert.IsTrue(session.State.Dialogue!.TextSymbol.Contains("ExplainTrainerTower"));
        for (int i = 0; i < 30 && session.SourceMenu is null; i++) Ack(session);
        Assert.AreEqual(3, session.SourceMenu!.Definition.Options.Length);
        Ack(session, 0);
        Assert.AreEqual(5, session.SourceMenu!.Definition.Options.Length);
        Ack(session, 4);
        Assert.IsTrue(session.State.Dialogue!.TextSymbol.Contains("PleaseVisitUsAgain"));
    }

    [TestMethod]
    public void NurseStillUsesNativeMenuAndHealingService()
    {
        var source = new StorySource("fixture/nurse.inc", "TestNurse", 1);
        var program = new StoryProgram("TestNurse", [
            new(StoryOp.Multichoice, "multichoice", ["19", "8", "MULTICHOICE_YES_NO", "FALSE | (TRUE << 1)"], source),
            new(StoryOp.Special, "special", ["HealPlayerParty"], source),
            new(StoryOp.End, "end", [], source)]);
        var catalog = WithProgram(program);
        var basis = Basis;
        basis = basis with { World = basis.World with { Player = basis.World.Player with { MapId = new("map.viridian-pokemon-center-1f"), X = 7, Y = 5 } },
            Party = basis.Party.SetItem(0, basis.Party[0] with { Pokemon = basis.Party[0].Pokemon with { CurrentHp = 1 } }) };
        var session = Open(catalog, basis, "TestNurse");
        Assert.IsNull(session.SourceMenu);
        Assert.AreEqual(StoryWaitKind.MenuChoice, session.State.Script.Wait!.Kind);
        Ack(session, 0);
        Assert.IsTrue(session.State.Party[0].Pokemon.CurrentHp > 1);
        Assert.IsTrue(session.State.Script.Completed);
    }

    [TestMethod]
    public void UnionRoomRemainsRejectedByPreflightEvenWithSharedMenuDefinition()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var basis = Basis;
        Install(session, catalog, basis);
        var error = Assert.Throws<TargetInvocationException>(() => Call(session, "StartStory", basis, "CableClub_EventScript_AskEnterUnionRoom"));
        Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        Assert.IsTrue(error.InnerException!.Message.Contains("unsupported") || error.InnerException.Message.Contains("center_"), error.InnerException.Message);
        Assert.AreSame(basis, session.State);
    }
}

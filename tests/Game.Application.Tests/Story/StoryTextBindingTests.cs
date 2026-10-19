using System.Reflection;
using Game.Application;

namespace FireRed.Tests.Application.Story;

[TestClass, TestCategory("Game")]
public sealed class StoryTextBindingTests
{
    [TestMethod]
    [DataRow("{STR_VAR_2}")]
    [DataRow("{PLAYER}")]
    [DataRow("{RIVAL}")]
    [DataRow("{KUN}")]
    [DataRow("{FONT_NORMAL}")]
    [DataRow("{PLAY_BGM}{MUS_LEVEL_UP}")]
    [DataRow("\\p")]
    [DataRow("$")]
    public void SourcePreparationAndBindingNeverReparseInsertedValues(string literalName)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        const string template = "First\n{FONT_NORMAL}{PLAYER}{KUN}|{RIVAL}|{STR_VAR_1}|{STR_VAR_2}|{UNKNOWN}\nLast";
        var state = session.State with
        {
            Dialogue = new("fixture:text-binding", [template], 0, false, false),
            TextBuffers = session.State.TextBuffers.SetItem("STR_VAR_1", literalName).SetItem("STR_VAR_2", "SECOND")
        };
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
                session.RulesVersion, catalog.ContentVersion, session.Revision));
        string bound = state.PlayerName + "|" + state.RivalName + "|" + literalName + "|SECOND|{UNKNOWN}";
        Assert.AreEqual("First\n{FONT_NORMAL}" + bound + "\nLast", session.VisibleText);
        int preparationCalls = 0;
        string Prepare(string source)
        {
            preparationCalls++;
            Assert.AreEqual(template, source, "Only the authored page reaches presentation control handling.");
            return source.Replace("{FONT_NORMAL}", "", StringComparison.Ordinal);
        }
        Assert.AreEqual("First\n" + bound + "\nLast", session.FormatVisibleText(Prepare));
        Assert.AreEqual(1, preparationCalls);
        Assert.AreEqual("First\n" + bound + "\nLast", CampaignSourceText.DisplayPage(template, state),
            "Battle result producers remove source cues before binding, just like ordinary field text.");
        Assert.AreEqual("First\n" + bound + "\nLast", session.FormatVisibleText(CampaignSourceText.PrepareDisplayPage));
        Assert.AreEqual(literalName + "|SECOND", CampaignSourceText.Bind("{FIRST}|{SECOND}",
            token => token switch { "FIRST" => literalName, "SECOND" => "SECOND", _ => null }));
        Assert.AreSame(state, session.State);
        Assert.AreEqual(template, session.State.Dialogue!.Pages[0]);
    }

    [TestMethod]
    public void NoDialogueDoesNotInvokeTemplatePreparation()
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        Assert.IsNull(session.State.Dialogue);
        Assert.IsNull(session.FormatVisibleText(_ => throw new InvalidOperationException("No template exists.")));
    }
}

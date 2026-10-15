using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Battle;
using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Application.Protocols;
[TestClass, TestCategory("Game")]
public sealed class DialogueTransactionTests : ProtocolFixture
{

    [TestMethod]
    public void dialogue_no_skipped_side_effects()
    {
        var source = new SourceRef(SourceKind.OriginalFixture, "fixture/scripts.inc", "fixture");
        var imported = PretDialogueImporter.Import("Talk::\n msgbox Text, MSGBOX_SIGN\n end\nSideEffect::\n setflag FLAG_X\n msgbox Text\n end", "Text::\n .string \"Hello {PLAYER}\\pNext page$\"", source, source);
        Assert.IsTrue(imported.Programs.Count == 1 && imported.UnsupportedScripts.Length == 1, "unsupported side effect was skipped");
        Assert.IsTrue(imported.Programs["Talk"].Pages.Length == 2, "pages not preserved");
    }

    [TestMethod]
    public void dialogue_transaction_and_save_boundary()
    {
        var map = new GridMap(new("fixture:lab"), 5, 5, Enumerable.Repeat(new TileCell(false), 25).ToImmutableArray());
        var dialogues = new Dictionary<string, ReadOnlyDialogue>
        {
            {
                "talk",
                new(new("script:talk"), new("text:talk"), ["Hi {PLAYER}", "Second"])
            }
        };
        var session = new ReferenceWorldSession(map, new(map.Id, 2, 2, 0, Direction.North), "content", _ => "talk", dialogues);
        var opened = session.Execute("open", 0, new("interact"));
        Assert.IsTrue(opened.Status == CommandStatus.Accepted && session.VisibleText == "Hi RED", "dialogue open failed");
        Assert.IsTrue(session.ValidateSave(session.State) == "save_event_active", "dialogue saved as player stable state");
        var blocked = session.Execute("move", 1, new("move", Direction.East));
        Assert.IsTrue(blocked.Status == CommandStatus.Rejected && session.State.World.StepCount == 0, "walk advanced during dialogue");
        session.Execute("next", 1, new("interact"));
        Assert.IsTrue(session.VisibleText == "Second", "page not advanced");
        session.Execute("close", 2, new("interact"));
        Assert.IsTrue(session.VisibleText is null && session.ValidateSave(session.State)is null, "dialogue not closed");
    }
}

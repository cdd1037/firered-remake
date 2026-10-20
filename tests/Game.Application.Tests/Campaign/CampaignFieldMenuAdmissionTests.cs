using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Options;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignFieldMenuAdmissionTests
{
    private const string OpenedFlag = "FLAG_OPENED_START_MENU";
    private const string SafariFlag = "FLAG_SYS_SAFARI_MODE";

    [TestMethod]
    public void NewGameMenuCommitsOnlyItsFlagAndStillSavesAndContinues()
    {
        var session = Fresh();
        var before = session.State;
        Assert.IsTrue(session.CanOpenFieldMenu);
        Assert.IsTrue(session.CanEditOptions);
        Assert.IsNull(session.ValidatePlayerSave(before));

        var result = session.Execute("start", session.Revision, new("field_start_menu"));

        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        Assert.AreEqual(1L, session.Revision);
        var expected = before with { Script = before.Script with {
            Flags = before.Script.Flags.SetItem(OpenedFlag, true) } };
        Assert.AreEqual(CanonicalJson.Hash(expected), CanonicalJson.Hash(session.State));
        Assert.IsTrue(session.CanOpenFieldMenu);
        Assert.IsNull(session.ValidatePlayerSave(session.State));

        var catalog = CurrentCampaignFixture.Catalog;
        var data = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(data, session.RulesVersion, catalog.ContentVersion,
            validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
        Assert.IsTrue(continued.CanOpenFieldMenu);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(continued.State));
    }

    [TestMethod]
    public void PublicBagRoundTripUsesTheSameMenuAdmissionWithoutChangingRejectedState()
    {
        var session = Fresh();
        Send(session, new("bag_open"));
        Assert.IsFalse(session.CanOpenFieldMenu);
        Assert.IsFalse(session.CanEditOptions);
        RejectUnchanged(session, new("field_start_menu"));
        Send(session, new("menu_back"));
        Assert.IsTrue(session.CanOpenFieldMenu);
        Assert.IsTrue(session.CanEditOptions);
        Send(session, new("field_start_menu"));
    }

    [TestMethod]
    [DataRow("script-running")]
    [DataRow("locked")]
    [DataRow("wait")]
    [DataRow("stack")]
    [DataRow("quest-log")]
    [DataRow("entry-program")]
    [DataRow("dialogue")]
    [DataRow("sign")]
    [DataRow("nickname")]
    [DataRow("picture")]
    [DataRow("black-screen")]
    [DataRow("hidden-player")]
    [DataRow("presentation-ticks")]
    [DataRow("motion")]
    [DataRow("arrival-facing")]
    [DataRow("bag")]
    [DataRow("pc-powered")]
    [DataRow("money-box")]
    [DataRow("town-map")]
    [DataRow("help-context")]
    [DataRow("safari-exit")]
    public void SharedCallbackBlockersRejectOrdinaryAndSafariStartWithoutMutation(string blocker)
    {
        foreach (bool safari in new[] { false, true })
        {
            var session = Fresh();
            var baseline = safari ? SafariState(session.State) : session.State;
            InstallControlledCallback(session, Block(baseline, blocker));
            Assert.IsFalse(session.CanOpenFieldMenu, $"{blocker}, Safari={safari}");
            Assert.IsFalse(session.CanEditOptions, $"Party/options must retain shared blocker {blocker}.");
            RejectUnchanged(session, new("field_start_menu"));
        }
    }

    [TestMethod]
    [DataRow("list-menu")]
    [DataRow("field-service")]
    [DataRow("oak-rating")]
    [DataRow("message-pointer")]
    [DataRow("berry-vendor")]
    [DataRow("white-screen")]
    [DataRow("trainer-records")]
    [DataRow("daisy")]
    [DataRow("diploma")]
    [DataRow("field-pickup")]
    public void OrdinaryWorkflowMarkersCannotBeBypassedWithDirectMenuCommand(string blocker)
    {
        var session = Fresh();
        InstallControlledCallback(session, Block(session.State, blocker));
        Assert.IsFalse(session.CanOpenFieldMenu, blocker);
        RejectUnchanged(session, new("field_start_menu"));
    }

    [TestMethod]
    public void ActiveSafariAllowsStartWhileTheIndependentSaveContractStillRejectsIt()
    {
        var session = Fresh();
        InstallControlledCallback(session, SafariState(session.State));
        var before = session.State;
        Assert.IsTrue(session.CanOpenFieldMenu);
        Assert.IsTrue(session.CanEditOptions);
        Assert.AreEqual("save_safari_game_active", session.ValidatePlayerSave(before));
        Send(session, new("field_start_menu"));
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Assert.AreEqual(before.SafariVisit, session.State.SafariVisit);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(OpenedFlag));
        Assert.AreEqual("save_safari_game_active", session.ValidatePlayerSave(session.State));
    }

    [TestMethod]
    [DataRow("not-active")]
    [DataRow("no-balls")]
    [DataRow("no-steps")]
    [DataRow("no-flag")]
    [DataRow("no-visit")]
    public void IncompleteSafariAdmissionDoesNotFallBackToOrdinaryMenu(string condition)
    {
        var session = Fresh();
        var safari = SafariState(session.State);
        safari = condition switch
        {
            "not-active" => safari with { SafariVisit = safari.SafariVisit! with { Active = false } },
            "no-balls" => safari with { SafariVisit = safari.SafariVisit! with { Balls = 0 } },
            "no-steps" => safari with { SafariVisit = safari.SafariVisit! with { Steps = 0 } },
            "no-flag" => safari with { Script = safari.Script with { Flags = safari.Script.Flags.Remove(SafariFlag) } },
            "no-visit" => safari with { SafariVisit = null, Script = safari.Script with {
                Flags = safari.Script.Flags.Remove(SafariFlag) } },
            _ => throw new AssertFailedException(condition)
        };
        InstallControlledCallback(session, safari);
        Assert.IsFalse(session.CanOpenFieldMenu);
        RejectUnchanged(session, new("field_start_menu"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RetainedElevatorDestinationIsNotAnActiveMenu(bool safari)
    {
        var session = Fresh();
        var state = safari ? SafariState(session.State) : session.State;
        var p = state.World.Player;
        var destination = new CampaignDepartmentElevator(p.MapId, 255, p.X, p.Y, p.Elevation, 0);
        InstallControlledCallback(session, state with { DepartmentElevator = destination });
        Assert.IsTrue(session.CanOpenFieldMenu, "A retained return destination is not a modal callback.");
        if (safari) return; // Safari keeps its existing, narrower callback contract.

        InstallControlledCallback(session, state with { DepartmentElevator = destination with { FloorWindowVisible = true } });
        Assert.IsFalse(session.CanOpenFieldMenu);
        RejectUnchanged(session, new("field_start_menu"));
        InstallControlledCallback(session, state with { DepartmentElevator = destination with { MenuDefault = 0 } });
        Assert.IsFalse(session.CanOpenFieldMenu);
        RejectUnchanged(session, new("field_start_menu"));
    }

    [TestMethod]
    public void PermanentTanobyReceiptDoesNotOwnFieldActionsButItsRunningScriptStillDoes()
    {
        var session = Fresh();
        // This is a callback-admission specimen, not fabricated endgame history.
        // The existing TanobyCompletionTests cover a fully valid completed player save.
        var completed = session.State with { TanobyCompletion = new([7, 1, 5, 2, 6, 3, 4]),
            Script = session.State.Script with { Flags = session.State.Script.Flags.SetItem(
                "FLAG_SYS_UNLOCKED_TANOBY_RUINS", true) } };
        InstallControlledCallback(session, completed);
        Assert.IsTrue(session.CanOpenFieldMenu);
        Assert.IsTrue(session.CanEditOptions);
        Assert.AreEqual("field_party_empty", RejectUnchanged(session, new("party_swap", Choice: 0, PartyIndex: 1)).Error);
        Assert.AreEqual("field_fly_party_index_invalid", RejectUnchanged(session, new("field_fly", PartyIndex: 0)).Error);
        Send(session, new("options_save") { Options = SourceGameOptions.Defaults with { Sound = SourceSound.Stereo } });
        Send(session, new("field_start_menu"));
        Assert.AreEqual(completed.TanobyCompletion, session.State.TanobyCompletion);

        InstallControlledCallback(session, completed with { Script = completed.Script with { Locked = true } });
        Assert.IsFalse(session.CanOpenFieldMenu);
        Assert.IsFalse(session.CanEditOptions);
        RejectUnchanged(session, new("field_start_menu"));
    }

    [TestMethod]
    public void FieldAdmissionDoesNotReplaceSaveIntegrityValidation()
    {
        var session = Fresh();
        InstallControlledCallback(session, session.State with { Money = -1 });
        Assert.IsTrue(session.CanOpenFieldMenu, "Field ownership is not serialized-state validation.");
        Assert.AreEqual("save_player_invalid", session.ValidatePlayerSave(session.State));
    }

    private static CampaignSession Fresh() => CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);

    private static CampaignState SafariState(CampaignState state)
    {
        var map = CurrentCampaignFixture.Catalog.MapFromSourceSymbol("MAP_SAFARI_ZONE_CENTER");
        return state with { SafariVisit = new(30, 600, true),
            World = state.World with { Player = state.World.Player with { MapId = map } },
            Script = state.Script with { Flags = state.Script.Flags.SetItem(SafariFlag, true) } };
    }

    private static CampaignState Block(CampaignState state, string blocker) => blocker switch
    {
        "script-running" => state with { Script = state.Script with { Completed = false } },
        "locked" => state with { Script = state.Script with { Locked = true } },
        "wait" => state with { Script = state.Script with { Wait = new(StoryWaitKind.Presentation, "controlled-wait") } },
        "stack" => state with { Script = state.Script with { Stack = [new("controlled-program", 0)] } },
        "quest-log" => state with { Script = state.Script with { QuestLogState = 1 } },
        "entry-program" => state with { EntryPrograms = ["controlled-program"] },
        "dialogue" => state with { Dialogue = new("controlled-text", ["Busy"], 0, false, true) },
        "sign" => state with { SignMessage = new() },
        "nickname" => state with { Nickname = new(0, null, false) },
        "picture" => state with { MonPicture = new(1, 0, 0) },
        "black-screen" => state with { ScreenIsBlack = true },
        "hidden-player" => state with { PlayerVisible = false },
        "presentation-ticks" => state with { PresentationTicks = 1 },
        "motion" => state with { Motions = [new("LOCALID_PLAYER", [state.World.Player], ["walk_down"])] },
        "arrival-facing" => state with { ArrivalFacing = Direction.South },
        "bag" => state with { ItemMenu = new("bag", "bag") },
        "pc-powered" => state with { PcPoweredOn = true },
        "money-box" => state with { MoneyBox = new(3000, 0, 0) },
        "town-map" => state with { TownMap = new() },
        "help-context" => state with { HelpContext = "HELPCONTEXT_UNUSED" },
        "safari-exit" => state with { SafariExitPending = true },
        "list-menu" => state with { Script = state.Script with { ListMenu = new("controlled-menu", 0, false) } },
        "field-service" => state with { FieldItemService = new("controlled-service") },
        "oak-rating" => state with { OakRatingHelpContextBackup = "map" },
        "message-pointer" => state with { MessagePointer = "controlled-text" },
        "berry-vendor" => state with { BerryPowderVendor = true },
        "white-screen" => state with { ScreenIsWhite = true },
        "trainer-records" => state with { TrainerTowerRecordsVisible = true },
        "daisy" => state with { DaisyGrooming = new(true) },
        "diploma" => state with { Diploma = new(false) },
        "field-pickup" => state with { FieldPickup = new("controlled-pickup") },
        _ => throw new AssertFailedException("Unknown callback blocker: " + blocker)
    };

    private static void InstallControlledCallback(CampaignSession session, CampaignState state)
    {
        // Transient callbacks are intentionally not restorable player saves. Inject an
        // in-memory command session, as the existing route fixtures do, then exercise
        // only public application queries and commands. No checkpoint file is read.
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
                session.RulesVersion, CurrentCampaignFixture.Catalog.ContentVersion, session.Revision));
    }

    private static void Send(CampaignSession session, CampaignCommand command)
    {
        var revision = session.Revision;
        var result = session.Execute("admission:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        Assert.AreEqual(revision + 1, session.Revision);
    }

    private static CommandOutcome<CampaignState> RejectUnchanged(CampaignSession session, CampaignCommand command)
    {
        var before = session.State;
        var hash = CanonicalJson.Hash(before);
        var revision = session.Revision;
        var result = session.Execute("rejected:" + revision + ":" + command.Kind, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status, result.Error);
        Assert.AreSame(before, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(hash, CanonicalJson.Hash(session.State));
        return result;
    }
}

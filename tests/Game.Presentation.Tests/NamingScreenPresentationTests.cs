using Game.Content;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class NamingScreenPresentationTests
{
    [TestMethod]
    public void NavigationWrapsAndPageChangesKeepCursorOnValidAction()
    {
        var grid = new NamingScreenPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        Assert.AreEqual(new NamingScreenKey(NamingScreenAction.Insert, 'A'), grid.Activate());
        grid.Move(-1, 0); Assert.IsTrue(grid.OnAction);
        Assert.AreEqual(NamingScreenAction.ChangePage, grid.Activate().Action);
        Assert.IsTrue(grid.Swapping); grid.AdvanceFrames(33); Assert.AreEqual(1, grid.Page);
        grid.Activate(); grid.AdvanceFrames(33); Assert.AreEqual(2, grid.Page); Assert.AreEqual(6, grid.Column);
        grid.Move(0, -1); Assert.AreEqual(2, grid.Row);
        Assert.AreEqual(NamingScreenAction.Submit, grid.Activate().Action);
        grid.Move(0, -1); Assert.AreEqual(NamingScreenAction.Delete, grid.Activate().Action);
        grid.Move(1, 0); Assert.AreEqual(0, grid.Column);
        Assert.AreEqual('5', grid.Activate().Character);
    }

    [TestMethod]
    public void FourthCharacterRowClampsWhenMovingOntoButtonsAndResetRestoresUppercase()
    {
        var grid = new NamingScreenPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        grid.Select(3, 7); grid.Move(1, 0); Assert.AreEqual(2, grid.Row);
        Assert.AreEqual(NamingScreenAction.Submit, grid.Activate().Action);
        grid.Select(0, grid.Columns); grid.Activate(); grid.AdvanceFrames(33); grid.SelectSubmit();
        Assert.AreEqual(NamingScreenAction.Submit, grid.Activate().Action);
        grid.Reset(); Assert.AreEqual(0, grid.Page); Assert.AreEqual(0, grid.Row); Assert.AreEqual(0, grid.Column);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => grid.Select(4, 0));
    }

    [TestMethod]
    public void PageSwapUsesNativeSinePriorityChangeAndRejectsRepeatedActivation()
    {
        var source = PretOpeningPresentation.Load(TestAssets.Root);
        var sine = CaptureBallPresentationAssets.Load(TestAssets.Root).Sine;
        var grid = new NamingScreenPresentation(source);
        grid.Select(0, grid.Columns); grid.Activate();
        CollectionAssert.AreEqual(new[] { new NamingScreenLayer(1, 0), new NamingScreenLayer(0, 0) }, grid.Layers(sine).ToArray());
        Assert.AreEqual(NamingScreenAction.None, grid.Activate().Action);
        grid.Move(1, 1); Assert.AreEqual(8, grid.Column); Assert.AreEqual(0, grid.Row);
        grid.AdvanceFrames(8); Assert.IsFalse(grid.PageButtonTextVisible); Assert.AreEqual(2, grid.PageButtonPage);
        grid.AdvanceFrames(8);
        CollectionAssert.AreEqual(new[] { new NamingScreenLayer(0, 40), new NamingScreenLayer(1, -40) }, grid.Layers(sine).ToArray());
        Assert.AreEqual(0, grid.Page); Assert.IsTrue(grid.Swapping);
        grid.AdvanceFrames(16); Assert.IsTrue(grid.Swapping);
        grid.AdvanceFrames(1); Assert.IsFalse(grid.Swapping); Assert.AreEqual(1, grid.Page);
        CollectionAssert.AreEqual(new[] { new NamingScreenLayer(1, 0) }, grid.Layers(sine).ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => grid.Layers([]));
    }
    [TestMethod]
    public void PageSwapAndDecorationClockAreChunkIndependentAndResettable()
    {
        var source = PretOpeningPresentation.Load(TestAssets.Root);
        var a = new NamingScreenPresentation(source); var b = new NamingScreenPresentation(source);
        foreach (var grid in new[] { a, b }) { grid.Select(0, grid.Columns); grid.Activate(); }
        a.AdvanceFrames(20); for (int i = 0; i < 20; i++) b.AdvanceFrames(1);
        Assert.AreEqual(a.PageButtonOffsetY, b.PageButtonOffsetY); Assert.AreEqual(a.ArrowOffsetX, b.ArrowOffsetX);
        Assert.AreEqual(a.UnderscoreOffsetY, b.UnderscoreOffsetY);
        a.Reset(); Assert.IsFalse(a.Swapping); Assert.AreEqual(0, a.VisualFrame); Assert.AreEqual(0, a.Page);
        a.Select(0, 7); Assert.AreEqual(161, a.CursorX);
    }
    [TestMethod]
    public void EveryExistingCallerUsesItsSourceTemplateWithoutChangingInitialTextOrEscapeSemantics()
    {
        var source = PretOpeningPresentation.Load(TestAssets.Root);
        var rows = new[]
        {
            (NamingScreenFlow.Player, "NAMING_SCREEN_PLAYER", 7, 98, NamingEscapeBehavior.ReturnToEntry),
            (NamingScreenFlow.Rival, "NAMING_SCREEN_RIVAL", 7, 98, NamingEscapeBehavior.ReturnToEntry),
            (NamingScreenFlow.Capture, "NAMING_SCREEN_CAUGHT_MON", 10, 86, NamingEscapeBehavior.HostQuit),
            (NamingScreenFlow.Gift, "NAMING_SCREEN_NICKNAME", 10, 86, NamingEscapeBehavior.KeepOriginal),
            (NamingScreenFlow.Hatch, "NAMING_SCREEN_NICKNAME", 10, 86, NamingEscapeBehavior.KeepOriginal),
            (NamingScreenFlow.NameRater, "NAMING_SCREEN_NICKNAME", 10, 86, NamingEscapeBehavior.Stay),
            (NamingScreenFlow.Box, "NAMING_SCREEN_BOX", 8, 94, NamingEscapeBehavior.Stay),
            (NamingScreenFlow.PartyNickname, "NAMING_SCREEN_NICKNAME", 10, 86, NamingEscapeBehavior.HostQuit)
        };
        CollectionAssert.AreEquivalent(Enum.GetValues<NamingScreenFlow>(), rows.Select(row => row.Item1).ToArray());
        var screen = new NamingScreenPresentation(source);
        foreach (var (flow, symbol, maxChars, entryX, escape) in rows)
        {
            var context = new NamingScreenContext(flow, "target:" + flow, "OLD", "PIKACHU", PokemonGender.Female);
            Assert.IsTrue(screen.Open(context));
            Assert.AreEqual(symbol, context.TemplateSymbol);
            Assert.AreSame(source.NamingTemplates[symbol], screen.Template);
            Assert.AreEqual(maxChars, screen.MaxChars);
            Assert.AreEqual(entryX, screen.EntryX);
            Assert.AreEqual(escape, context.EscapeBehavior);
            Assert.AreEqual(context.IsOpening ? "OLD" : "", context.InitialText);
            Assert.AreEqual((screen.Template.PrefixSpeciesName ? "PIKACHU" : "") + screen.Template.Title, screen.Title);
            Assert.AreEqual(screen.Template.InitialPage, screen.Page);
            Assert.AreEqual(PokemonGender.Female, context.Gender);
        }
    }

    [TestMethod]
    public void RepeatedSynchronizationPreservesPageAndClockButCloseAndTargetChangeReset()
    {
        var screen = new NamingScreenPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        var context = new NamingScreenContext(NamingScreenFlow.NameRater, "owned:42", "SPARKY", "PIKACHU");
        Assert.IsTrue(screen.Open(context));
        screen.Select(0, screen.Columns); screen.Activate(); screen.AdvanceFrames(17);
        Assert.IsFalse(screen.Open(context with { }));
        Assert.IsTrue(screen.Swapping); Assert.AreEqual(17, screen.SwapFrame); Assert.AreEqual(17, screen.VisualFrame);
        screen.AdvanceFrames(16); screen.Select(3, 5);
        for (int tick = 0; tick < 10; tick++) Assert.IsFalse(screen.Open(context with { }));
        Assert.AreEqual(1, screen.Page); Assert.AreEqual(3, screen.Row); Assert.AreEqual(5, screen.Column);
        Assert.AreEqual("SPARKY", context.OriginalName);
        Assert.IsTrue(screen.Open(context with { TargetIdentity = "owned:43" }));
        Assert.AreEqual(0, screen.Page); Assert.AreEqual(0, screen.Row); Assert.AreEqual(0, screen.Column);
        Assert.AreEqual(0, screen.VisualFrame); Assert.IsFalse(screen.Swapping);
        screen.SelectSubmit(); screen.Close();
        Assert.IsNull(screen.Context); Assert.ThrowsExactly<InvalidOperationException>(() => _ = screen.Template);
        Assert.IsTrue(screen.Open(context)); Assert.AreEqual(0, screen.Row); Assert.AreEqual(0, screen.Column);
    }

    [TestMethod]
    public void MouseSelectionSharesKeyboardActionsAcrossOpeningPokemonAndBoxContexts()
    {
        var screen = new NamingScreenPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        foreach (var flow in Enum.GetValues<NamingScreenFlow>())
        {
            screen.Open(new(flow, flow.ToString(), "OLD", "PIKACHU"));
            Assert.IsFalse(screen.SelectPoint(100, 49), "Native editor row must retain caret and selection ownership.");
            Assert.IsFalse(screen.SelectPoint(23, 80)); Assert.IsFalse(screen.SelectPoint(176, 80));
            Assert.IsFalse(screen.SelectPoint(224, 130)); Assert.IsFalse(screen.SelectPoint(200, 152));
            Assert.IsTrue(screen.SelectPoint(38, 81));
            Assert.AreEqual(new NamingScreenKey(NamingScreenAction.Insert, 'A'), screen.Activate());
            Assert.IsTrue(screen.SelectPoint(200, 105)); Assert.AreEqual(NamingScreenAction.Delete, screen.Activate().Action);
            Assert.IsTrue(screen.SelectPoint(200, 130)); Assert.AreEqual(NamingScreenAction.Submit, screen.Activate().Action);
            Assert.IsTrue(screen.SelectPoint(200, 75)); Assert.AreEqual(NamingScreenAction.ChangePage, screen.Activate().Action);
            Assert.IsFalse(screen.SelectPoint(38, 81)); Assert.AreEqual(NamingScreenAction.None, screen.Activate().Action);
            screen.AdvanceFrames(33);
            Assert.IsTrue(screen.SelectPoint(38, 81)); Assert.AreEqual('a', screen.Activate().Character);
            screen.Select(0, screen.Columns); screen.Activate(); screen.AdvanceFrames(33);
            Assert.IsTrue(screen.SelectPoint(148, 129)); Assert.AreEqual(5, screen.Column); Assert.AreEqual(3, screen.Row);
        }
    }

    [TestMethod]
    public void SourceBackKeyAlwaysDeletesAndContextRejectsMissingTargetOrUnknownFlow()
    {
        var screen = new NamingScreenPresentation(PretOpeningPresentation.Load(TestAssets.Root));
        foreach (var flow in Enum.GetValues<NamingScreenFlow>())
        {
            screen.Open(new(flow, flow.ToString(), "OLD"));
            screen.Select(1, screen.Columns);
            Assert.AreEqual(NamingScreenAction.Delete, screen.Activate().Action, flow.ToString());
        }
        var previous = screen.Context;
        Assert.ThrowsExactly<ArgumentException>(() => screen.Open(new(NamingScreenFlow.Box, "", "BOX1")));
        Assert.ThrowsExactly<ArgumentException>(() => screen.Open(new((NamingScreenFlow)999, "invalid", "OLD")));
        Assert.AreSame(previous, screen.Context);
    }

    [TestMethod]
    public void TrainerPortraitFadeHonorsUnequalSourceStepsPlatformToggleAndHold()
    {
        var fade = new OpeningPortraitTransition("red", "rival", 40);
        Assert.AreEqual(1f, fade.Alpha); Assert.IsTrue(fade.PlatformVisible);
        fade.AdvanceFrames(2); Assert.AreEqual(1f, fade.Alpha);
        fade.AdvanceFrames(1); Assert.AreEqual(15f / 16, fade.Alpha);
        fade.AdvanceFrames(21); Assert.AreEqual(.5f, fade.Alpha); Assert.IsFalse(fade.PlatformVisible);
        fade.AdvanceFrames(25); Assert.AreEqual("rival", fade.Portrait); Assert.AreEqual(0f, fade.Alpha);
        fade.AdvanceFrames(40); Assert.AreEqual(0f, fade.Alpha);
        fade.AdvanceFrames(3); Assert.AreEqual(.125f, fade.Alpha);
        fade.AdvanceFrames(9); Assert.AreEqual(.5f, fade.Alpha); Assert.IsTrue(fade.PlatformVisible);
        fade.AdvanceFrames(12); Assert.AreEqual(1f, fade.Alpha); Assert.IsFalse(fade.Complete);
        fade.AdvanceFrames(1); Assert.IsTrue(fade.Complete);
        var hide = new OpeningPortraitTransition("oak", null, 48); hide.AdvanceFrames(96); Assert.IsFalse(hide.Complete);
        hide.AdvanceFrames(1); Assert.IsTrue(hide.Complete);
    }
}

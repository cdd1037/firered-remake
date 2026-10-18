namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class EvolutionSceneIntroTests
{
    private sealed class Playback
    {
        public bool Available = true, CryPlaying, SoundPlaying;
        public bool MissingCry, MissingSound;
        public int Reads;
        public readonly List<EvolutionIntroCue> Cues = [];
        public EvolutionIntroSignals Read()
        { Reads++; return new(Available, CryPlaying, SoundPlaying); }
        public void Dispatch(EvolutionIntroCue cue)
        {
            Cues.Add(cue);
            if (cue == EvolutionIntroCue.Cry)
            { CryPlaying = true; if (MissingCry) Available = false; }
            if (cue == EvolutionIntroCue.Sound)
            { Available = !MissingSound; SoundPlaying = true; }
        }
    }

    private static EvolutionScenePresentation Scene(bool canCancel = true, string text = "X", int speed = 1) =>
        new(canCancel, text, speed);
    private static void Step(EvolutionScenePresentation scene, Playback audio, int frames = 1,
        bool held = false, bool pressed = false) => scene.AdvanceFrames(frames, audio.Read, audio.Dispatch, held, pressed);

    [TestMethod]
    public void IntroCryWaitsForPrinterEosAfterTheLastGlyphDelay()
    {
        var scene = Scene(speed: 4); var audio = new Playback();
        Assert.AreEqual(EvolutionScenePhase.Intro, scene.Phase);
        Assert.AreEqual("", scene.IntroVisibleText); Assert.IsFalse(scene.IntroTextComplete);
        Step(scene, audio);
        Assert.AreEqual("X", scene.IntroVisibleText); Assert.IsFalse(scene.IntroTextComplete);
        Step(scene, audio, 3);
        Assert.IsFalse(scene.IntroTextComplete); Assert.AreEqual(0, audio.Cues.Count);
        Step(scene, audio);
        Assert.IsTrue(scene.IntroTextComplete); Assert.AreEqual(EvolutionIntroStage.Cry, scene.IntroStage);
        CollectionAssert.AreEqual(new[] { EvolutionIntroCue.Cry }, audio.Cues);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(180)]
    public void ShortAndLongCriesGateIntroSoundThenMusicThenTheBackgroundFade(int cryFrames)
    {
        var scene = Scene(); var audio = new Playback();
        Step(scene, audio, 2); // One glyph, then EOS dispatches the cry.
        Step(scene, audio, cryFrames);
        Assert.AreEqual(EvolutionIntroStage.Cry, scene.IntroStage);
        Assert.AreEqual(1, audio.Cues.Count); Assert.IsFalse(scene.MovingBackgroundVisible);
        audio.CryPlaying = false; Step(scene, audio);
        Assert.AreEqual(EvolutionIntroStage.Sound, scene.IntroStage);
        Step(scene, audio, 120); // No guessed sound duration, even beyond the former 90-frame intro.
        Assert.AreEqual(EvolutionIntroStage.Sound, scene.IntroStage); Assert.AreEqual(2, audio.Cues.Count);
        audio.SoundPlaying = false; Step(scene, audio);
        Assert.AreEqual(EvolutionIntroStage.BackgroundFade, scene.IntroStage);
        CollectionAssert.AreEqual(new[] { EvolutionIntroCue.Cry, EvolutionIntroCue.Sound, EvolutionIntroCue.Music }, audio.Cues);
        Assert.AreEqual(0, scene.BackgroundFrame); Assert.IsFalse(scene.CanCancel);
        // BeginNormalPaletteFade(0,16,4): eight delta-two changes, split passes,
        // then five finishing callbacks. No terrain-pixel parity is asserted.
        Step(scene, audio, 53); Assert.AreEqual(EvolutionScenePhase.Intro, scene.Phase);
        Step(scene, audio); Assert.AreEqual(EvolutionScenePhase.Spiral, scene.Phase);
        Assert.AreEqual(EvolutionIntroStage.Complete, scene.IntroStage);
        Assert.AreEqual(0, scene.PhaseFrame); Assert.AreEqual(0, scene.BackgroundFrame);
        Step(scene, audio, 2000); Assert.AreEqual(EvolutionScenePhase.Complete, scene.Phase);
        Assert.AreEqual(3, audio.Cues.Count); // Sampling/later phases never replay a cue.
    }

    [TestMethod]
    public void BatchedFramesResamplePlaybackAfterSynchronousCueDispatch()
    {
        var scene = Scene(); var audio = new Playback();
        Step(scene, audio, 100);
        Assert.AreEqual(EvolutionIntroStage.Cry, scene.IntroStage);
        Assert.AreEqual(100, audio.Reads); Assert.AreEqual(1, audio.Cues.Count);
        audio.CryPlaying = false; Step(scene, audio, 100);
        Assert.AreEqual(EvolutionIntroStage.Sound, scene.IntroStage); Assert.AreEqual(2, audio.Cues.Count);
        audio.SoundPlaying = false; Step(scene, audio, 55);
        Assert.AreEqual(EvolutionScenePhase.Spiral, scene.Phase); Assert.AreEqual(3, audio.Cues.Count);
    }

    [TestMethod]
    public void MissingCryAndMissingIntroClipEachReleaseOnlyTheirAudioGate()
    {
        var missingCryScene = Scene(); var missingCry = new Playback { MissingCry = true };
        Step(missingCryScene, missingCry, 3);
        Assert.AreEqual(EvolutionIntroStage.Sound, missingCryScene.IntroStage);
        Step(missingCryScene, missingCry, 100);
        Assert.AreEqual(EvolutionIntroStage.Sound, missingCryScene.IntroStage); // Available SE still waits.
        missingCry.SoundPlaying = false; Step(missingCryScene, missingCry, 55);
        Assert.AreEqual(EvolutionScenePhase.Spiral, missingCryScene.Phase);

        var missingSoundScene = Scene(); var missingSound = new Playback { MissingSound = true };
        Step(missingSoundScene, missingSound, 2);
        missingSound.CryPlaying = false; Step(missingSoundScene, missingSound, 2);
        Assert.AreEqual(EvolutionIntroStage.BackgroundFade, missingSoundScene.IntroStage);
        Step(missingSoundScene, missingSound, 54);
        Assert.AreEqual(EvolutionScenePhase.Spiral, missingSoundScene.Phase);
        Assert.AreEqual(3, missingSound.Cues.Count);
    }

    [TestMethod]
    public void DisabledAudioStillPrintsTextAndRunsTheFadeWithoutAnAudioTimeout()
    {
        var scene = Scene(speed: 8); var cues = new List<EvolutionIntroCue>();
        // Explicitly unavailable outranks stale liveness left by a failed backend.
        EvolutionIntroSignals Read() => new(false, true, true);
        scene.AdvanceFrames(8, Read, cues.Add);
        Assert.IsFalse(scene.IntroTextComplete); Assert.AreEqual(0, cues.Count);
        scene.AdvanceFrames(3, Read, cues.Add);
        Assert.AreEqual(EvolutionIntroStage.BackgroundFade, scene.IntroStage);
        scene.AdvanceFrames(53, Read, cues.Add); Assert.AreEqual(EvolutionScenePhase.Intro, scene.Phase);
        scene.AdvanceFrames(1, Read, cues.Add); Assert.AreEqual(EvolutionScenePhase.Spiral, scene.Phase);
        CollectionAssert.AreEqual(new[] { EvolutionIntroCue.Cry, EvolutionIntroCue.Sound, EvolutionIntroCue.Music }, cues);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AudioFaultDuringCryOrSoundCannotStrandTheScene(bool faultDuringSound)
    {
        var scene = Scene(); var audio = new Playback(); Step(scene, audio, 2);
        if (faultDuringSound) { audio.CryPlaying = false; Step(scene, audio); }
        var stage = scene.IntroStage; Step(scene, audio, 100);
        Assert.AreEqual(stage, scene.IntroStage);
        var cues = new List<EvolutionIntroCue>();
        scene.AdvanceFrames(faultDuringSound ? 55 : 56,
            () => new(false, true, true), cues.Add);
        Assert.AreEqual(EvolutionScenePhase.Spiral, scene.Phase);
        Assert.AreEqual(faultDuringSound ? 1 : 2, cues.Count);
        Assert.AreEqual(EvolutionIntroCue.Music, cues[^1]);
    }

    [TestMethod]
    public void TextSpeedUpOnlyFinishesThePrinterAndCannotBypassCryOrCancelEarly()
    {
        var scene = Scene(speed: 8); var audio = new Playback();
        Step(scene, audio); Step(scene, audio, held: true, pressed: true);
        Assert.IsFalse(scene.IntroTextComplete);
        Step(scene, audio, held: true);
        Assert.IsTrue(scene.IntroTextComplete); Assert.AreEqual(EvolutionIntroStage.Cry, scene.IntroStage);
        Step(scene, audio, 500, held: true, pressed: true);
        Assert.AreEqual(EvolutionIntroStage.Cry, scene.IntroStage);
        Assert.IsFalse(scene.Cancel()); Assert.AreEqual(1, audio.Cues.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CompletionDrivenIntroPreservesOrdinaryAndItemCancellationBoundaries(bool canCancel)
    {
        var scene = Scene(canCancel); var audio = new Playback();
        Step(scene, audio, 2); Assert.IsFalse(scene.Cancel());
        audio.CryPlaying = false; Step(scene, audio); Assert.IsFalse(scene.Cancel());
        audio.SoundPlaying = false; Step(scene, audio, 55);
        Assert.AreEqual(EvolutionScenePhase.Spiral, scene.Phase); Assert.IsFalse(scene.Cancel());
        Step(scene, audio, 162 + 99);
        Assert.AreEqual(EvolutionScenePhase.Morph, scene.Phase);
        Assert.AreEqual(canCancel, scene.Cancel());
        Step(scene, audio, 2000);
        Assert.AreEqual(canCancel ? EvolutionScenePhase.Cancelled : EvolutionScenePhase.Complete, scene.Phase);
        Assert.AreEqual(3, audio.Cues.Count);
    }

    [TestMethod]
    public void IntroPrinterAndFadeAreIndependentOfHostFrameRateAndCapStalls()
    {
        var sixty = Scene(text: "What? TEST\nis evolving!", speed: 4);
        var oneTwenty = Scene(text: "What? TEST\nis evolving!", speed: 4);
        var first = new List<EvolutionIntroCue>(); var second = new List<EvolutionIntroCue>();
        for (int i = 0; i < 200; i++) sixty.Advance(1d / 60, () => EvolutionIntroSignals.Unavailable, first.Add);
        for (int i = 0; i < 400; i++) oneTwenty.Advance(1d / 120, () => EvolutionIntroSignals.Unavailable, second.Add);
        Assert.AreEqual(EvolutionScenePhase.Spiral, sixty.Phase);
        Assert.AreEqual(sixty.Phase, oneTwenty.Phase); Assert.AreEqual(sixty.PhaseFrame, oneTwenty.PhaseFrame);
        Assert.AreEqual(sixty.IntroVisibleText, oneTwenty.IntroVisibleText);
        CollectionAssert.AreEqual(first, second);
        var stalled = Scene(text: "What? TEST\nis evolving!", speed: 8);
        Assert.AreEqual(15, stalled.Advance(500, () => EvolutionIntroSignals.Unavailable, _ => Assert.Fail("Text is still printing.")));
        Assert.AreEqual(EvolutionIntroStage.Text, stalled.IntroStage); Assert.IsFalse(stalled.Cancel());
    }
}

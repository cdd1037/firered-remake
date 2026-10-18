using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class WeatherSourceOracleTests
{
    private const string Sand = "gFlyingSandCrescentSpriteTemplate", Rain = "gRainDropSpriteTemplate", Sun = "gSunlightRaySpriteTemplate";
    private sealed class Audio : ISourceAnimationAudio
    {
        public readonly List<SourceAnimationSound> Cues = [];
        public readonly List<(int Pan, int Frame)> Pans = [];
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) => Cues.Add(cue);
        public void SetPan(int pan, int frame) => Pans.Add((pan, frame));
        public void StopAnimationEffects() { }
    }
    private sealed class Occupied(int slot) : SourceBattleAnimation.VisualSprite(slot, 255)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, "occupied-weather-cell", default, default, 0, false, false, false, 256, null, Subpriority);
        public override void Step(SourceBattleAnimation owner) { }
    }
    private static string Folder => Path.Combine(TestAssets.Root, "content/generated/battle/animations");
    private static JsonDocument Vectors(string kind) => SourceVectorFixture.Read($"weather-{kind}-source-vectors.json", "python tests/Reference/Weather/check_vectors.py --reference content/raw/pokefirered/037335f4 --prepare tests/Game.Presentation.Tests/Fixtures");
    private static int[] N(JsonElement e) => e.EnumerateArray().Select(v => v.GetInt32()).ToArray();
    private static string Text(int n) => n.ToString(CultureInfo.InvariantCulture);
    private static T Field<T>(object value, string name)
    {
        var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing source-state observation {value.GetType().Name}.{name}");
        return (T)field.GetValue(value)!;
    }
    private static object[] Tasks(SourceBattleAnimation r) => Field<IEnumerable>(r, "_tasks").Cast<object>().ToArray();
    private static AnimationBattler[] Battlers(int visible = 15) => Enumerable.Range(0, 4).Select(i => new AnimationBattler(new(i), "b" + i, 0, 0, 64, 64) { Visible = (visible & (1 << i)) != 0 }).ToArray();
    private static CommittedMoveAnimation Cue(int move, int actor = 0, int target = 1, uint? seed = null) => new("weather-source", 1, move, new(actor), new(target)) { VisualSeed = seed };
    private static SourceAnimationScript Script(int move) => AnimationCompositionFixture.Script(move);
    private static SourceAnimationScript Primitive(int move, IEnumerable<SourceAnimationCommand> commands, string? graphics = null)
    {
        var list = new List<SourceAnimationCommand>();
        if (graphics is not null) list.Add(new("loadspritegfx", [graphics], 1));
        list.AddRange(commands); list.Add(new("waitforvisualfinish", [], 98)); list.Add(new("end", [], 99));
        return Script(move) with { Commands = list.ToArray(), Labels = new Dictionary<string, int>(), Subroutines = new Dictionary<string, SourceAnimationCommand[]>(), SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() };
    }
    // The source fixtures include the synchronous script initializer separately
    // from BattleMainCB2. Isolate that exact boundary without altering callbacks.
    private static SourceAnimationFrame Begin(SourceBattleAnimation runtime, bool graphics)
    {
        if (graphics) for (int i = 0; i < 3; i++) runtime.Tick();
        var run = typeof(SourceBattleAnimation).GetMethod("RunScript", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(run); run.Invoke(runtime, null); return runtime.Snapshot();
    }
    private static SourceAnimationFrame Advance(SourceBattleAnimation r, SourceBattleAnimation.SpriteScene scene, AnimationBattler[] battlers) => r.Active ? r.Tick() : scene.TickIdle(battlers, true);
    private static uint RandomState(SourceBattleAnimation r, uint seed)
    {
        var f = typeof(SourceBattleAnimation).GetField("_visualRandom", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f); return f.GetValue(r) is SourceVisualRandom random ? Field<uint>(random, "_state") : seed;
    }
    private static void Finish(SourceBattleAnimation r, SourceBattleAnimation.SpriteScene scene, AnimationBattler[] battlers)
    {
        for (int i = 0; (r.Active || r.HasResidualSprites) && i < 12; i++) Advance(r, scene, battlers);
        Assert.IsFalse(r.Active); Assert.IsFalse(r.HasResidualSprites); Assert.AreEqual(0, r.ActiveVisualCount); Assert.AreEqual(0, scene.Count);
    }

    [TestMethod]
    public void SandstormInitialStateEveryRegisterTickBothDirectionsAndSeparateCleanupMatchSource()
    {
        using var doc = Vectors("bg"); int cases = 0, rows = 0;
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (c.GetProperty("contest").GetBoolean() || c.GetProperty("cleanupProbe").GetBoolean()) continue;
            int actor = c.GetProperty("attacker").GetInt32(), flag = c.GetProperty("flag").GetInt32();
            var script = Primitive(201, [new("createvisualtask", ["AnimTask_LoadSandstormBackground", "5", Text(flag)], 2)]);
            var scene = new SourceBattleAnimation.SpriteScene(); var battlers = Battlers(); var r = new SourceBattleAnimation(script, Cue(201, actor, actor ^ 1), battlers, true, new Audio(), spriteScene: scene);
            var frame = Begin(r, false);
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int sourceFrame = row.GetProperty("frame").GetInt32(); if (sourceFrame != 0) frame = r.Tick();
                string at = $"sand actor={actor} flag={flag} tick={sourceFrame}"; int[] regs = N(row.GetProperty("registers")), data = N(row.GetProperty("data")), scroll = N(row.GetProperty("bg1"));
                Assert.AreEqual(row.GetProperty("alive").GetInt32(), r.ActiveVisualCount, at);
                Assert.AreEqual(regs[0] != 0, frame.BlendEnabled, at); Assert.AreEqual(regs[1] & 31, frame.AlphaA, at); Assert.AreEqual((regs[1] >> 8) & 31, frame.AlphaB, at);
                Assert.AreEqual(1, frame.BattlerBackgroundPriorities[0], at);
                if (data[12] >= 4) Assert.IsNull(frame.Overlay, at);
                else { Assert.IsNotNull(frame.Overlay, at); Assert.AreEqual("sandstorm", frame.Overlay.MaterialId, at); Assert.AreEqual(scroll[0], frame.Overlay.ScrollX, at); Assert.AreEqual(scroll[1], frame.Overlay.ScrollY, at); }
                if (row.GetProperty("alive").GetInt32() != 0)
                {
                    var task = Tasks(r).Single(); Assert.AreEqual(data[12], Field<int>(task, "_state"), at); Assert.AreEqual(data[10], Field<int>(task, "_timer"), at); Assert.AreEqual(data[11], Field<int>(task, "_alpha"), at);
                    Assert.AreEqual((ushort)scroll[0], Field<ushort>(task, "_x"), at); Assert.AreEqual((ushort)scroll[1], Field<ushort>(task, "_y"), at);
                }
                rows++;
            }
            Finish(r, scene, battlers); cases++;
        }
        Assert.AreEqual(8, cases); Assert.AreEqual(1280, rows);
    }

    [TestMethod]
    public void EveryCrescentRecipeSideFlagFractionAndDeferredDestroyMatchesSource()
    {
        using var doc = Vectors("motion"); int cases = 0, rows = 0;
        foreach (var c in doc.RootElement.GetProperty("crescents").EnumerateArray())
        {
            int actor = c.GetProperty("attacker").GetInt32(); int[] args = N(c.GetProperty("args"));
            var script = Primitive(201, [new("createsprite", [Sand, "ANIM_ATTACKER", "40", .. args.Select(Text)], 2)], "ANIM_TAG_FLYING_DIRT");
            var scene = new SourceBattleAnimation.SpriteScene(); var battlers = Battlers(); var r = new SourceBattleAnimation(script, Cue(201, actor, actor ^ 1), battlers, true, new Audio(), spriteScene: scene);
            Begin(r, true);
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int invocation = row.GetProperty("invocation").GetInt32(); if (invocation < 0) continue; if (invocation != 0) r.Tick();
                string at = $"crescent actor={actor} args={string.Join(',', args)} invocation={invocation}"; int alive = row.GetProperty("alive").GetInt32();
                Assert.AreEqual(alive, r.ActiveVisualCount, at); Assert.AreEqual(alive, scene.Count, at);
                CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(x => (short)x).ToArray(), r.Snapshot().AnimationArguments.Take(8).ToArray(), at);
                if (alive != 0)
                {
                    var sprite = scene.Sprites.Single(); var f = sprite.Frame; int[] p = N(row.GetProperty("position")), d = N(row.GetProperty("data"));
                    Assert.AreEqual(new PixelPoint(p[0], p[1]), f.Position, at); Assert.AreEqual(new PixelPoint(p[2], p[3]), f.Offset, at);
                    Assert.AreEqual(row.GetProperty("flip").GetInt32() != 0, f.HFlip, at); Assert.AreEqual(2, f.Subsprites.Length, at); Assert.AreEqual(32, f.Width, at); Assert.AreEqual(16, f.Height, at); Assert.IsFalse(f.Affine, at);
                    Assert.AreEqual(d[1], Field<int>(sprite, "_speed"), at); Assert.AreEqual(d[2], Field<int>(sprite, "_drop"), at); Assert.AreEqual((short)d[3], Field<short>(sprite, "_xFraction"), at); Assert.AreEqual((short)d[4], Field<short>(sprite, "_yFraction"), at);
                    Assert.AreEqual(row.GetProperty("callback").GetInt32() != 0, Field<bool>(sprite, "_destroyNext"), at);
                }
                rows++;
            }
            Finish(r, scene, battlers); cases++;
        }
        Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?56:14, cases); Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?2208:552, rows);
    }

    [TestMethod]
    public void HeatTargetsEverySourceOffsetAndPartnerVisibilityMatchesSource()
    {
        using var doc = Vectors("motion"); int cases = 0, rows = 0;
        foreach (var c in doc.RootElement.GetProperty("heat").EnumerateArray())
        {
            int actor = c.GetProperty("attacker").GetInt32(), target = c.GetProperty("target").GetInt32(), mask = c.GetProperty("visibleMask").GetInt32();
            var battlers = Battlers(mask); var scene = new SourceBattleAnimation.SpriteScene(); var r = new SourceBattleAnimation(Primitive(257, [new("createvisualtask", ["AnimTask_MoveHeatWaveTargets", "5"], 2)]), Cue(257, actor, target), battlers, true, new Audio(), spriteScene: scene);
            var offsets = Field<PixelPoint[]>(r, "_offsets"); for (int i = 0; i < 4; i++) offsets[i] = new(100 + i, 0);
            var frame = Begin(r, false);
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int sourceFrame = row.GetProperty("frame").GetInt32(); if (sourceFrame != 0) frame = r.Tick(); string at = $"heat {actor}->{target} visible={mask} frame={sourceFrame}";
                CollectionAssert.AreEqual(N(row.GetProperty("x2")), frame.Offsets.Select(p => p.X).ToArray(), at); Assert.AreEqual(row.GetProperty("alive").GetInt32(), r.ActiveVisualCount, at);
                if (r.ActiveVisualCount != 0)
                {
                    int[] d = N(row.GetProperty("data")); var task = Tasks(r).Single(); Assert.AreEqual(d[0], Field<int>(task, "_state"), at); Assert.AreEqual(d[1], Field<int>(task, "_counter"), at); Assert.AreEqual(d[2], Field<int>(task, "_parity"), at); Assert.AreEqual(d[10], Field<int>(task, "_translation"), at); Assert.AreEqual(d[11], Field<int>(task, "_jitter"), at);
                }
                rows++;
            }
            Finish(r, scene, battlers); cases++;
        }
        Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?32:6, cases); Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?4160:780, rows);
    }

    [TestMethod]
    public void RainEveryTaskPhaseRngReadAllocationFailureRawChildFrameAndDeferredExpiryMatchesSource()
    {
        using var doc = Vectors("engine"); int cases = 0, phases = 0;
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int scenario = c.GetProperty("scenario").GetInt32(); if (scenario == 10) continue; uint seed = c.GetProperty("seed").GetUInt32();
            int interval = 3, duration = scenario is 4 or 5 ? 9 : 120, occupied = scenario == 1 ? 64 : scenario == 2 ? 63 : 0;
            List<SourceAnimationCommand> commands = [new("createvisualtask", ["AnimTask_CreateRaindrops", scenario == 6 ? "2" : "5", "0", Text(interval), Text(duration)], 2)];
            if (scenario >= 4) commands.Add(new("createvisualtask", ["AnimTask_CreateRaindrops", scenario == 6 ? "2" : scenario == 4 ? "4" : "5", scenario == 6 ? "0" : "17", scenario == 6 ? "3" : "2", scenario == 6 ? "120" : "5"], 3));
            for (int i = 0; i < 8; i++) commands.Add(new("setarg", [Text(i), "-1234"], 4 + i));
            var script = Primitive(240, commands, "ANIM_TAG_RAIN_DROPS"); var scene = new SourceBattleAnimation.SpriteScene(); var battlers = Battlers(0); var r = new SourceBattleAnimation(script, Cue(240, seed: seed), battlers, true, new Audio(), spriteScene: scene);
            var fillers = Enumerable.Range(0, scenario == 3 ? 64 : occupied).Select(i => new Occupied(i) { Owner = r }).ToArray(); foreach (var f in fillers) scene.Sprites.Add(f);
            var frame = Begin(r, true); var instances = new Dictionary<SourceBattleAnimation.VisualSprite, int>(); var taskInstances = Tasks(r); int currentFrame = 0; uint beforeSpriteRng = seed; int beforeSpriteVisualCount = 0;
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int sourceFrame = row.GetProperty("frame").GetInt32(), phase = row.GetProperty("phase").GetInt32();
                if (sourceFrame > currentFrame) { beforeSpriteRng = RandomState(r, seed); beforeSpriteVisualCount = r.ActiveVisualCount; frame = Advance(r, scene, battlers); currentFrame = sourceFrame; }
                string at = $"rain scenario={scenario} seed={seed} sourceFrame={sourceFrame} phase={phase}";
                var expected = row.GetProperty("children").EnumerateArray().Select(N).ToArray();
                // A Tick snapshot captures the source OAM phase, while the scene
                // registry/RNG/task data expose the later RunTasks phase.
                if (phase == 1)
                {
                    Assert.AreEqual(row.GetProperty("rng").GetUInt32(), beforeSpriteRng, at); Assert.AreEqual(row.GetProperty("visualCount").GetInt32(), beforeSpriteVisualCount, at);
                    var actual = frame.Sprites.Where(s => s.Template == Rain).OrderBy(s => s.Slot).ToArray(); Assert.AreEqual(expected.Length, actual.Length, at);
                    for (int i = 0; i < expected.Length; i++) AssertRain(expected[i], actual[i], at);
                }
                else
                {
                    Assert.AreEqual(row.GetProperty("rng").GetUInt32(), RandomState(r, seed), at); Assert.AreEqual(row.GetProperty("visualCount").GetInt32(), r.ActiveVisualCount, at);
                    var actual = scene.Sprites.Where(s => s.Frame.Template == Rain).OrderBy(s => s.Slot).ToArray(); Assert.AreEqual(expected.Length, actual.Length, at);
                    for (int i = 0; i < expected.Length; i++)
                    {
                        int[] e = expected[i]; var s = actual[i]; AssertRain(e, s.Frame, at); Assert.IsFalse(s.Counted, at); Assert.AreSame(r, s.Owner, at);
                        if (!instances.TryGetValue(s, out int id)) instances.Add(s, id = e[1]); Assert.AreEqual(e[1], id, at);
                        Assert.AreEqual(e[14], Field<int>(s, "_age"), at); Assert.AreEqual(e[6] != 0, Field<bool>(s, "_initialized"), at);
                        var programField = s.GetType().GetField("_frames", BindingFlags.Instance | BindingFlags.NonPublic); Assert.IsNotNull(programField);
                        if (programField.GetValue(s) is SourceSpriteFrameProgram program) { Assert.AreEqual(e[8], program.DelayCounter, at); Assert.AreEqual(e[9] != 0, program.Ended, at); }
                        else Assert.AreEqual(0, e[6], at);
                    }
                    var expectedTasks = row.GetProperty("tasks").EnumerateArray().Select(N).Where(t => t[1] != 0).ToArray(); var liveTasks = Tasks(r); Assert.AreEqual(expectedTasks.Length, liveTasks.Length, at);
                    foreach (int[] t in expectedTasks) { var task = taskInstances[t[0]]; Assert.IsTrue(liveTasks.Contains(task), at); Assert.AreEqual(t[4], Field<int>(task, "_age"), at); }
                }
                if (sourceFrame == 0 && scenario == 3) foreach (var f in fillers) Assert.IsTrue(scene.Sprites.Remove(f), at);
                phases++;
            }
            if (scenario != 3) foreach (var f in fillers) Assert.IsTrue(scene.Sprites.Remove(f));
            Finish(r, scene, battlers); cases++;
        }
        Assert.AreEqual(8, cases); Assert.AreEqual(1696, phases);
    }
    private static void AssertRain(int[] e, SourceVisualSpriteFrame f, string at)
    {
        Assert.AreEqual(e[0], f.Slot, at); Assert.AreEqual(new PixelPoint(e[2], e[3]), f.Position, at); Assert.AreEqual(new PixelPoint(e[4], e[5]), f.Offset, at); Assert.AreEqual(e[10], f.Tile, at); Assert.AreEqual(e[11], f.Subpriority, at);
        Assert.AreEqual(16, f.Width, at); Assert.AreEqual(32, f.Height, at); Assert.IsFalse(f.Affine, at); Assert.IsFalse(f.Blend, at); Assert.IsFalse(f.HFlip, at); Assert.IsFalse(f.VFlip, at);
    }

    [TestMethod]
    public void SunlightImmediateInitializationEveryLinearAndAffineTickAndDestroyMatchSource()
    {
        using var doc = Vectors("engine"); var c = doc.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("scenario").GetInt32() == 10); int checkedRows = 0;
        for (int actor = 0; actor < 4; actor++)
        {
            var script = Primitive(241, [new("createsprite", [Sun, "ANIM_ATTACKER", "40"], 2)], "ANIM_TAG_SUNLIGHT"); var scene = new SourceBattleAnimation.SpriteScene(); var battlers = Battlers(); var r = new SourceBattleAnimation(script, Cue(241, actor, actor ^ 1), battlers, true, new Audio(), spriteScene: scene); Begin(r, true);
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int sourceFrame = row.GetProperty("frame").GetInt32(); if (sourceFrame != 0) r.Tick(); int[] e = N(row.GetProperty("sprite")); string at = $"sun actor={actor} callback={sourceFrame}";
                Assert.AreEqual(row.GetProperty("visualCount").GetInt32(), r.ActiveVisualCount, at); Assert.AreEqual(e[13], scene.Count, at);
                if (e[13] != 0)
                {
                    var s = scene.Sprites.Single(); var f = s.Frame; Assert.AreEqual(new PixelPoint(e[2], e[3]), f.Position, at); Assert.AreEqual(new PixelPoint(e[4], e[5]), f.Offset, at); Assert.IsTrue(f.Blend, at); Assert.IsTrue(f.Affine, at); Assert.AreEqual(e[6] == 5, Field<bool>(s, "_destroyNext"), at);
                    var affine = Field<SourceSpriteAffineProgram>(s, "_affine"); int[] a = N(row.GetProperty("affine")), bios = N(row.GetProperty("bios")); Assert.AreEqual(a[3], affine.XScale, at); Assert.AreEqual(a[4], affine.YScale, at); Assert.AreEqual(a[5], affine.Rotation, at);
                    var matrix = SourceAffineMatrix.FromScaleRotation(bios[0], bios[1], bios[2]); Assert.AreEqual(matrix.A, f.MatrixA, at); Assert.AreEqual(matrix.B, f.MatrixB, at); Assert.AreEqual(matrix.C, f.MatrixC, at); Assert.AreEqual(matrix.D, f.MatrixD, at);
                    var linear = Field<object>(s, "_linear"); Assert.AreEqual(e[14], Field<int>(linear, "_remaining"), at);
                    if (sourceFrame != 0) { Assert.AreEqual((int)(ushort)e[17], Field<int>(linear, "_x"), at); Assert.AreEqual((int)(ushort)e[18], Field<int>(linear, "_y"), at); }
                }
                checkedRows++;
            }
            Finish(r, scene, battlers);
        }
        Assert.AreEqual(252, checkedRows);
    }

    private static ushort OriginalPaletteColor(int index) => (ushort)(((index + 3) & 31) | (((index * 7 + 5) & 31) << 5) | (((index * 11 + 9) & 31) << 10));
    private static readonly int[] PaletteBanks = [1, 2, 3, 8, 16, 17, 18, 19, 31];

    [TestMethod]
    public void SunAndRainPaletteTasksMatchEverySourceDelayMaskCoefficientAndRgb555Row()
    {
        using var doc = Vectors("motion"); int cases = 0, rows = 0;
        foreach (var c in doc.RootElement.GetProperty("blends").EnumerateArray())
        {
            int kind = c.GetProperty("kind").GetInt32(); if (kind >= 4) continue;
            int actor = c.GetProperty("attacker").GetInt32(), visible = c.GetProperty("visibleMask").GetInt32();
            int delay = kind < 2 ? 1 : 2, peak = kind < 2 ? 6 : 4, from = (kind & 1) == 0 ? 0 : peak, to = (kind & 1) == 0 ? peak : 0, color = kind < 2 ? 32767 : 0;
            var script = Primitive(kind < 2 ? 241 : 240, [new("createvisualtask", ["AnimTask_BlendBattleAnimPal", "10", "1921", Text(delay), Text(from), Text(to), Text(color)], 2)]);
            var scene = new SourceBattleAnimation.SpriteScene(); var battlers = Battlers(visible); var r = new SourceBattleAnimation(script, Cue(kind < 2 ? 241 : 240, actor, actor ^ 1), battlers, true, new Audio(), spriteScene: scene); var frame = Begin(r, false);
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int f = row.GetProperty("frame").GetInt32(); if (f != 0) frame = r.Tick(); string at = $"palette kind={kind} actor={actor} visibility={visible} tick={f}";
                Assert.AreEqual(row.GetProperty("visualCount").GetInt32(), r.ActiveVisualCount, at);
                if (r.ActiveVisualCount != 0)
                {
                    int[] d = N(row.GetProperty("data")); var task = Tasks(r).Single(); Assert.AreEqual(d[9], Field<int>(task, "_counter"), at); Assert.AreEqual(d[10], Field<int>(task, "_coefficient"), at); Assert.AreEqual((uint)(ushort)d[0] | ((uint)(ushort)d[1] << 16), Field<uint>(task, "_mask"), at);
                }
                for (int b = 0; b < PaletteBanks.Length; b++)
                {
                    int bank = PaletteBanks[b]; var blend = frame.PaletteBlends.SingleOrDefault(p => p.Palette == bank);
                    int[] actual = Enumerable.Range(0, 16).Select(i => (int)SourcePaletteMath.Apply(OriginalPaletteColor(bank * 16 + i), blend, false)).ToArray();
                    CollectionAssert.AreEqual(N(row.GetProperty("palettes")[b]), actual, at + " bank=" + bank);
                }
                rows++;
            }
            Finish(r, scene, battlers); cases++;
        }
        Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?16:6, cases); Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?232:87, rows);
    }

    [TestMethod]
    public void HeatImmediateBackgroundAndHighestParticleBankMatchSourcePaletteWrites()
    {
        using var doc = Vectors("motion"); int cases = 0;
        foreach (var c in doc.RootElement.GetProperty("blends").EnumerateArray())
        {
            int kind = c.GetProperty("kind").GetInt32(); if (kind < 4 || kind == 4 && c.GetProperty("paletteIndex").GetInt32() != 15) continue;
            int actor = c.GetProperty("attacker").GetInt32(), visible = c.GetProperty("visibleMask").GetInt32(); var battlers = Battlers(visible); var scene = new SourceBattleAnimation.SpriteScene();
            SourceAnimationCommand[] commands = kind == 5
                ? [new("createvisualtask", ["AnimTask_LoadSandstormBackground", "5", "1"], 2), new("createvisualtask", ["AnimTask_BlendBackground", "5", "6", "RGB_RED"], 3)]
                : [new("createvisualtask", ["AnimTask_BlendParticle", "5", "ANIM_TAG_FLYING_DIRT", "0", "6", "6", "RGB_RED"], 2), new("delay", ["30"], 3)];
            var r = new SourceBattleAnimation(Primitive(257, commands, kind == 4 ? "ANIM_TAG_FLYING_DIRT" : null), Cue(257, actor, actor ^ 1), battlers, true, new Audio(), spriteScene: scene);
            if (kind == 4)
            {
                // Slot 15 exercises source unsigned mask bit 31. Runtime slots
                // 0..3 are battler reservations; those native helper probes are
                // covered below without pretending they are allocatable slots.
                var tags = Field<int[]>(r, "_spritePaletteTags"); for (int slot = 4; slot < 15; slot++) tags[slot] = 20000 + slot;
                for (int i = 0; i < 3; i++) r.Tick();
                var original = Field<ushort[][]>(r, "_spritePaletteUnfadedBanks"); var faded = Field<ushort[][]>(r, "_spritePaletteBanks");
                for (int slot = 0; slot < 16; slot++) for (int i = 0; i < 16; i++) original[slot][i] = faded[slot][i] = OriginalPaletteColor((16 + slot) * 16 + i);
            }
            var frame = Begin(r, false); var row = c.GetProperty("rows")[0]; string at = $"heat palette kind={kind} actor={actor} visibility={visible}";
            Assert.AreEqual(kind == 5 ? 1 : 0, r.ActiveVisualCount, at);
            for (int b = 0; b < PaletteBanks.Length; b++)
            {
                int bank = PaletteBanks[b]; var blend = frame.PaletteBlends.SingleOrDefault(p => p.Palette == bank);
                int[] actual = kind == 4 && bank == 31 ? frame.SpritePaletteSlots[15].Select(v => (int)v).ToArray() : Enumerable.Range(0, 16).Select(i => (int)SourcePaletteMath.Apply(OriginalPaletteColor(bank * 16 + i), blend, false)).ToArray();
                CollectionAssert.AreEqual(N(row.GetProperty("palettes")[b]), actual, at + " bank=" + bank);
            }
            r.Cancel(); Assert.AreEqual(0, r.ActiveVisualCount); Assert.AreEqual(0, scene.Count); cases++;
        }
        Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?8:2, cases);
    }

    [TestMethod]
    public void AllNativePaletteHelperProbesPreserveSourceUnfadedColorsAndExactIntegerChannels()
    {
        using var doc = Vectors("motion"); int rows = 0;
        foreach (var c in doc.RootElement.GetProperty("blends").EnumerateArray())
        {
            ushort[] expected = Enumerable.Range(0, 512).Select(OriginalPaletteColor).ToArray();
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                foreach (var ev in row.GetProperty("events").EnumerateArray())
                {
                    int[] e = N(ev); if (e[0] != 3) continue;
                    for (int i = e[1]; i < e[1] + e[2]; i++) expected[i] = SourcePaletteMath.Blend(OriginalPaletteColor(i), e[3], (ushort)e[4]);
                }
                for (int b = 0; b < PaletteBanks.Length; b++) CollectionAssert.AreEqual(N(row.GetProperty("palettes")[b]), expected.Skip(PaletteBanks[b] * 16).Take(16).Select(v => (int)v).ToArray());
                rows++;
            }
        }
        Assert.AreEqual(doc.RootElement.GetProperty("profile").GetString()=="full"?248:91, rows);
    }

    [TestMethod]
    public void PanAdjustNoneEverySignedByteIncrementDelayOvershootAndActorSideMatchesSource()
    {
        using var doc = Vectors("pan"); int cases = 0, rows = 0;
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray()) for (int actor = 0; actor < 4; actor++)
        {
            int[] args = N(c.GetProperty("args")); var audio = new Audio(); var battlers = Battlers(); var scene = new SourceBattleAnimation.SpriteScene();
            var script = Script(241) with { Commands = [new("panse_adjustnone", ["SOURCE_PAN", .. args.Select(Text)], 1), new("waitsound", [], 2), new("end", [], 3)], SoundEffects = new Dictionary<string, int> { ["SOURCE_PAN"] = 123 }, Labels = new Dictionary<string, int>(), Subroutines = new Dictionary<string, SourceAnimationCommand[]>(), SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() };
            var r = new SourceBattleAnimation(script, Cue(241, actor, actor ^ 1), battlers, true, audio, spriteScene: scene); Begin(r, false);
            foreach (var item in c.GetProperty("rows").EnumerateArray())
            {
                int[] e = N(item); if (e[0] != 0) r.Tick(); string at = $"pan actor={actor} args={string.Join(',', args)} tick={e[0]}";
                var tasks = Field<IEnumerable>(r, "_soundTasks").Cast<object>().ToArray(); Assert.AreEqual(e[2], tasks.Length, at); Assert.AreEqual(e[4], audio.Cues.Count, at); Assert.AreEqual(e[5], audio.Pans.Count, at); Assert.AreEqual(e[3], audio.Pans.Count == 0 ? audio.Cues.Last().Pan : audio.Pans.Last().Pan, at);
                if (tasks.Length != 0) { Assert.AreEqual(e[11], Field<int>(tasks[0], "_pan"), at); Assert.AreEqual(e[15], Field<int>(tasks[0], "_counter"), at); }
                rows++;
            }
            Finish(r, scene, battlers); cases++;
        }
        Assert.AreEqual(28, cases); Assert.AreEqual(1340, rows);
    }

    [TestMethod]
    public void MotionProfilesKeepRecipesDirectionsPartnerVisibilityAndPaletteBankBoundaries()
    {
        using var data=Vectors("motion");var root=data.RootElement;
        var crescents=root.GetProperty("crescents").EnumerateArray().ToArray();
        Assert.AreEqual(7,crescents.Select(c=>string.Join(",",N(c.GetProperty("args")).Take(3))).Distinct().Count());
        CollectionAssert.AreEquivalent(new[]{false,true},crescents.Select(c=>(c.GetProperty("attacker").GetInt32()&1)!=0&&c.GetProperty("args")[3].GetInt32()!=0).Distinct().ToArray());
        var heat=root.GetProperty("heat").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[]{3,15},heat.Select(c=>c.GetProperty("visibleMask").GetInt32()).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{0,1},heat.Select(c=>c.GetProperty("attacker").GetInt32()&1).Distinct().ToArray());
        var blends=root.GetProperty("blends").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(Enumerable.Range(0,6).ToArray(),blends.Select(c=>c.GetProperty("kind").GetInt32()).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{0,3,15},blends.Where(c=>c.GetProperty("kind").GetInt32()==4).Select(c=>c.GetProperty("paletteIndex").GetInt32()).Distinct().ToArray());
        Assert.AreEqual(154,root.GetProperty("abi").GetArrayLength(),"Material fragment indices retain the complete original ABI order.");
    }
}

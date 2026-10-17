using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class ClawSourceVectorTests
{
    private const string ClawTemplate = "gClawSlashSpriteTemplate";
    private const string ShakeTemplate = "gShakeMonOrTerrainSpriteTemplate";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly PixelPoint[] Centers = [new(32, 88), new(200, 40), new(90, 96), new(152, 32)];
    private static readonly PixelPoint[] SourcePos2 = [new(3, -2), new(-5, 4), new(7, -6), new(-9, 8)];
    private sealed class Audio : ISourceAnimationAudio
    {
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) { }
        public void StopAnimationEffects() { }
    }
    private static string Folder => Path.Combine(TestAssets.Root, "content/generated/battle/animations");
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static JsonDocument Vectors() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
        "tests/Game.Presentation.Tests/Fixtures/claw-shake-source-vectors.json")));
    private static int[] Numbers(JsonElement values) => values.EnumerateArray().Select(v => v.GetInt32()).ToArray();
    private static AnimationBattler[] Battlers(bool sourceProjectionCenters = false) => Enumerable.Range(0, 4)
        .Select(i => new AnimationBattler(new(i), "b" + i, 0, 0, 64, 64) {
            DisplayCenter = sourceProjectionCenters ? Centers[i] : null
        }).ToArray();
    private static SourceAnimationScript ClawScript(SourceAnimationScript source, int[] args) => source with {
        Commands = [new("loadspritegfx", ["ANIM_TAG_CLAW_SLASH"], 1),
            new("createsprite", new[] { ClawTemplate, "ANIM_TARGET", "2" }.Concat(args.Select(v => v.ToString())).ToArray(), 2),
            new("waitforvisualfinish", [], 3), new("end", [], 4)]
    };
    private static SourceAnimationScript ShakeScript(SourceAnimationScript source, int[] args) => source with {
        Commands = [new("createsprite", new[] { ShakeTemplate, "ANIM_ATTACKER", "2" }.Concat(args.Select(v => v.ToString())).ToArray(), 1),
            new("waitforvisualfinish", [], 2), new("end", [], 3)]
    };
    private static SourceBattleAnimation Runtime(SourceAnimationScript script, int actor, int target, bool sourceProjectionCenters = false) =>
        new(script, new("claw-source-vectors", 1, script.MoveId, new(actor), new(target)), Battlers(sourceProjectionCenters), true, new Audio());
    private static void Finish(SourceBattleAnimation runtime, string context)
    {
        for (int i = 0; i < 8 && runtime.Active; i++) runtime.Tick();
        Assert.IsFalse(runtime.Active, context);
        Assert.AreEqual(0, runtime.ActiveVisualCount, context);
        Assert.IsFalse(runtime.GraphicsLoaded, context);
    }

    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void AllClawPlacementsFrameSelectionsAndCallbackRetirementMatchOriginalC(int actor)
    {
        using var vectors = Vectors();
        var section = vectors.RootElement.GetProperty("claw");
        var configs = section.GetProperty("configs").EnumerateArray().ToArray();
        var source = Script(232);
        var recipe = source.SpriteRecipes[ClawTemplate];
        Assert.AreEqual("AnimClawSlash", recipe.Callback);
        int cases = 0;
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        for (int config = 0; config < configs.Length; config++)
        {
            var args = Numbers(configs[config]);
            var player = new SourceSpriteFrameProgram(recipe, args[2]);
            var runtime = Runtime(ClawScript(source, args), actor, target);
            // loadspritegfx consumes three source script frames. The creation frame
            // also runs the sprite engine, so its returned state is source tick 0.
            for (int tick = 0; tick < 3; tick++) Assert.AreEqual(0, runtime.Tick().Sprites.Length);
            string key = $"{actor}:{target}:{config}";
            foreach (var row in section.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v = Numbers(row);
                string context = key + " tick " + v[0];
                if (v[0] >= 0 && v[8] != 0) player.Step();
                Assert.AreEqual(v[10], args[2], context);
                Assert.AreEqual(v[5], player.Frame.Tile, context);
                Assert.AreEqual(v[6] != 0, player.Frame.HFlip, context);
                Assert.AreEqual(v[7] != 0, player.Frame.VFlip, context);
                Assert.AreEqual(v[11] != 0, player.Ended, context);
                Assert.AreEqual(v[13], player.DelayCounter, context);
                Assert.AreEqual(recipe.Animations[args[2]][v[12]], player.Frame, context);
                if (v[0] < 0) continue; // The selected player above checks BeginAnim.
                var frame = runtime.Tick();
                CollectionAssert.AreEqual(args, frame.AnimationArguments.Take(3).Select(a => (int)a).ToArray(), context);
                Assert.AreEqual(v[8], frame.Sprites.Length, context);
                Assert.AreEqual(v[8], runtime.ActiveVisualCount, context);
                if (v[8] == 0) continue;
                var sprite = frame.Sprites.Single();
                Assert.AreEqual(ClawTemplate, sprite.Template, context);
                Assert.AreEqual(new PixelPoint(v[1], v[2]), sprite.Position, context);
                Assert.AreEqual(new PixelPoint(v[3], v[4]), sprite.Offset, context);
                Assert.AreEqual(v[5], sprite.Tile, context);
                Assert.AreEqual(v[6] != 0, sprite.HFlip, context);
                Assert.AreEqual(v[7] != 0, sprite.VFlip, context);
                Assert.AreEqual("claw-slash", sprite.Asset, context);
                Assert.AreEqual(32, sprite.Width, context); Assert.AreEqual(32, sprite.Height, context);
                Assert.IsTrue(sprite.Visible, context); Assert.IsFalse(sprite.Blend, context);
                Assert.IsFalse(sprite.Affine, context); Assert.IsFalse(sprite.DoubleSize, context);
                // Original END and stored-callback swap rows remain visible. The
                // next source row destroys the sprite; END must not retire it early.
                if (v[9] == 1) Assert.IsTrue(player.Ended, context);
            }
            Finish(runtime, key); cases++;
        }
        Assert.AreEqual(12, cases); // Four DataRows cover every one of the 48 source cases.
    }

    private static T Field<T>(object instance, string name) => (T)(instance.GetType().GetField(name, PrivateInstance)
        ?.GetValue(instance) ?? throw new InvalidOperationException("Missing source-state field " + name));
    private static void SetField(object instance, string name, object value) =>
        (instance.GetType().GetField(name, PrivateInstance) ?? throw new InvalidOperationException("Missing source-state field " + name))
        .SetValue(instance, value);
    private static ushort Register(SourceBattleAnimation runtime, string name) =>
        (ushort)(typeof(SourceBattleAnimation).GetProperty(name, PrivateInstance)?.GetValue(runtime)
            ?? throw new InvalidOperationException("Missing source register " + name));
    private static void SetRegister(SourceBattleAnimation runtime, string name, int value) =>
        (typeof(SourceBattleAnimation).GetProperty(name, PrivateInstance) ?? throw new InvalidOperationException("Missing source register " + name))
        .SetValue(runtime, unchecked((ushort)value));
    private static int EnabledMask(SourceBattleAnimation runtime)
    {
        var enabled = Field<bool[]>(runtime, "_spriteCoordOffsetEnabled");
        int mask = 0;
        for (int slot = 0; slot < 4; slot++) if (enabled[slot]) mask |= 1 << slot;
        return mask;
    }
    private static void InitializeGlobals(SourceBattleAnimation runtime, int[] initial)
    {
        SetRegister(runtime, "BackgroundScrollX", initial[0]);
        SetRegister(runtime, "BackgroundScrollY", initial[1]);
        SetField(runtime, "_spriteCoordOffsetX", unchecked((ushort)initial[2]));
        SetField(runtime, "_spriteCoordOffsetY", unchecked((ushort)initial[3]));
        var enabled = Field<bool[]>(runtime, "_spriteCoordOffsetEnabled");
        for (int slot = 0; slot < 4; slot++) enabled[slot] = (initial[4] & (1 << slot)) != 0;
        Array.Copy(SourcePos2, Field<PixelPoint[]>(runtime, "_offsets"), 4);
    }
    private static object AddShakeController(SourceBattleAnimation runtime, int[] args)
    {
        var type = typeof(SourceBattleAnimation).GetNestedType("TerrainShakeController", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing original shake controller adapter.");
        var controller = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { 4, 2, args, runtime }, null)
            ?? throw new InvalidOperationException("Could not initialize original shake controller adapter.");
        var registry=Field<object>(runtime,"_sprites");
        registry.GetType().GetMethod("Add")!.Invoke(registry,[controller]);
        return controller;
    }
    private static SourceBattleAnimation HoldScript(SourceAnimationScript source, int actor, int target) =>
        Runtime(source with { Commands = [new("waitforvisualfinish", [], 1), new("end", [], 2)] }, actor, target, true);

    private static void AssertShakeRow(SourceBattleAnimation runtime, object controller, SourceAnimationFrame frame,
        int[] v, string context)
    {
        int bgX = Register(runtime, "BackgroundScrollX"), bgY = Register(runtime, "BackgroundScrollY");
        int globalX = Field<ushort>(runtime, "_spriteCoordOffsetX"), globalY = Field<ushort>(runtime, "_spriteCoordOffsetY");
        Assert.AreEqual(v[1], bgX, context); Assert.AreEqual(v[2], bgY, context);
        Assert.AreEqual(v[3], (int)unchecked((short)globalX), context);
        Assert.AreEqual(v[4], (int)unchecked((short)globalY), context);
        Assert.AreEqual(v[5], EnabledMask(runtime), context);
        Assert.AreEqual(v[6], runtime.ActiveVisualCount, context);
        // The source row records live inUse. A reflection-created controller
        // has not entered the public OAM pass at init (-1).
        Assert.AreEqual(v[0] < 0 ? 0 : v[6], frame.Sprites.Length, context);
        bool finished = (bool)(controller.GetType().GetProperty("Finished")!.GetValue(controller)!);
        Assert.AreEqual(v[6] == 0, finished, context);
        var controllerFrame = (SourceVisualSpriteFrame)controller.GetType().GetProperty("Frame")!.GetValue(controller)!;
        Assert.AreEqual(v[7] != 0, !controllerFrame.Visible, context);
        Assert.AreEqual(ShakeTemplate, controllerFrame.Template, context);
        Assert.IsNull(controllerFrame.Asset, context);
        Assert.AreEqual(v[8], Field<int>(controller, "_delta"), context);
        Assert.AreEqual(v[9], Field<int>(controller, "_delay"), context);
        Assert.AreEqual(v[10], Field<int>(controller, "_period"), context);
        Assert.AreEqual(v[11], Field<int>(controller, "_remaining"), context);
        Assert.AreEqual(v[12], (int)Field<ushort>(controller, "_original"), context);
        Assert.AreEqual(v[13], Field<int>(controller, "_mode"), context);
        Assert.AreEqual(v[14], Field<int>(controller, "_mode"), context);
        Assert.AreEqual(v[15], new[] { bgX, bgY, globalX, globalY }[v[14]], context);
        Assert.IsNotNull(frame.MoveBackground, context);
        Assert.AreEqual(v[1], frame.MoveBackground.ScrollX, context);
        Assert.AreEqual(v[2], frame.MoveBackground.ScrollY, context);
        CollectionAssert.AreEqual(SourcePos2, Field<PixelPoint[]>(runtime, "_offsets"), context);
        CollectionAssert.AreEqual(SourcePos2, frame.Offsets, context);
        for (int slot = 0; slot < 4; slot++)
        {
            int column = 16 + slot * 4;
            Assert.AreEqual(new PixelPoint(v[column], v[column + 1]), frame.Offsets[slot], context);
            if (v[0] < 0) continue; // Init precedes the first public BuildOamBuffer pass.
            // Original-C vectors expose unwrapped centers. Public ObjectOffsets
            // deliberately incorporates the hardware's 9-bit X / 8-bit Y wrap.
            int x = ((v[column + 2] - 32) & 511) - Centers[slot].X + 32;
            if (x > 255) x -= 512;
            int y = ((v[column + 3] - 32) & 255) - Centers[slot].Y + 32;
            Assert.AreEqual(new PixelPoint(x, y), frame.ObjectOffsets[slot], context + " actor " + slot);
        }
    }

    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void AllTerrainShakeRegistersEnableMasksUnsignedRestorationAndObjProjectionMatchOriginalC(int actor)
    {
        using var vectors = Vectors();
        var section = vectors.RootElement.GetProperty("shake");
        var configs = section.GetProperty("configs").EnumerateArray().ToArray();
        var initials = section.GetProperty("initials").EnumerateArray().ToArray();
        var source = Script(306);
        int cases = 0;
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        for (int config = 0; config < configs.Length; config++)
        for (int initial = 0; initial < initials.Length; initial++)
        {
            var runtime = HoldScript(source, actor, target);
            InitializeGlobals(runtime, Numbers(initials[initial]));
            // Reflection is restricted to source global storage and the private
            // controller seam, allowing init (-1) and prior-register ownership to
            // be observed. All later frames run the real VM's Tick/OAM path.
            var controller = AddShakeController(runtime, Numbers(configs[config]));
            string key = $"{actor}:{target}:{config}:{initial}";
            foreach (var row in section.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v = Numbers(row);
                var frame = v[0] < 0 ? runtime.Snapshot() : runtime.Tick();
                AssertShakeRow(runtime, controller, frame, v, key + " tick " + v[0]);
            }
            Finish(runtime, key); cases++;
        }
        Assert.AreEqual(120, cases); // Four DataRows cover all 480 source cases.
    }

    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void NativeClawShakeIsCreatedAndSteppedThroughTheRealVmInTheSameFrame(int actor)
    {
        using var vectors = Vectors();
        var section = vectors.RootElement.GetProperty("shake");
        var args = new[] { -4, 1, 10, 3, 1 };
        int config = section.GetProperty("configs").EnumerateArray().Select((v, i) => (Values: Numbers(v), Index: i))
            .Single(v => v.Values.SequenceEqual(args)).Index;
        int target = actor ^ 1;
        var runtime = Runtime(ShakeScript(Script(232), args), actor, target, true);
        InitializeGlobals(runtime, Numbers(section.GetProperty("initials")[1]));
        object? controller = null;
        foreach (var row in section.GetProperty("cases").GetProperty($"{actor}:{target}:{config}:1").EnumerateArray())
        {
            var v = Numbers(row);
            if (v[0] < 0) continue;
            var frame = runtime.Tick();
            controller ??= ((IEnumerable)Field<object>(runtime,"_sprites")).Cast<object>().First();
            AssertShakeRow(runtime, controller, frame, v, "native VM tick " + v[0]);
            CollectionAssert.AreEqual(args, frame.AnimationArguments.Take(5).Select(a => (int)a).ToArray());
        }
        Finish(runtime, "native shake root");
    }

    [TestMethod]
    public void CancellingShakeRestoresGlobalRegistersFlagsAndOrdinaryOffsetsIdempotently()
    {
        var source = Script(306);
        foreach (int mode in Enumerable.Range(0, 4))
        foreach (int ticks in new[] { 0, 1, 6, 11 })
        {
            var runtime = HoldScript(source, 3, 1);
            InitializeGlobals(runtime, [65534, 1, 32766, -32767, 15]);
            AddShakeController(runtime, [-4, 1, 10, mode, 1]);
            for (int tick = 0; tick < ticks; tick++) runtime.Tick();
            runtime.Cancel(); runtime.Cancel();
            var frame = runtime.Snapshot();
            Assert.IsFalse(runtime.Active); Assert.AreEqual(0, runtime.ActiveVisualCount);
            Assert.AreEqual(0, frame.Sprites.Length); Assert.AreEqual(0, EnabledMask(runtime));
            Assert.AreEqual((ushort)0, Register(runtime, "BackgroundScrollX"));
            Assert.AreEqual((ushort)0, Register(runtime, "BackgroundScrollY"));
            Assert.AreEqual((ushort)0, Field<ushort>(runtime, "_spriteCoordOffsetX"));
            Assert.AreEqual((ushort)0, Field<ushort>(runtime, "_spriteCoordOffsetY"));
            Assert.IsTrue(frame.Offsets.All(p => p == default));
            Assert.IsTrue(frame.ObjectOffsets.All(p => p == default));
        }
    }

    [TestMethod]
    public void ClawAndTerrainOperandsFailClosedOutsideSourceWidthsAndReviewedSelectors()
    {
        var claw = Script(232); var shake = Script(306);
        void Reject(SourceAnimationScript script) => Assert.Throws<SourceAnimationException>(() => Runtime(script, 0, 1));
        foreach (var args in new[] { new[] { -32769, 0, 0 }, new[] { 32768, 0, 0 },
            new[] { 0, -32769, 0 }, new[] { 0, 32768, 0 }, new[] { 0, 0, -1 }, new[] { 0, 0, 2 } })
            Reject(ClawScript(claw, args));
        var recipe = claw.SpriteRecipes[ClawTemplate];
        foreach (var bad in new[] { recipe with { Width = 64 }, recipe with { Height = 64 },
            recipe with { Blend = true }, recipe with { AffineDoubleSize = true },
            recipe with { Animations = [recipe.Animations[0]] }, recipe with { Callback = "UnknownClawCallback" } })
        {
            var recipes = claw.SpriteRecipes.ToDictionary(p => p.Key, p => p.Value); recipes[ClawTemplate] = bad;
            Reject(ClawScript(claw with { SpriteRecipes = recipes }, [0, 0, 0]));
        }
        foreach (var args in new[] { new[] { -32769, 1, 10, 3, 1 }, new[] { 32768, 1, 10, 3, 1 },
            new[] { -4, -32769, 10, 3, 1 }, new[] { -4, 32768, 10, 3, 1 },
            new[] { -4, 1, -1, 3, 1 }, new[] { -4, 1, 32768, 3, 1 },
            new[] { -4, 1, 10, -1, 1 }, new[] { -4, 1, 10, 4, 1 },
            new[] { -4, 1, 10, 3, -1 }, new[] { -4, 1, 10, 3, 3 }, new[] { -4, 1, 10, 3 } })
            Reject(ShakeScript(shake, args));
    }
}

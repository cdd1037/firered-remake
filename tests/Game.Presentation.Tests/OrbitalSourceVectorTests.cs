using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class OrbitalSourceVectorTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] Templates =
        ["gHiddenPowerOrbSpriteTemplate", "gHiddenPowerOrbScatterSpriteTemplate", "gSprayWaterDropletSpriteTemplate"];
    private static readonly ushort[] LeafColors = [31, 639, 1023, 992, 32197, 32086, 32438];
    private sealed class Audio : ISourceAnimationAudio
    {
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) { }
        public void StopAnimationEffects() { }
    }

    private static object? ReadField(object instance, string name) =>
        (instance.GetType().GetField(name, Instance) ?? throw new InvalidOperationException("Missing field " + name)).GetValue(instance);
    private static T Field<T>(object instance, string name) =>
        (T)(ReadField(instance, name) ?? throw new InvalidOperationException("Null field " + name));
    private static void SetField(object instance, string name, object value) =>
        (instance.GetType().GetField(name, Instance) ?? throw new InvalidOperationException("Missing field " + name)).SetValue(instance, value);
    private static T Property<T>(object instance, string name) =>
        (T)((instance.GetType().GetProperty(name, Instance) ?? throw new InvalidOperationException("Missing property " + name)).GetValue(instance)
            ?? throw new InvalidOperationException("Null property " + name));
    private static void SetProperty(object instance, string name, object value) =>
        (instance.GetType().GetProperty(name, Instance) ?? throw new InvalidOperationException("Missing property " + name)).SetValue(instance, value);
    private static object? Invoke(object instance, string name, params object[] args) =>
        (instance.GetType().GetMethod(name, Instance) ?? throw new InvalidOperationException("Missing method " + name)).Invoke(instance, args);
    private static object NewNested(string name, params object[] args) =>
        Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing adapter " + name), Instance, null, args, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Could not create " + name);
    private static bool Finished(object callback) => Property<bool>(callback, "Finished");
    private static object Sprites(SourceBattleAnimation owner) => Field<object>(owner, "_sprites");
    private static object[] LiveSprites(SourceBattleAnimation owner) => ((IEnumerable)Sprites(owner)).Cast<object>().ToArray();
    private static IList Tasks(SourceBattleAnimation owner) => Field<IList>(owner, "_tasks");
    private static long[] Numbers(JsonElement row) => row.EnumerateArray().Select(v => v.GetInt64()).ToArray();
    private static int[] Key(string key) => key.Split(':').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
    private static JsonDocument Vectors(string file) => SourceVectorFixture.Read(file, file == "orbit_spray_vectors.json"
        ? "python tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py --root . --section sprites --output tests/Game.Presentation.Tests/Fixtures/orbit_spray_vectors.json"
        : "python tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py --root . --section tasks --output tests/Game.Presentation.Tests/Fixtures/magical_sky_stretch_vectors.json");

    private sealed class Columns(JsonElement root, string name)
    {
        private readonly Dictionary<string, int> _indices = root.GetProperty(name).EnumerateArray()
            .Select((v, i) => (Name: v.GetString()!, Index: i)).ToDictionary(v => v.Name, v => v.Index);
        public long Read(long[] row, string name) => row[_indices[name]];
        public int Int(long[] row, string name) => checked((int)Read(row, name));
    }

    private static AnimationBattler[] Battlers(JsonElement root, bool doubles)
    {
        var geometry = root.GetProperty("geometry");
        var battlers = Enumerable.Range(0, doubles ? 4 : 2).Select(i => new AnimationBattler(new(i), "b" + i,
            geometry.GetProperty("yOffset")[i].GetInt32(), geometry.GetProperty("elevation")[i].GetInt32(), 64, 64)).ToArray();
        for (int i = 0; i < battlers.Length; i++)
        {
            Assert.AreEqual(new PixelPoint(geometry.GetProperty("x")[doubles ? 1 : 0][i].GetInt32(),
                geometry.GetProperty("y")[doubles ? 1 : 0][i].GetInt32()), battlers[i].Base(doubles));
            Assert.AreEqual(geometry.GetProperty("subpriority")[i].GetInt32(), battlers[i].Subpriority);
        }
        return battlers;
    }

    private static SourceBattleAnimation Runtime(SourceAnimationScript script, JsonElement root, int[] key, uint seed = 0) =>
        new(script with { Commands = [new("end", [], 1)], Subroutines = new Dictionary<string, SourceAnimationCommand[]>(),
                Labels = new Dictionary<string, int>(), SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() },
            new("orbital-source-vectors", 1, script.MoveId, new(key[2]), new(key[3])) { VisualSeed = seed },
            Battlers(root, key[1] != 0), key[1] != 0, new Audio());

    private static void Signal(SourceBattleAnimation owner, int tick, int stopTick)
    {
        if (tick == stopTick) Field<short[]>(owner, "_animationArgs")[7] = -1;
    }

    private static void StepSprite(SourceBattleAnimation owner, object sprite)
    {
        Assert.IsFalse(Finished(sprite));
        Invoke(sprite, "Step", owner);
        // Match AnimateSprites: destruction suppresses the ordinary frame/affine pass.
        if (Finished(sprite)) Invoke(Sprites(owner), "Remove", sprite);
        else Invoke(sprite, "AdvanceAnimation", owner);
    }

    private static void StepTask(SourceBattleAnimation owner, object task)
    {
        Assert.IsFalse(Finished(task));
        Invoke(task, "Step", owner);
        if (Finished(task)) Tasks(owner).Remove(task);
    }

    private static void AssertState(SourceBattleAnimation owner, object callback, long[] row, Columns columns, bool task,
        uint seed, int randomDraws, string context)
    {
        Assert.AreEqual(columns.Int(row, "count"), owner.ActiveVisualCount, context + " visual count");
        Assert.AreEqual(columns.Int(row, "count") == 0, Finished(callback), context + " callback retirement");
        Assert.AreEqual(columns.Int(row, "task"), Tasks(owner).Count, context + " task count");
        Assert.AreEqual(task && !Finished(callback), Tasks(owner).Contains(callback), context + " task ownership");
        Assert.AreEqual(columns.Int(row, "arg7"), (int)Field<short[]>(owner, "_animationArgs")[7], context + " arg7");
        int affineObjects = LiveSprites(owner).Count(s => Property<SourceVisualSpriteFrame>(s, "Frame").Affine);
        // Fixture counts exclude fixed battler matrices. This checks the adapter's
        // live affine ownership, while the root tests cover finite matrix admission.
        Assert.AreEqual(columns.Int(row, "matrices_before"), affineObjects, context + " pre-task affine ownership");
        Assert.AreEqual(columns.Int(row, "matrices_after"), affineObjects, context + " post-task affine ownership");
        Assert.AreEqual(columns.Int(row, "rngDraws"), randomDraws, context + " source draw count");
        if (randomDraws == 0)
        {
            Assert.IsNull(ReadField(owner, "_visualRandom"), context + " no RNG must be created");
            Assert.AreEqual((long)seed, columns.Read(row, "rngState"), context + " source RNG unchanged");
        }
        else
        {
            uint afterTwo = unchecked(unchecked(seed * 1103515245u + 24691u) * 1103515245u + 24691u);
            uint actual = Field<uint>(Field<SourceVisualRandom>(owner, "_visualRandom"), "_state");
            Assert.AreEqual(afterTwo, actual, context + " exactly two creation draws and none during motion");
            Assert.AreEqual(columns.Read(row, "rngState"), (long)actual, context + " source RNG state");
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void OrbitalAndSprayInitializersAndEveryOrdinaryCallbackMatchSourceC(int kind)
    {
        using var document = Vectors("orbit_spray_vectors.json");
        var root = document.RootElement;
        var stateColumns = new Columns(root, "columns");
        var spriteColumns = new Columns(root, "spriteColumns");
        var script = AnimationCompositionFixture.Script(kind == 2 ? 310 : 237);
        var recipe = script.SpriteRecipes[Templates[kind]];
        int cases = 0, rows = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateObject().Where(e => Key(e.Name)[0] == kind))
        {
            var key = Key(entry.Name);
            var config = Numbers(root.GetProperty("configs")[kind][key[4]]);
            uint seed = kind == 2 ? checked((uint)config[2]) : 0;
            var owner = Runtime(script, root, key, seed);
            var args = Field<short[]>(owner, "_animationArgs");
            args[0] = checked((short)config[0]);
            if (kind != 1) args[1] = checked((short)config[1]);
            int slot = key[1] == 0 ? 2 : 4;
            int priority = Battlers(root, key[1] != 0)[key[3]].Subpriority - 2;
            var sprite = NewNested("OrbitalParticle", slot, priority, recipe, owner);
            Invoke(Sprites(owner), "Add", sprite);
            int previousTick = -2;
            foreach (var row in entry.Value.EnumerateArray())
            {
                var state = Numbers(row.GetProperty("state"));
                int tick = stateColumns.Int(state, "tick");
                string context = entry.Name + " tick " + tick;
                Assert.AreEqual(previousTick + 1, tick, context + " no omitted source rows"); previousTick = tick;
                if (kind == 0) Signal(owner, tick, checked((int)config[2]));
                if (tick >= 0) StepSprite(owner, sprite);
                AssertState(owner, sprite, state, stateColumns, false, seed, kind == 2 ? 2 : 0, context);
                Assert.AreEqual(0, row.GetProperty("afterTaskSprites").GetArrayLength(), context);
                Assert.AreEqual(0, row.GetProperty("palettes").GetArrayLength(), context);
                var sourceSprites = row.GetProperty("sprites").EnumerateArray().ToArray();
                Assert.AreEqual(sourceSprites.Length, LiveSprites(owner).Length, context);
                if (sourceSprites.Length != 0)
                    AssertParticle(sprite, Numbers(sourceSprites.Single()), spriteColumns, kind, recipe, context);
                rows++;
            }
            Assert.IsTrue(Finished(sprite), entry.Name);
            Assert.AreEqual(0, owner.ActiveVisualCount, entry.Name);
            owner.Cancel(); cases++;
        }
        AssertCoverage(root, cases, rows, kind);
    }

    private static void AssertParticle(object sprite, long[] row, Columns c, int kind, SourceSpriteRecipe recipe, string context)
    {
        int S(string name) => c.Int(row, name);
        var frame = Property<SourceVisualSpriteFrame>(sprite, "Frame");
        Assert.AreEqual(S("slot"), frame.Slot, context);
        Assert.AreEqual(new PixelPoint(S("x"), S("y")), frame.Position, context + " position");
        Assert.AreEqual(new PixelPoint(S("x2"), S("y2")), frame.Offset, context + " offset");
        Assert.AreEqual(S("invisible") == 0, frame.Visible, context);
        Assert.AreEqual(S("tile"), frame.Tile, context);
        Assert.AreEqual(S("hflip") != 0, frame.HFlip, context);
        Assert.AreEqual(S("vflip") != 0, frame.VFlip, context);
        Assert.AreEqual(S("subpriority"), frame.Subpriority, context);
        Assert.AreEqual(new SourceAffineMatrix(S("a"), S("b"), S("c"), S("d")),
            new SourceAffineMatrix(frame.MatrixA, frame.MatrixB, frame.MatrixC, frame.MatrixD), context + " matrix");
        Assert.AreEqual(S("affine") != 0, frame.Affine, context);
        Assert.AreEqual(S("affineMode") == 3, frame.DoubleSize, context);
        Assert.AreEqual(recipe.Template, frame.Template, context); Assert.AreEqual(recipe.Asset, frame.Asset, context);
        Assert.AreEqual(recipe.Width, frame.Width, context); Assert.AreEqual(recipe.Height, frame.Height, context);
        Assert.IsFalse(frame.Blend, context); Assert.AreEqual(256, frame.Scale, context);
        if (kind == 2)
        {
            Assert.IsNull(ReadField(sprite, "_affine"), context);
            Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_vx"), context);
            Assert.AreEqual(S("data1"), (int)Field<short>(sprite, "_vy"), context);
            Assert.AreEqual(S("data2"), (int)Field<short>(sprite, "_direction"), context);
            Assert.AreEqual(S("data3"), (int)Field<short>(sprite, "_ticks"), context);
            // On non-affine source OAM, HFLIP aliases matrixNum bit 3. The
            // fixture's unused affine-state cells therefore need not be 256.
            return;
        }
        var affine = Field<SourceSpriteAffineProgram>(sprite, "_affine");
        Assert.AreEqual(S("affinePaused") != 0, Field<bool>(sprite, "_orbit"), context);
        Assert.AreEqual(S("affineEnded") != 0, affine.Ended, context);
        Assert.AreEqual(S("affineIndex"), Field<int>(affine, "_pc"), context);
        Assert.AreEqual(S("affineDelay"), Field<int>(affine, "_delay"), context);
        Assert.AreEqual(S("xScale"), affine.XScale, context); Assert.AreEqual(S("yScale"), affine.YScale, context);
        Assert.AreEqual(S("rotation"), affine.Rotation, context);
        if (kind == 1)
        {
            Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_vx"), context);
            Assert.AreEqual(S("data1"), (int)Field<short>(sprite, "_vy"), context);
        }
        else
        {
            Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_duration"), context);
            Assert.AreEqual(S("data1"), (int)Field<short>(sprite, "_angle"), context);
            Assert.AreEqual(S("data2"), (int)Field<short>(sprite, "_radiusX"), context);
            Assert.AreEqual(S("data3"), (int)Field<short>(sprite, "_radiusY"), context);
            Assert.AreEqual(S("data4"), (int)Field<short>(sprite, "_ticks"), context);
            Assert.AreEqual(S("data5"), (int)Field<short>(sprite, "_phase"), context);
            Assert.AreEqual(S("data7"), Field<int>(sprite, "_basePriority"), context);
        }
    }

    private static ushort PaletteInput(int slot, int color) => checked((ushort)(((256 + slot * 16 + color) * 73 + 31) & 32767));
    private static void InitializePalettes(SourceBattleAnimation owner)
    {
        var faded = Field<ushort[][]>(owner, "_spritePaletteBanks");
        var unfaded = Field<ushort[][]>(owner, "_spritePaletteUnfadedBanks");
        // Source-C probes deliberately include slots 0..3; production allocation
        // admission still reserves those battler banks and permits only 4..15.
        for (int slot = 0; slot < 16; slot++) for (int color = 0; color < 16; color++)
            faded[slot][color] = unfaded[slot][color] = PaletteInput(slot, color);
    }

    [TestMethod]
    public void MagicalLeafPaletteInitDelaySevenColorCyclesAndStopWriteMatchAllSourceRows()
    {
        using var document = Vectors("magical_sky_stretch_vectors.json"); var root = document.RootElement;
        var columns = new Columns(root, "columns"); var script = AnimationCompositionFixture.Script(345);
        var actualColors = (ushort[])typeof(SourceBattleAnimation).GetField("MagicalLeafColors", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        CollectionAssert.AreEqual(LeafColors, actualColors, "The seven source RGB colors must remain exact.");
        int cases = 0, rows = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateObject().Where(e => Key(e.Name)[0] == 3))
        {
            var key = Key(entry.Name); var config = Numbers(root.GetProperty("configs")[3][key[4]]);
            var slots = new[] { checked((int)config[0]), checked((int)config[1]) };
            var owner = Runtime(script, root, key); InitializePalettes(owner);
            var task = NewNested("MagicalLeafPaletteTask", (object)slots);
            SetField(task, "_delay", checked((short)config[3]));
            Signal(owner, -1, checked((int)config[2])); Invoke(task, "Initialize", owner);
            if (!Finished(task)) Tasks(owner).Add(task);
            int previousTick = -2;
            foreach (var row in entry.Value.EnumerateArray())
            {
                var state = Numbers(row.GetProperty("state")); int S(string name) => columns.Int(state, name);
                int tick = S("tick"); string context = entry.Name + " tick " + tick;
                Assert.AreEqual(previousTick + 1, tick, context); previousTick = tick;
                var before = Field<ushort[][]>(owner, "_spritePaletteBanks").Select(p => (ushort[])p.Clone()).ToArray();
                int coefficient = Field<int>(task, "_coefficient"), colorIndex = Field<int>(task, "_color");
                if (tick >= 0) { Signal(owner, tick, checked((int)config[2])); StepTask(owner, task); }
                AssertState(owner, task, state, columns, true, 0, 0, context);
                Assert.AreEqual(S("taskData9"), (int)Field<short>(task, "_delay"), context + " signed delay");
                Assert.AreEqual(S("taskData10"), Field<int>(task, "_coefficient"), context + " coefficient");
                Assert.AreEqual(S("taskData11"), Field<int>(task, "_color"), context + " color index");
                Assert.AreEqual((16 + slots[0]) * 16, S("taskData8"), context);
                Assert.AreEqual((16 + slots[1]) * 16, S("taskData12"), context);
                Assert.AreEqual(1, S("taskData0"), context + " synchronous phase-zero bank caching");
                Assert.AreEqual(0, row.GetProperty("sprites").GetArrayLength(), context);
                Assert.AreEqual(0, row.GetProperty("afterTaskSprites").GetArrayLength(), context);
                var sourcePalettes = row.GetProperty("palettes").EnumerateArray().Select(Numbers).ToArray();
                Assert.AreEqual(2, sourcePalettes.Length, context);
                var faded = Field<ushort[][]>(owner, "_spritePaletteBanks");
                var unfaded = Field<ushort[][]>(owner, "_spritePaletteUnfadedBanks");
                foreach (var palette in sourcePalettes)
                    CollectionAssert.AreEqual(palette.Skip(1).Select(v => checked((ushort)v)).ToArray(), faded[checked((int)palette[0])], context);
                for (int slot = 0; slot < 16; slot++)
                {
                    var baseline = Enumerable.Range(0, 16).Select(i => PaletteInput(slot, i)).ToArray();
                    CollectionAssert.AreEqual(baseline, unfaded[slot], context + " unfaded bank " + slot);
                    if (!slots.Contains(slot)) CollectionAssert.AreEqual(baseline, faded[slot], context + " unrelated bank " + slot);
                    if (S("blendWrites") == 0) CollectionAssert.AreEqual(before[slot], faded[slot], context + " no blend before delay expires");
                }
                if (S("blendWrites") != 0)
                {
                    Assert.AreEqual(2, S("blendWrites"), context);
                    Assert.AreEqual((16 + slots[0]) * 16, S("blendOffset0"), context);
                    Assert.AreEqual((16 + slots[1]) * 16, S("blendOffset1"), context);
                    Assert.AreEqual(coefficient, S("blendCoefficient"), context);
                    Assert.AreEqual((int)LeafColors[colorIndex], S("blendColor"), context);
                }
                rows++;
            }
            Assert.IsTrue(Finished(task), entry.Name); Assert.AreEqual(0, owner.ActiveVisualCount, entry.Name);
            owner.Cancel(); cases++;
        }
        Assert.AreEqual(5, cases); Assert.AreEqual(383, rows);
    }

    [TestMethod]
    public void SkyUppercutInitializationSignedDelayUnsignedWrapSidesAndStopMatchAllSourceRows()
    {
        using var document = Vectors("magical_sky_stretch_vectors.json"); var root = document.RootElement;
        var columns = new Columns(root, "columns"); var script = AnimationCompositionFixture.Script(327);
        int cases = 0, rows = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateObject().Where(e => Key(e.Name)[0] == 4))
        {
            var key = Key(entry.Name); var config = Numbers(root.GetProperty("configs")[4][key[4]]);
            var owner = Runtime(script, root, key);
            SetProperty(owner, "BackgroundScrollX", unchecked((ushort)config[2]));
            SetProperty(owner, "BackgroundScrollY", unchecked((ushort)config[3]));
            SetProperty(owner, "BackgroundScreenSize", 0); // TryCreateSlidingBackgroundTask's phase-zero setup.
            var task = NewNested("SkyUppercutBackgroundTask", checked((short)config[0]), (key[3] & 1) == 0);
            Signal(owner, -1, checked((int)config[1])); Invoke(task, "Initialize", owner);
            if (!Finished(task)) Tasks(owner).Add(task);
            int previousTick = -2;
            foreach (var row in entry.Value.EnumerateArray())
            {
                var state = Numbers(row.GetProperty("state")); int S(string name) => columns.Int(state, name);
                int tick = S("tick"); string context = entry.Name + " tick " + tick;
                Assert.AreEqual(previousTick + 1, tick, context); previousTick = tick;
                if (tick >= 0) { Signal(owner, tick, checked((int)config[1])); StepTask(owner, task); }
                AssertState(owner, task, state, columns, true, 0, 0, context);
                Assert.AreEqual(S("taskData0"), (int)Field<short>(task, "_state"), context);
                Assert.AreEqual(S("taskData8"), (int)Field<short>(task, "_delay"), context);
                Assert.AreEqual(S("taskData9"), (int)Field<short>(task, "_fx"), context);
                Assert.AreEqual(S("taskData10"), (int)Field<short>(task, "_fy"), context);
                var background = owner.Snapshot().MoveBackground; Assert.IsNotNull(background, context);
                Assert.AreEqual(S("bg3x"), background.ScrollX, context + " unsigned BG3 X");
                Assert.AreEqual(S("bg3y"), background.ScrollY, context + " unsigned BG3 Y");
                Assert.AreEqual(S("bgMode"), background.ScreenSize, context);
                Assert.AreEqual(Finished(task) ? 2 : 1, S("toggleCalls"), context + " init and terminal BG mode boundaries");
                Assert.AreEqual(0, row.GetProperty("sprites").GetArrayLength(), context);
                Assert.AreEqual(0, row.GetProperty("afterTaskSprites").GetArrayLength(), context);
                Assert.AreEqual(0, row.GetProperty("palettes").GetArrayLength(), context); rows++;
            }
            Assert.IsTrue(Finished(task), entry.Name); Assert.AreEqual(0, owner.ActiveVisualCount, entry.Name);
            Assert.AreEqual(0, (int)Property<ushort>(owner, "BackgroundScrollX"));
            Assert.AreEqual(0, (int)Property<ushort>(owner, "BackgroundScrollY"));
            Assert.AreEqual(1, Property<int>(owner, "BackgroundScreenSize"));
            owner.Cancel(); cases++;
        }
        Assert.AreEqual(180, cases); Assert.AreEqual(2620, rows);
    }

    private sealed record TargetState(PixelPoint Position, PixelPoint Offset, int Priority, bool DoubleSize, SourceAffineMatrix Matrix);
    private static TargetState Target(SourceBattleAnimation owner, int slot, bool doubles)
    {
        var battler = Field<Dictionary<int, AnimationBattler>>(owner, "_battlers")[slot];
        return new(battler.Center(doubles, true), Field<PixelPoint[]>(owner, "_offsets")[slot], battler.Subpriority,
            Field<bool[]>(owner, "_affineMode")[slot], new(Field<int[]>(owner, "_xScale")[slot], Field<int[]>(owner, "_matrixB")[slot],
                Field<int[]>(owner, "_matrixC")[slot], Field<int[]>(owner, "_yScale")[slot]));
    }
    private static void AssertTarget(TargetState target, JsonElement source, Columns c, int slot, string context)
    {
        Assert.AreEqual(1, source.GetArrayLength(), context); var row = Numbers(source[0]); int S(string name) => c.Int(row, name);
        Assert.AreEqual(slot, S("slot"), context);
        Assert.AreEqual(new PixelPoint(S("x"), S("y")), target.Position, context + " fixed battler position");
        Assert.AreEqual(new PixelPoint(S("x2"), S("y2")), target.Offset, context + " battler offset");
        Assert.AreEqual(S("subpriority"), target.Priority, context);
        Assert.AreEqual(new SourceAffineMatrix(S("a"), S("b"), S("c"), S("d")), target.Matrix, context + " task matrix");
        // Source battlers retain their fixed normal-affine allocation at reset.
        // The managed flag denotes prepared double-size mode, not that allocation.
        Assert.AreEqual(S("affineMode") == 3, target.DoubleSize, context);
        Assert.AreEqual(S("affinePaused") != 0, target.DoubleSize, context);
        Assert.AreEqual(1, S("affine"), context);
        Assert.AreEqual(0, S("invisible"), context); Assert.AreEqual(0, S("tile"), context);
        Assert.AreEqual(0, S("hflip"), context); Assert.AreEqual(0, S("vflip"), context);
        Assert.AreEqual(slot, S("data0"), context);
    }

    [TestMethod]
    public void StretchTargetPreTaskOamAndPostTaskShakeAffineFootAnchorAndResetMatchAllSourceRows()
    {
        using var document = Vectors("magical_sky_stretch_vectors.json"); var root = document.RootElement;
        var columns = new Columns(root, "columns"); var spriteColumns = new Columns(root, "spriteColumns");
        var script = AnimationCompositionFixture.Script(327);
        var commands = script.TaskAffinePrograms["sAffineAnims_StretchBattlerUp"];
        CollectionAssert.AreEqual(new SourceTaskAffineCommand[] { new("frame", 10, -13, 0, 10), new("frame", -10, 13, 0, 10), new("end") }, commands);
        int cases = 0, rows = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateObject().Where(e => Key(e.Name)[0] == 5))
        {
            var key = Key(entry.Name); int slot = key[3]; var owner = Runtime(script, root, key);
            var task = NewNested("StretchTargetTask", owner, new AnimationSlot(slot), commands);
            Tasks(owner).Add(task); var program = Field<SourceTaskAffineProgram>(task, "_program");
            int previousTick = -2;
            foreach (var row in entry.Value.EnumerateArray())
            {
                var state = Numbers(row.GetProperty("state")); int S(string name) => columns.Int(state, name);
                int tick = S("tick"); string context = entry.Name + " tick " + tick;
                Assert.AreEqual(previousTick + 1, tick, context); previousTick = tick;
                // AnimateSprites observes the previously written battler fields;
                // ordinary tasks run afterward. Capture both sides independently.
                var before = Target(owner, slot, key[1] != 0);
                AssertTarget(before, row.GetProperty("sprites"), spriteColumns, slot, context + " pre-task");
                if (tick >= 0) StepTask(owner, task);
                AssertTarget(Target(owner, slot, key[1] != 0), row.GetProperty("afterTaskSprites"), spriteColumns, slot, context + " post-task");
                AssertState(owner, task, state, columns, true, 0, 0, context);
                Assert.AreEqual(tick + 2, S("taskData0"), context + " init plus ordinary calls");
                Assert.AreEqual(S("taskData7"), program.CommandIndex, context);
                Assert.AreEqual(S("taskData8"), program.FrameTicks, context);
                Assert.AreEqual(S("taskData9"), program.LoopRemaining, context);
                Assert.AreEqual(S("taskData10"), program.XScale, context);
                Assert.AreEqual(S("taskData11"), program.YScale, context);
                Assert.AreEqual(S("taskData12"), program.Rotation, context);
                Assert.AreEqual(S("taskData15"), Field<int>(task, "_slot"), context);
                Assert.AreEqual(Finished(task), program.Ended, context);
                for (int i = 0; i < (key[1] == 0 ? 2 : 4); i++) if (i != slot)
                {
                    var other = Target(owner, i, key[1] != 0);
                    Assert.AreEqual(default, other.Offset, context + " unaffected battler " + i);
                    Assert.AreEqual(new SourceAffineMatrix(256, 0, 0, 256), other.Matrix, context);
                    Assert.IsFalse(other.DoubleSize, context);
                }
                Assert.AreEqual(0, row.GetProperty("palettes").GetArrayLength(), context); rows++;
            }
            Assert.IsTrue(Finished(task), entry.Name); Assert.AreEqual(0, owner.ActiveVisualCount, entry.Name);
            Assert.AreEqual(default, Target(owner, slot, key[1] != 0).Offset, entry.Name);
            Assert.IsFalse(Target(owner, slot, key[1] != 0).DoubleSize, entry.Name);
            owner.Cancel(); cases++;
        }
        Assert.AreEqual(20, cases); Assert.AreEqual(440, rows);
    }

    private static void AssertCoverage(JsonElement root, int cases, int rows, int kind)
    {
        string? profile = root.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full", "Explicit source-vector profile is required.");
        string[] required =
        [
            // All branch/boundary configurations: phases, durations, stop transitions, RNG signs and origins
            "0:0:0:1:0","0:0:0:1:1","0:0:0:1:2","0:0:0:1:3","0:0:0:1:4","0:0:0:1:5","0:0:0:1:6","0:0:0:1:7","0:0:0:1:8","0:0:0:1:9","0:0:0:1:10","0:0:0:1:11","0:0:0:1:12","0:0:0:1:13","0:0:0:1:14","0:0:0:1:15",
            // All branch/boundary configurations: phases, durations, stop transitions, RNG signs and origins
            "1:0:0:1:0","1:0:0:1:1","1:0:0:1:2","1:0:0:1:3","1:0:0:1:4","1:0:0:1:5","1:0:0:1:6","1:0:0:1:7",
            // All branch/boundary configurations: phases, durations, stop transitions, RNG signs and origins
            "2:0:0:1:0","2:0:0:1:1","2:0:0:1:2","2:0:0:1:3","2:0:0:1:4","2:0:0:1:5","2:0:0:1:6","2:0:0:1:7","2:0:0:1:8","2:0:0:1:9","2:0:0:1:10","2:0:0:1:11","2:0:0:1:12","2:0:0:1:13","2:0:0:1:14","2:0:0:1:15",
            // Fast: opposite-side priority and radius wrap
            "0:0:1:0:1","0:0:1:0:10","0:0:1:0:14",
            // Fast: rear-battler picture coordinates and subpriority
            "0:1:2:3:2",
            // Fast: rear-battler picture coordinates and subpriority
            "0:1:3:2:2",
            // Scatter: horizontal exits and both vertical exits from distinct source geometry
            "1:0:1:0:0","1:0:1:0:2","1:0:1:0:4","1:0:1:0:6",
            // Scatter: horizontal exits and both vertical exits from distinct source geometry
            "1:1:2:3:0","1:1:2:3:2","1:1:2:3:4","1:1:2:3:6",
            // Scatter: horizontal exits and both vertical exits from distinct source geometry
            "1:1:3:2:0","1:1:3:2:2","1:1:3:2:4","1:1:3:2:6",
            // Spray: both directions and attacker/target origins on each source side
            "2:0:1:0:0","2:0:1:0:1","2:0:1:0:2","2:0:1:0:3",
            // Spray: both directions and attacker/target origins on each source side
            "2:1:2:3:0","2:1:2:3:1","2:1:2:3:2","2:1:2:3:3",
            // Spray: both directions and attacker/target origins on each source side
            "2:1:3:2:0","2:1:3:2:1","2:1:3:2:2","2:1:3:2:3",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        (int Cases, int Rows)[] expected = profile == "full"
            ? [(320, 11420),(160, 2466),(320, 10240)]
            : [(21, 761),(20, 303),(28, 896)];
        Assert.AreEqual(expected[kind].Cases, cases); Assert.AreEqual(expected[kind].Rows, rows);
        Assert.AreEqual(actual.Length, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
    }
}

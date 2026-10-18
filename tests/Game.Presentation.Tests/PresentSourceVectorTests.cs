using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class PresentSourceVectorTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Bag = "gPresentSpriteTemplate";
    private const string Particle = "gPresentHealParticleSpriteTemplate";
    private const string Predicate = "AnimTask_IsHealingMove";
    private sealed class Audio : ISourceAnimationAudio
    {
        public int Cues;
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) => Cues++;
        public void StopAnimationEffects() { }
    }
    private static T Field<T>(object value, string name)
        => (T)value.GetType().GetField(name, Flags)!.GetValue(value)!;
    private static object? Invoke(object value, string name, params object[] args)
        => value.GetType().GetMethod(name, Flags)!.Invoke(value, args);
    private static SourceBattleAnimation.VisualSprite CreateSprite(int priority, SourceSpriteRecipe recipe, SourceBattleAnimation owner)
        => (SourceBattleAnimation.VisualSprite)Activator.CreateInstance(
            typeof(SourceBattleAnimation).GetNestedType("PresentSprite", BindingFlags.NonPublic)!,
            Flags, null, [0, priority, recipe, owner], CultureInfo.InvariantCulture)!;
    private static SourceAnimationCommand C(string op, params string[] args) => new(op, args, 1);
    private static SourceAnimationScript Primitive(SourceAnimationScript script) => script with
    {
        Commands = [C("end")],
        Labels = new Dictionary<string, int>(),
        Subroutines = new Dictionary<string, SourceAnimationCommand[]>(),
        SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>()
    };
    private static AnimationBattler[] Bodies(bool doubles, int yOffset)
        => Enumerable.Range(0, doubles ? 4 : 2).Select(i =>
            new AnimationBattler(new(i), "b" + i, yOffset, 0, 64, 64)
            { SourceVisibility = new(true, false, false) }).ToArray();
    private static int[] Numbers(JsonElement value) => value.EnumerateArray().Select(x => x.GetInt32()).ToArray();
    private static JsonDocument Vectors() => SourceVectorFixture.Read(
        "present-source-vectors.json",
        "python tests/Reference/Present/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/present-source-vectors.json");

    [TestMethod, DataRow(0, 18), DataRow(1, 96), DataRow(2, 54)]
    public void NativeBagHealAndSignedPredicateMatchEveryCapturedFrame(int selectedKind, int expectedCases)
    {
        using var vectors = Vectors();
        Assert.AreEqual("firered-present-source-v1", vectors.RootElement.GetProperty("schema").GetString());
        var corpus = vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.AreEqual(168, corpus.Length, "Missing source cases must fail closed.");
        CollectionAssert.AreEqual(new[] { "O0", "O2" }, vectors.RootElement.GetProperty("optimizationParity")
            .EnumerateArray().Select(x => x.GetString()!).ToArray());
        var script = AnimationCompositionFixture.Script(217);
        int cases = 0, comparedRows = 0;
        var layouts = new HashSet<(bool Doubles, int Actor, int Target)>();
        var damageInputs = new HashSet<int>();
        foreach (var c in corpus.Where(x => x.GetProperty("kind").GetInt32() == selectedKind))
        {
            int actor = c.GetProperty("actor").GetInt32(), target = c.GetProperty("target").GetInt32();
            bool doubles = c.GetProperty("doubles").GetBoolean();
            int damage = c.GetProperty("damage").GetInt32();
            layouts.Add((doubles, actor, target));
            damageInputs.Add(damage);
            var cue = new CommittedMoveAnimation("present-native", 1, 217, new(actor), new(target))
            {
                // The committed boundary owns this sign bit. The native task
                // corpus independently proves <=0, including signed32 extremes.
                IsHealingMove = damage <= 0
            };
            var audio = new Audio();
            var owner = new SourceBattleAnimation(Primitive(script), cue,
                Bodies(doubles, c.GetProperty("yOffset").GetInt32()), doubles, audio);
            try
            {
                var args = Field<short[]>(owner, "_animationArgs");
                Assert.AreEqual(16, args.Length);
                for (int i = 8; i < args.Length; i++) args[i] = (short)(-15000 + i);
                int[] nativeArgs = Numbers(c.GetProperty("arguments"));
                Assert.AreEqual(8, nativeArgs.Length);
                for (int i = 0; i < 8; i++) args[i] = (short)nativeArgs[i];
                short[] unused = args.Skip(8).ToArray();
                SourceBattleAnimation.VisualSprite? sprite = selectedKind == 2 ? null
                    : CreateSprite(selectedKind == 0 ? 2 : 4, script.SpriteRecipes[selectedKind == 0 ? Bag : Particle], owner);
                if (selectedKind == 2)
                    Assert.AreEqual(true, Invoke(owner, "TryCreatePresentTask", C("createvisualtask", Predicate, "2")));
                var rows = c.GetProperty("rows").EnumerateArray().ToArray();
                Assert.IsTrue(rows.Length > 0);
                int rowIndex = 0;
                bool sawFrameEnd = false, sawSettle = false, sawAffineRestart = false;
                foreach (var row in rows)
                {
                    int frame = row.GetProperty("frame").GetInt32();
                    string why = $"kind {selectedKind}, case {cases}, layout {doubles}/{actor}/{target}, frame {frame}";
                    Assert.AreEqual(rowIndex++, frame, why + " contiguous source frames");
                    if (frame > 0)
                    {
                        Assert.IsNotNull(sprite, why);
                        sprite.Step(owner);
                        if (!sprite.Finished) sprite.AdvanceAnimation(owner);
                    }
                    bool alive = sprite is not null && !sprite.Finished;
                    Assert.AreEqual(row.GetProperty("alive").GetInt32() != 0, alive, why);
                    Assert.AreEqual(row.GetProperty("visualCount").GetInt32(), alive ? 1 : 0, why + " primitive lifetime");
                    CollectionAssert.AreEqual(Numbers(row.GetProperty("args")).Select(x => (short)x).ToArray(), args.Take(8).ToArray(), why);
                    CollectionAssert.AreEqual(unused, args.Skip(8).ToArray(), why + " managed-only registers unchanged");
                    if (selectedKind == 2)
                    {
                        Assert.AreEqual(0, frame, why);
                        Assert.AreEqual(3, row.GetProperty("callback").GetInt32(), why);
                        Assert.AreEqual(JsonValueKind.Null, row.GetProperty("sprite").ValueKind, why);
                        CollectionAssert.AreEqual(new int[16], Numbers(row.GetProperty("data")), why);
                        Assert.AreEqual(0, owner.ActiveVisualCount, why + " immediate task has no retained owner");
                        Assert.AreEqual((short)(damage <= 0 ? 1 : 0), args[7], why);
                    }
                    else
                    {
                        Assert.IsNotNull(sprite, why);
                        var expected = Numbers(row.GetProperty("sprite"));
                        Assert.AreEqual(14, expected.Length, why);
                        var actual = sprite.Frame;
                        Assert.AreEqual(new PixelPoint(expected[0], expected[1]), actual.Position, why);
                        Assert.AreEqual(new PixelPoint(expected[2], expected[3]), actual.Offset, why);
                        Assert.AreEqual(expected[4], actual.Tile, why);
                        Assert.AreEqual(expected[5] != 0, actual.Visible, why);
                        Assert.AreEqual(expected[6], actual.OamPriority, why);
                        Assert.AreEqual(expected[7], actual.Subpriority, why);
                        Assert.IsFalse(actual.HFlip || actual.VFlip || actual.Blend, why);
                        Assert.AreEqual(selectedKind == 0 ? (32, 32) : (16, 16), (actual.Width, actual.Height), why);
                        CollectionAssert.AreEqual(Numbers(row.GetProperty("data")).Select(x => (short)x).ToArray(),
                            Field<short[]>(sprite, "_d"), why + " native packed and signed registers");
                        var frames = Field<SourceSpriteFrameProgram>(sprite, "_frames");
                        Assert.AreEqual(expected[8] != 0, frames.Ended, why);
                        Assert.AreEqual(expected[9], frames.DelayCounter, why);
                        sawFrameEnd |= frames.Ended;
                        var frameState = Numbers(row.GetProperty("frameState"));
                        CollectionAssert.AreEqual(new[] { 0, Field<int>(frames, "_index"), frames.LoopCounter, 0 }, frameState, why);
                        var affine = Field<SourceSpriteAffineProgram?>(sprite, "_affine");
                        if (selectedKind == 0)
                        {
                            Assert.IsNotNull(affine, why);
                            Assert.IsTrue(actual.Affine, why);
                            Assert.AreEqual(expected[10], affine.XScale, why);
                            Assert.AreEqual(expected[11], affine.YScale, why);
                            Assert.AreEqual(expected[12], affine.Rotation, why);
                            Assert.AreEqual(expected[13] != 0, affine.Ended, why);
                            var matrix = SourceAffineMatrix.FromScaleRotation(65536 / expected[10], 65536 / expected[11], expected[12]);
                            Assert.AreEqual((matrix.A, matrix.B, matrix.C, matrix.D),
                                (actual.MatrixA, actual.MatrixB, actual.MatrixC, actual.MatrixD), why);
                            var state = Numbers(row.GetProperty("affine"));
                            CollectionAssert.AreEqual(script.SpriteRecipes[Bag].AffineAnimations[state[0]],
                                Field<SourceSpriteAffineCommand[]>(affine, "_commands"), why + " source affine program");
                            // Native END decrements its program counter, while
                            // the managed player retains the END command itself.
                            Assert.AreEqual(state[1], Field<int>(affine, "_pc") - (affine.Ended ? 1 : 0), why);
                            Assert.AreEqual(state[2], Field<int>(affine, "_delay"), why);
                            Assert.AreEqual(state[3], Field<int>(affine, "_loopCounter"), why);
                            Assert.AreEqual(0, state[4], why + " source affine begins in the same frame");
                            Assert.IsFalse(Field<bool>(sprite, "_restart"), why + " one affine advance on restart");
                            Assert.AreEqual(row.GetProperty("callback").GetInt32(), Field<int>(sprite, "_phase"), why);
                            sawSettle |= Field<int>(sprite, "_phase") == 1;
                            sawAffineRestart |= state[0] == 1;
                        }
                        else
                        {
                            Assert.IsNull(affine, why);
                            Assert.IsFalse(actual.Affine, why);
                            Assert.AreEqual(2, row.GetProperty("callback").GetInt32(), why);
                        }
                    }
                    comparedRows++;
                }
                Assert.AreEqual(0, rows[^1].GetProperty("alive").GetInt32());
                if (selectedKind == 2) Assert.AreEqual(1, rows.Length);
                else
                {
                    Assert.IsTrue(sprite!.Finished);
                    Assert.IsTrue(sawFrameEnd, "Finite source frame END must be exercised.");
                    if (selectedKind == 0)
                    {
                        Assert.IsTrue(sawSettle && sawAffineRestart, "Both travel and settling affine programs must execute.");
                        Assert.IsTrue(rows.Length > 100, "Do not silently truncate the bag's native lifetime.");
                    }
                }
                Assert.AreEqual(0, audio.Cues);
            }
            finally { owner.Cancel(); }
            cases++;
        }
        Assert.AreEqual(expectedCases, cases);
        Assert.AreEqual(6, layouts.Count);
        Assert.IsTrue(comparedRows >= expectedCases);
        if (selectedKind == 2)
            CollectionAssert.AreEquivalent(new[] { int.MinValue, -65536, -32768, -1, 0, 1, 32767, 65536, int.MaxValue }, damageInputs.ToArray());
    }
}

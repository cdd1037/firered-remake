using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class WaterSportNativeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Fixture = "water-sport-source-vectors.json";
    private const string Generate = "python tests/Reference/WaterSport/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/water-sport-source-vectors.json";
    private sealed class Audio : ISourceAnimationAudio
    { public bool IsPlaying => false; public void Play(SourceAnimationSound cue) { } public void StopAnimationEffects() { } }
    private sealed class Occupied(int slot) : SourceBattleAnimation.VisualSprite(slot, 100)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, "occupied", default, default, 0, false, false, false, 256, null, 100);
        public override void Step(SourceBattleAnimation owner) { }
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Flags)!.GetValue(value)!;
    private static object? Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags)!.Invoke(value, args);
    private static int I(JsonElement value, string name) => value.GetProperty(name).GetInt32();
    private static int[] Vector(JsonElement value, string name) => value.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToArray();
    private static PixelPoint Point(JsonElement value, string name) { var p = Vector(value, name); return new(p[0], p[1]); }
    private static SourceAnimationScript Primitive(SourceAnimationScript source) => source with {
        Commands = [new("end", [], 1)], Labels = new Dictionary<string, int>(),
        Subroutines = new Dictionary<string, SourceAnimationCommand[]>(), SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() };

    [TestMethod]
    public void CorpusContainsSixLayoutsSignedSeedGeometryAndRecoverableAllocationFailures()
    {
        using var document = SourceVectorFixture.Read(Fixture, Generate);
        var root = document.RootElement;
        Assert.AreEqual("firered-water-sport-source-v1", root.GetProperty("schema").GetString());
        CollectionAssert.AreEqual(new[] { "O0", "O2" }, root.GetProperty("optimizationParity").EnumerateArray().Select(v => v.GetString()).ToArray());
        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.IsTrue(cases.Length > 0);
        foreach (uint seed in new uint[] { 0, uint.MaxValue })
        {
            var authored = cases.Where(c => c.GetProperty("branch").GetString() == "authored" && c.GetProperty("seed").GetUInt32() == seed).ToArray();
            CollectionAssert.AreEquivalent(new[] { (false, 0), (false, 1), (true, 0), (true, 1), (true, 2), (true, 3) },
                authored.Select(c => (c.GetProperty("doubles").GetBoolean(), I(c, "actor"))).ToArray());
        }
        foreach (string branch in new[] { "picture-offset-and-elevation", "one-free-slot", "saturated-then-release" })
            Assert.IsTrue(cases.Any(c => c.GetProperty("branch").GetString() == branch), branch);
        foreach (var c in cases)
        {
            var rows = c.GetProperty("rows").EnumerateArray().ToArray();
            Assert.IsTrue(rows.Length > 30);
            CollectionAssert.AreEqual(Enumerable.Range(0, rows.Length).ToArray(), rows.Select(r => I(r, "frame")).ToArray());
            Assert.AreEqual(0, I(rows[^1], "alive")); Assert.AreEqual(0, rows[^1].GetProperty("sprites").GetArrayLength());
            Assert.IsTrue(rows.Any(r => Vector(r, "task")[0] == 6));
            Assert.IsTrue(rows.Any(r => r.GetProperty("sprites").EnumerateArray().Any(s => I(s, "phase") == 1)));
            if (I(c, "release") == 0) continue;
            foreach (var r in rows.Take(I(c, "release")))
            { Assert.AreEqual(0, I(r, "allocated")); Assert.AreEqual(c.GetProperty("seed").GetUInt32(), r.GetProperty("rng").GetUInt32()); Assert.AreEqual(0, Vector(r, "task")[0]); }
        }
    }

    [TestMethod]
    public void EveryNativeFrameMatchesTaskRegistersRawSlotsBothArcsAndThreeDrawBounce()
    {
        using var document = SourceVectorFixture.Read(Fixture, Generate);
        var source = AnimationCompositionFixture.Script(346);
        int compared = 0, caseIndex = 0;
        foreach (var c in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles = c.GetProperty("doubles").GetBoolean(); int actor = I(c, "actor"), battlers = doubles ? 4 : 2;
            uint seed = c.GetProperty("seed").GetUInt32();
            var bodies = Enumerable.Range(0, battlers).Select(i => new AnimationBattler(new(i), "b" + i, I(c, "yOffset"), I(c, "elevation"), 64, 64)).ToArray();
            var cue = new CommittedMoveAnimation("water-sport-native", 1, 346, new(actor), new(actor)) { VisualSeed = seed };
            var scene = new SourceBattleAnimation.SpriteScene();
            var owner = new SourceBattleAnimation(Primitive(source), cue, bodies, doubles, new Audio(), spriteScene: scene);
            var owned = Field<object>(owner, "_sprites");
            var occupied = new List<Occupied>();
            for (int slot = battlers; slot < battlers + I(c, "occupied"); slot++)
            { var sprite = new Occupied(slot); occupied.Add(sprite); Invoke(owned, "Add", sprite); }
            var taskType = typeof(SourceBattleAnimation).GetNestedType("WaterSportTask", BindingFlags.NonPublic)!;
            var task = Activator.CreateInstance(taskType, Flags, null, [owner], null)!;
            Invoke(owner, "AddSourceVisualTask", task, 5);
            var instances = new Dictionary<SourceBattleAnimation.VisualSprite, int>();
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick = I(row, "frame"); string why = $"case {caseIndex}, actor {actor}, frame {tick}";
                if (tick > 0)
                {
                    if (I(c, "release") == tick) Invoke(owned, "Remove", occupied.Single(s => s.Slot == 63));
                    Invoke(owner, "AdvanceSourceSprites"); Invoke(owner, "RunSourceTasks");
                }
                CollectionAssert.AreEqual(Vector(row, "task").Select(n => unchecked((short)n)).ToArray(), Field<short[]>(task, "Data"), why + " task registers");
                Assert.AreEqual(I(row, "alive"), owner.ActiveVisualCount, why + " task visual ownership");
                var rng = Field<SourceVisualRandom?>(owner, "_visualRandom");
                Assert.AreEqual(row.GetProperty("rng").GetUInt32(), rng is null ? seed : Field<uint>(rng, "_state"), why + " exact RNG stream");
                var actual = ((IEnumerable)owned).Cast<SourceBattleAnimation.VisualSprite>().Where(s => s.Frame.Template != "occupied").OrderBy(s => s.Slot).ToArray();
                var expected = row.GetProperty("sprites").EnumerateArray().ToArray(); Assert.AreEqual(expected.Length, actual.Length, why + " live raw children");
                for (int i = 0; i < actual.Length; i++)
                {
                    var sprite = actual[i]; var e = expected[i]; var frame = sprite.Frame;
                    if (!instances.ContainsKey(sprite)) instances.Add(sprite, I(e, "instance"));
                    Assert.AreEqual(I(e, "instance"), instances[sprite], why + " slot reuse identity");
                    Assert.AreEqual(I(e, "slot"), frame.Slot, why + " ascending allocation");
                    Assert.AreEqual(Point(e, "position"), frame.Position, why + " base coordinates");
                    Assert.AreEqual(Point(e, "offset"), frame.Offset, why + " retained handoff offset");
                    Assert.AreEqual(I(e, "phase"), Field<int>(sprite, "_phase"), why + " callback phase");
                    Assert.IsFalse(sprite.Counted); Assert.AreEqual(10, frame.Subpriority); Assert.AreEqual(2, frame.OamPriority);
                    Assert.AreEqual(8, frame.Width); Assert.AreEqual(8, frame.Height); Assert.IsFalse(frame.Affine); Assert.IsFalse(frame.Blend);
                    Assert.AreEqual("gSmallWaterOrbSpriteTemplate", frame.Template); Assert.AreEqual("glowy-blue-orb", frame.Asset);
                    var arc = Field<object>(sprite, "_arc"); var linear = Field<object>(arc, "_linear");
                    int[] state = [Field<int>(linear, "_remaining"), Field<int>(linear, "_vx"), Field<int>(linear, "_vy"),
                        Field<int>(linear, "_x"), Field<int>(linear, "_y"), Field<int>(sprite, "_amplitude"), Field<short>(arc, "_step"), Field<short>(arc, "_angle")];
                    CollectionAssert.AreEqual(Vector(e, "data").Select(n => unchecked((short)n)).ToArray(), state.Select(n => unchecked((short)n)).ToArray(), why + " all source arc registers");
                }
                Assert.AreEqual(I(row, "allocated"), instances.Count, why + " every successful allocation"); compared++;
            }
            owner.Cancel(); Assert.AreEqual(0, scene.Count); caseIndex++;
        }
        Assert.IsTrue(caseIndex > 0 && compared > caseIndex);
    }
}

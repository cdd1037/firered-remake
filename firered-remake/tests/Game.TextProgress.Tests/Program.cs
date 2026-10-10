using System.Text;
using System.Text.Json;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Game.TextProgress.Tests <project-root>");
    return 2;
}
string root = Path.GetFullPath(args[0]);
string suite = Path.Combine(root, "tests/Game.TextProgress.Tests");
var results = new List<object>();
var observedTraces = new List<object>();
int passed = 0, failed = 0, checks = 0, observedFrames = 0;
void Require(bool value, string label) { checks++; if (!value) throw new InvalidDataException(label); }
void Test(string name, Action action)
{
    try { action(); passed++; results.Add(new { name, passed = true }); }
    catch (Exception e) { failed++; results.Add(new { name, passed = false, error = e.ToString() }); }
}
void Reject<T>(Action action) where T : Exception
{
    checks++;
    try { action(); }
    catch (T) { return; }
    throw new InvalidDataException("Expected rejection: " + typeof(T).Name);
}
object Snapshot(SourceTextProgress p) => new { p.VisibleText, p.VisibleGlyphs, p.Complete, p.ConfiguredSpeed, p.DelayCounter, p.HasPrintBeenSpedUp };
string SnapshotText(SourceTextProgress p) => JsonSerializer.Serialize(Snapshot(p));
string reference = "";
byte[] lockBytes = [], treeBytes = [];
Dictionary<string, string>? source = null;
Test("actual-net10-runtime-and-pinned-source-inputs", () =>
{
    Require(Environment.Version.Major == 10, "Requires actual .NET 10 runtime.");
    reference = File.ReadAllText(Path.Combine(root, ".tools/reference-root")).Trim();
    lockBytes = File.ReadAllBytes(Path.Combine(suite, "source-lock.json"));
    treeBytes = File.ReadAllBytes(Path.Combine(root, "content/raw/starter-audit/tree.json"));
    source = SourceVerifier.Read(reference, lockBytes, treeBytes);
    Require(source.Count == 4 && SourceTextProgress.SourceCommit == SourceVerifier.Commit, "Pinned four-file source identity differs.");
    string printer = source["src/text_printer.c"], renderer = source["src/text.c"], menu = source["src/new_menu_helpers.c"];
    Require(printer.Contains("--sTempTextPrinter.textSpeed;", StringComparison.Ordinal), "Source configured-speed decrement absent.");
    Require(printer.Contains("if (ret != 2)\n            return ret;", StringComparison.Ordinal)
        && source["include/text.h"].Contains("RENDER_PRINT,\n    RENDER_FINISH,\n    RENDER_REPEAT,", StringComparison.Ordinal), "Source same-frame RENDER_REPEAT loop absent.");
    Require(renderer.Contains("if (JOY_HELD(A_BUTTON | B_BUTTON) && subStruct->hasPrintBeenSpedUp)", StringComparison.Ordinal)
        && renderer.Contains("if (gTextFlags.canABSpeedUpPrint && JOY_NEW(A_BUTTON | B_BUTTON))", StringComparison.Ordinal), "Source A/B conditions absent.");
    Require(menu.Contains("[OPTIONS_TEXT_SPEED_SLOW] = 8", StringComparison.Ordinal)
        && menu.Contains("[OPTIONS_TEXT_SPEED_MID]  = 4", StringComparison.Ordinal)
        && menu.Contains("[OPTIONS_TEXT_SPEED_FAST] = 1", StringComparison.Ordinal), "Source option speeds differ.");
});
if (source is not null)
{
    Test("source-validation-rejects-missing-changed-and-incomplete-inputs", () =>
    {
        string scratch = Path.Combine(Path.GetTempPath(), "firered-text-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            Reject<InvalidDataException>(() => SourceVerifier.Read(scratch, lockBytes, treeBytes));
            foreach (string relative in SourceVerifier.Paths)
            {
                string output = Path.Combine(scratch, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(Path.Combine(reference, relative), output);
            }
            Require(SourceVerifier.Read(scratch, lockBytes, treeBytes).Count == 4, "Unchanged temporary source copy rejected.");
            File.AppendAllText(Path.Combine(scratch, "src/text.c"), "\n/* changed */\n");
            Reject<InvalidDataException>(() => SourceVerifier.Read(scratch, lockBytes, treeBytes));
            var changedTree = (byte[])treeBytes.Clone(); changedTree[^1] ^= 1;
            Reject<InvalidDataException>(() => SourceVerifier.Read(reference, lockBytes, changedTree));
            Reject<InvalidDataException>(() => SourceVerifier.Read(reference, Encoding.UTF8.GetBytes("{\"files\":[]}"), treeBytes));
        }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }
    });
    Test("literal-source-traces-cover-every-frame", () =>
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(suite, "Fixtures/source-traces.json"));
        Require(SourceVerifier.Sha256(bytes) == SourceVerifier.FixtureHash, "Reviewed literal expectations changed.");
        using var document = JsonDocument.Parse(bytes);
        var fixture = document.RootElement;
        Require(fixture.GetProperty("schemaVersion").GetInt32() == 1 && fixture.GetProperty("sourceCommit").GetString() == SourceVerifier.Commit, "Unknown trace identity.");
        var traces = fixture.GetProperty("traces").EnumerateArray().ToArray();
        Require(traces.Length == 12 && traces.Select(t => t.GetProperty("id").GetString()).Distinct().Count() == 12, "Source suite is empty or incomplete.");
        foreach (var trace in traces)
        {
            string id = trace.GetProperty("id").GetString()!;
            var progress = new SourceTextProgress();
            progress.Reset(trace.GetProperty("text").GetString()!, trace.GetProperty("speed").GetInt32());
            Require(progress.VisibleText == "" && progress.VisibleGlyphs == 0 && !progress.Complete && progress.DelayCounter == 0 && !progress.HasPrintBeenSpedUp, id + " reset state");
            var actual = new List<object>();
            int frame = 0;
            foreach (var row in trace.GetProperty("frames").EnumerateArray())
            {
                frame++; observedFrames++;
                Require(row.GetArrayLength() == 7, id + " incomplete literal row");
                progress.AdvanceFrames(1, row[0].GetInt32() == 1, row[1].GetInt32() == 1);
                actual.Add(new { frame, held = row[0].GetInt32() == 1, pressed = row[1].GetInt32() == 1, state = Snapshot(progress) });
                Require(progress.VisibleText == row[2].GetString() && progress.VisibleGlyphs == row[3].GetInt32()
                    && progress.Complete == (row[4].GetInt32() == 1) && progress.DelayCounter == row[5].GetInt32()
                    && progress.HasPrintBeenSpedUp == (row[6].GetInt32() == 1), id + " frame " + frame + ": " + SnapshotText(progress));
            }
            Require(progress.Complete && frame > 0, id + " missing EOS evidence");
            observedTraces.Add(new { id, frames = actual });
        }
        Require(observedFrames == 97, "Literal frame inventory differs.");
    });
}
Test("unicode-text-elements-remain-whole", () =>
{
    var p = new SourceTextProgress();
    p.Reset("ée\u0301𠀀👩‍🔬", 1);
    string[] expected = ["é", "ée\u0301", "ée\u0301𠀀", "ée\u0301𠀀👩‍🔬"];
    for (int i = 0; i < expected.Length; i++)
    {
        p.AdvanceFrames(1, false, false);
        Require(p.VisibleText == expected[i] && p.VisibleGlyphs == i + 1 && !p.Complete, "Unicode element was split or counted as code units.");
    }
    p.AdvanceFrames(1, false, false);
    Require(p.Complete && p.VisibleGlyphs == 4, "Unicode EOS differs.");
});
Test("newline-only-pages-finish-without-emitting-a-glyph", () =>
{
    foreach (int speed in new[] { 8, 4, 1 })
    {
        var p = new SourceTextProgress(); p.Reset("\n", speed);
        p.AdvanceFrames(speed - 1, false, false);
        Require(!p.Complete && p.VisibleGlyphs == 0, "LF-only page finished before its source EOS frame.");
        p.AdvanceFrames(1, false, false);
        Require(p.Complete && p.VisibleText == "\n" && p.VisibleGlyphs == 0, "LF-only page acquired a glyph or missed EOS.");
    }
});
Test("frame-batches-match-single-frame-input-semantics", () =>
{
    var batch = new SourceTextProgress(); var single = new SourceTextProgress();
    batch.Reset("ABCDEFG", 4); single.Reset("ABCDEFG", 4);
    batch.AdvanceFrames(1, false, false); single.AdvanceFrames(1, false, false);
    batch.AdvanceFrames(3, true, true);
    single.AdvanceFrames(1, true, true); single.AdvanceFrames(2, true, false);
    Require(SnapshotText(batch) == SnapshotText(single) && batch.VisibleText == "ABC", "Pressed must apply to the first frame only.");
    batch.AdvanceFrames(4, false, false); single.AdvanceFrames(4, false, false);
    Require(SnapshotText(batch) == SnapshotText(single) && batch.VisibleText == "ABCD", "Released batch must restore configured cadence.");
    batch.AdvanceFrames(100, true, false);
    Require(batch.Complete && batch.VisibleText == "ABCDEFG" && batch.VisibleGlyphs == 7, "Hold must reuse the page's speed-up latch.");
});
Test("zero-frames-do-not-queue-input-or-change-state", () =>
{
    var p = new SourceTextProgress(); p.Reset("AB", 4); p.AdvanceFrames(1, false, false);
    string before = SnapshotText(p);
    p.AdvanceFrames(0, true, true);
    Require(SnapshotText(p) == before, "Zero-frame update mutated presentation.");
    p.AdvanceFrames(1, true, false);
    Require(p.VisibleText == "A" && p.DelayCounter == 2 && !p.HasPrintBeenSpedUp, "Zero-frame input leaked into the next frame.");
});
Test("invalid-speed-frame-and-text-inputs-fail-atomically", () =>
{
    var p = new SourceTextProgress(); p.Reset("AB", 4); p.AdvanceFrames(1, false, false);
    string before = SnapshotText(p);
    foreach (int speed in new[] { int.MinValue, -1, 0, 2, 3, 5, 7, 9, 255, int.MaxValue })
        Reject<ArgumentOutOfRangeException>(() => p.Reset("NEW", speed));
    Reject<ArgumentOutOfRangeException>(() => p.AdvanceFrames(-1, true, true));
    Reject<ArgumentNullException>(() => p.Reset(null!, 4));
    foreach (string text in new[] { "A\r\nB", "A\tB", "A\0B", "\u001b", "\ud800", "\udc00", "\ud800A" })
        Reject<ArgumentException>(() => p.Reset(text, 4));
    Require(SnapshotText(p) == before, "Rejected input partially reset or advanced the printer.");
});
Test("reset-clears-old-page-counters-latch-and-text", () =>
{
    var p = new SourceTextProgress(); p.Reset("OLD", 8);
    p.AdvanceFrames(1, false, false); p.AdvanceFrames(1, true, true);
    Require(p.HasPrintBeenSpedUp, "Setup did not arm speed-up.");
    p.Reset("N", 4);
    Require(p.VisibleText == "" && p.VisibleGlyphs == 0 && !p.Complete && p.DelayCounter == 0 && !p.HasPrintBeenSpedUp && p.ConfiguredSpeed == 4, "Reset leaked prior page state.");
    p.AdvanceFrames(1, true, false); p.AdvanceFrames(1, true, false);
    Require(p.VisibleText == "N" && p.DelayCounter == 2 && !p.Complete, "Held from a previous page must not silently arm speed-up.");
});
Test("completion-is-stable-and-instances-are-independent", () =>
{
    var first = new SourceTextProgress(); var second = new SourceTextProgress();
    Require(first.Complete && first.VisibleText == "" && first.VisibleGlyphs == 0, "Unstarted presentation must be inert.");
    first.Reset("A", 1); second.Reset("B", 8);
    first.AdvanceFrames(2, false, false);
    string before = SnapshotText(first);
    first.AdvanceFrames(int.MaxValue, true, true);
    Require(SnapshotText(first) == before, "Completed presentation mutated or reprinted.");
    Require(!second.Complete && second.VisibleText == "" && second.DelayCounter == 0, "Other instance was changed.");
});
Test("expanded-input-contract-does-not-pretend-to-parse-rom-controls", () =>
{
    const string text = "{COLOR RED}\\p$";
    var p = new SourceTextProgress(); p.Reset(text, 1); p.AdvanceFrames(text.Length + 1, false, false);
    Require(p.Complete && p.VisibleText == text && p.VisibleGlyphs == text.Length, "Bounded Unicode adapter unexpectedly parsed source markup.");
});
Test("nonempty-complete-evidence", () => Require(results.Count >= 10 && checks > 200 && observedTraces.Count == 12 && observedFrames == 97, "Empty or incomplete test suite."));
var report = new
{
    status = failed == 0 ? "passed" : "failed", passed, failed, checks, observedFrames,
    runtime = Environment.Version.ToString(), sourceCommit = SourceVerifier.Commit,
    sourceLockSha256 = lockBytes.Length == 0 ? null : SourceVerifier.Sha256(lockBytes),
    fixtureSha256 = File.Exists(Path.Combine(suite, "Fixtures/source-traces.json")) ? SourceVerifier.Sha256(File.ReadAllBytes(Path.Combine(suite, "Fixtures/source-traces.json"))) : null,
    productSha256 = SourceVerifier.Sha256(File.ReadAllBytes(Path.Combine(root, "src/Game.Godot/SourceTextProgress.cs"))),
    results, traces = observedTraces,
    scope = "Pure source-derived ordinary field-page timing and Unicode adapter; no Godot input, wall-clock, glyph rasterization, battle controls, scroll, or full-ROM-renderer claim."
};
Directory.CreateDirectory(Path.Combine(suite, "evidence"));
File.WriteAllText(Path.Combine(suite, "evidence/results.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(report));
return failed == 0 ? 0 : 1;

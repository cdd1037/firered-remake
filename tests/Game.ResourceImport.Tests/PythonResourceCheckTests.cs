using System.Diagnostics;

namespace FireRed.Tests.ResourceImport;

[TestClass, TestCategory("ResourceImportHarness")]
public sealed class PythonResourceCheckTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NormalOutputSucceeds() =>
        await WithScript("print('normal-output')", path => PythonResourceCheck.Run(path, context: TestContext));

    [TestMethod]
    public async Task NonzeroExitKeepsBothDiagnostics() =>
        await WithScript("import sys\nprint('stdout-evidence', flush=True)\nprint('stderr-evidence', file=sys.stderr, flush=True)\nsys.exit(7)", async path =>
        {
            var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => PythonResourceCheck.Run(path));
            StringAssert.Contains(failure.Message, "stdout-evidence");
            StringAssert.Contains(failure.Message, "stderr-evidence");
            StringAssert.Contains(failure.Message, "7");
        });

    [TestMethod]
    public async Task EmptyOutputFails() =>
        await WithScript("pass", async path =>
        {
            var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => PythonResourceCheck.Run(path));
            StringAssert.Contains(failure.Message, "Empty resource check output");
        });

    [TestMethod]
    public Task PrivateTimeoutReapsChildAndRetainsPartialOutput() => CheckInterruptedChild(cancel: false);

    [TestMethod]
    public Task CallerCancellationReapsChildAndRetainsPartialOutput() => CheckInterruptedChild(cancel: true);

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ExitedParentWithInheritedPipeFailsWithinCleanupBudget()
    {
        // The orphan is deliberately short-lived, even if the assertion/harness fails.
        // Its completion marker proves script completion, not OS reaping by this harness.
        await WithScript("import pathlib, subprocess, sys\n" +
            "code = 'import pathlib, sys, time; time.sleep(12); pathlib.Path(sys.argv[1]).write_text(\"finished\")'\n" +
            "subprocess.Popen([sys.executable, '-c', code, sys.argv[1]])\n" +
            "print('parent-exited-with-open-pipe', flush=True)", async path =>
        {
            var finished = path + ".finished";
            try
            {
                var elapsed = Stopwatch.StartNew();
                var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
                    PythonResourceCheck.Run(path, [finished], TimeSpan.FromMilliseconds(500), TestContext));
                Assert.IsLessThan(TimeSpan.FromSeconds(14), elapsed.Elapsed);
                StringAssert.Contains(failure.Message, "Resource check timed out");
                StringAssert.Contains(failure.Message, "parent-exited-with-open-pipe");
                StringAssert.Contains(failure.Message, "Cleanup/reap/pipe drain incomplete");
            }
            finally
            {
                await WaitForFile(finished, TimeSpan.FromSeconds(16));
                File.Delete(finished);
            }
        });
    }

    private async Task CheckInterruptedChild(bool cancel)
    {
        await WithScript("import os, pathlib, sys, time\n" +
            "print('before-interruption', flush=True)\n" +
            "pathlib.Path(sys.argv[1]).write_text(str(os.getpid()))\ntime.sleep(60)", async path =>
        {
            var ready = path + ".pid";
            using var cancellation = new CancellationTokenSource();
            Process? child = null;
            try
            {
                var elapsed = Stopwatch.StartNew();
                var run = PythonResourceCheck.Run(path, [ready], TimeSpan.FromSeconds(cancel ? 20 : 2),
                    TestContext, cancellation.Token);
                await WaitForFile(ready, TimeSpan.FromSeconds(5));
                child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(ready)));
                if (cancel) cancellation.Cancel();
                var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => run);
                StringAssert.Contains(failure.Message, cancel ? "Resource check canceled" : "Resource check timed out");
                StringAssert.Contains(failure.Message, "before-interruption");
                Assert.IsLessThan(TimeSpan.FromSeconds(15), elapsed.Elapsed);
                Assert.IsTrue(child.HasExited, "Owned immediate Python child must be reaped before Run returns.");
            }
            finally
            {
                if (child is not null)
                {
                    if (!child.HasExited)
                    {
                        child.Kill(entireProcessTree: true);
                        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    child.Dispose();
                }
                File.Delete(ready);
            }
        });
    }

    private static async Task WithScript(string source, Func<string, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), "firered-resource-harness-" + Guid.NewGuid().ToString("N") + ".py");
        await File.WriteAllTextAsync(path, source);
        try { await test(path); }
        finally { File.Delete(path); }
    }

    private static async Task WaitForFile(string path, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        while (!File.Exists(path) || new FileInfo(path).Length == 0)
            await Task.Delay(20, deadline.Token);
    }
}

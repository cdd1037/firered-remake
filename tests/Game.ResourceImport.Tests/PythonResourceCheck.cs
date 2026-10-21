using System.Diagnostics;
using System.Text;

namespace FireRed.Tests.ResourceImport;

internal static class PythonResourceCheck
{
    // Leave room for this cleanup budget in every framework deadline.
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);

    public static async Task Run(string script, string[]? args = null, TimeSpan? timeout = null,
        TestContext? context = null, CancellationToken cancellationToken = default)
    {
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(5));
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(
            deadline.Token, cancellationToken, context?.CancellationToken ?? default);
        execution.Token.ThrowIfCancellationRequested();
        var interpreter = Environment.GetEnvironmentVariable("FIRERED_PYTHON")
            ?? (OperatingSystem.IsWindows() ? "python" : "python3");
        var info = new ProcessStartInfo(interpreter)
        {
            WorkingDirectory = TestAssets.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        info.ArgumentList.Add(Path.Combine(TestAssets.Root, script));
        foreach (var argument in args ?? [])
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Python could not start: " + script);
        using var pipeCancellation = new CancellationTokenSource();
        var output = new StringBuilder();
        var pipes = Task.WhenAll(ReadPipe(process.StandardOutput), ReadPipe(process.StandardError));
        string? failure = null;
        var cleanupFailures = new List<string>();
        try
        {
            await process.WaitForExitAsync(execution.Token);
            // A descendant can retain either pipe after the immediate child exits.
            await pipes.WaitAsync(execution.Token);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            failure = cancellationToken.IsCancellationRequested || context?.CancellationToken.IsCancellationRequested == true
                ? "Resource check canceled" : "Resource check timed out";
        }
        catch (Exception exception)
        {
            failure = "Resource check failed: " + exception;
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(CleanupTimeout);
            try
            {
                if (!process.HasExited)
                {
                    var kill = Task.Run(() =>
                    {
                        try { process.Kill(entireProcessTree: true); }
                        catch (InvalidOperationException) when (process.HasExited) { } // Exit race.
                    });
                    // Tree enumeration/termination must not defeat the cleanup deadline either.
                    _ = kill.ContinueWith(task => _ = task.Exception,
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    try { await kill.WaitAsync(cleanup.Token); }
                    catch (Exception exception)
                    {
                        cleanupFailures.Add("Owned process tree termination incomplete: " + exception.GetType().Name);
                    }
                }
                await process.WaitForExitAsync(cleanup.Token);
                await pipes.WaitAsync(cleanup.Token);
            }
            catch (Exception exception)
            {
                cleanupFailures.Add("Cleanup/reap/pipe drain incomplete: " + exception.GetType().Name);
            }
            finally
            {
                // Cancel our reads even when an exited child left an orphan holding a pipe.
                // Such an orphan is not discoverable via Process.Kill(entireProcessTree).
                pipeCancellation.Cancel();
                try { await pipes.WaitAsync(TimeSpan.FromSeconds(1)); }
                catch (OperationCanceledException) { }
                catch (Exception exception)
                {
                    cleanupFailures.Add("Pipe reader shutdown incomplete: " + exception.GetType().Name);
                    _ = pipes.ContinueWith(task => _ = task.Exception,
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                }
            }
        }

        string diagnostics;
        lock (output) diagnostics = output.ToString();
        context?.WriteLine(diagnostics);
        var details = script + "\n" + diagnostics + "\n" + string.Join("\n", cleanupFailures);
        Assert.IsNull(failure, failure + ": " + details);
        Assert.IsEmpty(cleanupFailures, details);
        Assert.AreEqual(0, process.ExitCode, details);
        Assert.IsFalse(string.IsNullOrWhiteSpace(diagnostics), "Empty resource check output: " + script);

        async Task ReadPipe(StreamReader reader)
        {
            var buffer = new char[4096];
            while (true)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), pipeCancellation.Token);
                if (count == 0) return;
                lock (output) output.Append(buffer, 0, count);
            }
        }
    }
}

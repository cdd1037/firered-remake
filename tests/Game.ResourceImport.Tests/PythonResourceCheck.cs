using System.Diagnostics;
using System.Text;

namespace FireRed.Tests.ResourceImport;

internal static class PythonResourceCheck
{
    public static async Task Run(string script, string[]? args = null, TimeSpan? timeout = null,
        TestContext? context = null)
    {
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
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(6));
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        var output = await stdout + await stderr;
        context?.WriteLine(output);
        Assert.IsFalse(timedOut, "Resource check timed out: " + script + "\n" + output);
        Assert.AreEqual(0, process.ExitCode, script + "\n" + output);
        Assert.IsFalse(string.IsNullOrWhiteSpace(output), "Empty resource check output: " + script);
    }
}

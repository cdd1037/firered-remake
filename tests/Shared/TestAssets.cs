using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize]
namespace FireRed.Tests;
internal static class TestAssets
{
    public static string Root { get; } = FindRoot();
    public static string Checkpoint => Environment.GetEnvironmentVariable("FIRERED_TEST_CHECKPOINT") ?? Path.GetFullPath(Path.Combine(Root, "checkpoints/current-ownership-v3-20261015/continued-checkpoint.json"));
    public static string Source => Path.Combine(Root, "content/raw/pokefirered/037335f4");

    public static string Fixtures(string feature) => Path.Combine(Root, "tests", feature, "Fixtures");
    public static string Output()
    {
        var p = Path.Combine(Root, ".toolhome/mstest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    public static TemporaryDirectory TemporaryOutput() => new(Output());

    internal sealed class TemporaryDirectory(string path) : IDisposable
    {
        public string Path { get; } = path;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private static readonly ConcurrentDictionary<string, object> Cache = new();
    public static T Cached<T>(Func<T> factory, [CallerArgumentExpression(nameof(factory))] string key = "")
        where T : notnull => (T)Cache.GetOrAdd(key, _ => factory());
    private static string FindRoot()
    {
        for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
            if (File.Exists(Path.Combine(p.FullName, "FireRed.Current.slnx")))
                return p.FullName;
        throw new DirectoryNotFoundException("FireRed.Current.slnx");
    }
}

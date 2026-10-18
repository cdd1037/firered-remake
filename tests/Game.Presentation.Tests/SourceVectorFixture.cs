using System.Text.Json;

namespace FireRed.Tests.Presentation;

internal static class SourceVectorFixture
{
    // Full native traces are generated explicitly into an external directory.
    // A requested full corpus must exist; never silently fall back to the small fixture.
    internal static JsonDocument Read(string fileName, string generationCommand)
    {
        var directory = Environment.GetEnvironmentVariable("FIRERED_SOURCE_VECTOR_DIR");
        var path = Path.Combine(string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(TestAssets.Root, "tests/Game.Presentation.Tests/Fixtures")
            : directory, fileName);
        using var file = File.OpenRead(TestAssets.GeneratedFile(path, generationCommand));
        return JsonDocument.Parse(file);
    }

}

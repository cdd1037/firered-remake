using System.Text.Json;

namespace Game.Content;

internal static class PretEarlyFieldData
{
    internal static JsonElement Read(string project, string relative)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(project, "content/generated", relative)));
        return document.RootElement.Clone();
    }
}

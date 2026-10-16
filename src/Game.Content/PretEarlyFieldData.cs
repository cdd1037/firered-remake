using System.Text.Json;

namespace Game.Content;

internal static class PretEarlyFieldData
{
    internal static JsonElement Read(string project, string relative)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(project, "content/generated", relative)));
        return document.RootElement.Clone();
    }

    internal static JsonElement LandHeaders(string project, params string[] maps)
    {
        var headers = Read(project, "data/encounters/early-land.json").EnumerateArray()
            .Where(row => maps.Contains(row.GetProperty("map").GetString(), StringComparer.Ordinal)).ToArray();
        if (headers.Length != maps.Length) throw new InvalidDataException("Early land header selection differs.");
        return JsonSerializer.SerializeToElement(headers);
    }
}

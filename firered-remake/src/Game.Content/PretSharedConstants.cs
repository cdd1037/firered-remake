using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>The one numeric symbol table shared by generated source packages.</summary>
internal static class PretSharedConstants
{
    private static readonly ConcurrentDictionary<string, Lazy<ImmutableDictionary<string, int>>> Tables = new(StringComparer.Ordinal);

    internal static ImmutableDictionary<string, int> ForPackage(string packageDirectory)
    {
        string generated = PretRuntimeMapValidation.GeneratedRoot(packageDirectory);
        return Tables.GetOrAdd(generated, directory => new(() => Read(directory))).Value;
    }

    private static ImmutableDictionary<string, int> Read(string generated)
    {
        using var document = JsonDocument.Parse(SafariSource.Read(generated, "data/constants.json"));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Shared constants must be a numeric symbol table.");
        var result = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) || property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt32(out int value) ||
                !result.TryAdd(property.Name, value))
                throw new InvalidDataException("Invalid or duplicate shared constant: " + property.Name);
        }
        if (result.Count == 0) throw new InvalidDataException("Shared constant table is empty.");
        return result.ToImmutable();
    }
}

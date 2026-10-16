using Game.Domain.Growth;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Generated resource reads using the shared strict JSON contract.</summary>
internal static class SafariSource
{
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    internal static byte[] Read(string root, string relative) => GeneratedResourceFile.Read(root, relative);

    internal static T Parse<T>(byte[] bytes, string? machineRoot = null, PretCanonicalPokemonCatalog? canonical = null)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        canonical?.AddReferenceConverters(options);
        if (machineRoot is not null)
        {
            SharedMachineCatalog.AddConverters(options, machineRoot);
            SharedTrainerCatalog.AddConverters(options, machineRoot);
        }
        return ContentJson.ParseStrict<T>(bytes, options);
    }
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Consumes the authenticated Memorial rows in the original TM acquisition source package.</summary>
public static class PretMemorialPillarTrainers
{
    public static MemorialPillarTrainerCatalog Load(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/original-tm-acquisition/memorial-trainers.json"));
        return Load(JsonSerializer.Deserialize<FiveIslandTrainerDocument>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("memorial_pillar_trainer_document_missing"));
    }
    public static MemorialPillarTrainerCatalog Load(FiveIslandTrainerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("memorial_pillar_trainer_records_missing");
        var result = new MemorialPillarTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id),
            document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        MemorialPillarTrainerPartyFactory.ValidateCatalog(result); return result;
    }
}

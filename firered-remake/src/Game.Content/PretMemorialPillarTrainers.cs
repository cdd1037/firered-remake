using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Consumes the authenticated Memorial rows in the original TM acquisition source package.</summary>
public static class PretMemorialPillarTrainers
{
    public const string TrainersSha256 = "b29d6e2da05765a29867eaa9b1eb4ccd56f6ff1491c671486801eddbb0f0a242";
    public static MemorialPillarTrainerCatalog Load(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/original-tm-acquisition/memorial-trainers.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != TrainersSha256)
            throw new InvalidDataException("memorial_pillar_trainer_document_bytes");
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

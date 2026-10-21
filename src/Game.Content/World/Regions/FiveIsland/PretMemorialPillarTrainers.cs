using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Consumes the authenticated Memorial rows in the original TM acquisition source package.</summary>
public static class PretMemorialPillarTrainers
{
    public static OrdinaryTrainerCatalog Load(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/maps/regions/original-tm-acquisition/memorial-trainers.json"));
        return Load(JsonSerializer.Deserialize<FiveIslandTrainerDocument>(bytes,
            SharedTrainerCatalog.Options(root))
            ?? throw new InvalidDataException("memorial_pillar_trainer_document_missing"));
    }
    public static OrdinaryTrainerCatalog Load(FiveIslandTrainerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("memorial_pillar_trainer_records_missing");
        return new(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles, "memorial-pillar-trainer");
    }
}

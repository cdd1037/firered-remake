using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record CinnabarTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Regional trainer references and alternate battle script entry points.</summary>
public static class PretCinnabarTrainers
{
    public static CinnabarTrainerDocument LoadDocument(string root)
    {
        var source = CinnabarSource.Catalogs(root); var d = source.Parse<CinnabarTrainerDocument>("trainers.json");
        var catalog = new CinnabarTrainerCatalog(d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        CinnabarTrainerPartyFactory.ValidateCatalog(catalog);
        return d;
    }
    public static CinnabarTrainerCatalog Load(string root)
    {
        var d = LoadDocument(root);
        return new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
    }
}

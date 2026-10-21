using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FuchsiaRouteTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement Actors);

public static class PretFuchsiaRouteTrainers
{
    public static FuchsiaRouteTrainerCatalog Load(string projectRoot)
    {
        var document = ContentJson.ParseStrict<FuchsiaRouteTrainerDocument>(
            SafariSource.Read(projectRoot, "content/generated/data/pokemon/fuchsia-route/trainers.json"),
            SharedTrainerCatalog.Options(projectRoot));
        if (document.SchemaVersion != 1)
            throw new InvalidDataException("Unsupported Fuchsia Route trainer document schema.");
        var catalog = new FuchsiaRouteTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id),
            document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FuchsiaRouteTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }
}

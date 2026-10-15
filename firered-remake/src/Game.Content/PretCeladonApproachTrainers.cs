using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record CeladonApproachTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement DeferredDoubleBattles);
public static class PretCeladonApproachTrainers
{
    public static CeladonApproachTrainerDocument LoadDocument(string root)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/trainer-battles/celadon-approach.json");
        var d=ContentJson.ParseStrict<CeladonApproachTrainerDocument>(Encoding.UTF8.GetString(bytes),SharedTrainerCatalog.Options(root));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported CeladonApproach trainer document schema.");
        CeladonApproachTrainerPartyFactory.ValidateCatalog(new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.Trainer)));
        return d;
    }
    public static CeladonApproachTrainerCatalog Load(string root)
    {var d=LoadDocument(root);return new(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(t=>t.Trainer));}
}

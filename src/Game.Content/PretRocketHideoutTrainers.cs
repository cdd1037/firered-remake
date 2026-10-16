using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record RocketHideoutTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames);
/// <summary>Exact source teams and move metadata; no campaign activation or battle execution.</summary>
public static class PretRocketHideoutTrainers
{
    public static RocketHideoutTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/trainer-battles/rocket-hideout/trainers.json");
        var d=ContentJson.ParseStrict<RocketHideoutTrainerDocument>(Encoding.UTF8.GetString(bytes),SharedTrainerCatalog.Options(root, canonical.ReferenceOptions()));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported RocketHideout trainer document schema.");
        RocketHideoutTrainerPartyFactory.ValidateCatalog(new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.Trainer)));
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static RocketHideoutTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root)
    {var d=LoadDocument(canonical, root);return new(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
}

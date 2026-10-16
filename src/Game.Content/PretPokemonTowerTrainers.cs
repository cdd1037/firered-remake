using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record PokemonTowerTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames);
/// <summary>Exact source teams and move metadata; no campaign activation or battle execution.</summary>
public static class PretPokemonTowerTrainers
{
    public static PokemonTowerTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot = null)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/trainer-battles/pokemon-tower/trainers.json");
        var d=ContentJson.ParseStrict<PokemonTowerTrainerDocument>(Encoding.UTF8.GetString(bytes),SharedTrainerCatalog.Options(dependencyProjectRoot ?? root, canonical.ReferenceOptions()));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported PokemonTower trainer document schema.");
        PokemonTowerTrainerPartyFactory.ValidateCatalog(new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.Trainer)));
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static PokemonTowerTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot = null)
    {var d=LoadDocument(canonical, root, dependencyProjectRoot);return new(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.Trainer));}
}

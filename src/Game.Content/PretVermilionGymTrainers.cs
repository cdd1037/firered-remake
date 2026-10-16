using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record VermilionGymTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretVermilionGymTrainers
{
    public static VermilionGymTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        }
        var doc=ContentJson.ParseStrict<VermilionGymTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/vermilion-gym.json")),SharedTrainerCatalog.Options(root, canonical.ReferenceOptions()));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("VermilionGym trainer document identity differs.");
        var catalog=new VermilionGymTrainerCatalog(doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        VermilionGymTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    public static PokemonCatalog ExtendPokemon(PretCanonicalPokemonCatalog canonical, string root, PokemonCatalog previous, bool route11Rules=false, bool rockTunnelRules=false, bool celadonApproachRules=false)
    {
        _ = Load(canonical, root);
        PokemonFactory.ValidateDefinitions(previous);
        if(previous.Species.Count != (celadonApproachRules?94:rockTunnelRules?93:route11Rules?79:73)) throw new InvalidDataException("Gym requires the exact selected regional species scope.");
        var doc=ReadDocument(canonical, root);
        var moves=previous.Moves;
        foreach(var move in doc.Moves)
        {
                    if(moves.TryGetValue(move.Id,out var existing) && (existing with { Flags=move.Flags } != move || !existing.Flags.SequenceEqual(move.Flags)))
                throw new InvalidDataException("Gym move conflicts with authenticated predecessor.");
            moves=moves.SetItem(move.Id,move);
        }
        var result=previous with { Moves=moves }; PokemonFactory.ValidateDefinitions(result); return result;
    }
    public static ImmutableDictionary<MoveId,string> LoadMoveNames(PretCanonicalPokemonCatalog canonical, string root)
    {
        _=Load(canonical, root); return ReadDocument(canonical, root).MoveNames.ToImmutableDictionary(m=>m.Id,m=>m.Name);
    }
    private static VermilionGymTrainerDocument ReadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var bytes=File.ReadAllBytes(GeneratedResourceFile.SafePath(root,"content/generated/data/trainer-battles/vermilion-gym.json"));
        return ContentJson.ParseStrict<VermilionGymTrainerDocument>(Encoding.UTF8.GetString(bytes),SharedTrainerCatalog.Options(root, canonical.ReferenceOptions()));
    }
}

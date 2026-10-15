using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record VermilionGymTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<string> Limitations);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretVermilionGymTrainers
{
    public static VermilionGymTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var doc=ContentJson.ParseStrict<VermilionGymTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-gym-trainers/trainers.json")),ContentJson.CreateOptions());
                if(doc.SchemaVersion!=1 || doc.CompleteSourceRoster) throw new InvalidDataException("VermilionGym trainer document identity differs.");
        var catalog=new VermilionGymTrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        VermilionGymTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    public static PokemonCatalog ExtendPokemon(string root, PokemonCatalog previous, bool route11Rules=false, bool rockTunnelRules=false, bool celadonApproachRules=false)
    {
        _ = Load(root);
        PokemonFactory.ValidateCatalog(previous);
        if(previous.Species.Count != (celadonApproachRules?94:rockTunnelRules?93:route11Rules?79:73)) throw new InvalidDataException("Gym requires the exact selected regional species scope.");
        var doc=ReadDocument(root);
        var moves=previous.Moves;
        foreach(var move in doc.Moves)
        {
                    if(moves.TryGetValue(move.Id,out var existing) && (existing with { Flags=move.Flags,Source=move.Source } != move || !existing.Flags.SequenceEqual(move.Flags)))
                throw new InvalidDataException("Gym move conflicts with authenticated predecessor.");
            moves=moves.SetItem(move.Id,move);
        }
        var result=previous with { Moves=moves }; PokemonFactory.ValidateCatalog(result); return result;
    }
    public static ImmutableDictionary<MoveId,string> LoadMoveNames(string root)
    {
        _=Load(root); return ReadDocument(root).MoveNames.ToImmutableDictionary(m=>m.Id,m=>m.Name);
    }
    private static VermilionGymTrainerDocument ReadDocument(string root)
    {
        var bytes=File.ReadAllBytes(SafeFile(root,"content/generated/vermilion-gym-trainers/trainers.json"));
        return ContentJson.ParseStrict<VermilionGymTrainerDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe VermilionGym trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("VermilionGym trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("VermilionGym trainer parent symlink rejected.");
        return f.FullName;
    }
}

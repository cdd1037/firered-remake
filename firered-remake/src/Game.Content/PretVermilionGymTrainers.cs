using System.Collections.Immutable;
using System.Security.Cryptography;
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
    public const string LockSha256 = "03508a0761395cc0fd645b3ff2eaae5979e36bcb543dffbb25186404adad2aa7";
    public const string GeneratedSha256 = "8d659ad637554364727352a3b04161b1be84b806f7f71a11266d1f3da96081ea";
    public static VermilionGymTrainerCatalog Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("VermilionGym trainer pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/vermilion-gym-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=VermilionGymTrainerPartyFactory.SourceCommit || s.GetProperty("profile").GetString()!=VermilionGymTrainerPartyFactory.RulesVersion ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("tree").GetString()!="7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0 || s.GetProperty("schemaVersion").GetInt32()!=1 || s.GetProperty("files").GetArrayLength()!=30) throw new InvalidDataException("VermilionGym trainer source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/vermilion-gym-trainers/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!files.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("VermilionGym trainer Git blob differs.");
        }
        var doc=ContentJson.ParseStrict<VermilionGymTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-gym-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r) { if(!files.TryGetValue(r.Path,out var h)||h!=r.Sha256||r.StartLine<=0||r.EndLine<r.StartLine) throw new InvalidDataException("VermilionGym trainer source reference differs."); }
        if(doc.SchemaVersion!=1 || doc.Profile!=VermilionGymTrainerPartyFactory.RulesVersion || doc.SourceCommit!=VermilionGymTrainerPartyFactory.SourceCommit || doc.CompleteSourceRoster) throw new InvalidDataException("VermilionGym trainer document identity differs.");
        foreach(var t in doc.Trainers) { Source(t.Source);Source(t.MoneySource);foreach(var p in t.Party){Source(p.Source);Source(p.SpeciesNameSource);} }
        foreach(var b in doc.Battles) Source(b.Source);
        var catalog=new VermilionGymTrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        VermilionGymTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    public static PokemonCatalog ExtendPokemon(string root, PokemonCatalog previous, bool route11Rules=false)
    {
        _ = Load(root);
        PokemonFactory.ValidateCatalog(previous);
        if(previous.Species.Count != (route11Rules?79:73)) throw new InvalidDataException("Gym requires the exact S.S. Anne species scope.");
        var doc=ReadDocument(root);
        var moves=previous.Moves;
        foreach(var move in doc.Moves)
        {
            if(moves.TryGetValue(move.Id,out var existing) && (existing with { Flags=move.Flags, Source=move.Source } != move || !existing.Flags.SequenceEqual(move.Flags)))
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
        var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,"content/generated/vermilion-gym-trainers/trainers.json"),2*1024*1024);
        if(Hash(bytes)!=GeneratedSha256) throw new InvalidDataException("Gym trainer document changed.");
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
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

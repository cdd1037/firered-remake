using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;
public sealed record VermilionMachineRow(ItemId Item,ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record VermilionMachinesDocument(int SchemaVersion,string Profile,string SourceCommit,string BaseCatalogSha256,
    ImmutableArray<VermilionMachineRow> Rows);
/// <summary>Adds only two source compatibility rows to each existing Bill machine. Item assets remain shared.</summary>
public static class PretVermilionMachines
{
    public const string Profile=PewterMachineRules.VermilionProfile;
    public const string LockSha256="2f69db2a2269bfe50a8bec8ad4159e5464f29980c1b839648f5355e91606a43d";
    public const string GeneratedSha256="8ae61f8c65babe3a90fddcbbc402e2d5196ff387a19f4405dcf4bf4c2280acb7";
    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("Vermilion machines pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/vermilion-machines.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=PewterMachineRules.SourceCommit || s.GetProperty("profile").GetString()!=Profile || s.GetProperty("files").GetArrayLength()!=2)
            throw new InvalidDataException("Vermilion machine source identity differs.");
        var inputs=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/vermilion-machines/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!inputs.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Vermilion machine Git blob differs.");
        }
        var d=ContentJson.ParseStrict<VermilionMachinesDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-machines/machines.json",GeneratedSha256)),ContentJson.CreateOptions());
        if(d.SchemaVersion!=1 || d.Profile!=Profile || d.SourceCommit!=PewterMachineRules.SourceCommit || d.BaseCatalogSha256!=PretBillMachines.GeneratedSha256 || d.Rows.Length!=8)
            throw new InvalidDataException("Vermilion machine extension scope differs.");
        var old=PretBillMachines.LoadCatalogProject(root);
        var prior=new[]{PewterMachineRules.Definition(old,PewterMachineRules.Tm39Id)!}.Concat(old.AdditionalMachines!.Value).ToArray();
        var machines=ImmutableArray.CreateBuilder<PewterAdditionalMachine>();
        for(int n=0;n<prior.Length;n++)
        {
            var row=d.Rows[n];
            if(row.Item!=prior[n].Machine.Item.Id || row.Compatibility.Length!=2)
                throw new InvalidDataException("Vermilion machine order differs.");
            foreach(var c in row.Compatibility)
                if(c.Source.Path!="src/data/pokemon/tmhm_learnsets.h" || c.Source.Sha256!=inputs[c.Source.Path] || c.Source.StartLine<=0 || c.Source.EndLine<c.Source.StartLine)
                    throw new InvalidDataException("Vermilion machine provenance differs.");
            machines.Add(prior[n] with{Compatibility=prior[n].Compatibility.AddRange(row.Compatibility)});
        }
        var result=old with{Profile=Profile,Compatibility=machines[0].Compatibility,AdditionalMachines=machines.Skip(1).ToImmutableArray()};
        PewterMachineRules.ValidateCatalog(result);return result;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Vermilion machine path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Vermilion machine symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Vermilion machine parent symlink rejected.");
        return f.FullName;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

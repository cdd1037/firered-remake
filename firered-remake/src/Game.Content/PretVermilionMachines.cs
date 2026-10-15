using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;
public sealed record VermilionMachineRow(ItemId Item,ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record VermilionMachinesDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<VermilionMachineRow> Rows);
/// <summary>Adds only two source compatibility rows to each existing Bill machine. Item assets remain shared.</summary>
public static class PretVermilionMachines
{
    public const string Profile=PewterMachineRules.VermilionProfile;
    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var d=ContentJson.ParseStrict<VermilionMachinesDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-machines/machines.json")),SharedMachineCatalog.Options(root));
                if(d.SchemaVersion!=1 || d.Rows.Length!=8)
            throw new InvalidDataException("Vermilion machine extension scope differs.");
        var old=PretBillMachines.LoadCatalogProject(root);
        var prior=new[]{PewterMachineRules.Definition(old,PewterMachineRules.Tm39Id)!}.Concat(old.AdditionalMachines!.Value).ToArray();
        var machines=ImmutableArray.CreateBuilder<PewterAdditionalMachine>();
        for(int n=0;n<prior.Length;n++)
        {
            var row=d.Rows[n];
            if(row.Item!=prior[n].Machine.Item.Id || row.Compatibility.Length!=2)
                throw new InvalidDataException("Vermilion machine order differs.");
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
}

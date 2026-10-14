using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.World;
namespace Game.Content;

public sealed record PretItemfinderInput(string Path,string Sha256,int ByteLength);
public sealed record PretItemfinderIcon(string Item,string File,string Sha256);
public sealed record PretItemfinderDocument(int SchemaVersion,string SourceCommit,int ItemfinderSourceId,int RequiredCaught,
    ImmutableArray<ItemfinderUnderfoot> Underfoot,ImmutableDictionary<string,string> Texts,string ItemfinderDescription,string IconSha256,ImmutableArray<PretItemfinderIcon> Icons,
    ImmutableArray<PretItemfinderInput> Inputs,ImmutableArray<string> Limitations);
public static class PretItemfinderSource
{
    public const string DocumentSha256="d0a86f8e71a39880239c870d6dcf898ec57e6b8552510ad36baf40e08f36325f";
    public static PretItemfinderDocument Load(string root)
    {
        var directory=Path.Combine(root,"content/generated/itemfinder-source");
        var bytes=File.ReadAllBytes(Path.Combine(directory,"itemfinder-source.json"));
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=DocumentSha256)throw new InvalidDataException("itemfinder_document_hash");
        var result=ContentJson.ParseStrict<PretItemfinderDocument>(System.Text.Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        if(result.SchemaVersion!=1||result.SourceCommit!=Game.Domain.Items.OpeningInventoryRules.SourceCommit||result.ItemfinderSourceId!=261||result.RequiredCaught!=30||result.Underfoot.Length!=5||result.Inputs.Length!=31||result.Icons.Length!=4)throw new InvalidDataException("itemfinder_source_contract");
        foreach(var input in result.Inputs)
        {
            var raw=File.ReadAllBytes(Path.Combine(directory,"source",input.Path));
            if(raw.Length!=input.ByteLength||Convert.ToHexStringLower(SHA256.HashData(raw))!=input.Sha256)throw new InvalidDataException("itemfinder_source_input:"+input.Path);
        }
        var icon=File.ReadAllBytes(Path.Combine(directory,"itemfinder.rgba"));
        if(icon.Length!=24*24*4||Convert.ToHexStringLower(SHA256.HashData(icon))!=result.IconSha256)throw new InvalidDataException("itemfinder_icon_hash");
        foreach(var entry in result.Icons)
        {
            if(entry.File.Contains('/')||entry.File.Contains('\\')||!entry.File.EndsWith(".rgba",StringComparison.Ordinal))throw new InvalidDataException("itemfinder_icon_path");
            var pixels=File.ReadAllBytes(Path.Combine(directory,entry.File));
            if(pixels.Length!=24*24*4||Convert.ToHexStringLower(SHA256.HashData(pixels))!=entry.Sha256)throw new InvalidDataException("itemfinder_item_icon_hash");
        }
        return result;
    }
}
public sealed partial class PretCampaignCatalog:IItemfinderSourceCatalog
{
    public PretItemfinderDocument? ItemfinderDocument {get;internal set;}
    public ItemfinderSourceContract? ItemfinderSource=>ItemfinderDocument is {} source?new(source.Underfoot.AddRange(CapeBrinkUnderfootItems),source.Texts):null;
}

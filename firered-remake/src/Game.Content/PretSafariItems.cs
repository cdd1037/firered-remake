using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SafariItemsDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs,ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<string> Limitations,ImmutableArray<SnorlaxArtOutput> Outputs);
public sealed record SafariMachineAddition(ItemId Item,ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SafariMachinesDocument(int SchemaVersion,string Profile,string SourceCommit,string BaselineSpeciesSha256,
    ImmutableArray<ItemId> InheritedItemIds,ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<PewterAdditionalMachine> NewMachines,ImmutableArray<string> Limitations);

/// <summary>Twelve original Safari items, source icons and five full110 source HM matrices. Loading grants no rewards or field action.</summary>
public sealed class PretSafariItems
{
    public const string Profile="firered-safari-items-v1";
    public const string LockSha256="038989466424735694c3096f68e8c0834f115d747efda8e08526f59f5b7978ca";
    public const int LockByteLength=6921;
    public const string GeneratedSha256="e6baf7f218392248ea43b08196cd9905f8f6c09089ff144ed6c17aa978e9cd14";
    public const string MachinesSha256="e51d1ef5e48acd7424ae8addc80b20fc72e1170ba7340a068ebf56e9e8e53b9a";
    public const string GeneratedDirectory="content/generated/safari-items/";
    public static ItemId Hm03Id { get; }=new("pret:ITEM_HM03");
    public static ItemId Hm04Id { get; }=new("pret:ITEM_HM04");
    public static ItemId GoldTeethId { get; }=new("pret:ITEM_GOLD_TEETH");
    public SafariItemsDocument Document { get; }
    public SafariMachinesDocument Machines { get; }
    public ImmutableArray<PretBillSourceItem> Items=>Document.Items;
    public ImmutableArray<PretPewterMachineIcon> Icons=>Document.Icons;
    public ImmutableDictionary<string,byte[]> Files { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretSafariItems(SafariItemsDocument document,SafariMachinesDocument machines,ImmutableDictionary<string,byte[]> files)
    {
        Document=document;Machines=machines;Files=files;
        AdditionalSourceHashes=["safari-items-lock:"+LockSha256,"safari-items:"+GeneratedSha256,"safari-machines:"+MachinesSha256,
            ..document.Inputs.Select(i=>"safari-items-source:"+i.Path+":"+i.Sha256),
            ..document.Outputs.Select(o=>"safari-item-pixels:"+o.Path+":"+o.Sha256)];
    }
    public static PretSafariItems LoadProject(string projectRoot)
    {
        string root=Path.GetFullPath(projectRoot);
        _=SafariSource.Read(root,"scripts/safari-items.lock.json",LockSha256,LockByteLength);
        var doc=SafariSource.Parse<SafariItemsDocument>(SafariSource.Read(root,GeneratedDirectory+"items.json",GeneratedSha256,72118));
        SafariSource.Require(doc.SchemaVersion==1&&doc.Profile==Profile&&doc.SourceCommit==PretSafariPokemon.SourceCommit&&doc.Inputs.Length==28&&
            doc.Items.Length==12&&doc.Icons.Length==12&&doc.Outputs.Length==12&&!doc.RulesSources.IsDefaultOrEmpty&&!doc.Limitations.IsDefaultOrEmpty,"Safari item scope differs.");
        var lines=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var i in doc.Inputs)
        {
            var bytes=SafariSource.Read(root,"content/raw/safari-items/"+i.Path,i.Sha256,i.ByteLength);
            SafariSource.Require(SafariSource.GitBlob(bytes)==i.GitBlobSha1,"Safari item Git bytes differ.");
            lines.Add(i.Path,i.Path.EndsWith(".png",StringComparison.Ordinal)?1:new UTF8Encoding(false,true).GetString(bytes).Count(c=>c=='\n')+1);
        }
        void Source(OpeningItemSourceRef r)=>SafariSource.Require(doc.Inputs.Any(i=>i.Path==r.Path&&i.Sha256==r.Sha256)&&
            r.StartLine>0&&r.EndLine>=r.StartLine&&r.EndLine<=lines[r.Path]&&!string.IsNullOrWhiteSpace(r.Symbol),"Safari item source reference differs.");
        foreach(var r in doc.RulesSources)Source(r);
        SafariSource.Require(doc.Items.Select(i=>i.Metadata.Item.Id).SequenceEqual(new[]{"HM03","HM04","GOLD_TEETH","TM11","TM47","TM32","MAX_POTION","FULL_RESTORE","LEAF_STONE","PROTEIN","QUICK_CLAW","MAX_REVIVE"}.Select(s=>new ItemId("pret:ITEM_"+s))),"Safari item identity differs.");
        foreach(var i in doc.Items)
        {
            var item=i.Metadata.Item;Source(item.Source);Source(i.Metadata.DescriptionSource);
            SafariSource.Require(item.CanGive==(item.Importance==0)&&item.CanToss==(item.Importance==0)&&item.CanSell==(item.Price!=0)&&
                i.EffectBytes.IsEmpty&&i.EffectSource is null,"Safari key/HM properties differ.");
        }
        var files=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var o in doc.Outputs)files.Add(o.Path,SafariSource.Read(root,GeneratedDirectory+o.Path,o.Sha256,o.ByteLength));
        foreach(var i in doc.Icons)
        {
            Source(i.Source);Source(i.PaletteSource);Source(i.BindingSource);
            var rgba=Convert.FromBase64String(i.RgbaBase64);
            SafariSource.Require(i.Width==24&&i.Height==24&&rgba.Length==24*24*4&&SafariSource.Hash(rgba)==i.RgbaSha256&&
                files.ContainsKey(i.PngPath)&&SafariSource.Hash(files[i.PngPath])==i.PngSha256,"Safari item pixels differ.");
        }
        var machines=SafariSource.Parse<SafariMachinesDocument>(SafariSource.Read(root,GeneratedDirectory+"machines.json",MachinesSha256,172225));
        SafariSource.Require(machines.SchemaVersion==1&&machines.Profile=="firered-safari-machines-v1"&&machines.SourceCommit==doc.SourceCommit&&
            machines.BaselineSpeciesSha256=="3775e076364c67d145c5754e88907d1bdf3de334b7e714e90a63b86cb87b396d"&&
            machines.InheritedItemIds.Length==19&&machines.Additions.Length==19&&machines.NewMachines.Length==5&&
            machines.Additions.Select(a=>a.Item).SequenceEqual(machines.InheritedItemIds)&&
            machines.NewMachines.Select(m=>m.Machine.Item.Id).SequenceEqual(new[]{"HM03","HM04","TM11","TM47","TM32"}.Select(s=>new ItemId("pret:ITEM_"+s))),"Safari machine metadata scope differs.");
        foreach(var a in machines.Additions)
        {
            SafariSource.Require(a.Compatibility.Select(c=>c.SourceSpeciesId).SequenceEqual([113,123,128]),"Safari added3 compatibility scope differs.");
            foreach(var c in a.Compatibility)Source(c.Source);
        }
        foreach(var m in machines.NewMachines)
        {
            SafariSource.Require(m.Machine==doc.Items.Single(i=>i.Metadata.Item.Id==m.Machine.Item.Id).Metadata&&m.Compatibility.Length==110&&
                m.Compatibility.Select(c=>c.Species).Distinct().Count()==110&&m.Compatibility.Select(c=>c.SourceSpeciesId).SequenceEqual(m.Compatibility.Select(c=>c.SourceSpeciesId).Order()),"Safari newHM metadata differs.");
            foreach(var c in m.Compatibility)Source(c.Source);
        }
        SafariSource.Require(machines.NewMachines[0] is {Move.Value:"pret:MOVE_SURF",SourceMoveId:57,MovePp:15}&&
            machines.NewMachines[1] is {Move.Value:"pret:MOVE_STRENGTH",SourceMoveId:70,MovePp:15},"Safari HM move mapping differs.");
        return new(doc,machines,files.ToImmutable());
    }
    public EarlyShopItemDefinition Definition(ItemId item)=>PretSsAnneItems.Definition(Items.Single(i=>i.Metadata.Item.Id==item));
    public byte[] ReadTexture(string relative)
    {
        if(!relative.StartsWith(GeneratedDirectory,StringComparison.Ordinal)||!Files.TryGetValue(relative[GeneratedDirectory.Length..],out var bytes))
            throw new InvalidDataException("Unknown Safari item texture.");
        return [..bytes];
    }
}

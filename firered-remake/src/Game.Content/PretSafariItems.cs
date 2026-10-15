using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SafariItemsDocument(int SchemaVersion,ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<SnorlaxArtOutput> Outputs);
public sealed record SafariMachineAddition(ItemId Item,ImmutableArray<MachineCompatibility> Compatibility);
public sealed record SafariMachinesDocument(int SchemaVersion, ImmutableArray<ItemId> InheritedItemIds,
    ImmutableArray<SafariMachineAddition> Additions, ImmutableArray<MachineDefinition> NewMachines);

/// <summary>Twelve original Safari items, source icons and five full110 source HM matrices. Loading grants no rewards or field action.</summary>
public sealed class PretSafariItems
{
    public const string Profile="firered-safari-items-v1";
    public const string GeneratedDirectory="content/generated/data/items/safari/";
    public static ItemId Hm03Id { get; }=new("pret:ITEM_HM03");
    public static ItemId Hm04Id { get; }=new("pret:ITEM_HM04");
    public static ItemId GoldTeethId { get; }=new("pret:ITEM_GOLD_TEETH");
    public SafariItemsDocument Document { get; }
    public SafariMachinesDocument Machines { get; }
    public ImmutableArray<PretBillSourceItem> Items=>Document.Items;
    public ImmutableArray<PretPewterMachineIcon> Icons=>Document.Icons;
    public ImmutableDictionary<string,byte[]> Files { get; }
    private PretSafariItems(SafariItemsDocument document,SafariMachinesDocument machines,ImmutableDictionary<string,byte[]> files)
    {
        Document=document;Machines=machines;Files=files;
    }
    public static PretSafariItems LoadProject(string projectRoot)
    {
        string root=Path.GetFullPath(projectRoot);
        var doc=SafariSource.Parse<SafariItemsDocument>(SafariSource.Read(root,GeneratedDirectory+"items.json"), root);
        SafariSource.Require(doc.SchemaVersion==1&&
            doc.Items.Length==12&&doc.Icons.Length==12,"Safari item scope differs.");
        SafariSource.Require(doc.Items.Select(i=>i.Metadata.Item.Id).SequenceEqual(new[]{"HM03","HM04","GOLD_TEETH","TM11","TM47","TM32","MAX_POTION","FULL_RESTORE","LEAF_STONE","PROTEIN","QUICK_CLAW","MAX_REVIVE"}.Select(s=>new ItemId("pret:ITEM_"+s))),"Safari item identity differs.");
        foreach(var i in doc.Items)
        {
            var item=i.Metadata.Item;            SafariSource.Require(item.CanGive==(item.Importance==0)&&item.CanToss==(item.Importance==0)&&item.CanSell==(item.Price!=0)&&
                i.EffectBytes.IsEmpty,"Safari key/HM properties differ.");
        }
        var files=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var o in doc.Outputs)files.Add(o.Path,SafariSource.Read(root,o.Path.StartsWith("content/generated/", StringComparison.Ordinal) ? o.Path : GeneratedDirectory+o.Path));
        foreach(var i in doc.Icons)
        {
            var pngBytes=files[i.PngPath];
            SafariSource.Require(i.Width==24&&i.Height==24&&pngBytes.Length > 0&&
                files.ContainsKey(i.PngPath),"Safari item pixels differ.");
        }
        var machines=SafariSource.Parse<SafariMachinesDocument>(SafariSource.Read(root,GeneratedDirectory+"machines.json"), root);
        SafariSource.Require(machines.SchemaVersion==1&&
            machines.InheritedItemIds.Length==19&&machines.Additions.Length==19&&machines.NewMachines.Length==5&&
            machines.Additions.Select(a=>a.Item).SequenceEqual(machines.InheritedItemIds)&&
            machines.NewMachines.Select(m=>m.Machine.Item.Id).SequenceEqual(new[]{"HM03","HM04","TM11","TM47","TM32"}.Select(s=>new ItemId("pret:ITEM_"+s))),"Safari machine metadata scope differs.");
        foreach(var a in machines.Additions)
        {
            SafariSource.Require(a.Compatibility.Select(c=>c.SourceSpeciesId).SequenceEqual([113,123,128]),"Safari added3 compatibility scope differs.");
        }
        foreach(var m in machines.NewMachines)
        {
            SafariSource.Require(m.Machine==doc.Items.Single(i=>i.Metadata.Item.Id==m.Machine.Item.Id).Metadata&&m.Compatibility.Length==110&&
                m.Compatibility.Select(c=>c.Species).Distinct().Count()==110&&m.Compatibility.Select(c=>c.SourceSpeciesId).SequenceEqual(m.Compatibility.Select(c=>c.SourceSpeciesId).Order()),"Safari newHM metadata differs.");
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

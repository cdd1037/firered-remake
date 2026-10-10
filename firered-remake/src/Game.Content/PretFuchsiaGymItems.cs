using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretFuchsiaGymItemsDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs,ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,ImmutableArray<string> Limitations);

/// <summary>Authenticated TM06 metadata for future composition. No reward or teaching handler is registered.</summary>
public static class PretFuchsiaGymItems
{
    public const string Profile="firered-fuchsia-gym-items-v1";
    public const string LockSha256="5a95a2ac27483b79e8138975fdfc8aa6f645a95a7867b6e972ee3d0449e01719";
    public const string ManifestSha256="4df3627dd0169c22efae5120f195db60affa558a4a07258be2de07aa3c5df0dc";
    public const string RawManifestSha256="36f9005ca947b758ed76b6254971eafab55b70ae26b60db17de2646231858692";
    public const string FullSourceHash="08bf5d43d2d6d7800705cf9a1a7a142aa951dfbd35a4659b679bf0cca756ffdd";
    public const string GeneratedSha256="95da022131501cbd8bc654ee2477ff3fca199f5913acfdfc183d6b3e73a548c6";
    public static readonly ItemId Tm06Id=new("pret:ITEM_TM06");
    public static PretFuchsiaGymItemsDocument LoadProject(string root)
    {
        var source=PretCeladonApproachSource.Load(root,"fuchsia-gym-items",Profile,LockSha256,16);
        _=PretCeladonApproachSource.Read(root,"content/raw/fuchsia-gym-items/manifest.json",RawManifestSha256);
        var outputs=PretCeladonApproachSource.Outputs(root,"fuchsia-gym-items",Profile,ManifestSha256,LockSha256,16);
        Need(outputs.Count==3&&PretCeladonApproachSource.Hash(outputs["fuchsia-gym-items.json"])==GeneratedSha256,"Fuchsia Gym item outputs differ.");
        var document=ContentJson.ParseStrict<PretFuchsiaGymItemsDocument>(Encoding.UTF8.GetString(outputs["fuchsia-gym-items.json"]),ContentJson.CreateOptions());
        Need(document.SchemaVersion==1&&document.Profile==Profile&&document.SourceCommit==PretWorldMap.SourceCommit&&document.Inputs.Length==16&&
            document.Items.Length==1&&document.Icons.Length==1&&!document.RulesSources.IsDefaultOrEmpty&&!document.Limitations.IsDefaultOrEmpty,"Fuchsia Gym item scope differs.");
        Need(document.Inputs.Select(f=>(f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength)).SequenceEqual(source.Files.Select(f=>(f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength))),"Fuchsia Gym item source inventory differs.");
        Need(PretCeladonApproachSource.Hash(Encoding.UTF8.GetBytes(string.Concat(document.Inputs.OrderBy(i=>i.Path,StringComparer.Ordinal).Select(i=>i.Path+"\0"+i.Sha256+"\n"))))==FullSourceHash,"Fuchsia Gym item source set differs.");
        void Ref(OpeningItemSourceRef r)=>source.Reference(r.Path,r.Sha256,r.StartLine,r.EndLine,r.Symbol);
        foreach(var r in document.RulesSources)Ref(r);
        var item=document.Items.Single();var d=item.Metadata.Item;
        Need(d.Id==Tm06Id&&d.SourceItemSymbol=="ITEM_TM06"&&d.SourceItemId==294&&d.Name=="TM06"&&d.Price==3000&&
            d.Importance==0&&d.Registrability==0&&d.Pocket=="POCKET_TM_CASE"&&d.Type=="ITEM_TYPE_PARTY_MENU"&&d.HoldEffect=="HOLD_EFFECT_NONE"&&
            d.HoldEffectParam==0&&d.FieldUseFunction=="NULL"&&d.BattleUsage==0&&d.BattleUseFunction=="NULL"&&
            d.SecondaryId==0&&d.CanToss&&d.CanSell&&d.CanGive&&item.EffectBytes.IsEmpty&&item.EffectSource is null,"TM06 source item differs.");
        Ref(d.Source);Ref(item.Metadata.DescriptionSource);
        var icon=document.Icons.Single();
        Need(icon.Item==Tm06Id&&icon.Width==24&&icon.Height==24&&icon.PngPath=="item_tm06.png"&&
            PretCeladonApproachSource.Hash(outputs[icon.PngPath])==icon.PngSha256,"TM06 source icon differs.");
        Ref(icon.Source);Ref(icon.PaletteSource);Ref(icon.BindingSource);_=PretPewterMachines.IconRgba(icon);
        return document;
    }
    public static EarlyShopItemDefinition Definition(PretFuchsiaGymItemsDocument document)=>PretSsAnneItems.Definition(document.Items.Single());
    private static void Need(bool condition,string message)=>PretCeladonApproachSource.Require(condition,message);
}

/// <summary>One TM06 entry and all 107 source compatibility rows. The caller owns campaign composition.</summary>
public static class PretFuchsiaGymMachines
{
    public const string Profile="firered-fuchsia-gym-machines-v1";
    public const string GeneratedSha256="29a06d024eebb4315f0c1d5b62d3e296c54f9b51ac09f9a8f39c0f14f01534ea";
    public static PretRockTunnelMachinesDocument LoadProject(string root)
    {
        var items=PretFuchsiaGymItems.LoadProject(root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/fuchsia-gym-items/fuchsia-gym-machines.json",GeneratedSha256);
        var document=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        void Need(bool value,string message)=>PretCeladonApproachSource.Require(value,message);
        Need(document.SchemaVersion==1&&document.Profile==Profile&&document.SourceCommit==PretWorldMap.SourceCommit&&document.Inputs.SequenceEqual(items.Inputs)&&
            document.Machines.Length==1&&document.Icons.Length==1&&!document.Limitations.IsDefaultOrEmpty,"Fuchsia Gym machine scope differs.");
        Need(JsonSerializer.Serialize(document.Icons)==JsonSerializer.Serialize(items.Icons),"Fuchsia Gym machine icon provenance differs.");
        var inputs=document.Inputs.ToDictionary(i=>i.Path,StringComparer.Ordinal);
        void Ref(OpeningItemSourceRef r)=>Need(inputs.TryGetValue(r.Path,out var p)&&r.Sha256==p.Sha256&&r.StartLine>0&&r.EndLine>=r.StartLine&&!string.IsNullOrWhiteSpace(r.Symbol),"Fuchsia Gym machine reference differs.");
        foreach(var r in document.RulesSources)Ref(r);
        var machine=document.Machines.Single();
        Need(machine.Machine==items.Items.Single().Metadata&&machine.Move.Value=="pret:MOVE_TOXIC"&&machine.SourceMoveId==92&&machine.MovePp==10&&
            machine.Compatibility.Length==107&&machine.Compatibility.Select(c=>c.Species).Distinct().Count()==107&&
            machine.Compatibility.Select(c=>c.SourceSpeciesId).SequenceEqual(machine.Compatibility.Select(c=>c.SourceSpeciesId).Order()),"TM06 mapping/compatibility differs.");
        foreach(var row in machine.Compatibility)Ref(row.Source);
        return document;
    }
}

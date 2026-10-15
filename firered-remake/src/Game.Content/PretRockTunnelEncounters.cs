using System.Collections.Immutable;
using System.Text;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record RockTunnelEncounterDocument(int SchemaVersion,
    ImmutableArray<EarlyLandEncounterTable> Tables, int HeldItemDrawBound,
    ImmutableArray<EncounterHeldItemRow> HeldItems);

/// <summary>Generated land encounter definitions; method admission remains in campaign rules.</summary>
public sealed class PretRockTunnelEncounters
{
    public const string Profile = "firered-rocktunnel-encounters-v1";
    public ImmutableDictionary<MapId, EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<EncounterHeldItemRow> HeldItems { get; }

    private PretRockTunnelEncounters(RockTunnelEncounterDocument document)
    {
        Tables = document.Tables.ToImmutableDictionary(table => table.Map);
        HeldItems = document.HeldItems;
    }

    public static PretRockTunnelEncounters Load(string root)
    {
        var bytes = File.ReadAllBytes(SafeFile(root, "content/generated/data/encounters/rocktunnel.json"));
        var document = ContentJson.ParseStrict<RockTunnelEncounterDocument>(
            Encoding.UTF8.GetString(bytes), ContentJson.CreateOptions());
        GeneratedLandEncounterValidation.Validate(document.SchemaVersion, document.Tables,
            document.HeldItemDrawBound, document.HeldItems,
            row => row.Common == "ITEM_NONE" && row.Rare == (row.Species == "pret:SPECIES_ONIX" ? "ITEM_HARD_STONE" : "ITEM_NONE"));
        return new(document);
    }

    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe RockTunnel encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("RockTunnel encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("RockTunnel encounter parent symlink rejected.");
        return f.FullName;
    }
}

using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private GeneratedFieldTilesets _sharedFieldTilesets=null!;
    private ImmutableDictionary<MapId,string> _fieldLayouts=ImmutableDictionary<MapId,string>.Empty;
    public ImmutableDictionary<int,int> CutGrassMetatiles{get;private set;}=ImmutableDictionary<int,int>.Empty;
    public FieldMetatileEdit? SourceMetatileAt(MapId map,int x,int y)=>SourceMaps.TryGetValue(map,out var source)&&source.GetSourceCell(x,y) is {} cell
        ?new(x,y,cell.MetatileId,cell.Collision,cell.Elevation):null;
    private void LoadFieldCut(string root)
    {
        _fieldLayouts=SourceMaps.ToImmutableDictionary(p=>p.Key,p=>p.Value.Events.GetProperty("layout").GetString()!);
        _sharedFieldTilesets=GeneratedFieldTilesets.ForPackage(Path.Combine(root,"content/generated"));
        var table=JsonSerializer.Deserialize<int[][]>(File.ReadAllBytes(Path.Combine(root,"content/generated/data/field/cut-grass.json")))
            ??throw new InvalidDataException("cut_grass_metatiles_missing");
        if(table.Length==0||table.Any(r=>r.Length!=2||r.Any(n=>n is <0 or >1023))||table.Select(r=>r[0]).Distinct().Count()!=table.Length)
            throw new InvalidDataException("cut_grass_metatiles_invalid");
        CutGrassMetatiles=table.ToImmutableDictionary(r=>r[0],r=>r[1]);
    }
}

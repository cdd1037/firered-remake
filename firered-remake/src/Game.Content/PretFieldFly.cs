using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Loads Fly destinations and HM02 definitions, reusing TM40 art.</summary>
public static class PretFieldFly
{
    public static ItemId ReusedHm02Icon { get; } = new("pret:ITEM_TM40");
    private static byte[] Read(string root,string path)
    {
        if (Path.IsPathRooted(path) || path.Split('/').Any(part => part is "" or "." or "..")) throw new InvalidDataException("field_fly_path");
        return File.ReadAllBytes(Path.Combine(root, path));
    }
    public static FieldFlySourceDocument LoadDocument(string root, SharedMachineCatalog? shared = null) => ContentJson.ParseStrict<FieldFlySourceDocument>(
        Encoding.UTF8.GetString(Read(root,"content/generated/data/services/field-fly/field.json")), (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions());
    public static MachineDefinition LoadHm02(string root, SharedMachineCatalog? shared = null) => ContentJson.ParseStrict<MachineDefinition>(
        Encoding.UTF8.GetString(Read(root,"content/generated/data/services/field-fly/hm02.json")), (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions());
    public static FieldFlyCatalog Load(string root, SharedMachineCatalog? shared = null) => FieldFlyCatalog.Authenticate(LoadDocument(root, shared));
}

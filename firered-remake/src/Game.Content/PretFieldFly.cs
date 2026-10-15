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
    public static FieldFlySourceDocument LoadDocument(string root) => ContentJson.ParseStrict<FieldFlySourceDocument>(
        Encoding.UTF8.GetString(Read(root,"content/generated/field-fly/field.json")), SharedMachineCatalog.Options(root));
    public static PewterAdditionalMachine LoadHm02(string root) => ContentJson.ParseStrict<PewterAdditionalMachine>(
        Encoding.UTF8.GetString(Read(root,"content/generated/field-fly/hm02.json")), SharedMachineCatalog.Options(root));
    public static FieldFlyCatalog Load(string root) => FieldFlyCatalog.Authenticate(LoadDocument(root));
    public static FieldFlyMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor) =>
        FieldFlyMachineCatalog.Authenticate(predecessor, LoadHm02(root));
}

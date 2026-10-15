using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretPewterMachineIcon(ItemId Item, int Width, int Height, string PngPath, ImageRegion? Region = null);
public sealed record PretPewterMachinesDocument(int SchemaVersion,
    ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Loads the shared TM39 and TM CASE presentation icons.</summary>
public static class PretPewterMachines
{

    public static PretPewterMachinesDocument LoadDocument(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretPewterMachinesDocument>(json, machines.CreateJsonOptions());
        if (document.SchemaVersion != 1 ||
            document.Icons.IsDefault || document.Icons.Length != 2)
            throw ContentJson.Error("pewter_machines_profile", "$", "TM39 and TM CASE icon metadata are required.");
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            if (icon.Item != (n == 0 ? MachineRules.Tm39Id : MachineRules.TmCaseId) ||
                icon.Region is null)
                throw new InvalidDataException("Pewter machine icon identity differs.");
            ValidateIcon(icon);         }
        return document;
    }

    public static void ValidateIcon(PretPewterMachineIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (icon.Width != 24 || icon.Height != 24 || string.IsNullOrWhiteSpace(icon.PngPath) ||
            Path.IsPathRooted(icon.PngPath) || icon.PngPath.Contains('\\') ||
            icon.PngPath.Split('/').Any(part => part is "" or "." or "..") || !icon.PngPath.EndsWith(".png", StringComparison.Ordinal))
            throw new InvalidDataException("Item icon metadata differs.");
        if (icon.Region is {} region && (region.X < 0 || region.Y < 0 || region.Width != icon.Width || region.Height != icon.Height))
            throw new InvalidDataException("Item icon atlas region differs.");
    }

    public static byte[] TexturePage(PretPewterMachineIcon icon, byte[] png)
    {
        ValidateIcon(icon);
        return png;
    }
}

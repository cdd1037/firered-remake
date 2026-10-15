using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretPewterMachineInput(string Path);
public sealed record PretPewterMachineIcon(ItemId Item, int Width, int Height, string PngPath, OpeningItemSourceRef Source,
    OpeningItemSourceRef PaletteSource, OpeningItemSourceRef BindingSource);
public sealed record PretPewterMachinesDocument(int SchemaVersion, ImmutableArray<PretPewterMachineInput> Inputs,
    ImmutableArray<OpeningItemSourceRef> RulesSources, ImmutableArray<string> Limitations, PewterMachineCatalog Catalog,
    ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Loads TM39 items, compatibility rows and icons.</summary>
public static class PretPewterMachines
{

    public static PewterMachineCatalog Load(string generatedJson) => LoadDocument(generatedJson).Catalog;

    public static PewterMachineCatalog LoadProject(string projectRoot)
    {
        string Read(string relative) => File.ReadAllText(SafePath(projectRoot, relative), new UTF8Encoding(false, true));
        var document = LoadDocument(Read("content/generated/pewter-machines/pewter-machines.json"));
        return document.Catalog;
    }

    public static PewterMachineCatalog LoadRoute22Project(string projectRoot)
    {
        var old=LoadProject(projectRoot);
        const string path="src/data/pokemon/tmhm_learnsets.h";
        string text = File.ReadAllText(SafePath(projectRoot, PretSourcePaths.FireRedRawDirectory + "/" + path), new UTF8Encoding(false, true));
        var source = old.Compatibility[0].Source;
        var extra=ImmutableArray.CreateBuilder<PewterMachineCompatibility>();
        foreach(var (symbol,id) in new[]{("SPEAROW",21),("FEAROW",22),("MANKEY",56)})
        {
            var match=System.Text.RegularExpressions.Regex.Match(text,@"\[SPECIES_"+symbol+@"\]\s*=\s*TMHM_LEARNSET\((.*?)\),",System.Text.RegularExpressions.RegexOptions.Singleline);
            if(!match.Success)throw new InvalidDataException("Route22 machine source missing: "+symbol);
            int line=1+text[..match.Index].Count(c=>c=='\n');
            extra.Add(new(new("pret:SPECIES_"+symbol),id,match.Value.Contains("TMHM(TM39_ROCK_TOMB)",StringComparison.Ordinal),source with { Path = path, Symbol = "SPECIES_" + symbol, StartLine = line, EndLine = line + match.Value.Count(c => c == '\n') }));
        }
        var result=old with{Profile=PewterMachineRules.Route22Profile,Compatibility=old.Compatibility.AddRange(extra)};
        PewterMachineRules.ValidateCatalog(result);return result;
    }

    public static PewterMachineCatalog LoadRoute3Project(string projectRoot)
    {
        var old = LoadRoute22Project(projectRoot);
        const string path = "src/data/pokemon/tmhm_learnsets.h";
        string text = File.ReadAllText(SafePath(projectRoot, PretSourcePaths.FireRedRawDirectory + "/" + path), new UTF8Encoding(false, true));
        var source = old.Compatibility[0].Source;
        var extra = ImmutableArray.CreateBuilder<PewterMachineCompatibility>();
        foreach (var (symbol, id) in new[] { ("EKANS",23),("NIDORAN_F",29),("NIDORINA",30),("NIDORAN_M",32),("NIDORINO",33),("JIGGLYPUFF",39),("MAGIKARP",129),("GYARADOS",130) })
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\[SPECIES_" + symbol + @"\]\s*=\s*TMHM_LEARNSET\((.*?)\),", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!match.Success) throw new InvalidDataException("Route3 machine source missing: " + symbol);
            int line = 1 + text[..match.Index].Count(c => c == '\n');
            extra.Add(new(new("pret:SPECIES_" + symbol), id, match.Value.Contains("TMHM(TM39_ROCK_TOMB)", StringComparison.Ordinal),
                source with { Path = path, Symbol = "SPECIES_" + symbol, StartLine = line, EndLine = line + match.Value.Count(c => c == '\n') }));
        }
        var result = old with { Profile = PewterMachineRules.Route3Profile,Compatibility = old.Compatibility.AddRange(extra) };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }

    public static PewterMachineCatalog LoadMtMoonProject(string projectRoot)
    {
        var old = LoadRoute3Project(projectRoot);
        var items = PretMtMoonItems.LoadProject(projectRoot);
        var result = old with { Profile = PewterMachineRules.MtMoonProfile,Compatibility = items.Tm39Compatibility,AdditionalMachines = items.Machines };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }

    private static string SafePath(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative));
        for (FileSystemInfo? current = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
            current is not null; current = current is FileInfo f ? f.Directory : ((DirectoryInfo)current).Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Pewter machine symlink path rejected.");
        return path;
    }

    public static PretPewterMachinesDocument LoadDocument(string json)
    {
        var document = ContentJson.ParseStrict<PretPewterMachinesDocument>(json, ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Inputs.IsDefault ||
            document.Icons.IsDefault || document.Icons.Length != 2 ||
            document.RulesSources.IsDefaultOrEmpty || document.Limitations.IsDefaultOrEmpty || document.Catalog is null)
            throw ContentJson.Error("pewter_machines_profile", "$", "The TM39 catalog and icon metadata are required.");
        var inputs = new Dictionary<string, PretPewterMachineInput>(StringComparer.Ordinal);
        foreach (var input in document.Inputs)
            if (input is null || string.IsNullOrWhiteSpace(input.Path) || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("pewter_machines_inputs", "$.inputs", "Invalid or duplicate source identity.");
        try { PewterMachineRules.ValidateCatalog(document.Catalog); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        { throw ContentJson.Error("pewter_machines_catalog", "$.catalog", ex.Message); }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            if (icon.Item != (n == 0 ? PewterMachineRules.Tm39Id : PewterMachineRules.TmCaseId) ||
                icon.PngPath != (n == 0 ? "item_tm39.png" : "item_tm_case.png"))
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
    }

    public static byte[] IconRgba(PretPewterMachineIcon icon, byte[] png)
    {
        ValidateIcon(icon);
        return PngRgba8.Decode(png, icon.Width, icon.Height);
    }
}

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretPewterMachineInput(string Path, int ByteLength, string GitBlobSha1, string Sha256);
public sealed record PretPewterMachineIcon(ItemId Item, int Width, int Height, string RgbaBase64, string RgbaSha256,
    string PngPath, string PngSha256, OpeningItemSourceRef Source, OpeningItemSourceRef PaletteSource, OpeningItemSourceRef BindingSource);
public sealed record PretPewterMachinesDocument(int SchemaVersion, ImmutableArray<PretPewterMachineInput> Inputs,
    ImmutableArray<OpeningItemSourceRef> RulesSources, ImmutableArray<string> Limitations, PewterMachineCatalog Catalog,
    ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Authenticates the reviewed local TM39 document; never loosens the older inventory profiles.</summary>
public static class PretPewterMachines
{
    public const string GeneratedSha256 = "e39de814e0322ab7d3f0010a8cfe605698746b9c300935e30286997108621052";
    public const string SourceManifestSha256 = "7f75dd2a164400237aa43a7159218c5559c36dd86cbbd7c224141b0440c82bfb";
    public const string LockSha256 = "41b20edbd36253b5ba5b3dea6458926ee5c5aa670a102c43d98ab84bed907637";

    public static PewterMachineCatalog Load(string generatedJson) => LoadDocument(generatedJson).Catalog;

    public static PewterMachineCatalog LoadProject(string projectRoot)
    {
        string Read(string relative) => File.ReadAllText(SafePath(projectRoot, relative), new UTF8Encoding(false, true));
        if (Hash(Read("scripts/pewter-machines.lock.json")) != LockSha256)
            throw new InvalidDataException("Pewter machine source lock differs.");
        var document = LoadDocument(Read("content/generated/pewter-machines/pewter-machines.json"));
        foreach (var input in document.Inputs)
        {
            var data = File.ReadAllBytes(SafePath(projectRoot, "content/raw/pewter-machines/" + input.Path));
            byte[] header = Encoding.ASCII.GetBytes("blob " + data.Length.ToString(CultureInfo.InvariantCulture) + "\0");
            if (data.Length != input.ByteLength || Convert.ToHexStringLower(SHA256.HashData(data)) != input.Sha256 ||
                Convert.ToHexStringLower(SHA1.HashData([.. header, .. data])) != input.GitBlobSha1)
                throw new InvalidDataException("Pewter machine source bytes differ: " + input.Path);
        }
        foreach (var icon in document.Icons)
            if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(SafePath(projectRoot,
                "content/generated/pewter-machines/" + icon.PngPath)))) != icon.PngSha256)
                throw new InvalidDataException("Pewter machine icon PNG differs.");
        return document.Catalog;
    }

    public static PewterMachineCatalog LoadRoute22Project(string projectRoot)
    {
        var old=LoadProject(projectRoot); // Authenticates every original source, including the full TM/HM table.
        const string path="src/data/pokemon/tmhm_learnsets.h";
        var bytes=File.ReadAllBytes(SafePath(projectRoot,"content/raw/pewter-machines/"+path));
        string text=new UTF8Encoding(false,true).GetString(bytes),hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        var extra=ImmutableArray.CreateBuilder<PewterMachineCompatibility>();
        foreach(var (symbol,id) in new[]{("SPEAROW",21),("FEAROW",22),("MANKEY",56)})
        {
            var match=System.Text.RegularExpressions.Regex.Match(text,@"\[SPECIES_"+symbol+@"\]\s*=\s*TMHM_LEARNSET\((.*?)\),",System.Text.RegularExpressions.RegexOptions.Singleline);
            if(!match.Success)throw new InvalidDataException("Route22 machine source missing: "+symbol);
            int line=1+text[..match.Index].Count(c=>c=='\n');
            extra.Add(new(new("pret:SPECIES_"+symbol),id,match.Value.Contains("TMHM(TM39_ROCK_TOMB)",StringComparison.Ordinal),new(path,"SPECIES_"+symbol,line,line+match.Value.Count(c=>c=='\n'),hash)));
        }
        var result=old with{Profile=PewterMachineRules.Route22Profile,Compatibility=old.Compatibility.AddRange(extra)};
        PewterMachineRules.ValidateCatalog(result);return result;
    }

    public static PewterMachineCatalog LoadRoute3Project(string projectRoot)
    {
        var old = LoadRoute22Project(projectRoot);
        const string path = "src/data/pokemon/tmhm_learnsets.h";
        var bytes = File.ReadAllBytes(SafePath(projectRoot, "content/raw/pewter-machines/" + path));
        string text = new UTF8Encoding(false, true).GetString(bytes), hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (old.Compatibility.Any(entry => entry.Source.Path != path || entry.Source.Sha256 != hash))
            throw new InvalidDataException("Route3 TM/HM source differs from the authenticated legacy table.");
        var extra = ImmutableArray.CreateBuilder<PewterMachineCompatibility>();
        foreach (var (symbol, id) in new[] { ("EKANS",23),("NIDORAN_F",29),("NIDORINA",30),("NIDORAN_M",32),
            ("NIDORINO",33),("JIGGLYPUFF",39),("MAGIKARP",129),("GYARADOS",130) })
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\[SPECIES_" + symbol + @"\]\s*=\s*TMHM_LEARNSET\((.*?)\),", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!match.Success) throw new InvalidDataException("Route3 machine source missing: " + symbol);
            int line = 1 + text[..match.Index].Count(c => c == '\n');
            extra.Add(new(new("pret:SPECIES_" + symbol), id, match.Value.Contains("TMHM(TM39_ROCK_TOMB)", StringComparison.Ordinal),
                new(path, "SPECIES_" + symbol, line, line + match.Value.Count(c => c == '\n'), hash)));
        }
        var result = old with { Profile = PewterMachineRules.Route3Profile, Compatibility = old.Compatibility.AddRange(extra) };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }

    public static PewterMachineCatalog LoadMtMoonProject(string projectRoot)
    {
        var old = LoadRoute3Project(projectRoot);
        var items = PretMtMoonItems.LoadProject(projectRoot);
        // Existing forty source rows must remain identical, including provenance spans.
        if (!old.Compatibility.SequenceEqual(items.Tm39Compatibility.Take(40)))
            throw new InvalidDataException("MtMoon changed legacy TM39 compatibility.");
        var result = old with { Profile = PewterMachineRules.MtMoonProfile,
            Compatibility = items.Tm39Compatibility, AdditionalMachines = items.Machines };
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
        if (document.SchemaVersion != 1 || document.Inputs.IsDefault || document.Inputs.Length != 23 ||
            document.Icons.IsDefault || document.Icons.Length != 2 ||
            document.RulesSources.IsDefaultOrEmpty || document.Limitations.IsDefaultOrEmpty || document.Catalog is null)
            throw ContentJson.Error("pewter_machines_profile", "$", "The complete source-authenticated TM39 document is required.");
        var inputs = new Dictionary<string, PretPewterMachineInput>(StringComparer.Ordinal);
        foreach (var input in document.Inputs)
            if (input is null || string.IsNullOrWhiteSpace(input.Path) || input.ByteLength <= 0 ||
                input.Sha256 is null || input.GitBlobSha1 is null || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("pewter_machines_inputs", "$.inputs", "Invalid or duplicate source identity.");
        string manifest = string.Concat(inputs.Values.OrderBy(x => x.Path, StringComparer.Ordinal).Select(x =>
            x.Path + "|" + x.ByteLength.ToString(CultureInfo.InvariantCulture) + "|" + x.GitBlobSha1 + "|" + x.Sha256 + "\n"));
        if (Hash(manifest) != SourceManifestSha256)
            throw ContentJson.Error("pewter_machines_source_hash", "$.inputs", "Source identities differ from the reviewed Git blobs.");
        void Source(OpeningItemSourceRef source)
        {
            if (source is null || source.Path is null || string.IsNullOrWhiteSpace(source.Symbol) ||
                source.StartLine < 1 || source.EndLine < source.StartLine ||
                !inputs.TryGetValue(source.Path, out var input) || input.Sha256 != source.Sha256)
                throw ContentJson.Error("pewter_machines_source_ref", "$", "Invalid source span or identity.");
        }
        try { PewterMachineRules.ValidateCatalog(document.Catalog); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        { throw ContentJson.Error("pewter_machines_catalog", "$.catalog", ex.Message); }
        Source(document.Catalog.Tm39.Item.Source); Source(document.Catalog.Tm39.DescriptionSource);
        Source(document.Catalog.TmCase.Item.Source); Source(document.Catalog.TmCase.DescriptionSource);
        foreach (var compatibility in document.Catalog.Compatibility) Source(compatibility.Source);
        foreach (var source in document.RulesSources) Source(source);
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            if (icon.Item != (n == 0 ? PewterMachineRules.Tm39Id : PewterMachineRules.TmCaseId) ||
                icon.PngPath != (n == 0 ? "item_tm39.png" : "item_tm_case.png"))
                throw new InvalidDataException("Pewter machine icon identity differs.");
            _ = IconRgba(icon); Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
        }
        if (Hash(json) != GeneratedSha256)
            throw ContentJson.Error("pewter_machines_generated_hash", "$", "Generated bytes differ from the reviewed source-derived document.");
        return document;
    }

    public static byte[] IconRgba(PretPewterMachineIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        byte[] bytes;
        try { bytes = Convert.FromBase64String(icon.RgbaBase64); }
        catch (FormatException) { throw new InvalidDataException("Malformed Pewter machine icon RGBA."); }
        if (icon.Width != 24 || icon.Height != 24 || bytes.Length != 2304 ||
            Convert.ToHexStringLower(SHA256.HashData(bytes)) != icon.RgbaSha256)
            throw new InvalidDataException("Pewter machine icon RGBA differs.");
        return bytes;
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Game.Content;
public sealed record PretParcelEventSource(ImmutableDictionary<string,int> Constants,ImmutableDictionary<string,string> Text)
{
    public const string LockSha256="7fb853f5dc081c71fe80acd16396ec7e9b661768583c26344ff0940c414014a9";
    public static PretParcelEventSource Load(string root)
    {
        var bytes=File.ReadAllBytes(Path.Combine(root,"scripts/parcel-events.lock.json"));
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=LockSha256)throw new InvalidDataException("Parcel source lock differs.");
        using var doc=JsonDocument.Parse(bytes);if(doc.RootElement.GetProperty("commit").GetString()!=PretWorldMap.SourceCommit)throw new InvalidDataException("Parcel source commit differs.");
        var constants=ImmutableDictionary.CreateBuilder<string,int>(StringComparer.Ordinal);var sources=new Dictionary<string,string>();
        foreach(var e in doc.RootElement.GetProperty("files").EnumerateArray())
        {
            var path=e.GetProperty("path").GetString()!;if(Path.IsPathRooted(path)||path.Split('/').Contains(".."))throw new InvalidDataException("Parcel source path invalid.");
            var source=File.ReadAllBytes(Path.Combine(root,"content/raw/parcel-events",path));var header=Encoding.ASCII.GetBytes($"blob {source.Length}\0");
            if(source.Length!=e.GetProperty("byteLength").GetInt32()||Convert.ToHexStringLower(SHA256.HashData(source))!=e.GetProperty("sha256").GetString()||Convert.ToHexStringLower(SHA1.HashData([..header,..source]))!=e.GetProperty("gitBlobSha1").GetString())throw new InvalidDataException("Parcel source differs: "+path);
            var text=Encoding.UTF8.GetString(source);sources.Add(path,text);
            if(path.StartsWith("include/constants/",StringComparison.Ordinal))foreach(Match m in Regex.Matches(text,@"^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$",RegexOptions.Multiline|RegexOptions.CultureInvariant))
                constants[m.Groups[1].Value]=m.Groups[2].Value.StartsWith("0x",StringComparison.Ordinal)?Convert.ToInt32(m.Groups[2].Value,16):int.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);
        }
        var bank=ImmutableDictionary.CreateBuilder<string,string>(StringComparer.Ordinal);
        foreach(var symbol in new[]{"gText_ItemsPocket","gText_KeyItemsPocket","gText_PokeBallsPocket"})
        {
            var m=Regex.Match(sources["src/strings.c"],"const u8 "+symbol+"\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);",RegexOptions.CultureInvariant);
            if(!m.Success)throw new InvalidDataException("Parcel pocket text missing: "+symbol);bank.Add(symbol,m.Groups[1].Value);
        }
        return new(constants.ToImmutable(),bank.ToImmutable());
    }
}

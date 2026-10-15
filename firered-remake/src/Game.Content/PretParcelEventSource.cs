using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Game.Content;
public sealed record PretParcelEventSource(ImmutableDictionary<string,int> Constants,ImmutableDictionary<string,string> Text)
{
    public static PretParcelEventSource Load(string root)
    {
        var bytes=File.ReadAllBytes(Path.Combine(root,"scripts/parcel-events.lock.json"));
        using var doc=JsonDocument.Parse(bytes);        var constants=ImmutableDictionary.CreateBuilder<string,int>(StringComparer.Ordinal);var sources=new Dictionary<string,string>();
        foreach(var e in doc.RootElement.GetProperty("files").EnumerateArray())
        {
            var path=e.GetProperty("path").GetString()!;if(Path.IsPathRooted(path)||path.Split('/').Contains(".."))throw new InvalidDataException("Parcel source path invalid.");
            var source=File.ReadAllBytes(Path.Combine(root,PretSourcePaths.FireRedRawDirectory,path));            var text=Encoding.UTF8.GetString(source);sources.Add(path,text);
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

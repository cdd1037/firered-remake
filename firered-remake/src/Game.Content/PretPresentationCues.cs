using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Game.Content;
/// <summary>Source fanfare task counters only. This does not implement or verify audible playback.</summary>
public sealed record PretPresentationCues(ImmutableSortedDictionary<string,int> FanfareCounters)
{
    public const string SourceSha256="851313931ebdbf2f0933b52295707f82e948735813fac84fdba74c0446ada958";
    public static PretPresentationCues Load(string root)
    {
        var bytes=File.ReadAllBytes(Path.Combine(root,"content/raw/presentation-cues/src/sound.c"));
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=SourceSha256)throw new InvalidDataException("Fanfare timing source checksum mismatch.");
        var table=Regex.Match(Encoding.UTF8.GetString(bytes),@"static const struct Fanfare sFanfares\[\] = \{(.*?)\n\};",RegexOptions.Singleline|RegexOptions.CultureInvariant);
        if(!table.Success)throw new InvalidDataException("Fanfare source table missing.");
        var counters=ImmutableSortedDictionary.CreateBuilder<string,int>(StringComparer.Ordinal);
        foreach(Match m in Regex.Matches(table.Groups[1].Value,@"\[FANFARE_[A-Z_]+\]\s*=\s*\{\s*(MUS_[A-Z_]+),\s*([0-9]+)\s*\}",RegexOptions.CultureInvariant))
            counters.Add(m.Groups[1].Value,int.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture));
        if(counters.Count!=14||counters.Values.Any(n=>n is <1 or >1000))throw new InvalidDataException("Incomplete fanfare counters.");
        return new(counters.ToImmutable());
    }
    // PlayFanfare's original fallback is the first table entry, MUS_LEVEL_UP, not a silent zero wait.
    public int CounterFor(string cue)=>FanfareCounters.GetValueOrDefault(cue,FanfareCounters["MUS_LEVEL_UP"]);
}

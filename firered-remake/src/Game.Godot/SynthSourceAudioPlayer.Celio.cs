using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private sealed record CelioSynthManifest(int SchemaVersion, string SourceCommit, SynthCue[] Cues, SynthCue[] InheritedCues);

    private static SynthCue[] ExtendCelioCues(string root, bool includeFiveIsland, SynthCue[] cues, JsonSerializerOptions options, string sourceCommit)
    {
        if (!includeFiveIsland) throw new InvalidDataException("Celio audio requires Five Island.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Path.GetFullPath(root),CelioPresentationAssets.Base+"synth-manifest.json"));
        if (bytes.Length != CelioPresentationAssets.SynthBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != CelioPresentationAssets.SynthSha256)
            throw new InvalidDataException("Celio synth identity differs.");
        var extension = JsonSerializer.Deserialize<CelioSynthManifest>(bytes,options) ?? throw new InvalidDataException("Celio synth missing.");
        if (extension.SchemaVersion != 1 || extension.SourceCommit != sourceCommit || extension.Cues.Length != CelioPresentationAssets.SynthCueCount ||
            !extension.Cues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["CRY_RAIKOU_NORMAL","CRY_ENTEI_NORMAL","CRY_SUICUNE_NORMAL"]) ||
            extension.Cues.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Celio source cue scope differs.");
        if (extension.InheritedCues is not {Length:4} ||
            !extension.InheritedCues.Select(c => c.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["MUS_OBTAIN_TMHM","SE_CLICK","SE_PC_ON","MUS_NET_CENTER"]) ||
            extension.InheritedCues.Any(c => cues.SingleOrDefault(old => old.Id == c.Id) != c))
            throw new InvalidDataException("Celio inherited cues differ.");
        // Handoff cues retain the existing instances and sequence-driven playback path.
        return [..cues,..extension.Cues];
    }
}

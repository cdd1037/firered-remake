using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    /// <summary>Admit the original menu BGM and verify the inherited show/text cues.</summary>
    private static SynthCue[] ExtendTeachyTvCues(string root, SynthCue[] cues,
        JsonSerializerOptions options)
    {
        const string source = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
        string path = Path.Combine(Path.GetFullPath(root),
            "content/generated/teachy-tv-audio/synth-manifest.json");
        byte[] bytes = File.ReadAllBytes(path);
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Teachy TV synth manifest missing.");
        SynthCue[] expected =
        [
            new("MUS_TEACHY_TV_MENU", source, "MUS_TEACHY_TV_MENU",
                "sound/songs/midi/mus_teachy_tv_menu.mid", 0, 0, null, 0x086FBE8C, 0, true)
        ];
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.SequenceEqual(expected) || expected.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Teachy TV music binding differs.");
        SynthCue[] inherited =
        [
            new("MUS_FOLLOW_ME", source, "MUS_FOLLOW_ME",
                "sound/songs/midi/mus_follow_me.mid", 0, 0, null, 0x086C07E4, 0, true),
            new("SE_SELECT", source, "SE_SELECT",
                "sound/songs/midi/se_select.mid", 2, 5, null, 0x086B5720, 0, false)
        ];
        if (inherited.Any(c => !cues.Where(old => old.Id == c.Id).SequenceEqual(new[] { c })))
            throw new InvalidDataException("Inherited Teachy TV sound bindings differ.");
        // The constructor validates the song header in the source pack.
        // Application state owns music changes. This only registers resources and never
        // starts/restarts BGM; menu, show, battle and field restoration keep one owner.
        return [.. cues, .. extension.Cues];
    }
}

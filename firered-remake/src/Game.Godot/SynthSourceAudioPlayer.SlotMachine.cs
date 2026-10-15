using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendSlotMachineCues(string root, SynthCue[] cues,
        JsonSerializerOptions options)
    {
        string path = Path.Combine(Path.GetFullPath(root),
            "content/generated/ui/slot-machine-presentation/synth-manifest.json");
        byte[] bytes = File.ReadAllBytes(path);
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Slot machine synth manifest missing.");
        SynthCue[] expected =
        [
            new("SE_RS_SHOP", "SE_RS_SHOP", 1, 5, null, 0x086B6C7C, 0, false),
            new("SE_CONTEST_PLACE", "SE_CONTEST_PLACE", 2, 4, null, 0x086B5B84, 0, false),
            new("MUS_SLOTS_WIN", "MUS_SLOTS_WIN", 2, 5, 150, 0x086C0120, 0, false),
            new("MUS_SLOTS_JACKPOT", "MUS_SLOTS_JACKPOT", 2, 5, 250, 0x086BFFE0, 0, false)
        ];
        if (extension.SchemaVersion != 1 ||
            !extension.Cues.SequenceEqual(expected) || expected.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Slot machine sound bindings differ.");
        SynthCue[] inherited =
        [
            new("SE_PIN", "SE_PIN", 1, 4, null, 0x086B5AB8, 0, false),
            new("SE_WIN_OPEN", "SE_WIN_OPEN", 1, 5, null, 0x086B573C, 0, false)
        ];
        if (inherited.Any(c => !cues.Where(old => old.Id == c.Id).SequenceEqual(new[] { c })))
            throw new InvalidDataException("Inherited slot machine sound bindings differ.");
        // The constructor checks each song header in the source pack.
        // PlaySoundEffect preserves SE1/SE2 routing; PlayFanfare owns the original
        // 150/250 counters, their separate zero-counter frame, and BGM restoration.
        return [.. cues, .. extension.Cues];
    }
}

using System.Security.Cryptography;
using System.Text.Json;

public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendSlotMachineCues(string root, SynthCue[] cues,
        JsonSerializerOptions options, string sourceCommit)
    {
        const string source = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
        const int length = 1157;
        const string sha256 = "9bdea1f521f5d02e9ca20f40d65cbf1a1e099da3e3c7e3aceb36283964ffcecb";
        if (sourceCommit != source)
            throw new InvalidDataException("Slot machine audio source revision differs.");
        string path = Path.Combine(Path.GetFullPath(root),
            "content/generated/slot-machine-presentation/synth-manifest.json");
        using var stream = File.OpenRead(path);
        if (stream.Length != length)
            throw new InvalidDataException("Slot machine synth manifest length differs.");
        byte[] bytes = new byte[length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || stream.Length != bytes.Length ||
            Convert.ToHexStringLower(SHA256.HashData(bytes)) != sha256)
            throw new InvalidDataException("Slot machine synth manifest identity differs.");
        var extension = JsonSerializer.Deserialize<SynthManifest>(bytes, options)
            ?? throw new InvalidDataException("Slot machine synth manifest missing.");
        SynthCue[] expected =
        [
            new("SE_RS_SHOP", source, "SE_RS_SHOP", "sound/songs/midi/se_rs_shop.mid", 1, 5, null, 0x086B6C7C, 0, false),
            new("SE_CONTEST_PLACE", source, "SE_CONTEST_PLACE", "sound/songs/midi/se_contest_place.mid", 2, 4, null, 0x086B5B84, 0, false),
            new("MUS_SLOTS_WIN", source, "MUS_SLOTS_WIN", "sound/songs/midi/mus_slots_win.mid", 2, 5, 150, 0x086C0120, 0, false),
            new("MUS_SLOTS_JACKPOT", source, "MUS_SLOTS_JACKPOT", "sound/songs/midi/mus_slots_jackpot.mid", 2, 5, 250, 0x086BFFE0, 0, false)
        ];
        if (extension.SchemaVersion != 1 || extension.SourceCommit != source ||
            !extension.Cues.SequenceEqual(expected) || expected.Any(c => cues.Any(old => old.Id == c.Id)))
            throw new InvalidDataException("Slot machine sound bindings differ.");
        SynthCue[] inherited =
        [
            new("SE_PIN", source, "SE_PIN", "sound/songs/midi/se_pin.mid", 1, 4, null, 0x086B5AB8, 0, false),
            new("SE_WIN_OPEN", source, "SE_WIN_OPEN", "sound/songs/midi/se_win_open.mid", 1, 5, null, 0x086B573C, 0, false)
        ];
        if (inherited.Any(c => !cues.Where(old => old.Id == c.Id).SequenceEqual(new[] { c })))
            throw new InvalidDataException("Inherited slot machine sound bindings differ.");
        // The constructor authenticates the unchanged pack and checks each song header.
        // PlaySoundEffect preserves SE1/SE2 routing; PlayFanfare owns the original
        // 150/250 counters, their separate zero-counter frame, and BGM restoration.
        return [.. cues, .. extension.Cues];
    }
}

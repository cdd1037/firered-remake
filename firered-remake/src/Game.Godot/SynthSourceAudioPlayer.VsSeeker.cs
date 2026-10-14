public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendVsSeekerCues(SynthCue[] cues, string sourceCommit)
    {
        const string source = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
        if (sourceCommit != source) throw new InvalidDataException("Vs Seeker audio source revision differs.");
        var pin = new SynthCue("SE_PIN", source, "SE_PIN", "sound/songs/midi/se_pin.mid",
            1, 4, null, 0x086B5AB8, 0, false);
        if (!cues.Where(c => c.Id == pin.Id).SequenceEqual(new[] { pin }))
            throw new InvalidDataException("Inherited Vs Seeker response sound differs.");
        // Original source sound 92; unchanged authenticated pack at song-table entry 0x084A35AC.
        var signal = new SynthCue("SE_CONTEST_MONS_TURN", source, "SE_CONTEST_MONS_TURN",
            "sound/songs/midi/se_contest_mons_turn.mid", 1, 5, null, 0x086B6DF4, 0, false);
        if (cues.Any(c => c.Id == signal.Id)) throw new InvalidDataException("Duplicated Vs Seeker signal sound.");
        return [.. cues, signal];
    }
}

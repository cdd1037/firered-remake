public partial class SynthSourceAudioPlayer
{
    private static SynthCue[] ExtendVsSeekerCues(SynthCue[] cues)
    {
        var pin = new SynthCue("SE_PIN", "SE_PIN",
            1, 4, null, 0x086B5AB8, 0, false);
        if (!cues.Where(c => c.Id == pin.Id).SequenceEqual(new[] { pin }))
            throw new InvalidDataException("Inherited Vs Seeker response sound differs.");
        // Original source sound 92; unchanged authenticated pack at song-table entry 0x084A35AC.
        var signal = new SynthCue("SE_CONTEST_MONS_TURN", "SE_CONTEST_MONS_TURN", 1, 5, null, 0x086B6DF4, 0, false);
        if (cues.Any(c => c.Id == signal.Id)) throw new InvalidDataException("Duplicated Vs Seeker signal sound.");
        return [.. cues, signal];
    }
}

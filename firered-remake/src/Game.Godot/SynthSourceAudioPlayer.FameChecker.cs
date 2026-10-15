public partial class SynthSourceAudioPlayer
{
    /// <summary>Verify the three inherited Fame Checker effects and admit its one missing source effect.</summary>
    private static SynthCue[] ExtendFameCheckerCues(SynthCue[] cues)
    {
        const string source = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
        SynthCue[] inherited =
        [
            new("SE_SELECT", source, "SE_SELECT", "sound/songs/midi/se_select.mid", 2, 5, null, 0x086B5720, 0, false),
            new("SE_M_SWAGGER2", source, "SE_M_SWAGGER2", "sound/songs/midi/se_m_swagger2.mid", 1, 4, null, 0x086B9C28, 0, false),
            new("SE_M_SWIFT", source, "SE_M_SWIFT", "sound/songs/midi/se_m_swift.mid", 1, 4, null, 0x086BA41C, 0, false)
        ];
        if (inherited.Any(c => !cues.Where(old => old.Id == c.Id).SequenceEqual(new[] { c })))
            throw new InvalidDataException("Inherited Fame Checker sound bindings differ.");

        // Source song 203, SE1, priority 4. The source song table and the unchanged
        // authenticated pack agree on header 0x086BA80C and track 0x086BA7C8.
        // tests/Game.FameCheckerAudio.Tests derives this binding independently.
        var addition = new SynthCue("SE_M_LOCK_ON", source, "SE_M_LOCK_ON",
            "sound/songs/midi/se_m_lock_on.mid", 1, 4, null, 0x086BA80C, 0, false);
        if (cues.Any(c => c.Id == addition.Id))
            throw new InvalidDataException("Duplicated Fame Checker sound binding.");
        // The constructor validates every song header in the source pack.
        // Existing PlaySoundEffect routing owns priority and SE1/SE2 replacement.
        return [.. cues, addition];
    }
}

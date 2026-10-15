using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class RhythmAndTiedNoteTests : SequenceFixture
{

    [TestMethod]
    public void rhythm36_selects_original_kick_sounding_root64_plus_transpose12_MIDI_key36()
    {
        var drums = new Synth(real)
        {
            EnableTrace = false
        };
        var drumPlayer = new Player(real, drums, Song.Read(real, 0x086e8810), 0);
        var drumTrack = new Track(drumPlayer.Song.Tracks[0], 0)
        {
            Program = 0,
            Key = 36,
            KeyShift = 12
        };
        drums.Note(drumPlayer, drumTrack, 48, 100, -1);
        var kick = drums.Voices[0];
        Assert.IsTrue(kick.MidiKey == 36 && kick.Key == 76 && kick.RhythmPan == 0 && kick.Instrument!.Type == 8 && kick.Instrument.Data == real.U32(0x0848a284), "rhythm36 selects original kick, sounding root64 plus transpose12, MIDI key36");
    }

    [TestMethod]
    public void EOT_does_not_match_a_drum_s_sounding_pitch()
    {
        var drums = new Synth(real)
        {
            EnableTrace = false
        };
        var drumPlayer = new Player(real, drums, Song.Read(real, 0x086e8810), 0);
        var drumTrack = new Track(drumPlayer.Song.Tracks[0], 0)
        {
            Program = 0,
            Key = 36,
            KeyShift = 12
        };
        drums.Note(drumPlayer, drumTrack, 48, 100, -1);
        var kick = drums.Voices[0];
        drums.EndTie(drumPlayer, drumTrack, 76);
        Assert.IsTrue(!kick.Releasing, "EOT does not match a drum's sounding pitch");
    }

    [TestMethod]
    public void EOT_matches_original_drum_MIDI_key_despite_later_transpose_changes()
    {
        var drums = new Synth(real)
        {
            EnableTrace = false
        };
        var drumPlayer = new Player(real, drums, Song.Read(real, 0x086e8810), 0);
        var drumTrack = new Track(drumPlayer.Song.Tracks[0], 0)
        {
            Program = 0,
            Key = 36,
            KeyShift = 12
        };
        drums.Note(drumPlayer, drumTrack, 48, 100, -1);
        var kick = drums.Voices[0];
        drums.EndTie(drumPlayer, drumTrack, 76);
        drumTrack.KeyShift = -12;
        drums.EndTie(drumPlayer, drumTrack, 36);
        Assert.IsTrue(kick.Releasing, "EOT matches original drum MIDI key despite later transpose changes");
    }

    [TestMethod]
    public void rhythm42_applies_source_child_root_and_positive_pan()
    {
        var drums = new Synth(real)
        {
            EnableTrace = false
        };
        var drumPlayer = new Player(real, drums, Song.Read(real, 0x086e8810), 0);
        var drumTrack = new Track(drumPlayer.Song.Tracks[0], 0)
        {
            Program = 0,
            Key = 36,
            KeyShift = 12
        };
        drums.Note(drumPlayer, drumTrack, 48, 100, -1);
        drums.EndTie(drumPlayer, drumTrack, 76);
        drumTrack.KeyShift = -12;
        drums.EndTie(drumPlayer, drumTrack, 36);
        drumTrack.Key = 42;
        drums.Note(drumPlayer, drumTrack, 30, 100, -1);
        var snare = drums.Voices[1];
        Assert.IsTrue(snare.MidiKey == 42 && snare.Key == 52 && snare.RhythmPan == 32 && snare.Instrument!.Data == real.U32(0x0848a2cc), "rhythm42 applies source child root and positive pan");
    }

    [TestMethod]
    public void rhythm57_sign_extends_child_pan_to_minus70()
    {
        var drums = new Synth(real)
        {
            EnableTrace = false
        };
        var drumPlayer = new Player(real, drums, Song.Read(real, 0x086e8810), 0);
        var drumTrack = new Track(drumPlayer.Song.Tracks[0], 0)
        {
            Program = 0,
            Key = 36,
            KeyShift = 12
        };
        drums.Note(drumPlayer, drumTrack, 48, 100, -1);
        drums.EndTie(drumPlayer, drumTrack, 76);
        drumTrack.KeyShift = -12;
        drums.EndTie(drumPlayer, drumTrack, 36);
        drumTrack.Key = 42;
        drums.Note(drumPlayer, drumTrack, 30, 100, -1);
        drumTrack.Key = 57;
        drums.Note(drumPlayer, drumTrack, 45, 100, -1);
        Assert.IsTrue(drums.Voices[2].RhythmPan == -70, "rhythm57 sign-extends child pan to minus70");
    }

    [TestMethod]
    public void rhythm_pan_combines_with_track_gain_using_original_integer_factors()
    {
        var panTrack = new Track(Resources.Base, 0)
        {
            Volume = 120
        };
        Assert.IsTrue(Synth.ChannelVolume(panTrack, 127, 32) == (87, 148), "rhythm pan combines with track gain using original integer factors");
    }

    [TestMethod]
    public void sequence_rhythm_TIE_retains_distinct_MIDI_and_sounding_keys()
    {
        var tieResources = Fixture(new byte[] { 0xbc, 12, 0xcf, 36, 127, 0x81, 0xce, 36, 0x81, 0xb1 }, 0x80);
        var tieSynth = new Synth(tieResources)
        {
            EnableTrace = false
        };
        var tiePlayer=new Player(tieResources,tieSynth,Song.Read(tieResources,Resources.Base),0);
        tiePlayer.Frame();
        Assert.IsTrue(tieSynth.Voices[0].MidiKey == 36 && tieSynth.Voices[0].Key == 76 && !tieSynth.Voices[0].Releasing, "sequence rhythm TIE retains distinct MIDI and sounding keys");
    }

    [TestMethod]
    public void sequence_EOT36_releases_transposed_rhythm_TIE()
    {
        var tieResources = Fixture(new byte[] { 0xbc, 12, 0xcf, 36, 127, 0x81, 0xce, 36, 0x81, 0xb1 }, 0x80);
        var tieSynth = new Synth(tieResources)
        {
            EnableTrace = false
        };
        var tiePlayer=new Player(tieResources,tieSynth,Song.Read(tieResources,Resources.Base),0);
        tiePlayer.Frame();tiePlayer.Frame();
        Assert.IsTrue(tieSynth.Voices[0].Releasing, "sequence EOT36 releases transposed rhythm TIE");
    }

    [TestMethod]
    public void explicit_EOT61_updates_the_key_inherited_by_following_omitted_key_TIE()
    {
        var rememberedResources = Fixture(new byte[] { 0xcf, 60, 127, 0x81, 0xce, 61, 0x81, 0xcf, 0x81, 0xb1 });
        var rememberedSynth = new Synth(rememberedResources)
        {
            EnableTrace = false
        };
        var rememberedPlayer = new Player(rememberedResources, rememberedSynth, Song.Read(rememberedResources, Resources.Base), 0);
        for (int frame = 0; frame < 3; frame++)
            rememberedPlayer.Frame();
        Assert.IsTrue(rememberedPlayer.Tracks[0].Key == 61 && rememberedSynth.Voices[1].MidiKey == 61, "explicit EOT61 updates the key inherited by following omitted-key TIE");
    }

    [TestMethod]
    public void EOT_releases_only_newest_eligible_same_key_note()
    {
        var duplicateResources = Fixture(new byte[] { 0xcf, 60, 127, 0xcf, 60, 127, 0x81, 0xce, 60, 0x81, 0xb1 });
        var duplicateSynth = new Synth(duplicateResources)
        {
            EnableTrace = false
        };
        var duplicatePlayer=new Player(duplicateResources,duplicateSynth,Song.Read(duplicateResources,Resources.Base),0);
        duplicatePlayer.Frame();duplicatePlayer.Frame();
        Assert.IsTrue(duplicateSynth.Voices[1].Releasing && !duplicateSynth.Voices[0].Releasing, "EOT releases only newest eligible same-key note");
    }

    [TestMethod]
    public void second_EOT_skips_releasing_newest_note_and_releases_next_eligible_note()
    {
        var duplicateResources = Fixture(new byte[] { 0xcf, 60, 127, 0xcf, 60, 127, 0x81, 0xce, 60, 0x81, 0xb1 });
        var duplicateSynth = new Synth(duplicateResources)
        {
            EnableTrace = false
        };
        var duplicatePlayer = new Player(duplicateResources, duplicateSynth, Song.Read(duplicateResources, Resources.Base), 0);
        duplicatePlayer.Frame();
        duplicatePlayer.Frame();
        duplicateSynth.EndTie(duplicatePlayer, duplicatePlayer.Tracks[0], 60);
        Assert.IsTrue(duplicateSynth.Voices[0].Releasing, "second EOT skips releasing newest note and releases next eligible note");
    }

    [TestMethod]
    public void keysplit_only_tone_retains_sequence_pitch_and_ignores_child_rhythm_pan()
    {
        var splitResources = Fixture(new byte[] { 0xbc, 12, 0xcf, 36, 127, 0x81, 0xb1 }, 0x40);
        var splitSynth = new Synth(splitResources)
        {
            EnableTrace = false
        };
        new Player(splitResources, splitSynth, Song.Read(splitResources, Resources.Base), 0).Frame();
        Assert.IsTrue(splitSynth.Voices[0].Key == 48 && splitSynth.Voices[0].RhythmPan == 0, "keysplit-only tone retains sequence pitch and ignores child rhythm pan");
    }

    [TestMethod]
    public void PSG_wave_noise_flags0x_remain_explicitly_unsupported()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        foreach (int type in new[]
        {
            0x13,
            0x14,
            0x1b,
            0x1c
        }

        )
        {
            var unsupported = Fixture(new byte[] { 0xcf, 60, 127, 0x81, 0xb1 }, leafType: type);
            Reject(() => new Player(unsupported, new Synth(unsupported), Song.Read(unsupported, Resources.Base), 0).Frame(), $"PSG wave/noise flags0x{type:X2} remain explicitly unsupported");
        }
    }

    [TestMethod]
    public void nested_rhythm_child_remains_an_explicit_failure()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var nested = Fixture(new byte[] { 0xcf, 36, 127, 0x81, 0xb1 }, 0x80, 0x40);
        Reject(() => new Player(nested, new Synth(nested), Song.Read(nested, Resources.Base), 0).Frame(), "nested rhythm child remains an explicit failure");
    }

    [TestMethod]
    public void rhythm_child_address_is_bounds_checked()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var escaped = Fixture(new byte[] { 0xcf, 36, 127, 0x81, 0xb1 }, 0x80, invalidChildAddress: true);
        Reject(() => new Player(escaped, new Synth(escaped), Song.Read(escaped, Resources.Base), 0).Frame(), "rhythm child address is bounds checked");
    }
}

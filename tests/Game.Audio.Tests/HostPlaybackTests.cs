using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class HostPlaybackTests
{
    private FireRedAudio.Resources real = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    static (long Tick, uint Cursor, int Wait, long TrackTick, int LfoPhase) State(Player player)
    {
        var track = player.Tracks[0];
        return (player.Tick, track.Cursor, track.Wait, track.Tick, track.LfoPhase);
    }

    static Resources Fixture()
    {
        var bytes = new byte[Resources.End - Resources.Base];
        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void Header(int offset, params int[] tracks)
        {
            bytes[offset] = (byte)tracks.Length;
            Word(offset + 4, Resources.Base + 2048);
            for (int n = 0; n < tracks.Length; n++)
                Word(offset + 8 + n * 4, Resources.Base + (uint)tracks[n]);
        }

        Header(0, 1024);
        Header(64, 1152);
        Header(128, 1280);
        Header(192, 1024, 1280);
        void Loop(int offset, byte program)
        {
            byte[] sequence =
            {
                0xbb,
                101,
                0xbd,
                program,
                0xc4,
                6,
                0xcf,
                60,
                127,
                0x84,
                0xce,
                60,
                0xcf,
                64,
                127,
                0x84,
                0xce,
                64,
                0xb2,
                0,
                0,
                0,
                0
            };
            sequence.CopyTo(bytes, offset);
            Word(offset + 19, Resources.Base + (uint)offset + 6);
        }

        Loop(1024, 0);
        Loop(1280, 1);
        new byte[]
        {
            0xcf,
            60,
            127,
            0x81,
            0xb1
        }.CopyTo(bytes, 1152);
        bytes[2048 + 1] = 60;
        Word(2048 + 4, Resources.Base + 4096);
        bytes[2048 + 8] = bytes[2048 + 9] = bytes[2048 + 10] = 255;
        bytes[2048 + 11] = 224;
        new byte[]
        {
            1,
            60,
            0,
            8,
            2,
            0,
            0,
            0,
            0,
            0,
            15,
            4
        }.CopyTo(bytes, 2060);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4098), 0x4000);
        Word(4100, 16384 * 1024);
        Word(4108, 64);
        for (int n = 0; n < 64; n++)
            bytes[4112 + n] = unchecked((byte)(sbyte)(n < 32 ? 80 : -80));
        for (int n = 0; n < 12; n++)
            Word((int)(0x08489abc - Resources.Base) + 4 * n, 0x80000000);
        return new Resources(bytes);
    }

    [TestMethod]
    public void five_host_slots_begin_absent_idle_unpaused_zero_position_and_unity_gain()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var shared = Engine();
        Assert.IsTrue(Enumerable.Range(0, 5).All(slot => !shared.HasPlayer(slot) && !shared.PlayerPlaying(slot) && !shared.PlayerPaused(slot) && shared.PlayerElapsedFrames(slot) == 0 && shared.GetPlayerGain(slot) == 1), "five host slots begin absent, idle, unpaused, zero-position and unity-gain");
    }

    [TestMethod]
    public void BGM_and_three_independent_SE_players_concurrently_share_the_five_PCM_slots()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var shared = Engine();
        foreach (int slot in new[]
        {
            0,
            1,
            3,
            4
        }

        )
            shared.Start(Resources.Base, slot);
        shared.Render(new float[200]);
        Assert.IsTrue(new[] { 0, 1, 3, 4 }.All(slot => shared.HasPlayer(slot) && shared.PlayerPlaying(slot) && shared.PlayerElapsedFrames(slot) == 100) && shared.Voices.Count(v => v.Active) == 4 && shared.Voices.Take(5).Count(v => v.Active) == 4, "BGM and three independent SE players concurrently share the five PCM slots");
    }

    [TestMethod]
    public void added_source_players_do_not_create_extra_PCM_or_PSG_channels_or_occupy_cry_slot()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var shared = Engine();
        foreach (int slot in new[]
        {
            0,
            1,
            3,
            4
        }

        )
            shared.Start(Resources.Base, slot);
        shared.Render(new float[200]);
        Assert.IsTrue(shared.Voices.Length == 9 && shared.MaxPcm == 4 && !shared.HasPlayer(2), "added source players do not create extra PCM or PSG channels or occupy cry slot");
    }

    [TestMethod]
    public void stopping_SE2_leaves_BGM_SE1_and_SE3_voices_and_sample_positions_untouched()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var shared = Engine();
        foreach (int slot in new[]
        {
            0,
            1,
            3,
            4
        }

        )
            shared.Start(Resources.Base, slot);
        shared.Render(new float[200]);
        var intact = shared.Voices.Where(v => v.Active && v.Player!.Id != 3).Select(v => (v.Player, v.Age, v.Position)).ToArray();
        shared.Stop(3);
        Assert.IsTrue(!shared.HasPlayer(3) && !shared.PlayerPlaying(3) && shared.PlayerElapsedFrames(3) == 0 && intact.SequenceEqual(shared.Voices.Where(v => v.Active).Select(v => (v.Player, v.Age, v.Position))), "stopping SE2 leaves BGM, SE1 and SE3 voices and sample positions untouched");
    }

    [TestMethod]
    public void restarting_one_SE_resets_only_that_player_s_elapsed_position()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var shared = Engine();
        foreach (int slot in new[]
        {
            0,
            1,
            3,
            4
        }

        )
            shared.Start(Resources.Base, slot);
        shared.Render(new float[200]);
        shared.Stop(3);
        shared.Start(Resources.Base, 3);
        Assert.IsTrue(shared.PlayerElapsedFrames(3) == 0 && shared.PlayerElapsedFrames(1) == 100, "restarting one SE resets only that player's elapsed position");
    }

    [TestMethod]
    public void equal_priority_shared_square_channel_honors_original_BGM_SE1_SE2_SE3_address_order()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var priority = Engine();
        foreach (int slot in new[]
        {
            4,
            3,
            1,
            0
        }

        )
            priority.Start(Resources.Base + 128, slot);
        priority.Frame();
        Assert.IsTrue(priority.Voices[5].Player!.Id == 0 && priority.Rejected == 3, "equal-priority shared square channel honors original BGM, SE1, SE2, SE3 address order");
    }

    [TestMethod]
    public void SE2_precedes_SE3_for_an_equal_priority_PSG_allocation()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var priority = Engine();
        foreach (int slot in new[]
        {
            4,
            3,
            1,
            0
        }

        )
            priority.Start(Resources.Base + 128, slot);
        priority.Frame();
        priority.Stop(0);
        priority.Stop(1);
        priority.Start(Resources.Base + 128, 4);
        priority.Start(Resources.Base + 128, 3);
        priority.Frame();
        Assert.IsTrue(priority.Voices[5].Player!.Id == 3, "SE2 precedes SE3 for an equal-priority PSG allocation");
    }

    [TestMethod]
    public void original_style_pause_retains_BGM_player_but_immediately_silences_its_linked_voices()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var paused = Engine();
        var reference = Engine();
        paused.Start(Resources.Base, 0);
        reference.Start(Resources.Base, 0);
        paused.Start(Resources.Base, 1);
        for (int n = 0; n < 2; n++)
        {
            paused.Frame();
            reference.Frame();
        }

        paused.PausePlayer(0);
        Assert.IsTrue(paused.HasPlayer(0) && paused.PlayerPaused(0) && !paused.PlayerPlaying(0) && paused.Voices.All(v => !v.Active || v.Player!.Id != 0), "original-style pause retains BGM player but immediately silences its linked voices");
    }

    [TestMethod]
    public void pause_preserves_BGM_cursor_waits_ticks_and_LFO_while_SE_and_shared_sound_clock_continue()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var paused = Engine();
        var reference = Engine();
        paused.Start(Resources.Base, 0);
        reference.Start(Resources.Base, 0);
        paused.Start(Resources.Base, 1);
        for (int n = 0; n < 2; n++)
        {
            paused.Frame();
            reference.Frame();
        }

        var bgm = paused.Voices.First(v => v.Active && v.Player!.Id == 0).Player!;
        var se = paused.Voices.First(v => v.Active && v.Player!.Id == 1).Player!;
        var cursor = State(bgm);
        long seTick = se.Tick;
        paused.PausePlayer(0);
        for (int n = 0; n < 20; n++)
            paused.Frame();
        Assert.IsTrue(State(bgm) == cursor && se.Tick > seTick && paused.GameFrames == 22, "pause preserves BGM cursor, waits, ticks and LFO while SE and shared sound clock continue");
    }

    [TestMethod]
    public void resume_clears_pause_without_resurrecting_stopped_waveform_voices()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var paused = Engine();
        var reference = Engine();
        paused.Start(Resources.Base, 0);
        reference.Start(Resources.Base, 0);
        paused.Start(Resources.Base, 1);
        for (int n = 0; n < 2; n++)
        {
            paused.Frame();
            reference.Frame();
        }

        paused.PausePlayer(0);
        for (int n = 0; n < 20; n++)
            paused.Frame();
        paused.ResumePlayer(0);
        Assert.IsTrue(!paused.PlayerPaused(0) && paused.PlayerPlaying(0) && paused.Voices.All(v => !v.Active || v.Player!.Id != 0), "resume clears pause without resurrecting stopped waveform voices");
    }

    [TestMethod]
    public void resume_retains_fractional_tempo_remainder_and_emits_subsequent_notes_at_the_original_cursor()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var paused = Engine();
        var reference = Engine();
        paused.Start(Resources.Base, 0);
        reference.Start(Resources.Base, 0);
        paused.Start(Resources.Base, 1);
        for (int n = 0; n < 2; n++)
        {
            paused.Frame();
            reference.Frame();
        }

        var bgm = paused.Voices.First(v => v.Active && v.Player!.Id == 0).Player!;
        var expected = reference.Voices.First(v => v.Active).Player!;
        paused.PausePlayer(0);
        for (int n = 0; n < 20; n++)
            paused.Frame();
        paused.ResumePlayer(0);
        bool sequenceMatches = true;
        for (int n = 0; n < 100; n++)
        {
            paused.Frame();
            reference.Frame();
            sequenceMatches &= State(bgm) == State(expected);
        }

        Assert.IsTrue(sequenceMatches && paused.Voices.Any(v => v.Active && v.Player!.Id == 0), "resume retains fractional tempo remainder and emits subsequent notes at the original cursor");
    }

    [TestMethod]
    public void elapsed_48_kHz_frame_positions_freeze_independently_during_repeated_pause()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var position = Engine();
        position.Start(Resources.Base, 0);
        position.Start(Resources.Base, 4);
        position.Render(new float[514]);
        position.PausePlayer(0);
        position.PausePlayer(0);
        position.Render(new float[2000]);
        Assert.IsTrue(position.PlayerElapsedFrames(0) == 257 && position.PlayerElapsedFrames(4) == 1257, "elapsed 48 kHz frame positions freeze independently during repeated pause");
    }

    [TestMethod]
    public void repeated_resume_preserves_elapsed_position_and_advances_only_subsequently_rendered_frames()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var position = Engine();
        position.Start(Resources.Base, 0);
        position.Start(Resources.Base, 4);
        position.Render(new float[514]);
        position.PausePlayer(0);
        position.PausePlayer(0);
        position.Render(new float[2000]);
        position.ResumePlayer(0);
        position.ResumePlayer(0);
        position.Render(new float[86]);
        Assert.IsTrue(position.PlayerElapsedFrames(0) == 300 && position.PlayerElapsedFrames(4) == 1300, "repeated resume preserves elapsed position and advances only subsequently rendered frames");
    }

    [TestMethod]
    public void Start_replaces_a_paused_player_and_resets_its_pause_and_position()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var position = Engine();
        position.Start(Resources.Base, 0);
        position.Start(Resources.Base, 4);
        position.Render(new float[514]);
        position.PausePlayer(0);
        position.PausePlayer(0);
        position.Render(new float[2000]);
        position.ResumePlayer(0);
        position.ResumePlayer(0);
        position.Render(new float[86]);
        position.PausePlayer(0);
        position.Start(Resources.Base, 0);
        Assert.IsTrue(!position.PlayerPaused(0) && position.PlayerElapsedFrames(0) == 0, "Start replaces a paused player and resets its pause and position");
    }

    [TestMethod]
    public void player_status_remains_playing_for_an_owned_release_tail_after_sequence_FINE()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        var release = tail.Voices.First(v => v.Active);
        Assert.IsTrue(release.Detached && release.Releasing && release.Player!.Tracks.All(t => t.Ended) && tail.PlayerPlaying(1), "player status remains playing for an owned release tail after sequence FINE");
    }

    [TestMethod]
    public void TrackStop_style_pause_leaves_already_detached_FINE_tails_active()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        var release = tail.Voices.First(v => v.Active);
        tail.PausePlayer(1);
        Assert.IsTrue(release.Active && tail.PlayerPlaying(1) && tail.PlayerPaused(1), "TrackStop-style pause leaves already-detached FINE tails active");
    }

    [TestMethod]
    public void detached_release_tails_keep_decaying_while_their_sequence_is_paused()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        var release = tail.Voices.First(v => v.Active);
        int envelope = release.Env;
        tail.PausePlayer(1);
        tail.Frame();
        Assert.IsTrue(release.Env < envelope, "detached release tails keep decaying while their sequence is paused");
    }

    [TestMethod]
    public void finished_player_remains_identifiable_after_its_last_tail_ends()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        tail.PausePlayer(1);
        tail.Frame();
        for (int n = 0; n < 100; n++)
            tail.Frame();
        Assert.IsTrue(tail.HasPlayer(1) && !tail.PlayerPlaying(1), "finished player remains identifiable after its last tail ends");
    }

    [TestMethod]
    public void finished_sequences_do_not_accumulate_extra_playback_position()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        tail.PausePlayer(1);
        tail.Frame();
        for (int n = 0; n < 100; n++)
            tail.Frame();
        tail.ResumePlayer(1);
        tail.Render(new float[20]);
        Assert.IsTrue(tail.PlayerElapsedFrames(1) == 0, "finished sequences do not accumulate extra playback position");
    }

    [TestMethod]
    public void hard_Stop_removes_paused_or_completed_players()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        tail.PausePlayer(1);
        tail.Frame();
        for (int n = 0; n < 100; n++)
            tail.Frame();
        tail.ResumePlayer(1);
        tail.Render(new float[20]);
        tail.Stop(1);
        Assert.IsTrue(!tail.HasPlayer(1) && !tail.PlayerPaused(1), "hard Stop removes paused or completed players");
    }

    [TestMethod]
    public void pause_and_resume_on_an_absent_slot_are_harmless_no_ops()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame();
        tail.Frame();
        tail.PausePlayer(1);
        tail.Frame();
        for (int n = 0; n < 100; n++)
            tail.Frame();
        tail.ResumePlayer(1);
        tail.Render(new float[20]);
        tail.Stop(1);
        tail.PausePlayer(4);
        tail.ResumePlayer(4);
        Assert.IsTrue(!tail.HasPlayer(4) && !tail.PlayerPaused(4), "pause and resume on an absent slot are harmless no-ops");
    }

    [TestMethod]
    public void host_gain_halves_both_PCM_and_PSG_contributions_exactly_in()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        foreach (AudioProfile profile in Enum.GetValues<AudioProfile>())
        {
            var full = Engine(profile);
            var half = Engine(profile);
            full.Start(Resources.Base + 192, 4);
            half.Start(Resources.Base + 192, 4);
            half.SetPlayerGain(4, 0.5);
            var x = new float[4096];
            var y = new float[x.Length];
            full.Render(x);
            half.Render(y);
            Assert.IsTrue(x.Any(v => v != 0) && x.Select(v => v * 0.5f).SequenceEqual(y), $"host gain halves both PCM and PSG contributions exactly in {profile}");
            half.SetPlayerGain(4, 0);
            Assert.IsTrue(half.PlayerPlaying(4), $"zero gain preserves sequence status in {profile}");
        }
    }

    [TestMethod]
    public void muting_SE1_leaves_SE2_audio_exact_and_still_advances_SE1_playback()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var independent = Engine();
        var solo = Engine();
        independent.Start(Resources.Base, 1);
        independent.Start(Resources.Base, 3);
        independent.SetPlayerGain(1, 0);
        solo.Start(Resources.Base, 3);
        var mixed = new float[1024];
        var single = new float[mixed.Length];
        independent.Render(mixed);
        solo.Render(single);
        Assert.IsTrue(mixed.SequenceEqual(single) && independent.PlayerElapsedFrames(1) == 512, "muting SE1 leaves SE2 audio exact and still advances SE1 playback");
    }

    [TestMethod]
    public void host_gains_persist_across_hard_Stop_and_Start_without_changing_another_slot()
    {
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var independent = Engine();
        independent.Start(Resources.Base, 1);
        independent.Start(Resources.Base, 3);
        independent.SetPlayerGain(1, 0);
        var mixed = new float[1024];
        independent.Render(mixed);
        independent.SetPlayerGain(1, 0.25);
        independent.Stop(1);
        independent.Start(Resources.Base, 1);
        Assert.IsTrue(independent.GetPlayerGain(1) == 0.25 && independent.GetPlayerGain(3) == 1, "host gains persist across hard Stop and Start without changing another slot");
    }

    [TestMethod]
    public void Start_rejects_player()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is ArgumentOutOfRangeException or InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var rejected = Engine();
        rejected.Start(Resources.Base, 0);
        rejected.Render(new float[200]);
        rejected.SetPlayerGain(0, 0.75);
        foreach (int invalid in new[]
        {
            -1,
            5,
            int.MaxValue
        }

        )
        {
            Reject(() => rejected.Start(Resources.Base, invalid), $"Start rejects player {invalid}");
            Reject(() => rejected.Stop(invalid), $"Stop rejects player {invalid}");
            Reject(() => rejected.PausePlayer(invalid), $"PausePlayer rejects player {invalid}");
            Reject(() => rejected.ResumePlayer(invalid), $"ResumePlayer rejects player {invalid}");
            Reject(() => rejected.HasPlayer(invalid), $"HasPlayer rejects player {invalid}");
            Reject(() => rejected.PlayerPlaying(invalid), $"PlayerPlaying rejects player {invalid}");
            Reject(() => rejected.PlayerPaused(invalid), $"PlayerPaused rejects player {invalid}");
            Reject(() => rejected.PlayerElapsedFrames(invalid), $"PlayerElapsedFrames rejects player {invalid}");
            Reject(() => rejected.GetPlayerGain(invalid), $"GetPlayerGain rejects player {invalid}");
            Reject(() => rejected.SetPlayerGain(invalid, 0.5), $"SetPlayerGain rejects player {invalid}");
        }
    }

    [TestMethod]
    public void ordinary_song_Start_cannot_replace_dedicated_cry_slot()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is ArgumentOutOfRangeException or InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var rejected = Engine();
        rejected.Start(Resources.Base, 0);
        rejected.Render(new float[200]);
        rejected.SetPlayerGain(0, 0.75);
        foreach (int invalid in new[]
        {
            -1,
            5,
            int.MaxValue
        }

        )
        {
            Reject(() => rejected.Start(Resources.Base, invalid), $"Start rejects player {invalid}");
            Reject(() => rejected.Stop(invalid), $"Stop rejects player {invalid}");
            Reject(() => rejected.PausePlayer(invalid), $"PausePlayer rejects player {invalid}");
            Reject(() => rejected.ResumePlayer(invalid), $"ResumePlayer rejects player {invalid}");
            Reject(() => rejected.HasPlayer(invalid), $"HasPlayer rejects player {invalid}");
            Reject(() => rejected.PlayerPlaying(invalid), $"PlayerPlaying rejects player {invalid}");
            Reject(() => rejected.PlayerPaused(invalid), $"PlayerPaused rejects player {invalid}");
            Reject(() => rejected.PlayerElapsedFrames(invalid), $"PlayerElapsedFrames rejects player {invalid}");
            Reject(() => rejected.GetPlayerGain(invalid), $"GetPlayerGain rejects player {invalid}");
            Reject(() => rejected.SetPlayerGain(invalid, 0.5), $"SetPlayerGain rejects player {invalid}");
        }

        Reject(() => rejected.Start(Resources.Base, 2), "ordinary song Start cannot replace dedicated cry slot");
    }

    [TestMethod]
    public void host_gain_rejects()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is ArgumentOutOfRangeException or InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var rejected = Engine();
        rejected.Start(Resources.Base, 0);
        rejected.Render(new float[200]);
        rejected.SetPlayerGain(0, 0.75);
        foreach (int invalid in new[]
        {
            -1,
            5,
            int.MaxValue
        }

        )
        {
            Reject(() => rejected.Start(Resources.Base, invalid), $"Start rejects player {invalid}");
            Reject(() => rejected.Stop(invalid), $"Stop rejects player {invalid}");
            Reject(() => rejected.PausePlayer(invalid), $"PausePlayer rejects player {invalid}");
            Reject(() => rejected.ResumePlayer(invalid), $"ResumePlayer rejects player {invalid}");
            Reject(() => rejected.HasPlayer(invalid), $"HasPlayer rejects player {invalid}");
            Reject(() => rejected.PlayerPlaying(invalid), $"PlayerPlaying rejects player {invalid}");
            Reject(() => rejected.PlayerPaused(invalid), $"PlayerPaused rejects player {invalid}");
            Reject(() => rejected.PlayerElapsedFrames(invalid), $"PlayerElapsedFrames rejects player {invalid}");
            Reject(() => rejected.GetPlayerGain(invalid), $"GetPlayerGain rejects player {invalid}");
            Reject(() => rejected.SetPlayerGain(invalid, 0.5), $"SetPlayerGain rejects player {invalid}");
        }

        foreach (double gain in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            -0.001,
            1.001
        }

        )
            Reject(() => rejected.SetPlayerGain(0, gain), $"host gain rejects {gain}");
    }

    [TestMethod]
    public void bad_song_header_0x_fails_before_replacing_player()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is ArgumentOutOfRangeException or InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var rejected = Engine();
        rejected.Start(Resources.Base, 0);
        rejected.Render(new float[200]);
        rejected.SetPlayerGain(0, 0.75);
        foreach (int invalid in new[]
        {
            -1,
            5,
            int.MaxValue
        }

        )
        {
            Reject(() => rejected.Start(Resources.Base, invalid), $"Start rejects player {invalid}");
            Reject(() => rejected.Stop(invalid), $"Stop rejects player {invalid}");
            Reject(() => rejected.PausePlayer(invalid), $"PausePlayer rejects player {invalid}");
            Reject(() => rejected.ResumePlayer(invalid), $"ResumePlayer rejects player {invalid}");
            Reject(() => rejected.HasPlayer(invalid), $"HasPlayer rejects player {invalid}");
            Reject(() => rejected.PlayerPlaying(invalid), $"PlayerPlaying rejects player {invalid}");
            Reject(() => rejected.PlayerPaused(invalid), $"PlayerPaused rejects player {invalid}");
            Reject(() => rejected.PlayerElapsedFrames(invalid), $"PlayerElapsedFrames rejects player {invalid}");
            Reject(() => rejected.GetPlayerGain(invalid), $"GetPlayerGain rejects player {invalid}");
            Reject(() => rejected.SetPlayerGain(invalid, 0.5), $"SetPlayerGain rejects player {invalid}");
        }

        foreach (double gain in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            -0.001,
            1.001
        }

        )
            Reject(() => rejected.SetPlayerGain(0, gain), $"host gain rejects {gain}");
        foreach (uint header in new[]
        {
            Resources.Base - 1,
            Resources.End - 1,
            Resources.Base + 512,
            uint.MaxValue
        }

        )
            Reject(() => rejected.Start(header, 0), $"bad song header 0x{header:X8} fails before replacing player");
    }

    [TestMethod]
    public void all_rejected_host_requests_preserve_current_player_voice_pause_gain_and_elapsed_position()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is ArgumentOutOfRangeException or InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile)
        {
            EnableTrace = false
        };
        var rejected = Engine();
        rejected.Start(Resources.Base, 0);
        rejected.Render(new float[200]);
        rejected.SetPlayerGain(0, 0.75);
        var originalVoice = rejected.Voices.First(v => v.Active);
        var originalPlayer = originalVoice.Player;
        foreach (int invalid in new[]
        {
            -1,
            5,
            int.MaxValue
        }

        )
        {
            Reject(() => rejected.Start(Resources.Base, invalid), $"Start rejects player {invalid}");
            Reject(() => rejected.Stop(invalid), $"Stop rejects player {invalid}");
            Reject(() => rejected.PausePlayer(invalid), $"PausePlayer rejects player {invalid}");
            Reject(() => rejected.ResumePlayer(invalid), $"ResumePlayer rejects player {invalid}");
            Reject(() => rejected.HasPlayer(invalid), $"HasPlayer rejects player {invalid}");
            Reject(() => rejected.PlayerPlaying(invalid), $"PlayerPlaying rejects player {invalid}");
            Reject(() => rejected.PlayerPaused(invalid), $"PlayerPaused rejects player {invalid}");
            Reject(() => rejected.PlayerElapsedFrames(invalid), $"PlayerElapsedFrames rejects player {invalid}");
            Reject(() => rejected.GetPlayerGain(invalid), $"GetPlayerGain rejects player {invalid}");
            Reject(() => rejected.SetPlayerGain(invalid, 0.5), $"SetPlayerGain rejects player {invalid}");
        }

        foreach (double gain in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            -0.001,
            1.001
        }

        )
            Reject(() => rejected.SetPlayerGain(0, gain), $"host gain rejects {gain}");
        foreach (uint header in new[]
        {
            Resources.Base - 1,
            Resources.End - 1,
            Resources.Base + 512,
            uint.MaxValue
        }

        )
            Reject(() => rejected.Start(header, 0), $"bad song header 0x{header:X8} fails before replacing player");
        Assert.IsTrue(originalVoice.Active && originalVoice.Player == originalPlayer && rejected.HasPlayer(0) && rejected.PlayerPlaying(0) && !rejected.PlayerPaused(0) && rejected.PlayerElapsedFrames(0) == 100 && rejected.GetPlayerGain(0) == 0.75, "all rejected host requests preserve current player, voice, pause, gain and elapsed position");
    }

    [TestMethod]
    public void real_BGM_three_source_SE_sequences_and_dedicated_cry_coexist_in_one_bounded_synth()
    {
        var cry = new Synth(real)
        {
            EnableTrace = false
        };
        cry.Start(0x086dd844, 0);
        foreach (int slot in new[]
        {
            1,
            3,
            4
        }

        )
            cry.Start(0x086b5720, slot);
        cry.StartNormalCry();
        cry.Render(new float[1024]);
        Assert.IsTrue(Enumerable.Range(0, 5).All(cry.HasPlayer) && cry.CryPlaying && cry.BgmDucked && cry.MaxPcm <= 5, "real BGM, three source SE sequences and dedicated cry coexist in one bounded synth");
    }

    [TestMethod]
    public void host_BGM_gain_does_not_overwrite_source_cry_duck_track_volume()
    {
        var cry = new Synth(real)
        {
            EnableTrace = false
        };
        cry.Start(0x086dd844, 0);
        foreach (int slot in new[]
        {
            1,
            3,
            4
        }

        )
            cry.Start(0x086b5720, slot);
        cry.StartNormalCry();
        cry.Render(new float[1024]);
        var cryTrack = cry.Voices.First(v => v.Active && v.Player!.Id == 0).Track!;
        cry.SetPlayerGain(0, 0.4);
        Assert.IsTrue(cryTrack.ExternalVolume == 21 && cry.GetPlayerGain(0) == 0.4, "host BGM gain does not overwrite source cry-duck track volume");
    }

    [TestMethod]
    public void stopping_a_new_SE_slot_preserves_original_BGM_SE1_and_cry_ownership()
    {
        var cry = new Synth(real)
        {
            EnableTrace = false
        };
        cry.Start(0x086dd844, 0);
        foreach (int slot in new[]
        {
            1,
            3,
            4
        }

        )
            cry.Start(0x086b5720, slot);
        cry.StartNormalCry();
        cry.Render(new float[1024]);
        cry.SetPlayerGain(0, 0.4);
        cry.Stop(3);
        Assert.IsTrue(cry.HasPlayer(0) && cry.HasPlayer(1) && cry.HasPlayer(2) && cry.HasPlayer(4) && cry.CryPlaying, "stopping a new SE slot preserves original BGM, SE1 and cry ownership");
    }

    [TestMethod]
    public void dedicated_cry_slot_still_supports_hard_Stop()
    {
        var cry = new Synth(real)
        {
            EnableTrace = false
        };
        cry.Start(0x086dd844, 0);
        foreach (int slot in new[]
        {
            1,
            3,
            4
        }

        )
            cry.Start(0x086b5720, slot);
        cry.StartNormalCry();
        cry.Render(new float[1024]);
        cry.SetPlayerGain(0, 0.4);
        cry.Stop(3);
        cry.Stop(2);
        Assert.IsTrue(!cry.CryPlaying && !cry.HasPlayer(2), "dedicated cry slot still supports hard Stop");
    }
}
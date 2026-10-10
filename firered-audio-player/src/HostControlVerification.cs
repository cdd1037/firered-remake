using System.Buffers.Binary;
namespace FireRedAudio;

public static class HostControlVerification
{
    public static int Run(Resources real)
    {
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL " + name);
            Console.WriteLine("PASS " + name);
            passed++;
        }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (Exception error) when (error is ArgumentOutOfRangeException or InvalidDataException or NotSupportedException)
            {
                Check(true, name);
                return;
            }
            throw new Exception("FAIL " + name);
        }
        var resources = Fixture();
        Synth Engine(AudioProfile profile = AudioProfile.ModernDryV1) => new(resources, profile) { EnableTrace = false };
        var shared = Engine();
        Check(Enumerable.Range(0, 5).All(slot => !shared.HasPlayer(slot) && !shared.PlayerPlaying(slot) &&
            !shared.PlayerPaused(slot) && shared.PlayerElapsedFrames(slot) == 0 && shared.GetPlayerGain(slot) == 1),
            "five host slots begin absent, idle, unpaused, zero-position and unity-gain");
        foreach (int slot in new[] { 0, 1, 3, 4 }) shared.Start(Resources.Base, slot);
        shared.Render(new float[200]);
        Check(new[] { 0, 1, 3, 4 }.All(slot => shared.HasPlayer(slot) && shared.PlayerPlaying(slot) && shared.PlayerElapsedFrames(slot) == 100) &&
            shared.Voices.Count(v => v.Active) == 4 && shared.Voices.Take(5).Count(v => v.Active) == 4,
            "BGM and three independent SE players concurrently share the five PCM slots");
        Check(shared.Voices.Length == 9 && shared.MaxPcm == 4 && !shared.HasPlayer(2),
            "added source players do not create extra PCM or PSG channels or occupy cry slot");
        var intact = shared.Voices.Where(v => v.Active && v.Player!.Id != 3).Select(v => (v.Player, v.Age, v.Position)).ToArray();
        shared.Stop(3);
        Check(!shared.HasPlayer(3) && !shared.PlayerPlaying(3) && shared.PlayerElapsedFrames(3) == 0 &&
            intact.SequenceEqual(shared.Voices.Where(v => v.Active).Select(v => (v.Player, v.Age, v.Position))),
            "stopping SE2 leaves BGM, SE1 and SE3 voices and sample positions untouched");
        shared.Start(Resources.Base, 3);
        Check(shared.PlayerElapsedFrames(3) == 0 && shared.PlayerElapsedFrames(1) == 100,
            "restarting one SE resets only that player's elapsed position");

        var priority = Engine();
        foreach (int slot in new[] { 4, 3, 1, 0 }) priority.Start(Resources.Base + 128, slot);
        priority.Frame();
        Check(priority.Voices[5].Player!.Id == 0 && priority.Rejected == 3,
            "equal-priority shared square channel honors original BGM, SE1, SE2, SE3 address order");
        priority.Stop(0);
        priority.Stop(1);
        priority.Start(Resources.Base + 128, 4);
        priority.Start(Resources.Base + 128, 3);
        priority.Frame();
        Check(priority.Voices[5].Player!.Id == 3, "SE2 precedes SE3 for an equal-priority PSG allocation");

        var paused = Engine();
        var reference = Engine();
        paused.Start(Resources.Base, 0);
        reference.Start(Resources.Base, 0);
        paused.Start(Resources.Base, 1);
        for (int n = 0; n < 2; n++) { paused.Frame(); reference.Frame(); }
        var bgm = paused.Voices.First(v => v.Active && v.Player!.Id == 0).Player!;
        var expected = reference.Voices.First(v => v.Active).Player!;
        var se = paused.Voices.First(v => v.Active && v.Player!.Id == 1).Player!;
        var cursor = State(bgm);
        long seTick = se.Tick;
        paused.PausePlayer(0);
        Check(paused.HasPlayer(0) && paused.PlayerPaused(0) && !paused.PlayerPlaying(0) &&
            paused.Voices.All(v => !v.Active || v.Player!.Id != 0),
            "original-style pause retains BGM player but immediately silences its linked voices");
        for (int n = 0; n < 20; n++) paused.Frame();
        Check(State(bgm) == cursor && se.Tick > seTick && paused.GameFrames == 22,
            "pause preserves BGM cursor, waits, ticks and LFO while SE and shared sound clock continue");
        paused.ResumePlayer(0);
        Check(!paused.PlayerPaused(0) && paused.PlayerPlaying(0) && paused.Voices.All(v => !v.Active || v.Player!.Id != 0),
            "resume clears pause without resurrecting stopped waveform voices");
        bool sequenceMatches = true;
        for (int n = 0; n < 100; n++)
        {
            paused.Frame(); reference.Frame();
            sequenceMatches &= State(bgm) == State(expected);
        }
        Check(sequenceMatches && paused.Voices.Any(v => v.Active && v.Player!.Id == 0),
            "resume retains fractional tempo remainder and emits subsequent notes at the original cursor");

        var position = Engine();
        position.Start(Resources.Base, 0);
        position.Start(Resources.Base, 4);
        position.Render(new float[514]);
        position.PausePlayer(0);
        position.PausePlayer(0);
        position.Render(new float[2000]);
        Check(position.PlayerElapsedFrames(0) == 257 && position.PlayerElapsedFrames(4) == 1257,
            "elapsed 48 kHz frame positions freeze independently during repeated pause");
        position.ResumePlayer(0);
        position.ResumePlayer(0);
        position.Render(new float[86]);
        Check(position.PlayerElapsedFrames(0) == 300 && position.PlayerElapsedFrames(4) == 1300,
            "repeated resume preserves elapsed position and advances only subsequently rendered frames");
        position.PausePlayer(0);
        position.Start(Resources.Base, 0);
        Check(!position.PlayerPaused(0) && position.PlayerElapsedFrames(0) == 0,
            "Start replaces a paused player and resets its pause and position");

        var tail = Engine();
        tail.Start(Resources.Base + 64, 1);
        tail.Frame(); tail.Frame();
        var release = tail.Voices.First(v => v.Active);
        Check(release.Detached && release.Releasing && release.Player!.Tracks.All(t => t.Ended) && tail.PlayerPlaying(1),
            "player status remains playing for an owned release tail after sequence FINE");
        int envelope = release.Env;
        tail.PausePlayer(1);
        Check(release.Active && tail.PlayerPlaying(1) && tail.PlayerPaused(1),
            "TrackStop-style pause leaves already-detached FINE tails active");
        tail.Frame();
        Check(release.Env < envelope, "detached release tails keep decaying while their sequence is paused");
        for (int n = 0; n < 100; n++) tail.Frame();
        Check(tail.HasPlayer(1) && !tail.PlayerPlaying(1), "finished player remains identifiable after its last tail ends");
        tail.ResumePlayer(1);
        tail.Render(new float[20]);
        Check(tail.PlayerElapsedFrames(1) == 0, "finished sequences do not accumulate extra playback position");
        tail.Stop(1);
        Check(!tail.HasPlayer(1) && !tail.PlayerPaused(1), "hard Stop removes paused or completed players");
        tail.PausePlayer(4); tail.ResumePlayer(4);
        Check(!tail.HasPlayer(4) && !tail.PlayerPaused(4), "pause and resume on an absent slot are harmless no-ops");

        foreach (AudioProfile profile in Enum.GetValues<AudioProfile>())
        {
            var full = Engine(profile);
            var half = Engine(profile);
            full.Start(Resources.Base + 192, 4);
            half.Start(Resources.Base + 192, 4);
            half.SetPlayerGain(4, 0.5);
            var x = new float[4096];
            var y = new float[x.Length];
            full.Render(x); half.Render(y);
            Check(x.Any(v => v != 0) && x.Select(v => v * 0.5f).SequenceEqual(y),
                $"host gain halves both PCM and PSG contributions exactly in {profile}");
            half.SetPlayerGain(4, 0);
            Check(half.PlayerPlaying(4), $"zero gain preserves sequence status in {profile}");
        }
        var independent = Engine();
        var solo = Engine();
        independent.Start(Resources.Base, 1);
        independent.Start(Resources.Base, 3);
        independent.SetPlayerGain(1, 0);
        solo.Start(Resources.Base, 3);
        var mixed = new float[1024];
        var single = new float[mixed.Length];
        independent.Render(mixed); solo.Render(single);
        Check(mixed.SequenceEqual(single) && independent.PlayerElapsedFrames(1) == 512,
            "muting SE1 leaves SE2 audio exact and still advances SE1 playback");
        independent.SetPlayerGain(1, 0.25);
        independent.Stop(1);
        independent.Start(Resources.Base, 1);
        Check(independent.GetPlayerGain(1) == 0.25 && independent.GetPlayerGain(3) == 1,
            "host gains persist across hard Stop and Start without changing another slot");

        var rejected = Engine();
        rejected.Start(Resources.Base, 0);
        rejected.Render(new float[200]);
        rejected.SetPlayerGain(0, 0.75);
        var originalVoice = rejected.Voices.First(v => v.Active);
        var originalPlayer = originalVoice.Player;
        foreach (int invalid in new[] { -1, 5, int.MaxValue })
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
        foreach (double gain in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.001, 1.001 })
            Reject(() => rejected.SetPlayerGain(0, gain), $"host gain rejects {gain}");
        foreach (uint header in new[] { Resources.Base - 1, Resources.End - 1, Resources.Base + 512, uint.MaxValue })
            Reject(() => rejected.Start(header, 0), $"bad song header 0x{header:X8} fails before replacing player");
        Check(originalVoice.Active && originalVoice.Player == originalPlayer && rejected.HasPlayer(0) &&
            rejected.PlayerPlaying(0) && !rejected.PlayerPaused(0) && rejected.PlayerElapsedFrames(0) == 100 &&
            rejected.GetPlayerGain(0) == 0.75,
            "all rejected host requests preserve current player, voice, pause, gain and elapsed position");

        var cry = new Synth(real) { EnableTrace = false };
        cry.Start(0x086dd844, 0);
        foreach (int slot in new[] { 1, 3, 4 }) cry.Start(0x086b5720, slot);
        cry.StartNormalCry();
        cry.Render(new float[1024]);
        Check(Enumerable.Range(0, 5).All(cry.HasPlayer) && cry.CryPlaying && cry.BgmDucked && cry.MaxPcm <= 5,
            "real BGM, three source SE sequences and dedicated cry coexist in one bounded synth");
        var cryTrack = cry.Voices.First(v => v.Active && v.Player!.Id == 0).Track!;
        cry.SetPlayerGain(0, 0.4);
        Check(cryTrack.ExternalVolume == 21 && cry.GetPlayerGain(0) == 0.4,
            "host BGM gain does not overwrite source cry-duck track volume");
        cry.Stop(3);
        Check(cry.HasPlayer(0) && cry.HasPlayer(1) && cry.HasPlayer(2) && cry.HasPlayer(4) && cry.CryPlaying,
            "stopping a new SE slot preserves original BGM, SE1 and cry ownership");
        cry.Stop(2);
        Check(!cry.CryPlaying && !cry.HasPlayer(2), "dedicated cry slot still supports hard Stop");
        return passed;
    }

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
            for (int n = 0; n < tracks.Length; n++) Word(offset + 8 + n * 4, Resources.Base + (uint)tracks[n]);
        }
        Header(0, 1024);
        Header(64, 1152);
        Header(128, 1280);
        Header(192, 1024, 1280);
        void Loop(int offset, byte program)
        {
            byte[] sequence = { 0xbb, 101, 0xbd, program, 0xc4, 6, 0xcf, 60, 127, 0x84,
                0xce, 60, 0xcf, 64, 127, 0x84, 0xce, 64, 0xb2, 0, 0, 0, 0 };
            sequence.CopyTo(bytes, offset);
            Word(offset + 19, Resources.Base + (uint)offset + 6);
        }
        Loop(1024, 0); Loop(1280, 1);
        new byte[] { 0xcf, 60, 127, 0x81, 0xb1 }.CopyTo(bytes, 1152);
        bytes[2048 + 1] = 60;
        Word(2048 + 4, Resources.Base + 4096);
        bytes[2048 + 8] = bytes[2048 + 9] = bytes[2048 + 10] = 255;
        bytes[2048 + 11] = 224;
        new byte[] { 1, 60, 0, 8, 2, 0, 0, 0, 0, 0, 15, 4 }.CopyTo(bytes, 2060);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4098), 0x4000);
        Word(4100, 16384 * 1024);
        Word(4108, 64);
        for (int n = 0; n < 64; n++) bytes[4112 + n] = unchecked((byte)(sbyte)(n < 32 ? 80 : -80));
        for (int n = 0; n < 12; n++) Word((int)(0x08489abc - Resources.Base) + 4 * n, 0x80000000);
        return new Resources(bytes);
    }
}

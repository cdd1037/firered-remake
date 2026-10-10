using System.Buffers.Binary;
namespace FireRedAudio;

public static class PsgVerification
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
            catch (Exception error) when (error is InvalidDataException or NotSupportedException)
            {
                Check(true, name);
                return;
            }
            throw new Exception("FAIL " + name);
        }
        string[] originalWaves =
        {
            "01258adefec9631001258adefec96310", "02468acefdb9753102468acefdb97531",
            "fedcba9876543210fedcba9876543210", "fdb9888877776531fdb9888877776531",
            "048cca86ffed4321048cca86ffed4321", "fffffe864303468efffffe864303468e",
            "aaaaa82100000128aaaaa82100000128", "ffffffffffee886644330033446688ee",
            "6789abc8989656546789abc898965654", "021426384a5c6e7f7e6c5a4836241200",
            "0426384a5c5a48360426384a5c5a4836"
        };
        var waveTones = Enumerable.Range(0, Synth.WaveAssetCount)
            .Select(n => (Type: 3, Data: Synth.FirstWaveAddress + (uint)n * 16, Length: 0)).ToArray();
        var waveResources = Fixture(real, waveTones);
        var waves = new Synth(waveResources) { EnableTrace = false };
        var wavePlayer = new Player(waveResources, waves, Song.Read(waveResources, Resources.Base), 0);
        var waveTrack = new Track(Resources.Base + 256, 0);
        for (int n = 0; n < originalWaves.Length; n++)
        {
            byte[] payload = Convert.FromHexString(originalWaves[n]);
            uint address = Synth.FirstWaveAddress + (uint)n * 16;
            Check(payload.SequenceEqual(Enumerable.Range(0, 16).Select(i => real.U8(address + (uint)i))),
                $"wave{n + 1} original physical16-byte payload matches fixed ROM");
            waveTrack.Program = n;
            waves.Note(wavePlayer, waveTrack, 60, 127, -1);
            byte[] expected = payload.SelectMany(value => new byte[] { (byte)(value >> 4), (byte)(value & 15) }).ToArray();
            Check(waves.Voices[7].WaveSamples!.SequenceEqual(expected), $"wave{n + 1} decodes32 high-nibble-first original samples");
        }
        Check(Synth.NoisePitchTable.SequenceEqual(Enumerable.Range(0, 60).Select(n => real.U8(0x08489ba0 + (uint)n))),
            "all60 noise pitch table bytes match original driver");
        Check(Synth.WaveVolumeTable.SequenceEqual(Enumerable.Range(0, 16).Select(n => real.U8(0x08489bdc + (uint)n))),
            "all16 wave volume classes match original driver");
        Check(Enumerable.Range(0, 16).Select(Synth.WaveGain).SequenceEqual(new double[]
            { 0, 0, .25, .25, .25, .25, .5, .5, .5, .5, .75, .75, .75, .75, 1, 1 }),
            "wave envelope uses GBA mute/quarter/half/three-quarter/full quantization");
        var pitchTrack = new Track(Resources.Base, 0);
        var waveTone = new Instrument(3, 60, 0, 0, Synth.FirstWaveAddress, 0, 0, 15, 0);
        Check(Math.Abs(Synth.WaveClock(60, pitchTrack, waveTone) - 2097152.0 / 501) < 1e-10 &&
            Math.Abs(Synth.WaveClock(60, pitchTrack, waveTone with { Type = 11 }) - 2097152.0 / 500) < 1e-10,
            "wave nibble clock follows original CGB register and alt-FIX rounding");
        Check(Synth.NoiseClock(0) == 524288 && Synth.NoiseClock(1) == 262144 && Synth.NoiseClock(0x14) == 32768,
            "noise divider-zero special case and shift clock equations");
        Check(Math.Abs(Synth.NoiseClock(0xd7) - 262144.0 / (7 * 8192)) < 1e-12 &&
            Synth.NoiseClock(0xe7) == 0 && Synth.NoiseClock(0xf7) == 0,
            "noise high shifts13/14/15 follow documented clock limits");
        Check(Synth.NoiseRegister(53, pitchTrack, 0) == 0x57 && Synth.NoiseRegister(53, pitchTrack, 1) == 0x5f &&
            Synth.NoiseRegister(0, pitchTrack, 0) == 0xd7 && Synth.NoiseRegister(127, pitchTrack, 0) == 0,
            "noise key clamps and width bit preserve original NR43 mapping");
        pitchTrack.Tune = 1;
        Check(Synth.NoiseRegister(53, pitchTrack, 0) == 0x57, "noise ignores fractional fine pitch as original driver does");
        pitchTrack.Tune = 0;

        foreach (bool shortMode in new[] { false, true })
        {
            int seed = shortMode ? 0x40 : 0x4000;
            int state = seed;
            var prefix = new char[48];
            for (int n = 0; n < prefix.Length; n++)
            {
                state = Synth.StepNoise(state, shortMode, out int output);
                prefix[n] = output == 0 ? '0' : '1';
            }
            string expected = shortMode
                ? "000000100000110000101000111100100010110011101010"
                : "000000000000001000000000000011000000000000101000";
            Check(new string(prefix) == expected, $"{(shortMode ? 7 : 15)}-bit GBA LFSR first48-bit golden vector");
            state = seed;
            int cycle = 0, ones = 0;
            do
            {
                state = Synth.StepNoise(state, shortMode, out int output);
                ones += output;
                cycle++;
                if (cycle > 32767 || state == 0) throw new Exception("LFSR failed bounded cycle");
            } while (state != seed);
            Check(cycle == (shortMode ? 127 : 32767) && ones == (shortMode ? 64 : 16384),
                $"{(shortMode ? 7 : 15)}-bit LFSR complete nonzero period and output balance");
        }
        var averaged = new Voice { WaveSamples = Enumerable.Range(0, 32).Select(n => (byte)(n % 2 == 0 ? 0 : 15)).ToArray() };
        Check(Synth.RenderWave(averaged, 2) == 0 && averaged.Position == 2,
            "wave host sample averages real nibble intervals rather than a substitute oscillator");
        averaged.Position = 31.5;
        Check(Synth.RenderWave(averaged, 1) == 0 && averaged.Position == .5, "wave sample integration wraps exactly inside32 samples");

        var toneSet = new[]
        {
            (9, 2u, 0), (10, 2u, 0), (11, Synth.FirstWaveAddress, 0), (12, 0u, 0), (0, Resources.Base + 4096, 0),
            (3, Synth.FirstWaveAddress + 1, 0), (3, Resources.End - 8, 0), (3, Resources.Base + 128, 0),
            (4, 2u, 0), (3, Synth.FirstWaveAddress, 1), (4, 0u, 1), (0x13, Synth.FirstWaveAddress, 0)
        };
        var resources = Fixture(real, toneSet);
        var engine = new Synth(resources) { EnableTrace = false };
        var song = Song.Read(resources, Resources.Base);
        var player = new Player(resources, engine, song, 0);
        var track = new Track(Resources.Base + 256, 0);
        for (int program = 0; program < 5; program++)
        {
            track.Program = program;
            engine.Note(player, track, 60, 127, -1);
        }
        Check(engine.Voices.Length == 9 && new[] { 0, 5, 6, 7, 8 }.All(index => engine.Voices[index].Active),
            "fivePCM plus four isolated CGB slots are allocated by actual voice family");
        long[] ages = engine.Voices.Select(voice => voice.Age).ToArray();
        long waveAge = engine.Voices[7].Age;
        for (int program = 5; program < toneSet.Length; program++)
        {
            track.Program = program;
            Reject(() => engine.Note(player, track, 60, 127, -1),
                $"invalid wave/noise fixture program{program} rejected before allocation");
        }
        Check(engine.Voices[7].Active && engine.Voices[7].Age == waveAge && ages.SequenceEqual(engine.Voices.Select(v => v.Age)),
            "invalid wave pointer/width/length/type preserves every active channel");
        var effect = new Player(resources, engine, song with { Priority = 10 }, 1);
        var effectTrack = new Track(Resources.Base + 256, 0) { Program = 2 };
        engine.Note(effect, effectTrack, 72, 127, -1);
        Check(engine.Voices[7].Player == effect && new[] { 0, 5, 6, 8 }.All(index => engine.Voices[index].Age == ages[index]),
            "higher-priority wave steals wave slot only");
        effectTrack.Program = 3;
        engine.Note(effect, effectTrack, 60, 127, -1);
        Check(engine.Voices[8].Player == effect && new[] { 0, 5, 6 }.All(index => engine.Voices[index].Age == ages[index]),
            "higher-priority noise steals noise slot only");
        track.Program = 2;
        long rejected = engine.Rejected;
        engine.Note(player, track, 64, 127, -1);
        Check(engine.Rejected == rejected + 1 && engine.Voices[7].Player == effect, "lower-priority wave cannot steal active SE wave");
        engine.Voices[8].Phase = .75;
        engine.Voices[8].NoiseState = 1;
        engine.Note(effect, effectTrack, 60, 127, -1);
        Check(engine.Voices[8].NoiseState == 0x4000 && engine.Voices[8].Phase == 0 && engine.Voices[8].NoiseOutput == 0,
            "noise note restart resets documented seed and explicit host phase");
        engine.Stop(1);
        Check(!engine.Voices[7].Active && !engine.Voices[8].Active && engine.Voices[0].Active && engine.Voices[5].Active,
            "stopping SE wave/noise leaves BGM PCM and squares intact");
        engine.Stop(0);
        var silence = new float[2048];
        engine.Render(silence);
        Check(silence.All(value => value == 0) && engine.Ended, "hard stop clears all four CGB families");

        foreach (int kind in new[] { 3, 11, 4, 12 })
        {
            var r = Fixture(real, new[] { (kind, kind % 8 == 3 ? Synth.FirstWaveAddress : 0u, 0) });
            float[] Render(int block)
            {
                var synth = new Synth(r, AudioProfile.SourceFiltered) { EnableTrace = false };
                synth.Start(Resources.Base, 0);
                var output = new float[2 * Synth.Rate * 2];
                for (int offset = 0; offset < output.Length;)
                {
                    int count = Math.Min(block * 2, output.Length - offset);
                    synth.Render(output.AsSpan(offset, count));
                    offset += count;
                }
                return output;
            }
            var first = Render(512);
            Check(first.Any(value => value != 0) && first.All(float.IsFinite) && first.SequenceEqual(Render(257)),
                $"wave/noise type0x{kind:X2} actually renders finite nonzero block-independent audio");
        }
        return passed;
    }

    static Resources Fixture(Resources real, (int Type, uint Data, int Length)[] tones)
    {
        var bytes = new byte[Resources.End - Resources.Base];
        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        bytes[0] = 1;
        Word(4, Resources.Base + 512);
        Word(8, Resources.Base + 256);
        new byte[] { 0xcf, 60, 127, 0xb0, 0xb1 }.CopyTo(bytes, 256);
        for (int n = 0; n < tones.Length; n++)
        {
            int offset = 512 + n * 12;
            var tone = tones[n];
            bytes[offset] = (byte)tone.Type;
            bytes[offset + 1] = 60;
            bytes[offset + 2] = (byte)tone.Length;
            Word(offset + 4, tone.Data);
            bytes[offset + 8] = (byte)((tone.Type & 7) == 0 ? 255 : 0);
            bytes[offset + 10] = (byte)((tone.Type & 7) == 0 ? 255 : 15);
            bytes[offset + 11] = 1;
        }
        Word(4096, 0x40000000);
        Word(4100, 10512 * 1024);
        Word(4108, 16);
        Array.Fill(bytes, (byte)32, 4112, 16);
        for (uint n = 0; n < Synth.WaveAssetCount * 16; n++)
            bytes[Synth.FirstWaveAddress - Resources.Base + n] = real.U8(Synth.FirstWaveAddress + n);
        return new Resources(bytes);
    }
}

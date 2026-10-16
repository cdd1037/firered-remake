namespace FireRedAudio;

public sealed class Track
{
    public uint Cursor;
    public byte Status;
    public int Wait, Key = 60, Velocity = 100, Program;
    public int Volume = 127, Pan, Bend, BendRange = 2, KeyShift, Tune;
    public int LfoSpeed = 22, LfoDelay, Mod, ModType, LfoPhase, LfoWait;
    public int Priority;
    public int ExternalVolume = 64, ExternalPan, ReleaseOverride = -1, ExtendedWaitTimer;
    public int PseudoEchoVolume, PseudoEchoLength;
    public bool Ended;
    public readonly uint[] Returns = new uint[3];
    public int Depth;
    public int Index;
    public long Tick;
    public Track(uint p, int index)
    {
        Cursor = p;
        Index = index;
    }
    public double PitchMod => ModType == 0 ? ((Triangle(LfoPhase) * Mod) >> 6) / 16.0 : 0;
    static int Triangle(int p) => p < 64 ? p : p < 192 ? 128 - p : p - 256;
}
public sealed class Player
{
    static readonly int[] Durations = Enumerable.Range(0, 25).Concat(new[] { 28, 30, 32, 36, 40, 42, 44, 48, 52, 54, 56, 60, 64, 66, 68, 72, 76, 78, 80, 84, 88, 90, 92, 96 }).ToArray();
    public readonly Song Song;
    public readonly Track[] Tracks;
    public readonly int Id;
    public int Tempo = 150;
    int tempoRemainder;
    public long Tick;
    readonly SequenceImage r;
    readonly Synth synth;
    public Player(Resources r, Synth synth, Song song, int id, byte[]? dynamicSequence = null)
    {
        this.r = new SequenceImage(r, dynamicSequence);
        this.synth = synth;
        Song = song;
        Id = id;
        Tracks = song.Tracks.Select((p, i) => new Track(p, i)).ToArray();
    }
    public void Frame()
    {
        tempoRemainder += Tempo;
        while (tempoRemainder >= 150)
        {
            tempoRemainder -= 150;
            Step();
        }
    }
    // Process tracks in source order: a later track has not expired its gates yet
    // when an earlier track tries to allocate a new voice on the same tick.
    void Step()
    {
        foreach (var t in Tracks)
        {
            if (t.Ended)
                continue;
            synth.GateTick(this, t);
            t.Tick = Tick;
            if (t.Wait > 0)
                t.Wait--;
            int budget = 4096;
            while (!t.Ended && t.Wait == 0)
            {
                if (--budget == 0)
                    throw new InvalidDataException("Sequence instruction budget exceeded (no-wait loop)");
                uint origin = t.Cursor;
                byte op = r.U8(t.Cursor);
                // Control bytes below VOICE do not replace the running status.
                if (op >= 0x80)
                {
                    t.Cursor++;
                    if (op >= 0xbd)
                        t.Status = op;
                }
                else
                {
                    op = t.Status;
                    if (op < 0xbd)
                        throw new InvalidDataException("Missing running status");
                }
                if (op <= 0xb0 && op >= 0x80)
                {
                    t.Wait = Durations[op - 0x80];
                    continue;
                }
                if (op >= 0xcf)
                {
                    int duration = op == 0xcf ? -1 : Durations[op - 0xcf];
                    if (r.U8(t.Cursor) < 0x80)
                    {
                        t.Key = Read(t);
                        if (r.U8(t.Cursor) < 0x80)
                        {
                            t.Velocity = Read(t);
                            if (r.U8(t.Cursor) < 0x80 && duration >= 0)
                                duration += Read(t);
                        }
                    }
                    if (t.LfoDelay > 0)
                    {
                        t.LfoWait = t.LfoDelay;
                        t.LfoPhase = 0;
                    }
                    synth.Note(this, t, t.Key + t.KeyShift, t.Velocity, duration);
                    continue;
                }
                switch (op)
                {
                    case 0xb1:
                        t.Ended = true;
                        if (Id == 2 && synth.EnableTrace)
                            synth.Log(this, t, "cry-sequence-fine");
                        synth.ReleaseTrack(this, t);
                        break;
                    case 0xb2:
                        {
                            uint target = r.Pointer(t.Cursor);
                            if (synth.EnableTrace)
                                synth.Log(this, t, $"goto 0x{target:X8}");
                            t.Cursor = target;
                            break;
                        }
                    case 0xb3:
                        if (t.Depth == 3)
                            throw new InvalidDataException("Pattern stack overflow");
                        t.Returns[t.Depth++] = t.Cursor + 4;
                        t.Cursor = r.Pointer(t.Cursor);
                        break;
                    case 0xb4:
                        // Original ply_pend permits inline pattern bodies:
                        // their first straight-through PEND has no return slot.
                        if (t.Depth != 0)
                            t.Cursor = t.Returns[--t.Depth];
                        break;
                    case 0xba:
                        t.Priority = Read(t);
                        break;
                    case 0xbb:
                        Tempo = Read(t) * 2;
                        if (Tempo == 0)
                            throw new InvalidDataException("Zero tempo");
                        break;
                    case 0xbc:
                        t.KeyShift = unchecked((sbyte)Read(t));
                        break;
                    case 0xbd:
                        t.Program = Read(t);
                        break;
                    case 0xbe:
                        t.Volume = Read(t);
                        break;
                    case 0xbf:
                        t.Pan = Read(t) - 64;
                        break;
                    case 0xc0:
                        t.Bend = Read(t) - 64;
                        break;
                    case 0xc1:
                        t.BendRange = Read(t);
                        break;
                    case 0xc2:
                        t.LfoSpeed = Read(t);
                        if (t.LfoSpeed == 0)
                            t.LfoPhase = 0;
                        break;
                    case 0xc3:
                        t.LfoDelay = Read(t);
                        break;
                    case 0xc4:
                        t.Mod = Read(t);
                        if (t.Mod == 0)
                            t.LfoPhase = 0;
                        break;
                    case 0xc5:
                        t.ModType = Read(t);
                        if (t.ModType != 0)
                            throw new NotSupportedException("Only pitch modulation is implemented");
                        break;
                    case 0xc8:
                        t.Tune = Read(t) - 64;
                        break;
                    case 0xcd:
                        byte command = Read(t);
                        switch (command)
                        {
                            case 7:
                                t.ReleaseOverride = Read(t);
                                break;
                            case 8:
                                t.PseudoEchoVolume = Read(t);
                                break;
                            case 9:
                                t.PseudoEchoLength = Read(t);
                                break;
                            case 12:
                                ushort length = r.U16(t.Cursor);
                                if (t.ExtendedWaitTimer < length)
                                {
                                    t.ExtendedWaitTimer++;
                                    t.Cursor -= 2;
                                    t.Wait = 1;
                                }
                                else
                                {
                                    t.ExtendedWaitTimer = 0;
                                    t.Cursor += 2;
                                }
                                break;
                            case 13:
                                uint progress = r.U32(t.Cursor);
                                t.Cursor += 4;
                                if (progress != 0)
                                    throw new NotSupportedException("Cry progress offsets are not implemented");
                                break;
                            default:
                                throw new NotSupportedException($"XCMD 0x{command:X2} is not implemented");
                        }
                        break;
                    case 0xce:
                        if (r.U8(t.Cursor) < 0x80)
                            t.Key = Read(t);
                        synth.EndTie(this, t, t.Key);
                        break;
                    default:
                        throw new NotSupportedException($"Opcode 0x{op:X2} at 0x{origin:X8}, player {Id}, track {t.Index}, tick {Tick}");
                }
            }
            if (t.LfoWait > 0)
                t.LfoWait--;
            else if (t.Mod > 0)
                t.LfoPhase = (t.LfoPhase + t.LfoSpeed) & 255;
        }
        Tick++;
    }
    byte Read(Track t) => r.U8(t.Cursor++);
}

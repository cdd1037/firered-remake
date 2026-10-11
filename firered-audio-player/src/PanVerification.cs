using System.Buffers.Binary;
namespace FireRedAudio;

/// <summary>Bounded source panX control tests. No recorded audio or output files.</summary>
public static class PanVerification
{
    public static int Run(Resources real, string? oraclePath = null)
    {
        int checks = 0;
        void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("FAIL panX: " + message);
            checks++; Console.WriteLine("PASS panX: " + message);
        }
        void Reject(Action action, string message)
        {
            try { action(); } catch (ArgumentOutOfRangeException) { Check(true, message); return; }
            throw new InvalidOperationException("FAIL panX: " + message);
        }
        var fixture = Fixture();
        Synth New(AudioProfile p = AudioProfile.SourceFiltered) => new(fixture, p) { EnableTrace = false };
        Track[] Tracks(Synth s, int player) => s.Voices.First(v => v.Active && v.Player!.Id == player).Player!.Tracks;
        double[] Energy(Synth s)
        {
            var pcm = new float[8192]; s.Render(pcm);
            if (pcm.Any(x => !float.IsFinite(x))) throw new InvalidDataException("Nonfinite pan PCM");
            double[] e = [0, 0]; for (int n = 0; n < pcm.Length; n++) e[n % 2] += pcm[n] * pcm[n];
            return e;
        }
        var absent = New();
        Check(absent.SetPlayerPan(1, 63) == 0 && !absent.HasPlayer(1), "absent player is not created");
        Reject(() => absent.SetPlayerPan(-1, 0), "invalid player rejected");
        Reject(() => absent.SetPlayerPan(5, 0), "upper invalid player rejected");
        Reject(() => absent.SetPlayerPan(1, -129), "pan below signed byte rejected");
        Reject(() => absent.SetPlayerPan(1, 128), "pan above signed byte rejected");
        var selection = New(); selection.Start(Resources.Base, 1); selection.Frame();
        var tracks = Tracks(selection, 1);
        Check(selection.SetPlayerPan(1, -64, 5) == 2 && tracks.Select(t => t.ExternalPan).SequenceEqual(new[]{-64,0,-64,0}), "track mask selects zero-based existing tracks");
        Check(selection.SetPlayerPan(1, 63, 0) == 0 && selection.SetPlayerPan(1, 63, 0x8000) == 0 && tracks[0].ExternalPan == -64, "empty and out-of-range masks are no-ops");
        tracks[1].Ended = true;
        Check(selection.SetPlayerPan(1, 127) == 3 && tracks[1].ExternalPan == 0, "FINE track is excluded");
        selection.PausePlayer(1);
        Check(selection.SetPlayerPan(1, -128, 1) == 1 && tracks[0].ExternalPan == -128 && selection.PlayerPaused(1), "pause retains existing track pan without resuming");
        selection.Stop(1);
        Check(selection.SetPlayerPan(1, 63) == 0 && tracks[0].ExternalPan == -128, "hard stop prevents stale track mutation");
        selection.Start(Resources.Base, 1); selection.Frame();
        Check(Tracks(selection, 1).All(t => t.ExternalPan == 0), "restart resets panX");
        foreach (AudioProfile profile in Enum.GetValues<AudioProfile>())
        {
            foreach (uint offset in new uint[]{64, 128})
            {
                var left = New(profile); left.Start(Resources.Base + offset, 1); left.SetPlayerPan(1, -128);
                var right = New(profile); right.Start(Resources.Base + offset, 1); right.SetPlayerPan(1, 127);
                var center = New(profile); center.Start(Resources.Base + offset, 1);
                var l = Energy(left); var r = Energy(right); var c = Energy(center);
                Check(l[0] > l[1] * 4 && r[1] > r[0] * 4 && c.All(x => x > 0) && Math.Max(c[0],c[1]) < 1.15*Math.Min(c[0],c[1]), $"{profile} {(offset==64?"PCM":"PSG")} left/right/center direction");
                left.SetPlayerPan(1, 127); var reversed = Energy(left);
                Check(reversed[1] > reversed[0]*4, $"{profile} {(offset==64?"PCM":"PSG")} live pan reversal");
            }
        }
        var coexist = New(); coexist.Start(Resources.Base + 192, 1); coexist.SetPlayerPan(1, -64); coexist.Frame();
        var ct = Tracks(coexist, 1)[0];
        Check(ct.Pan == 32 && ct.ExternalPan == -64, "sequence PAN and host panX coexist on first frame");
        var first = Synth.ChannelVolume(ct, 127); Check(Math.Abs(first.Left-first.Right)<=2, "opposite sequence PAN and panX cancel");
        coexist.Frame(); coexist.Frame();
        Check(ct.Pan == -32 && ct.ExternalPan == -64 && Synth.ChannelVolume(ct,127).Right == 0, "later sequence PAN retains external panX and clamps sum");
        var independent = New(); independent.Start(Resources.Base+64,0); independent.Start(Resources.Base+64,1); independent.Start(Resources.Base+64,3); independent.Frame();
        Check(independent.SetPlayerPan(1, -64) == 1 && Tracks(independent,0)[0].ExternalPan==0 && Tracks(independent,3)[0].ExternalPan==0, "selected player leaves BGM and unrelated SE untouched");
        var ended = New(); ended.Start(Resources.Base+256,1); ended.SetPlayerPan(1,-64); ended.Frame(); ended.Frame();
        Check(ended.SetPlayerPan(1,63)==0, "naturally FINE track cannot be repanned");
        // Small real-data signal check; the source-filtered gameplay profile is included.
        foreach (AudioProfile profile in new[]{AudioProfile.ModernDryV1,AudioProfile.SourceFiltered})
        {
            var plain=new Synth(real,profile){EnableTrace=false};var zero=new Synth(real,profile){EnableTrace=false};
            plain.Start(0x086b5720,1);zero.Start(0x086b5720,1);zero.SetPlayerPan(1,0);
            var a=new float[16384];var b=new float[a.Length];plain.Render(a);zero.Render(b);
            Check(a.Any(x=>x!=0)&&a.SequenceEqual(b), $"{profile} explicit zero pan equals default PCM");
        }
        if (oraclePath is not null)
        {
            int volumes=0,controls=0;
            foreach(string line in File.ReadLines(oraclePath))
            {
                var fields=line.Split(' ');var v=fields.Skip(1).Select(int.Parse).ToArray();
                if(fields[0]=="V")
                {
                    var t=new Track(0,0){Pan=v[0],ExternalPan=v[1],Volume=v[2],ExternalVolume=v[3]};
                    var actual=Synth.ChannelVolume(t,v[4],v[5]);
                    if(actual!=(v[6],v[7]))throw new InvalidDataException("Original C volume oracle differs: "+line+" actual="+actual);
                    volumes++;
                }
                else if(fields[0]=="C")
                {
                    // Invalid ident is represented by no registered player in the safe core API.
                    var synth=New();Track[]? ts=null;
                    if(v[0]!=0){synth.Start(Resources.Base,1);synth.Frame();ts=Tracks(synth,1);foreach(var t in ts)t.ExternalPan=7;ts[1].Ended=true;}
                    synth.SetPlayerPan(1,v[2],checked((ushort)v[1]));
                    int[] actual=ts is null?[7,7,7,7]:ts.Select(t=>t.ExternalPan).ToArray();
                    if(!actual.SequenceEqual(v.Skip(3)))throw new InvalidDataException("Original C control oracle differs: "+line);
                    controls++;
                }
                else throw new InvalidDataException("Unknown oracle row");
            }
            Check(volumes==2646,"2646 extracted original C volume vectors");
            Check(controls==112,"112 extracted original C control vectors");
        }
        Check(checks>=50,"nonempty panX suite");
        return checks;
    }
    internal static Resources Fixture()
    {
        var b=new byte[Resources.End-Resources.Base];
        void W(int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
        void Header(int o,params int[] tracks){b[o]=(byte)tracks.Length;W(o+4,Resources.Base+2048);for(int n=0;n<tracks.Length;n++)W(o+8+4*n,Resources.Base+(uint)tracks[n]);}
        Header(0,1024,1024,1024,1024);Header(64,1024);Header(128,1152);Header(192,1280);Header(256,1408);
        new byte[]{0xbd,0,0xcf,60,127,0xb0,0xb2,0,0,0,0}.CopyTo(b,1024);W(1031,Resources.Base+1029);
        new byte[]{0xbd,1,0xcf,60,127,0xb0,0xb2,0,0,0,0}.CopyTo(b,1152);W(1159,Resources.Base+1157);
        new byte[]{0xbf,96,0xcf,60,127,0x82,0xbf,32,0xb0,0xb2,0,0,0,0}.CopyTo(b,1280);W(1290,Resources.Base+1288);
        new byte[]{0xcf,60,127,0x81,0xb1}.CopyTo(b,1408);
        b[2049]=60;W(2052,Resources.Base+4096);b[2056]=b[2057]=b[2058]=255;b[2059]=224;
        new byte[]{1,60,0,8,2,0,0,0,0,0,15,4}.CopyTo(b,2060);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(4098),0x4000);W(4100,16384*1024);W(4108,64);
        for(int n=0;n<64;n++)b[4112+n]=unchecked((byte)(sbyte)(n<32?80:-80));
        for(int n=0;n<12;n++)W((int)(0x08489abc-Resources.Base)+4*n,0x80000000);
        return new Resources(b);
    }
}

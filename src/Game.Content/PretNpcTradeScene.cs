using System.Collections.Immutable;
namespace Game.Content;

public sealed record NpcTradeSceneAsset(string Id, int X, int Y, int Width, int Height);
public sealed record NpcTradeSceneDocument(int SchemaVersion, int Width, int Height,
    ImmutableArray<int> BallVelocity, ImmutableArray<ImmutableArray<int>> SignalParameters, ImmutableArray<NpcTradeSceneAsset> Assets);

/// <summary>Converted NPC wireless-scene pixels and callback table, with no original-file dependency.</summary>
public sealed class PretNpcTradeScene
{
    public const string Directory = "content/generated/ui/npc-trade-scene/";
    public NpcTradeSceneDocument Document { get; }
    public byte[] Atlas { get; }
    private PretNpcTradeScene(NpcTradeSceneDocument document, byte[] atlas) => (Document, Atlas) = (document, atlas);
    public static PretNpcTradeScene Load(string root)
    {
        var d = ContentJson.ParseStrict<NpcTradeSceneDocument>(GeneratedResourceFile.Read(root, Directory + "scene.json"), ContentJson.CreateOptions());
        if (d.SchemaVersion != 1 || d.Width is < 1 or > 2048 || d.Height is < 1 or > 2048 ||
            d.BallVelocity.Length != 108 || d.BallVelocity.Any(v => v is < -4 or > 4) || d.Assets.IsDefaultOrEmpty)
            throw new InvalidDataException("Invalid NPC trade scene document.");
        if (d.SignalParameters.IsDefaultOrEmpty || d.SignalParameters[^1].Length != 2 || d.SignalParameters[^1][1] != -1 ||
            d.SignalParameters.Take(d.SignalParameters.Length-1).Any(p => p.Length != 2 || p[0] is < 0 or > 16 || p[1] is < 0 or > 30))
            throw new InvalidDataException("Invalid NPC wireless signal timing.");
        var required = new HashSet<string>(["shadow", "console", "crossing", "affine", "pokeball", "link_mon_glow", "link_mon_shadow", "cable_end", "gba_screen", "signal_send", "signal_receive"], StringComparer.Ordinal);
        foreach (var a in d.Assets)
            if (!required.Remove(a.Id) || a.X < 0 || a.Y < 0 || a.Width <= 0 || a.Height <= 0 || a.X + a.Width > d.Width || a.Y + a.Height > d.Height)
                throw new InvalidDataException("Invalid NPC trade atlas region.");
        if (required.Count != 0) throw new InvalidDataException("Incomplete NPC trade atlas.");
        byte[] bytes = GeneratedResourceFile.Read(root, Directory + "atlas.png");
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4)) != d.Width ||
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4)) != d.Height)
            throw new InvalidDataException("Invalid NPC trade scene PNG.");
        return new(d, bytes);
    }
}

public enum NpcTradeScenePhase
{
    SlideIn, SendMessage, Recall, DepartBounce, DepartLaunch, FadeToSend, SendFadeIn,
    ZoomOut, SendFlashDelay, SendFlash, SendSignal, PanAway, TravelOut, TravelOffscreen, FadeToCrossing,
    CrossingEnter, CrossingFlash, CrossingPortraits, CrossingExit, FadeToReceive, ReceiveFadeIn,
    TravelIn, PanToConsole, ReceiveSignal, ArrivedDelay, CenterConsole, ReceiveFlash, ZoomIn, FadeToPokemon,
    PokemonFadeIn, ArriveBounce, Release, ReceivedMessage, TakeCare, Ready
}

/// <summary>Pure visual timeline for the NPC wireless sequence. Never exchanges Pokemon or emits commands.</summary>
public static class NpcTradeSceneTimeline
{
    // Durations follow DoTradeAnim_Wireless and its sprite callbacks. State-only handshakes are
    // folded into adjacent stages. Cry completion is additionally gated by the audio owner.
    private static readonly int[] Durations = [60,80,61,44,43,17,17,16,21,42,101,32,50,45,17,
        30,4,75,44,17,17,21,51,60,10,33,126,19,17,17,107,61,250,60];
    public static int ReadyFrame { get; } = Durations.Sum();
    public static int StartFrame(NpcTradeScenePhase phase)
    {
        if (phase < NpcTradeScenePhase.SlideIn || phase > NpcTradeScenePhase.Ready) throw new ArgumentOutOfRangeException(nameof(phase));
        int total = 0; for (int i = 0; i < (int)phase; i++) total += Durations[i]; return total;
    }
    public static (NpcTradeScenePhase Phase, int Frame) At(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        for (int i = 0; i < Durations.Length; i++)
        { if (frame < Durations[i]) return ((NpcTradeScenePhase)i, frame); frame -= Durations[i]; }
        return (NpcTradeScenePhase.Ready, 0);
    }
    public static int TextStage(NpcTradeScenePhase phase) => phase switch
    {
        NpcTradeScenePhase.SlideIn => -1,
        NpcTradeScenePhase.SendMessage => 0,
        >= NpcTradeScenePhase.Recall and <= NpcTradeScenePhase.FadeToSend => 1,
        >= NpcTradeScenePhase.Release and <= NpcTradeScenePhase.ReceivedMessage => 2,
        >= NpcTradeScenePhase.TakeCare => 3,
        _ => -1
    };
    public static IReadOnlyList<(int Frame, string Symbol)> SoundCues { get; } = BuildSoundCues();
    private static IReadOnlyList<(int Frame, string Symbol)> BuildSoundCues()
    {
        var cues = new List<(int Frame,string Symbol)>();
        void Add(NpcTradeScenePhase phase,int offset,string symbol) => cues.Add((StartFrame(phase)+offset,symbol));
        void Repeat(NpcTradeScenePhase phase,int interval,string symbol)
        { for(int offset=interval;offset<Durations[(int)phase];offset+=interval)Add(phase,offset,symbol); }
        Add(NpcTradeScenePhase.SlideIn,0,"MUS_EVOLUTION");
        Add(NpcTradeScenePhase.Recall,32,"SE_BALL_TRADE");
        Add(NpcTradeScenePhase.DepartBounce,22,"SE_BALL_BOUNCE_1");
        Add(NpcTradeScenePhase.DepartLaunch,0,"SE_M_MEGA_KICK");
        Repeat(NpcTradeScenePhase.SendFlash,15,"SE_M_MINIMIZE");
        Repeat(NpcTradeScenePhase.ReceiveFlash,15,"SE_M_MINIMIZE");
        Add(NpcTradeScenePhase.SendSignal,0,"SE_M_HEAL_BELL");
        Add(NpcTradeScenePhase.SendSignal,51,"SE_M_HEAL_BELL");
        // Glow's counter continues across consecutive movement states.
        int first=StartFrame(NpcTradeScenePhase.TravelOut),end=StartFrame(NpcTradeScenePhase.FadeToCrossing);
        for(int f=first+10;f<end;f+=10)cues.Add((f,"SE_M_SWAGGER2"));
        first=StartFrame(NpcTradeScenePhase.ReceiveFadeIn);end=StartFrame(NpcTradeScenePhase.ReceiveSignal);
        for(int f=first+10;f<end;f+=10)cues.Add((f,"SE_M_SWAGGER2"));
        Add(NpcTradeScenePhase.CrossingEnter,17,"SE_WARP_OUT");
        Add(NpcTradeScenePhase.CrossingPortraits,54,"SE_WARP_IN");
        Add(NpcTradeScenePhase.PanToConsole,10,"SE_M_HEAL_BELL");
        Add(NpcTradeScenePhase.ReceiveSignal,10,"SE_M_HEAL_BELL");
        Add(NpcTradeScenePhase.ZoomIn,0,"SE_M_SAND_ATTACK");
        Add(NpcTradeScenePhase.ArriveBounce,21,"SE_BALL_BOUNCE_1");
        Add(NpcTradeScenePhase.ArriveBounce,65,"SE_BALL_BOUNCE_2");
        Add(NpcTradeScenePhase.ArriveBounce,91,"SE_BALL_BOUNCE_3");
        Add(NpcTradeScenePhase.ArriveBounce,106,"SE_BALL_BOUNCE_4");
        Add(NpcTradeScenePhase.ReceivedMessage,10,"MUS_EVOLVED");
        return cues.OrderBy(c=>c.Frame).ToArray();
    }
    public static int DepartBallY(int frame, IReadOnlyList<int> velocity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        if (velocity.Count != 108) throw new ArgumentException("Incomplete velocity table.", nameof(velocity));
        int y = 32;
        for (int i = 0; i < Math.Min(frame,44); i++) y += velocity[i];
        for (int i = 0; i < Math.Clamp(frame-64,0,23); i++) y -= velocity[i];
        return y;
    }
    public static int ArriveBallY(int frame, IReadOnlyList<int> velocity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        if (velocity.Count != 108) throw new ArgumentException("Incomplete velocity table.", nameof(velocity));
        if (frame <= 21) return -8 + frame * 4;
        int y = 76; for (int i = 22; i < 22+Math.Clamp(frame-21,0,86); i++) y += velocity[i]; return y;
    }
}

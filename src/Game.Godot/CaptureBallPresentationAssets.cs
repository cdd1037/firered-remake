using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Capture;

/// <summary>Generated native ball atlas. Conversion recipes and original paths stay in tooling.</summary>
public sealed record CaptureTrainerThrowFrame(int Frame,int Ticks,int XOffset);
public sealed class CaptureBallPresentationAssets
{
    public string TexturePath { get; }
    public ImmutableArray<int> Sine { get; }
    public ImmutableArray<int> FadeColors { get; }
    public ImmutableArray<CaptureTrainerThrowFrame> TrainerThrowFrames { get; }
    public CaptureTrainerThrowFrame TrainerFrame(int tick)
    {
        foreach(var frame in TrainerThrowFrames){if(tick<frame.Ticks)return frame;tick-=frame.Ticks;}
        return new(0,1,-32);
    }
    private readonly ImmutableDictionary<CaptureBall, int> _columns;
    public int Column(CaptureBall ball) => _columns.TryGetValue(ball, out int x) ? x : throw new InvalidDataException("Unknown capture ball.");
    private CaptureBallPresentationAssets(string path, ImmutableArray<int> sine, ImmutableDictionary<CaptureBall,int> columns, ImmutableArray<int> colors,ImmutableArray<CaptureTrainerThrowFrame> frames)
        => (TexturePath, Sine, _columns, FadeColors, TrainerThrowFrames) = (path, sine, columns, colors, frames);
    public static CaptureBallPresentationAssets Load(string root)
    {
        string folder = Path.Combine(root, "content/generated/battle/capture-balls");
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "balls.json")));
        var data = json.RootElement;
        if (data.GetProperty("schemaVersion").GetInt32() != 1 || data.GetProperty("width").GetInt32() != 192 ||
            data.GetProperty("height").GetInt32() != 48 || data.GetProperty("texturePath").GetString() != "balls.png")
            throw new InvalidDataException("Invalid capture ball atlas.");
        var columns = ImmutableDictionary.CreateBuilder<CaptureBall,int>();
        foreach (var ball in data.GetProperty("balls").EnumerateArray())
        {
            int id = ball.GetProperty("ball").GetInt32(), x = ball.GetProperty("x").GetInt32();
            if (id is < 1 or > 12 || x != (id - 1) * 16 || !columns.TryAdd((CaptureBall)id, x))
                throw new InvalidDataException("Invalid capture ball atlas entry.");
        }
        var sine = data.GetProperty("sine").EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray();
        if (columns.Count != 12 || sine.Length != 320 || sine.Any(x => x is < -256 or > 256) || sine[0] != 0 || sine[64] != 256)
            throw new InvalidDataException("Incomplete capture ball presentation.");
        var colors=data.GetProperty("fadeColors").EnumerateArray().Select(x=>x.GetInt32()).ToImmutableArray();
        if(colors.Length!=12||colors.Any(x=>x is <0 or >32767))throw new InvalidDataException("Invalid capture fade colors.");
        var frames=data.GetProperty("trainerThrowFrames").EnumerateArray().Select(x=>new CaptureTrainerThrowFrame(x.GetProperty("frame").GetInt32(),x.GetProperty("ticks").GetInt32(),x.GetProperty("xOffset").GetInt32())).ToImmutableArray();
        if(frames.IsEmpty||frames.Any(f=>f.Frame is <0 or >4||f.Ticks is <1 or >60||f.XOffset is <-64 or >0))throw new InvalidDataException("Invalid trainer throw frames.");
        return new(Path.Combine(folder,"balls.png"),sine,columns.ToImmutable(),colors,frames);
    }
}

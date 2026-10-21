/// <summary>Logical frame ownership for the existing Pokédex turn/zoom programs.
/// Help suspends the program; leaving Pokédex cancels it rather than deferring it.</summary>
internal sealed class PokedexTransitionClock
{
    private double _frames;
    public int Kind { get; private set; }
    public bool Active => Kind != 0;
    public int Frame => (int)Math.Floor(_frames + 1e-7);
    public void Begin(int kind)
    {
        if (kind is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        _frames = 0;
    }
    public void Advance(double delta, bool paused)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (Active && !paused) _frames += delta * 60;
    }
    public void Reset() { Kind = 0; _frames = 0; }
}

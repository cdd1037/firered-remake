using Game.Domain.Options;

[Flags]
public enum CampaignButton { None = 0, A = 1, B = 2, Start = 4, Select = 8, Up = 16, Down = 32, Left = 64, Right = 128, L = 256, R = 512 }
public readonly record struct CampaignButtonEdge(CampaignButton Pressed, CampaignButton Released);

/// <summary>Physical owners retain their buttons. L=A adds A without removing L;
/// releasing one of several A owners cannot release their shared logical button.</summary>
public sealed class CampaignButtonInput
{
    private readonly Dictionary<(int Device, long Control), CampaignButton> _owners = [];
    public SourceButtonMode Mode { get; private set; }
    public CampaignButton Held { get; private set; }
    public CampaignButtonEdge Observe(int device, long control, CampaignButton button, bool pressed)
    {
        var before = Held;
        if (pressed) _owners[(device, control)] = button;
        else _owners.Remove((device, control));
        Recompute();
        return new(Held & ~before, before & ~Held);
    }
    public CampaignButtonEdge SetMode(SourceButtonMode mode)
    {
        var before = Held; Mode = mode; Recompute();
        return new(Held & ~before, before & ~Held);
    }
    public CampaignButtonEdge Disconnect(int device)
    {
        var before = Held;
        foreach (var owner in _owners.Keys.Where(k => k.Device == device).ToArray()) _owners.Remove(owner);
        Recompute(); return new(Held & ~before, before & ~Held);
    }
    public void Reset() { _owners.Clear(); Held = CampaignButton.None; }
    private void Recompute()
    {
        Held = CampaignButton.None;
        foreach (var button in _owners.Values) Held |= button;
        if (Mode == SourceButtonMode.LEqualsA && Held.HasFlag(CampaignButton.L)) Held |= CampaignButton.A;
    }
}

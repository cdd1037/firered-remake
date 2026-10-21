using System.Collections.Immutable;

/// <summary>Presentation-only Oak Pokéball release/return callbacks, using the shared source sine table.</summary>
public sealed class OpeningNidoranPresentation(bool returning, ImmutableArray<int> sine)
{
    public bool Returning { get; } = returning;
    public int Frame { get; private set; }
    public bool Complete => Frame >= (Returning ? 112 : 96);
    private int Age => Math.Max(0, Frame - 33);
    public bool TargetVisible => Returning ? Frame < 66 : Frame >= 33;
    public float Scale => Returning ? Math.Clamp((256 - Math.Min(18, Age) * 2 - Math.Max(0, Age - 18) * 16) / 256f, 0, 1) : Math.Min(256, 40 + Math.Min(12, Age) * 18) / 256f;
    private int Trig => Math.Min(128, Age * 4);
    private int Arc => Trig < 128 ? -(sine[Trig] / 8) : 0;
    public int X => Returning ? 96 : 100 + (-4 * Trig / 128) + Arc;
    public int Y => Returning ? 96 - (Age * 96 >> 8) - Math.Clamp(Frame - 40, 0, 23) : 66 + 30 * Trig / 128 + Arc;
    public bool BallVisible => Returning ? Frame < 64 : Frame < 43;
    public int BallFrame => Frame < 33 ? 0 : Returning && Frame >= 61 ? Frame < 66 ? 1 : 0 : Frame < 38 ? 1 : 2;
    public int ParticleAge => Frame - 33;
    public int TintBlend => Frame < 33 ? 0 : Math.Clamp(16 - Math.Max(0, Age - 34), 0, 16);
    public float BackgroundFlash => Complete || Frame < 33 ? 0 : Age < 34 ? Math.Min(16, Age / 2) / 16f : Math.Max(0, 16 - (Age - 34) / 2) / 16f;
    public void AdvanceFrame() { if (!Complete) Frame++; }
}

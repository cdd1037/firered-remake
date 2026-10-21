namespace Game.Presentation.BattleAnimations;

/// <summary>
/// Integer GBA BIOS SWI 0x0A output for FireRed's signed-16 vector inputs.
/// Behavioral reference: mGBA src/gba/bios.c, _ArcTan and _ArcTan2.
/// This is a behavioral implementation using the BIOS's fixed-point constants;
/// it does not use host trigonometry or floating-point rounding.
/// </summary>
public static class SourceBiosAngles
{
    public static ushort ArcTan2(short x, short y)
    {
        // Short arguments match the FireRed wrapper; arithmetic promotes to int.
        if (y == 0) return (ushort)(x < 0 ? 0x8000 : 0);
        if (x == 0) return (ushort)(y < 0 ? 0xC000 : 0x4000);

        // Signed division truncates toward zero. Keep the sign in the quotient:
        // the BIOS polynomial's final arithmetic shift is not odd-symmetric.
        int magnitudeX = x < 0 ? -x : x;
        int magnitudeY = y < 0 ? -y : y;
        int angle;
        if (magnitudeX >= magnitudeY)
        {
            int octant = Polynomial((y << 14) / x);
            angle = octant + (x < 0 ? 0x8000 : y < 0 ? 0x10000 : 0);
        }
        else
        {
            int octant = Polynomial((x << 14) / y);
            angle = (y < 0 ? 0xC000 : 0x4000) - octant;
        }
        // The original quadrant branch assigns negative diagonals differently;
        // both routes coincide exactly because Polynomial(+/-16384)=+/-8192.
        return unchecked((ushort)angle);
    }

    public static ushort ArcTan2Neg(short x, short y) => unchecked((ushort)-ArcTan2(x, y));

    private static int Polynomial(int tangent)
    {
        // ArcTan2 bounds tangent to [-16384,16384], so every product here
        // stays inside signed 32-bit range. >> must be arithmetic, not >>>.
        int square = -((tangent * tangent) >> 14);
        int accumulator = 0xA9;
        ReadOnlySpan<int> coefficients = [0x390, 0x91C, 0xFB6, 0x16AA, 0x2081, 0x3651, 0xA2F9];
        foreach (int coefficient in coefficients)
            accumulator = ((accumulator * square) >> 14) + coefficient;
        return (tangent * accumulator) >> 16;
    }
}

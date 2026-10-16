/* Independent integer BIOS behavior adapter for extracted FireRed callbacks.
 * Include this in the C oracle translation unit after standard headers. Its
 * int16_t/uint16_t signatures are compatible with FireRed s16/u16 aliases.
 * Source behavior: mGBA src/gba/bios.c _ArcTan/_ArcTan2, with the
 * BIOS fixed-point constants retained below.
 * No BIOS ROM, floating-point math, timing or register emulation is required.
 */
#include <stdint.h>

/* The adapter's compiler must implement arithmetic signed right shift. */
_Static_assert((-1 >> 1) == -1, "BIOS angle adapter needs arithmetic right shift");

static int32_t bios_angle_polynomial(int32_t tangent)
{
    const int32_t coefficients[] = {912, 2332, 4022, 5802, 8321, 13905, 41721};
    int32_t negative_square = -((tangent * tangent) >> 14);
    int32_t accumulator = 169;
    for (unsigned i = 0; i < sizeof(coefficients) / sizeof(coefficients[0]); ++i)
        accumulator = ((accumulator * negative_square) >> 14) + coefficients[i];
    return (tangent * accumulator) >> 16;
}

uint16_t ArcTan2(int16_t input_x, int16_t input_y)
{
    int32_t x = input_x, y = input_y;
    if (y == 0) return x < 0 ? 0x8000 : 0;
    if (x == 0) return y < 0 ? 0xC000 : 0x4000;
    int32_t magnitude_x = x < 0 ? -x : x;
    int32_t magnitude_y = y < 0 ? -y : y;
    int32_t angle;
    if (magnitude_x >= magnitude_y) {
        /* Multiplication avoids undefined left-shift of a negative C value.
         * For signed16 x/y, multiplying by 16384 cannot overflow int32. */
        int32_t a = bios_angle_polynomial((y * 16384) / x);
        angle = a + (x < 0 ? 0x8000 : y < 0 ? 0x10000 : 0);
    } else {
        int32_t a = bios_angle_polynomial((x * 16384) / y);
        angle = (y < 0 ? 0xC000 : 0x4000) - a;
    }
    return (uint16_t)angle;
}

uint16_t ArcTan2Neg(int16_t x, int16_t y)
{
    return (uint16_t)-ArcTan2(x, y);
}

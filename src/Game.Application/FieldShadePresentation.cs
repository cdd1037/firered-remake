namespace Game.Application;

/// <summary>Source field-weather NORMAL gamma table at index 3. Reserved BG
/// banks and ordinary field-object ALT palettes are unchanged.</summary>
public static class FieldShadePresentation
{
    public static int Channel(int rgb5)
    {
        if(rgb5 is <0 or >31)throw new ArgumentOutOfRangeException(nameof(rgb5));
        return (rgb5*13)>>4;
    }
    public static void TransformRgba(Span<byte> pixels)
    {
        if(pixels.Length%4!=0)throw new ArgumentException("Incomplete RGBA pixels.",nameof(pixels));
        for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]!=0)
            for(int c=0;c<3;c++){int value=Channel(pixels[i+c]>>3);pixels[i+c]=(byte)((value<<3)|(value>>2));}
    }
}

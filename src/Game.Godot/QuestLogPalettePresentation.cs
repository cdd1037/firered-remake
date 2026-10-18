namespace Game.Presentation;

/// <summary>Original field palette grayscale and Quest Log color restoration in RGB555.</summary>
public static class QuestLogPalettePresentation
{
    public static ushort Transform(ushort color,int coefficient)
    {
        if(coefficient is <0 or >16)throw new ArgumentOutOfRangeException(nameof(coefficient));
        if(coefficient==0)return color;
        int r=color&31,g=(color>>5)&31,b=(color>>10)&31;
        int gray=(76*r+151*g+29*b)>>8;
        r+=coefficient*(gray-r)>>4;
        g+=coefficient*(gray-g)>>4;
        b+=coefficient*(gray-b)>>4;
        return (ushort)((color&0x8000)|r|(g<<5)|(b<<10));
    }

    public static void TransformRgba(byte[] pixels,int coefficient,int blackCoefficient=0)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if(pixels.Length%4!=0)throw new ArgumentException("RGBA pixels must contain complete pixels.",nameof(pixels));
        if(coefficient is <0 or >16)throw new ArgumentOutOfRangeException(nameof(coefficient));
        if(blackCoefficient is <0 or >16)throw new ArgumentOutOfRangeException(nameof(blackCoefficient));
        if(coefficient==0&&blackCoefficient==0)return;
        for(int i=0;i<pixels.Length;i+=4)
        {
            if(pixels[i+3]==0)continue;
            ushort color=Transform((ushort)((pixels[i]>>3)|((pixels[i+1]>>3)<<5)|((pixels[i+2]>>3)<<10)),coefficient);
            for(int c=0;c<3;c++)
            {
                // Quest tones modify the normal palette before the field fade acts
                // on it. Both steps retain the original RGB555 integer rounding.
                int channel=(((color>>(5*c))&31)*(16-blackCoefficient))>>4;
                pixels[i+c]=(byte)((channel<<3)|(channel>>2));
            }
        }
    }
}

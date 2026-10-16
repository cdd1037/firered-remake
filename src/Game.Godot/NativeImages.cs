using Godot;

/// <summary>The engine owns PNG decoding. CPU pixels are requested only by pixel compositors.</summary>
public static class NativeImages
{
    public static long DecodedPngs { get; private set; }

    public static Image LoadPng(byte[] png, int? width = null, int? height = null)
    {
        var image = new Image();
        if (image.LoadPngFromBuffer(png) != Error.Ok || image.IsEmpty() ||
            width is { } w && image.GetWidth() != w || height is { } h && image.GetHeight() != h)
        {
            image.Dispose();
            throw new InvalidDataException("PNG could not be decoded or has unexpected dimensions.");
        }
        DecodedPngs++;
        image.Convert(Image.Format.Rgba8);
        return image;
    }

    public static byte[] ReadPixels(byte[] png, int? width = null, int? height = null)
    {
        using var image = LoadPng(png, width, height);
        return image.GetData();
    }

}

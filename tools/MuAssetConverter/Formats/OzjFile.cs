namespace MuAssetConverter.Formats;

/// <summary>
/// Decodes .OZJ and .OZT files — these are standard JPEG files with a
/// variable-length proprietary header prepended by WebZen.
/// Strategy: scan forward until the JPEG magic bytes (FF D8 FF) are found.
/// </summary>
public static class OzjFile
{
    private const byte JpegMagic0 = 0xFF;
    private const byte JpegMagic1 = 0xD8;
    private const byte JpegMagic2 = 0xFF;

    public static ReadOnlyMemory<byte> ExtractJpeg(string path)
    {
        var raw = File.ReadAllBytes(path);
        return ExtractJpeg(raw);
    }

    public static ReadOnlyMemory<byte> ExtractJpeg(ReadOnlySpan<byte> raw)
    {
        for (int i = 0; i <= raw.Length - 3; i++)
        {
            if (raw[i] == JpegMagic0 && raw[i + 1] == JpegMagic1 && raw[i + 2] == JpegMagic2)
            {
                return raw[i..].ToArray();
            }
        }

        throw new InvalidDataException("No JPEG magic bytes found — file may not be an OZJ/OZT.");
    }
}

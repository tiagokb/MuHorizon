namespace MuAssetConverter.Formats;

/// <summary>
/// Decodes .OZJ files — these are standard JPEG files with a variable-length
/// proprietary header prepended by WebZen.
///
/// Some OZJ files embed a small thumbnail JPEG before the main image. Each segment
/// has its own DQT (FF DB) marker. We always extract from the LAST DQT, which
/// belongs to the main high-resolution image, and prepend a fresh SOI (FF D8).
/// This avoids "Bad Quantization Table" errors that ImageSharp throws when it tries
/// to decode the thumbnail/metadata segment instead of the main image.
/// </summary>
public static class OzjFile
{
    public static byte[] ExtractJpeg(string path)
        => ExtractJpeg(File.ReadAllBytes(path));

    public static byte[] ExtractJpeg(ReadOnlySpan<byte> raw)
    {
        // Step 1 — skip the WebZen header: find the first SOI (FF D8 FF).
        int soiAt = -1;
        for (int i = 0; i <= raw.Length - 3; i++)
        {
            if (raw[i] == 0xFF && raw[i + 1] == 0xD8 && raw[i + 2] == 0xFF)
            {
                soiAt = i;
                break;
            }
        }
        if (soiAt < 0)
            throw new InvalidDataException("No JPEG magic bytes found — file may not be an OZJ.");

        var jpeg = raw[soiAt..];

        // Step 2 — multi-segment OZJ files (thumbnail + main) each have their own DQT.
        // Find the LAST DQT (FF DB) to skip over any thumbnail segments.
        int lastDqt = -1;
        for (int i = 0; i < jpeg.Length - 1; i++)
            if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xDB)
                lastDqt = i;

        // Single-DQT file: return from SOI as-is.
        if (lastDqt < 0)
            return jpeg.ToArray();

        // Multi-DQT: reconstruct SOI + (everything from the last DQT onward).
        var result = new byte[2 + jpeg.Length - lastDqt];
        result[0] = 0xFF;
        result[1] = 0xD8; // SOI
        jpeg[lastDqt..].CopyTo(result.AsSpan(2));
        return result;
    }
}
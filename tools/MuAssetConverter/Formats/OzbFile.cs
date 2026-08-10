namespace MuAssetConverter.Formats;

/// <summary>
/// Parses MU Online terrain heightmap (.OZB) files.
///
/// Two format variants observed in the wild:
///   Variant A (ushort): payload = 257×257×2 = 132098 bytes (Season 6 server builds)
///   Variant B (byte):   payload = 257×257×1 =  66049 bytes (common client builds)
///
/// Both variants may have a variable-length header; we detect it by subtracting the
/// known payload size from the total file size.
/// </summary>
public sealed class OzbFile
{
    public const int VertexCount = 257;

    public ushort[,] Heights { get; } = new ushort[VertexCount, VertexCount];
    public ushort Min { get; private set; } = ushort.MaxValue;
    public ushort Max { get; private set; } = ushort.MinValue;

    public static OzbFile Load(string path, byte xorKey = 0)
    {
        var raw = File.ReadAllBytes(path);
        const int payloadBytes = VertexCount * VertexCount;
        const int payloadUshorts = VertexCount * VertexCount * 2;

        if (raw.Length >= payloadUshorts)
            return LoadVariant(raw, raw.Length - payloadUshorts, sizeof(ushort), xorKey);

        if (raw.Length >= payloadBytes)
            return LoadVariant(raw, raw.Length - payloadBytes, sizeof(byte), xorKey);

        throw new InvalidDataException(
            $"OZB file too small: {raw.Length} bytes (need ≥{payloadBytes}).");
    }

    private static OzbFile LoadVariant(byte[] raw, int offset, int stride, byte xorKey)
    {
        var file = new OzbFile();
        var data = raw.AsSpan(offset);

        for (int y = 0; y < VertexCount; y++)
        {
            for (int x = 0; x < VertexCount; x++)
            {
                int idx = (y * VertexCount + x) * stride;
                ushort h;
                if (stride == 2)
                {
                    byte lo = (byte)(data[idx] ^ xorKey);
                    byte hi = (byte)(data[idx + 1] ^ xorKey);
                    h = (ushort)(lo | (hi << 8));
                }
                else
                {
                    h = (ushort)((byte)(data[idx] ^ xorKey));
                }

                file.Heights[x, y] = h;
                if (h < file.Min) file.Min = h;
                if (h > file.Max) file.Max = h;
            }
        }

        return file;
    }

    /// <summary>
    /// Returns height normalized to [0,1] for use in a grayscale PNG.
    /// </summary>
    public float NormalizedHeight(int x, int y)
    {
        if (Max == Min) return 0f;
        return (Heights[x, y] - Min) / (float)(Max - Min);
    }
}

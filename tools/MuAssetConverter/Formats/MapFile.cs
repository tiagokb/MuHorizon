namespace MuAssetConverter.Formats;

/// <summary>
/// Parses MU Online terrain mapping (.map) files.
///
/// Format:
///   [1 or 2 bytes header — version byte must be 0x00]
///   [256×256 = 65536 bytes]  Layer1 — primary tile index (into terrain texture array)
///   [256×256 = 65536 bytes]  Layer2 — secondary tile index (blend target)
///   [256×256 = 65536 bytes]  Alpha  — blend weight (0 = full Layer1, 255 = full Layer2)
///
/// Total: 1 + 196608 = 196609  (plain)
///   or   2 + 196608 = 196610  (when an extra crypto header byte is present)
///
/// EncTerrainN.map files are encrypted with MapFileDecrypt (same algorithm as EncTerrainN.obj).
/// </summary>
public sealed class MapFile
{
    public const int MapSize = 256;
    private const int DataSize = MapSize * MapSize; // one layer

    public byte[,] Layer1 { get; } = new byte[MapSize, MapSize];
    public byte[,] Layer2 { get; } = new byte[MapSize, MapSize];
    public byte[,] Alpha  { get; } = new byte[MapSize, MapSize];

    public static MapFile Load(string path)
    {
        var raw = File.ReadAllBytes(path);

        int? hdr = ResolveHeader(raw);
        if (hdr is null)
        {
            raw = ObjFile.Decrypt(raw);
            hdr = ResolveHeader(raw);
        }
        if (hdr is null)
            throw new InvalidDataException($"Terrain.map: unrecognised format ({raw.Length} bytes).");

        var file    = new MapFile();
        int offset  = hdr.Value;
        int stride  = DataSize;

        for (int y = 0; y < MapSize; y++)
        {
            for (int x = 0; x < MapSize; x++)
            {
                int i = y * MapSize + x;
                file.Layer1[x, y] = raw[offset + i];
                file.Layer2[x, y] = raw[offset + i + stride];
                file.Alpha[x, y]  = raw[offset + i + stride * 2];
            }
        }

        return file;
    }

    // Returns the header size if the buffer matches the expected layout, else null.
    private static int? ResolveHeader(byte[] buf)
    {
        if (buf.Length == 0 || buf[0] != 0) return null;
        foreach (var hdr in new[] { 1, 2 })
            if (buf.Length == hdr + 3 * DataSize) return hdr;
        return null;
    }
}
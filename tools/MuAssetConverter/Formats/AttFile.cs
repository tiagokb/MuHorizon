namespace MuAssetConverter.Formats;

/// <summary>
/// Parses MU Online terrain attribute (.ATT) files.
///
/// Format (Season 6 Episode 3):
///   [3 bytes header — ignored]
///   [256 × 256 bytes — row-major, index = x + y*256]
///
/// Tile values:
///   0 = walkable (open ground)
///   1 = walkable + safe zone
///   ≥2 = not walkable (wall / obstacle)
/// </summary>
public sealed class AttFile
{
    public const int MapSize = 256;
    private const int HeaderSize = 3;
    private const int DataSize = MapSize * MapSize;

    public byte[,] Tiles { get; } = new byte[MapSize, MapSize];

    public static AttFile Load(string path)
    {
        var raw = File.ReadAllBytes(path);
        if (raw.Length < HeaderSize + DataSize)
        {
            throw new InvalidDataException(
                $"ATT file too small: {raw.Length} bytes (expected ≥{HeaderSize + DataSize}).");
        }

        var file = new AttFile();
        var data = raw.AsSpan(HeaderSize, DataSize);

        for (int i = 0; i < DataSize; i++)
        {
            byte x = (byte)(i & 0xFF);
            byte y = (byte)((i >> 8) & 0xFF);
            file.Tiles[x, y] = data[i];
        }

        return file;
    }

    public bool IsWalkable(int x, int y) => Tiles[x, y] <= 1;
    public bool IsSafezone(int x, int y) => Tiles[x, y] == 1;
}
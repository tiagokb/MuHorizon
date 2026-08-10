namespace MuAssetConverter.Export;

using MuAssetConverter.Formats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class TerrainExporter
{
    /// <summary>
    /// Exports the walkable/safezone map to a grayscale PNG:
    ///   white (255)      = walkable open ground
    ///   mid-gray (128)   = safe zone (town areas)
    ///   black (0)        = blocked / wall
    /// </summary>
    public static void ExportWalkable(AttFile att, string outputPath)
    {
        using var img = new Image<L8>(AttFile.MapSize, AttFile.MapSize);

        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < AttFile.MapSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < AttFile.MapSize; x++)
                {
                    byte v = att.Tiles[x, y] switch
                    {
                        0 => 255,
                        1 => 128,
                        _ => 0,
                    };
                    row[x] = new L8(v);
                }
            }
        });

        img.SaveAsPng(outputPath);
    }

    /// <summary>
    /// Exports the heightmap to a grayscale PNG (normalized: darkest = lowest point).
    /// Output is 257×257 (vertex grid). If all heights are equal, outputs solid black.
    /// </summary>
    public static void ExportHeightmap(OzbFile ozb, string outputPath)
    {
        using var img = new Image<L8>(OzbFile.VertexCount, OzbFile.VertexCount);

        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < OzbFile.VertexCount; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < OzbFile.VertexCount; x++)
                {
                    byte v = (byte)(ozb.NormalizedHeight(x, y) * 255f);
                    row[x] = new L8(v);
                }
            }
        });

        img.SaveAsPng(outputPath);
    }

    /// <summary>
    /// Decodes an OZJ file (JPEG with optional prepended header) and saves as PNG.
    /// </summary>
    public static void ExportOzj(string ozjPath, string outputPath)
    {
        var jpeg = OzjFile.ExtractJpeg(ozjPath);
        using var ms = new MemoryStream(jpeg.ToArray());
        using var img = Image.Load(ms);
        img.SaveAsPng(outputPath);
    }

    /// <summary>
    /// Decodes an OZT file (TGA with 4-byte prepended header) and saves as PNG.
    /// </summary>
    public static void ExportOzt(string oztPath, string outputPath)
    {
        var raw = File.ReadAllBytes(oztPath);
        // OZT = 4-byte WebZen header + standard TGA
        const int HeaderSize = 4;
        if (raw.Length <= HeaderSize)
            throw new InvalidDataException($"OZT file too small: {raw.Length} bytes.");

        using var ms = new MemoryStream(raw, HeaderSize, raw.Length - HeaderSize, writable: false);
        using var img = Image.Load(ms);
        img.SaveAsPng(outputPath);
    }
}

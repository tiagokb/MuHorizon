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
    /// Exports the three splatmap layers from a terrain .map file.
    /// Outputs mapping_layer1.png, mapping_layer2.png, mapping_alpha.png —
    /// each a 256×256 grayscale PNG where R * 255 = tile texture array index.
    /// </summary>
    public static void ExportSplatmap(MapFile map, string outputDir)
    {
        const int Size = 256;

        using var l1 = new Image<L8>(Size, Size);
        using var l2 = new Image<L8>(Size, Size);
        using var al = new Image<L8>(Size, Size);

        l1.ProcessPixelRows(l2, al, (a1, a2, aA) =>
        {
            for (int y = 0; y < Size; y++)
            {
                var r1 = a1.GetRowSpan(y);
                var r2 = a2.GetRowSpan(y);
                var rA = aA.GetRowSpan(y);
                for (int x = 0; x < Size; x++)
                {
                    r1[x] = new L8(map.Layer1[x, y]);
                    r2[x] = new L8(map.Layer2[x, y]);
                    rA[x] = new L8(map.Alpha[x, y]);
                }
            }
        });

        l1.SaveAsPng(Path.Combine(outputDir, "mapping_layer1.png"));
        l2.SaveAsPng(Path.Combine(outputDir, "mapping_layer2.png"));
        al.SaveAsPng(Path.Combine(outputDir, "mapping_alpha.png"));
    }

    /// <summary>
    /// Decodes an OZJ file (JPEG with optional prepended header) and saves as PNG.
    /// </summary>
    public static void ExportOzj(string ozjPath, string outputPath)
    {
        var jpeg = OzjFile.ExtractJpeg(ozjPath);
        using var ms  = new MemoryStream(jpeg);
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

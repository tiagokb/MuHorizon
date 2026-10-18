using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuAssetConverter.Formats;

/// <summary>
/// Parses MU Online terrain object placement (.obj) files.
///
/// Two variants:
///   Plain  (Terrain.obj)   — no encryption, starts with Version byte directly
///   Encrypted (EncTerrainN.obj) — MapFileDecrypt then same layout
///
/// Decrypted layout:
///   [0]      byte  Version    (unused)
///   [1]      byte  MapNumber  (matches world index, e.g. 1 for World1)
///   [2–3]    short Count      (number of objects, little-endian)
///   Per object (30 bytes):
///     [0–1]  short  Type      (indexes into Object1/*.bmd list)
///     [2–13] float3 Position  (X, Y, Z in world units; tile = units / 100)
///     [14–25]float3 Angle     (degrees, Euler XYZ)
///     [26–29]float  Scale
/// </summary>
public sealed class ObjFile
{
    public int MapNumber { get; }
    public MapObject[] Objects { get; }

    private ObjFile(int mapNumber, MapObject[] objects)
    {
        MapNumber = mapNumber;
        Objects   = objects;
    }

    // ------------------------------------------------------------------ load

    public static ObjFile LoadEncrypted(string path)
    {
        var enc  = File.ReadAllBytes(path);
        var data = Decrypt(enc);
        return Parse(data);
    }

    public static ObjFile LoadPlain(string path)
    {
        return Parse(File.ReadAllBytes(path));
    }

    // ------------------------------------------------------------------ export

    public void WriteJson(string outPath)
    {
        // Some legacy files (e.g. Devias' EncTerrain3.obj) hold corrupted entries with infinite or
        // NaN coordinates, which JSON can't represent; they aren't placeable anyway, so skip them.
        var valid = Array.FindAll(Objects, o => o.IsFinite);
        if (valid.Length < Objects.Length)
            Console.WriteLine($"  skipped {Objects.Length - valid.Length} object(s) with non-finite values");

        var opts = new JsonSerializerOptions { WriteIndented = true };
        using var fs = File.Create(outPath);
        JsonSerializer.Serialize(fs, new ObjFileJson(MapNumber, valid), opts);
    }


    // ------------------------------------------------------------------ model resolution

    /// <summary>
    /// Returns the GLB stem (relative path from assets/models/, no extension) for a given
    /// object type and map number, e.g. "Object1/Tree01" or null when unknown.
    ///
    /// World 1 (Lorencia, WorldActive=0) uses named BMD files per category.
    /// All other worlds use the generic pattern Object{world}/{prefix}{type+1:D2}.
    /// </summary>
    public static string? ResolveModel(int mapNumber, int type)
    {
        if (type < 0 || type >= 160) return null;

        if (mapNumber == 1)
            return World1ModelMap.TryGetValue(type, out var stem) ? $"Object1/{stem}" : null;

        int worldNum = mapNumber;
        return $"Object{worldNum}/Object{type + 1:D2}";
    }

    // Map from Type → BMD stem for World1 (Lorencia, WorldActive=0).
    // Source: _enum.h enum + MapManager.cpp CMapManager::Load() case WD_0LORENCIA.
    private static readonly Dictionary<int, string> World1ModelMap = BuildWorld1Map();

    private static Dictionary<int, string> BuildWorld1Map()
    {
        var m = new Dictionary<int, string>();
        AddRange(m,   0, "Tree",           1, 13);
        AddRange(m,  20, "Grass",          1,  8);
        AddRange(m,  30, "Stone",          1,  5);
        AddRange(m,  40, "StoneStatue",    1,  3);
        m[43]  = "SteelStatue01";
        AddRange(m,  44, "Tomb",           1,  3);
        AddRange(m,  50, "FireLight",      1,  2);
        m[52]  = "Bonfire01";
        m[55]  = "DoungeonGate01";
        AddRange(m,  56, "MerchantAnimal", 1,  2);
        m[58]  = "TreasureDrum01";
        m[59]  = "TreasureChest01";
        m[60]  = "Ship01";
        AddRange(m,  65, "SteelWall",      1,  3);
        m[68]  = "SteelDoor01";
        AddRange(m,  69, "StoneWall",      1,  6);
        AddRange(m,  75, "StoneMuWall",    1,  4);
        m[80]  = "Bridge01";
        AddRange(m,  81, "Fence",          1,  4);
        m[85]  = "BridgeStone01";
        m[90]  = "StreetLight01";
        AddRange(m,  91, "Cannon",         1,  3);
        m[95]  = "Curtain01";
        AddRange(m,  96, "Sign",           1,  2);
        AddRange(m,  98, "Carriage",       1,  4);
        AddRange(m, 102, "Straw",          1,  2);
        m[105] = "Waterspout01";
        AddRange(m, 106, "Well",           1,  4);
        m[110] = "Hanging01";
        m[111] = "Stair01";
        AddRange(m, 115, "House",          1,  5);
        m[120] = "Tent01";
        AddRange(m, 121, "HouseWall",      1,  6);
        AddRange(m, 127, "HouseEtc",       1,  3);
        AddRange(m, 130, "Light",          1,  3);
        m[133] = "PoseBox01";
        AddRange(m, 140, "Furniture",      1,  7);
        m[150] = "Candle01";
        AddRange(m, 151, "Beer",           1,  3);
        return m;
    }

    private static void AddRange(Dictionary<int, string> m, int baseType, string prefix, int first, int count)
    {
        for (int i = 0; i < count; i++)
            m[baseType + i] = $"{prefix}{first + i:D2}";
    }

    // ------------------------------------------------------------------ internals

    private static ObjFile Parse(byte[] data)
    {
        int ptr = 0;
        ptr += 1;                                                   // Version (skip)
        int   mapNumber = data[ptr++];
        short count     = MemoryMarshal.Read<short>(data.AsSpan(ptr, 2)); ptr += 2;

        if (count < 0)
            throw new InvalidDataException($"Object count is negative ({count}); file may be corrupt.");

        const int ObjectBytes = 2 + 12 + 12 + 4; // Type + Position + Angle + Scale
        int expectedBytes = 4 + count * ObjectBytes;
        if (data.Length < expectedBytes)
            throw new InvalidDataException(
                $"OBJ data too short: {data.Length} bytes, expected ≥{expectedBytes} for {count} objects.");

        var objects = new MapObject[count];
        for (int i = 0; i < count; i++)
        {
            short type  = MemoryMarshal.Read<short>(data.AsSpan(ptr, 2)); ptr += 2;
            float px    = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;
            float py    = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;
            float pz    = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;
            float ax    = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;
            float ay    = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;
            float az    = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;
            float scale = MemoryMarshal.Read<float>(data.AsSpan(ptr, 4)); ptr += 4;

            // Convert from MU world units to tile space (TERRAIN_SCALE = 100)
            objects[i] = new MapObject(
                Type:   type,
                TileX:  px / 100f,
                TileY:  py / 100f,
                AltitudeUnits: pz,
                AngleX: ax,
                AngleY: ay,
                AngleZ: az,
                Scale:  scale);
        }

        return new ObjFile(mapNumber, objects);
    }

    /// <summary>
    /// MapFileDecrypt from ZzzLodTerrain.h — XOR key + chained byte subtraction.
    /// Shared with MapFile for .map splatmap decryption.
    /// </summary>
    internal static byte[] Decrypt(byte[] src)
    {
        ReadOnlySpan<byte> xorKey = [
            0xD1, 0x73, 0x52, 0xF6, 0xD2, 0x9A, 0xCB, 0x27,
            0x3E, 0xAF, 0x59, 0x31, 0x37, 0xB3, 0xE7, 0xA2
        ];

        var  dst = new byte[src.Length];
        uint key = 0x5E;
        for (int i = 0; i < src.Length; i++)
        {
            dst[i] = unchecked((byte)((src[i] ^ xorKey[i % 16]) - key));
            key    = (uint)((src[i] + 0x3D) & 0xFF);
        }
        return dst;
    }
}

// ------------------------------------------------------------------ data types

public readonly record struct MapObject(
    int   Type,
    float TileX,
    float TileY,
    float AltitudeUnits,
    float AngleX,
    float AngleY,
    float AngleZ,
    float Scale)
{
    public bool IsFinite =>
        float.IsFinite(TileX) && float.IsFinite(TileY) && float.IsFinite(AltitudeUnits)
        && float.IsFinite(AngleX) && float.IsFinite(AngleY) && float.IsFinite(AngleZ) && float.IsFinite(Scale);
}

// ------------------------------------------------------------------ JSON layout

file sealed class ObjFileJson(int mapNumber, MapObject[] objects)
{
    [JsonPropertyName("mapNumber")] public int       MapNumber { get; } = mapNumber;
    [JsonPropertyName("objects")]   public ObjJson[] Objects   { get; } = Array.ConvertAll(objects, o => new ObjJson(mapNumber, o));
}

file sealed class ObjJson(int mapNumber, MapObject o)
{
    [JsonPropertyName("type")]     public int     Type          { get; } = o.Type;
    [JsonPropertyName("model")]    public string? Model         { get; } = ObjFile.ResolveModel(mapNumber, o.Type);
    [JsonPropertyName("tileX")]    public float   TileX         { get; } = MathF.Round(o.TileX, 4);
    [JsonPropertyName("tileY")]    public float   TileY         { get; } = MathF.Round(o.TileY, 4);
    [JsonPropertyName("altUnits")] public float   AltitudeUnits { get; } = o.AltitudeUnits;
    [JsonPropertyName("angleX")]   public float   AngleX        { get; } = MathF.Round(o.AngleX, 3);
    [JsonPropertyName("angleY")]   public float   AngleY        { get; } = MathF.Round(o.AngleY, 3);
    [JsonPropertyName("angleZ")]   public float   AngleZ        { get; } = MathF.Round(o.AngleZ, 3);
    [JsonPropertyName("scale")]    public float   Scale         { get; } = MathF.Round(o.Scale,  4);
}
using MuAssetConverter.Export;
using MuAssetConverter.Formats;

// muconv --input <mu-data-dir> --output <godot-res-dir> [--map <world-dir-name>] [--xor <key>]
//
// Example:
//   muconv --input "C:/MUOnline/Data" --output "res://assets/terrain" --map "World1"
//   muconv --input "C:/MUOnline/Data" --output "out" --map World1 --xor FD
//
// Output layout inside <output>/<map>/:
//   walkable.png    — 256×256 grayscale: white=walkable, mid-gray=safezone, black=wall
//   heightmap.png   — 257×257 grayscale normalized heightmap
//   texture_*.png   — decoded OZJ/OZT textures

string? inputDir = null;
string? outputDir = null;
string? mapName = null;
byte xorKey = 0;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input":  inputDir  = args[++i]; break;
        case "--output": outputDir = args[++i]; break;
        case "--map":    mapName   = args[++i]; break;
        case "--xor":    xorKey    = Convert.ToByte(args[++i], 16); break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (inputDir is null || outputDir is null)
{
    PrintUsage();
    return 1;
}

var mapsToProcess = new List<string>();

if (mapName is not null)
{
    mapsToProcess.Add(mapName);
}
else
{
    // Auto-discover: any sub-directory that contains a .att file
    foreach (var dir in Directory.EnumerateDirectories(inputDir))
    {
        if (Directory.EnumerateFiles(dir, "*.att", SearchOption.TopDirectoryOnly).Any() ||
            Directory.EnumerateFiles(dir, "*.ATT", SearchOption.TopDirectoryOnly).Any())
        {
            mapsToProcess.Add(Path.GetFileName(dir));
        }
    }

    if (mapsToProcess.Count == 0)
    {
        Console.Error.WriteLine($"No map directories found in '{inputDir}'. Use --map to specify one explicitly.");
        return 1;
    }

    Console.WriteLine($"Found {mapsToProcess.Count} map(s): {string.Join(", ", mapsToProcess)}");
}

int processed = 0;
int errors = 0;

foreach (var map in mapsToProcess)
{
    var mapDir = Path.Combine(inputDir, map);
    if (!Directory.Exists(mapDir))
    {
        Console.Error.WriteLine($"[{map}] Directory not found: {mapDir}");
        errors++;
        continue;
    }

    var outMapDir = Path.Combine(outputDir, map);
    Directory.CreateDirectory(outMapDir);

    Console.WriteLine($"[{map}] Processing...");

    // --- ATT → walkable.png ---
    var attPath = FindFile(mapDir, "*.att");
    if (attPath is not null)
    {
        try
        {
            var att = AttFile.Load(attPath);
            var outPath = Path.Combine(outMapDir, "walkable.png");
            TerrainExporter.ExportWalkable(att, outPath);
            Console.WriteLine($"  walkable.png  ({AttFile.MapSize}×{AttFile.MapSize})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] ATT failed: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"  [SKIP] No .att file found");
    }

    // --- OZB → heightmap.png ---
    var ozbPath = FindFile(mapDir, "*.ozb");
    if (ozbPath is not null)
    {
        try
        {
            var ozb = OzbFile.Load(ozbPath, xorKey);
            var outPath = Path.Combine(outMapDir, "heightmap.png");
            TerrainExporter.ExportHeightmap(ozb, outPath);
            Console.WriteLine($"  heightmap.png ({OzbFile.VertexCount}×{OzbFile.VertexCount}, height range {ozb.Min}–{ozb.Max})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] OZB failed: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"  [SKIP] No .ozb file found");
    }

    // --- OZJ/OZT → texture_*.png ---
    var textureFiles = Directory.EnumerateFiles(mapDir)
        .Where(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            return ext is ".ozj" or ".ozt";
        })
        .OrderBy(f => f)
        .ToList();

    int texCount = 0;
    var seenStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var texPath in textureFiles)
    {
        var ext = Path.GetExtension(texPath).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(texPath);
        // deduplicate: prefer OZJ over OZT for same stem
        if (ext == ".ozt" && seenStems.Contains(stem)) continue;
        seenStems.Add(stem);

        var outPath = Path.Combine(outMapDir, $"texture_{stem}.png");
        try
        {
            if (ext == ".ozt")
                TerrainExporter.ExportOzt(texPath, outPath);
            else
                TerrainExporter.ExportOzj(texPath, outPath);
            texCount++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] {stem}{ext}: [{ex.GetType().Name}] {ex.Message}");
        }
    }

    if (texCount > 0)
        Console.WriteLine($"  {texCount} texture(s) exported");

    processed++;
}

Console.WriteLine($"\nDone. {processed} map(s) processed, {errors} error(s).");
return errors > 0 ? 1 : 0;

static string? FindFile(string dir, string pattern)
{
    var result = Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
    if (result is not null) return result;

    // Try uppercase variant on case-sensitive file systems
    var upper = pattern.ToUpperInvariant();
    return Directory.EnumerateFiles(dir, upper, SearchOption.TopDirectoryOnly).FirstOrDefault();
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage: muconv --input <mu-data-dir> --output <godot-res-dir> [OPTIONS]

        Options:
          --map <name>    Process only this map sub-directory (e.g. World1)
          --xor <hex>     XOR key for OZB height decryption (e.g. FD); default: 0

        Output per map:
          <output>/<map>/walkable.png    256×256 walkable grid (white/gray/black)
          <output>/<map>/heightmap.png   257×257 normalized height grid
          <output>/<map>/texture_*.png   decoded terrain textures
        """);
}
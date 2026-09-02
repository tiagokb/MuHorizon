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

// --bmd <input.bmd> <output.glb> [--data-dir <dir>]
//   --data-dir  root of the MU data tree to scan for textures (default: grandparent of the BMD)
if (args.Length >= 3 && args[0] == "--bmd")
{
    string bmdFull  = Path.GetFullPath(args[1]);
    string bmdDir   = Path.GetDirectoryName(bmdFull)!;
    // Default: scan the parent of the BMD's folder (e.g. Data/ when BMD is in Data/NPC/)
    string dataRoot = Path.GetDirectoryName(bmdDir) ?? bmdDir;
    for (int i = 3; i < args.Length - 1; i++)
        if (args[i] is "--data-dir" or "--texture-dir") dataRoot = args[i + 1];

    Console.Write($"Indexing textures from '{dataRoot}'... ");
    var texIndex = BmdGltfExporter.BuildTextureIndex(dataRoot);
    Console.WriteLine($"{texIndex.Count} found.");

    var bmd = BmdFile.Load(bmdFull);
    bool isPlayer = Path.GetFileNameWithoutExtension(bmdFull).Equals("player", StringComparison.OrdinalIgnoreCase);
    BmdGltfExporter.Export(bmd, args[2], texIndex);
    var manifest = BmdGltfExporter.BuildManifest(bmd, Path.GetFileName(bmdFull),
        labels: isPlayer ? ModelManifest.PlayerActionLabels : null);
    manifest.WriteTo(Path.ChangeExtension(args[2], ".manifest.json"));
    Console.WriteLine($"Exported: {args[2]}  (meshes={bmd.Meshes.Length} bones={bmd.Bones.Length} actions={bmd.Actions.Length})");
    return 0;
}

// --bmd-dir <input-dir> <output-dir> [--data-dir <dir>]
//   Batch-exports every .bmd in <input-dir> as <output-dir>/<stem>.glb.
//   Texture index is built once from --data-dir (default: parent of input-dir).
if (args.Length >= 3 && args[0] == "--bmd-dir")
{
    string inDir    = Path.GetFullPath(args[1]);
    string outDir   = Path.GetFullPath(args[2]);
    string dataRoot = Path.GetDirectoryName(inDir) ?? inDir;
    for (int i = 3; i < args.Length - 1; i++)
        if (args[i] is "--data-dir") dataRoot = args[i + 1];

    Directory.CreateDirectory(outDir);

    Console.Write($"Indexing textures from '{dataRoot}'... ");
    var texIndex = BmdGltfExporter.BuildTextureIndex(dataRoot);
    Console.WriteLine($"{texIndex.Count} found.");

    var bmdFiles = Directory.EnumerateFiles(inDir, "*.bmd", SearchOption.TopDirectoryOnly).ToList();
    Console.WriteLine($"Exporting {bmdFiles.Count} BMD(s) from '{Path.GetFileName(inDir)}'...");

    int ok = 0, fail = 0;
    foreach (var bmdPath in bmdFiles)
    {
        string stem   = Path.GetFileNameWithoutExtension(bmdPath);
        string outGlb = Path.Combine(outDir, stem + ".glb");
        try
        {
            var bmd = BmdFile.Load(bmdPath);
            bool isPlayer = stem.Equals("player", StringComparison.OrdinalIgnoreCase);
            BmdGltfExporter.Export(bmd, outGlb, texIndex);
            var manifest = BmdGltfExporter.BuildManifest(bmd, stem + ".bmd",
                labels: isPlayer ? ModelManifest.PlayerActionLabels : null);
            manifest.WriteTo(Path.ChangeExtension(outGlb, ".manifest.json"));
            ok++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  FAIL {stem}.bmd: {ex.GetType().Name}: {ex.Message}");
            fail++;
        }
    }
    Console.WriteLine($"Done. {ok} exported, {fail} failed.");
    return fail > 0 ? 1 : 0;
}

// --bake-player <player-bmd-dir> <output-dir> [--data-dir <dir>]
//   Generates composed player GLBs by combining player.bmd (skeleton + animations) with
//   the default outfit parts for each class group.
//   Output:
//     player_class.glb  — Dark Wizard (HelmClass01 + ArmorClass01 + PantClass01 + GloveClass01 + BootClass01)
//     player_male.glb   — Dark Knight (HelmMale01  + ArmorMale01  + PantMale01  + GloveMale01  + BootMale01)
//     player_elf.glb    — Elf         (HelmElf01   + ArmorElf01   + PantElf01   + GloveElf01   + BootElf01)
//     player_monk.glb   — Rage Fighter (HelmMonk01 + ArmorMonk01 + PantMonk01  + BootMonk01; missing parts skipped)
if (args.Length >= 3 && args[0] == "--bake-player")
{
    string playerDir = Path.GetFullPath(args[1]);
    string outDir    = Path.GetFullPath(args[2]);
    string dataRoot  = Path.GetDirectoryName(playerDir) ?? playerDir;
    for (int i = 3; i < args.Length - 1; i++)
        if (args[i] is "--data-dir") dataRoot = args[i + 1];

    Directory.CreateDirectory(outDir);

    Console.Write($"Indexing textures from '{dataRoot}'... ");
    var texIndex = BmdGltfExporter.BuildTextureIndex(dataRoot);
    Console.WriteLine($"{texIndex.Count} found.");

    var skeletonPath = Path.Combine(playerDir, "player.bmd");
    if (!File.Exists(skeletonPath))
    {
        Console.Error.WriteLine($"ERROR: skeleton not found at '{skeletonPath}'");
        return 1;
    }
    var skeleton = BmdFile.Load(skeletonPath);
    Console.WriteLine($"Skeleton: player.bmd  (bones={skeleton.Bones.Length} actions={skeleton.Actions.Length})");

    static BmdFile? TryLoadPart(string dir, string name)
    {
        var path = Path.Combine(dir, name + ".bmd");
        if (!File.Exists(path)) { Console.WriteLine($"  [SKIP] {name}.bmd not found"); return null; }
        return BmdFile.Load(path);
    }

    static int BakeOutfit(BmdFile skeleton, (string Name, BmdFile? File)[] parts, string outPath,
        string group, IReadOnlyDictionary<string, string> texIndex)
    {
        var valid      = parts.Where(p => p.File is not null).ToList();
        int totalMeshes = valid.Sum(p => p.File!.Meshes.Length);
        Console.WriteLine($"  Parts: {valid.Count}/5  Meshes: {totalMeshes}");
        BmdGltfExporter.ExportCharacterBake(skeleton, valid.Select(p => p.File!), outPath, texIndex);
        var partNames = valid.Select(p => p.Name).ToArray();
        var manifest = BmdGltfExporter.BuildManifest(skeleton, "player.bmd", type: "bake", group: group,
            parts: partNames, labels: ModelManifest.PlayerActionLabels);
        manifest.WriteTo(Path.ChangeExtension(outPath, ".manifest.json"));
        Console.WriteLine($"  -> {Path.GetFileName(outPath)}");
        return 0;
    }

    int fail = 0;

    // Dark Wizard
    Console.WriteLine("\n[player_class] Dark Wizard default outfit:");
    try
    {
        BakeOutfit(skeleton, [
            ("HelmClass01",  TryLoadPart(playerDir, "HelmClass01")),
            ("ArmorClass01", TryLoadPart(playerDir, "ArmorClass01")),
            ("PantClass01",  TryLoadPart(playerDir, "PantClass01")),
            ("GloveClass01", TryLoadPart(playerDir, "GloveClass01")),
            ("BootClass01",  TryLoadPart(playerDir, "BootClass01")),
        ], Path.Combine(outDir, "player_class.glb"), "class", texIndex);
    }
    catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }

    // Dark Knight
    Console.WriteLine("\n[player_male] Dark Knight default outfit:");
    try
    {
        BakeOutfit(skeleton, [
            ("HelmMale01",  TryLoadPart(playerDir, "HelmMale01")),
            ("ArmorMale01", TryLoadPart(playerDir, "ArmorMale01")),
            ("PantMale01",  TryLoadPart(playerDir, "PantMale01")),
            ("GloveMale01", TryLoadPart(playerDir, "GloveMale01")),
            ("BootMale01",  TryLoadPart(playerDir, "BootMale01")),
        ], Path.Combine(outDir, "player_male.glb"), "male", texIndex);
    }
    catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }

    // Elf
    Console.WriteLine("\n[player_elf] Elf default outfit:");
    try
    {
        BakeOutfit(skeleton, [
            ("HelmElf01",  TryLoadPart(playerDir, "HelmElf01")),
            ("ArmorElf01", TryLoadPart(playerDir, "ArmorElf01")),
            ("PantElf01",  TryLoadPart(playerDir, "PantElf01")),
            ("GloveElf01", TryLoadPart(playerDir, "GloveElf01")),
            ("BootElf01",  TryLoadPart(playerDir, "BootElf01")),
        ], Path.Combine(outDir, "player_elf.glb"), "elf", texIndex);
    }
    catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }

    // Rage Fighter (GloveMonk doesn't exist — falls back to GloveMale01)
    Console.WriteLine("\n[player_monk] Rage Fighter default outfit:");
    try
    {
        var gloveMonk = TryLoadPart(playerDir, "GloveMonk01");
        BakeOutfit(skeleton, [
            ("HelmMonk01",  TryLoadPart(playerDir, "HelmMonk01")),
            ("ArmorMonk01", TryLoadPart(playerDir, "ArmorMonk01")),
            ("PantMonk01",  TryLoadPart(playerDir, "PantMonk01")),
            (gloveMonk is not null ? "GloveMonk01" : "GloveMale01",
             gloveMonk ?? TryLoadPart(playerDir, "GloveMale01")),
            ("BootMonk01",  TryLoadPart(playerDir, "BootMonk01")),
        ], Path.Combine(outDir, "player_monk.glb"), "monk", texIndex);
    }
    catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }

    Console.WriteLine($"\nDone. {4 - fail} GLB(s) exported, {fail} failed.");
    return fail > 0 ? 1 : 0;
}

// --debug-bmd <input.bmd>  — dumps raw vertex/bone data without exporting
if (args.Length == 2 && args[0] == "--debug-bmd")
{
    var bmd = BmdFile.Load(args[1]);
    BmdGltfExporter.DumpDebug(bmd);
    return 0;
}

// --obj <input.obj> <output.json> [--plain]
//   Parses EncTerrainN.obj (or plain Terrain.obj with --plain) and writes a JSON
//   object list for Godot.
//   Coordinates in JSON: tileX/tileY are in tile space (0–255); altUnits is raw
//   MU world-unit altitude (divide by 100 to convert to tiles); angles are degrees.
if (args.Length >= 3 && args[0] == "--obj")
{
    bool plain = args.Contains("--plain");
    var  obj   = plain ? ObjFile.LoadPlain(args[1]) : ObjFile.LoadEncrypted(args[1]);
    obj.WriteJson(args[2]);
    Console.WriteLine($"Exported: {args[2]}  (map={obj.MapNumber} objects={obj.Objects.Length})");
    return 0;
}

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
    // Prefer plain (unencrypted) ATT files; encrypted variants (EncTerrain*.att) contain raw
    // encrypted bytes that AttFile.Load reads as-is, producing all-blocked output.
    var attPath = Directory.EnumerateFiles(mapDir, "*.att", SearchOption.TopDirectoryOnly)
        .OrderBy(f => Path.GetFileName(f).StartsWith("Enc", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
        .FirstOrDefault();
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

    // --- Terrain.map → mapping_layer1/2/alpha.png ---
    // Prefer plain Terrain.map over EncTerrainN.map (both carry same data; plain is unencrypted).
    var mapPath = FindFile(mapDir, "Terrain.map")
               ?? FindFile(mapDir, "Terrain2.map")
               ?? FindFile(mapDir, "*.map");
    if (mapPath is not null)
    {
        try
        {
            var mapFile = MapFile.Load(mapPath);
            TerrainExporter.ExportSplatmap(mapFile, outMapDir);
            Console.WriteLine($"  mapping_layer1/2/alpha.png (256×256, splatmap)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] Terrain.map failed: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"  [SKIP] No .map file found");
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

    // Export grass billboard textures (OZT with alpha) before deduplication so we can
    // grab them even when an OZJ takes priority for the flat tile layer.
    // Output: grass_{stem}.png — loaded separately by TerrainMesh for billboard grass.
    foreach (var texPath in textureFiles)
    {
        var ext  = Path.GetExtension(texPath).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(texPath);
        if (ext != ".ozt" || !stem.StartsWith("TileGrass", StringComparison.OrdinalIgnoreCase)) continue;
        var grassOut = Path.Combine(outMapDir, $"grass_{stem}.png");
        try   { TerrainExporter.ExportOzt(texPath, grassOut); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] grass_{stem}: [{ex.GetType().Name}] {ex.Message}");
        }
    }

    int texCount = 0;
    // Build a deduplicated map stem → best file, preferring OZJ > OZT for terrain tiles.
    // OZJ (JPEG) = full-size square opaque ground texture.
    // OZT (TGA RGBA) = smaller 256×64 billboard variant — handled separately above.
    var bestTex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var texPath in textureFiles)
    {
        var ext  = Path.GetExtension(texPath).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(texPath);
        if (!bestTex.TryGetValue(stem, out var existing))
        {
            bestTex[stem] = texPath;
        }
        else
        {
            var curExt = Path.GetExtension(existing).ToLowerInvariant();
            if (ext == ".ozj" && curExt != ".ozj") bestTex[stem] = texPath;
        }
    }
    foreach (var (stem, texPath) in bestTex.OrderBy(kv => kv.Key))
    {
        var ext = Path.GetExtension(texPath).ToLowerInvariant();

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
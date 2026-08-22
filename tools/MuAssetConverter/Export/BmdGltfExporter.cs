using System.Numerics;
using MuAssetConverter.Formats;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;
using SixLabors.ImageSharp;

namespace MuAssetConverter.Export;

// Aliases kept short — these generic combos are long.
using GVB    = VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>;
using MeshBld = MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>;

/// <summary>
/// Converts a parsed <see cref="BmdFile"/> to a binary glTF (.glb) file.
///
/// Coordinate system:
///   BMD uses Z-up (characters stand along +Z).
///   glTF 2.0 and Godot both use Y-up.
///   Conversion: (x,y,z) → (x,z,−y), baked into every vertex and bone transform.
///
/// Vertex positions:
///   BMD stores vertex positions in bone-local space.  Each vertex is transformed
///   by its bone's bind-pose world matrix before writing to glTF (which requires
///   model-space positions).
/// </summary>
public static class BmdGltfExporter
{
    private const float Fps = 24f;

    // −90° around X: converts BMD Z-up into glTF Y-up.
    private static readonly Quaternion CoordFix =
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2f);

    private static Vector3    YUp(Vector3 v)    => new(v.X, v.Z, -v.Y);
    private static Quaternion YUp(Quaternion q) =>
        Quaternion.Normalize(CoordFix * q * Quaternion.Conjugate(CoordFix));

    // -------------------------------------------------------------------------
    //  Public entry point
    // -------------------------------------------------------------------------

    public static void Export(BmdFile bmd, string outputPath, IReadOnlyDictionary<string, string>? textureIndex = null)
    {
        var (boneNodes, jointRemap) = BuildSkeleton(bmd);
        var meshBld = BuildMesh(bmd, jointRemap, textureIndex);
        BuildAnimations(bmd, boneNodes, jointRemap);

        var scene = new SceneBuilder("default");

        // SharpGLTF requires all skin joints to share a single root node.
        // If the BMD has multiple root bones, attach them under a virtual root
        // so the joint array forms a valid tree.
        var roots = boneNodes.Where(n => n is not null && n.Parent is null).ToList();
        NodeBuilder? virtualRoot = null;
        if (roots.Count > 1)
        {
            virtualRoot = new NodeBuilder("_root");
            foreach (var r in roots)
                virtualRoot.AddNode(r!);
            scene.AddNode(virtualRoot);
        }
        else
        {
            foreach (var r in roots)
                scene.AddNode(r!);
        }

        var joints = new NodeBuilder[jointRemap.Count];
        foreach (var (bmdIdx, gltfIdx) in jointRemap)
            joints[gltfIdx] = boneNodes[bmdIdx]!;

        // When a virtual root was injected, include it as joint[0] and shift others.
        if (virtualRoot is not null)
        {
            var expanded = new NodeBuilder[joints.Length + 1];
            expanded[0] = virtualRoot;
            Array.Copy(joints, 0, expanded, 1, joints.Length);
            joints = expanded;
        }

        scene.AddSkinnedMesh(meshBld, Matrix4x4.Identity, joints);

        var model = scene.ToGltf2();
        model.SaveGLB(outputPath);
    }

    /// <summary>Dumps raw BMD data to stdout for debugging.</summary>
    public static void DumpDebug(BmdFile bmd)
    {
        Console.WriteLine($"Name: '{bmd.Name}'  Version: {bmd.Version}");
        Console.WriteLine($"Meshes: {bmd.Meshes.Length}  Bones: {bmd.Bones.Length}  Actions: {bmd.Actions.Length}");

        for (int mi = 0; mi < bmd.Meshes.Length; mi++)
        {
            var m = bmd.Meshes[mi];
            Console.WriteLine($"\nMesh[{mi}] texture='{m.TexturePath}' verts={m.Vertices.Length} tris={m.Triangles.Length}");
            int show = Math.Min(8, m.Vertices.Length);
            for (int i = 0; i < show; i++)
            {
                var v = m.Vertices[i];
                Console.WriteLine($"  vert[{i}] node={v.Node} pos=({v.X:F3},{v.Y:F3},{v.Z:F3})");
            }
        }

        Console.WriteLine("\nBones:");
        ComputeBoneWorldTransforms(bmd, out var wPos, out var wRot);
        for (int i = 0; i < Math.Min(12, bmd.Bones.Length); i++)
        {
            var b = bmd.Bones[i];
            if (b.IsDummy) { Console.WriteLine($"  bone[{i}] DUMMY"); continue; }
            var lp = b.Matrixes.Length > 0 && b.Matrixes[0].Positions.Length > 0
                ? b.Matrixes[0].Positions[0] : Vector3.Zero;
            Console.WriteLine($"  bone[{i}] '{b.Name}' parent={b.Parent}  local=({lp.X:F3},{lp.Y:F3},{lp.Z:F3})  world=({wPos[i].X:F3},{wPos[i].Y:F3},{wPos[i].Z:F3})");
        }
    }

    // -------------------------------------------------------------------------
    //  Skeleton
    // -------------------------------------------------------------------------

    private static (NodeBuilder?[] nodes, Dictionary<int, int> remap) BuildSkeleton(BmdFile bmd)
    {
        var nodes     = new NodeBuilder?[bmd.Bones.Length];
        var remap     = new Dictionary<int, int>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < bmd.Bones.Length; i++)
        {
            var bone = bmd.Bones[i];
            if (bone.IsDummy) continue;

            string baseName = string.IsNullOrEmpty(bone.Name) ? $"bone_{i}" : bone.Name;
            string name     = baseName;
            for (int s = 1; !usedNames.Add(name); s++) name = $"{baseName}_{s}";

            var nb = bone.Parent >= 0 && nodes[bone.Parent] is not null
                ? nodes[bone.Parent]!.CreateNode(name)
                : new NodeBuilder(name);

            if (bone.Matrixes.Length > 0 && bone.Matrixes[0].Positions.Length > 0)
            {
                var m = bone.Matrixes[0];
                nb.LocalTransform = new AffineTransform(
                    null,
                    YUp(m.Quaternions[0]),
                    YUp(m.Positions[0])
                );
            }

            nodes[i] = nb;
            remap[i] = remap.Count;
        }

        return (nodes, remap);
    }

    // -------------------------------------------------------------------------
    //  Bone world transforms (BMD Z-up space)
    // -------------------------------------------------------------------------

    private static void ComputeBoneWorldTransforms(
        BmdFile bmd,
        out Vector3[]    worldPos,
        out Quaternion[] worldRot)
    {
        worldPos = new Vector3[bmd.Bones.Length];
        worldRot = new Quaternion[bmd.Bones.Length];

        for (int i = 0; i < bmd.Bones.Length; i++)
        {
            var bone = bmd.Bones[i];
            if (bone.IsDummy || bone.Matrixes.Length == 0 || bone.Matrixes[0].Positions.Length == 0)
            {
                worldRot[i] = Quaternion.Identity;
                continue;
            }

            var localPos = bone.Matrixes[0].Positions[0];
            var localRot = bone.Matrixes[0].Quaternions[0];

            int p = bone.Parent;
            if (p >= 0 && p < bmd.Bones.Length && !bmd.Bones[p].IsDummy)
            {
                worldRot[i] = Quaternion.Multiply(worldRot[p], localRot);
                worldPos[i] = Vector3.Transform(localPos, worldRot[p]) + worldPos[p];
            }
            else
            {
                worldRot[i] = localRot;
                worldPos[i] = localPos;
            }
        }
    }

    // -------------------------------------------------------------------------
    //  Mesh
    // -------------------------------------------------------------------------

    private static MeshBld BuildMesh(
        BmdFile bmd, Dictionary<int, int> jointRemap,
        IReadOnlyDictionary<string, string>? textureIndex)
    {
        ComputeBoneWorldTransforms(bmd, out var boneWorldPos, out var boneWorldRot);

        string meshName = bmd.Name.Length > 0 ? bmd.Name : "mesh";
        var meshBld = new MeshBld(meshName);

        for (int mi = 0; mi < bmd.Meshes.Length; mi++)
        {
            var bmdMesh = bmd.Meshes[mi];
            var matName = Path.GetFileNameWithoutExtension(bmdMesh.TexturePath);

            var mat = new MaterialBuilder(matName)
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader();

            if (textureIndex is not null)
            {
                var texBytes = TryLoadTexture(bmdMesh.TexturePath, textureIndex);
                if (texBytes is not null)
                {
                    mat.UseChannel("BaseColor").UseTexture().PrimaryImage = new MemoryImage(texBytes);
                    Console.WriteLine($"  tex[{mi}] OK   '{bmdMesh.TexturePath}'");
                }
                else
                {
                    Console.WriteLine($"  tex[{mi}] MISS '{bmdMesh.TexturePath}'");
                }
            }

            var prim = meshBld.UsePrimitive(mat);

            foreach (var tri in bmdMesh.Triangles)
            {
                var v = new GVB?[tri.Polygon];
                bool ok = true;
                for (int c = 0; c < tri.Polygon; c++)
                {
                    if (!TryMakeVertex(bmdMesh, tri, c, jointRemap, boneWorldPos, boneWorldRot, out var gv))
                    { ok = false; break; }
                    v[c] = gv;
                }
                if (!ok) continue;

                prim.AddTriangle(v[0]!.Value, v[2]!.Value, v[1]!.Value);
                if (tri.Polygon == 4)
                    prim.AddTriangle(v[0]!.Value, v[2]!.Value, v[3]!.Value);
            }
        }

        return meshBld;
    }

    private static bool TryMakeVertex(
        BmdMesh bmdMesh, BmdTriangle tri, int corner,
        Dictionary<int, int> jointRemap,
        Vector3[] boneWorldPos, Quaternion[] boneWorldRot,
        out GVB vertex)
    {
        vertex = default;
        int vi = tri.VertexIndex[corner];
        int ni = tri.NormalIndex[corner];
        int ti = tri.TexCoordIndex[corner];

        if ((uint)vi >= (uint)bmdMesh.Vertices.Length)  return false;
        if ((uint)ni >= (uint)bmdMesh.Normals.Length)   return false;
        if ((uint)ti >= (uint)bmdMesh.TexCoords.Length) return false;

        var vtx = bmdMesh.Vertices[vi];
        var nrm = bmdMesh.Normals[ni];
        var tex = bmdMesh.TexCoords[ti];

        int boneIdx = vtx.Node;

        var localPos = new Vector3(vtx.X,  vtx.Y,  vtx.Z);
        var localNrm = new Vector3(nrm.NX, nrm.NY, nrm.NZ);

        Vector3 modelPos, modelNrm;
        if (boneIdx >= 0 && boneIdx < boneWorldPos.Length)
        {
            modelPos = Vector3.Transform(localPos, boneWorldRot[boneIdx]) + boneWorldPos[boneIdx];
            modelNrm = Vector3.Transform(localNrm, boneWorldRot[boneIdx]);
        }
        else
        {
            modelPos = localPos;
            modelNrm = localNrm;
        }

        var pos    = YUp(modelPos);
        var normal = YUp(Vector3.Normalize(modelNrm));
        int joint  = jointRemap.TryGetValue(boneIdx, out int ji) ? ji : 0;

        vertex = new GVB(
            new VertexPositionNormal(pos, normal),
            new VertexTexture1(new Vector2(tex.U, tex.V)),
            new VertexJoints4(joint)
        );
        return true;
    }

    // -------------------------------------------------------------------------
    //  Animations
    // -------------------------------------------------------------------------

    private static void BuildAnimations(
        BmdFile bmd, NodeBuilder?[] boneNodes, Dictionary<int, int> remap)
    {
        for (int a = 0; a < bmd.Actions.Length; a++)
        {
            var action = bmd.Actions[a];
            if (action.NumKeys <= 1) continue;

            string animName = $"action_{a}";

            for (int bi = 0; bi < bmd.Bones.Length; bi++)
            {
                if (boneNodes[bi] is null) continue;
                var bone = bmd.Bones[bi];
                if (bone.IsDummy || a >= bone.Matrixes.Length) continue;

                var mat = bone.Matrixes[a];
                if (mat.Positions.Length != action.NumKeys) continue;

                var nb   = boneNodes[bi]!;
                var tCrv = nb.UseTranslation(animName);
                var rCrv = nb.UseRotation(animName);

                for (int k = 0; k < action.NumKeys; k++)
                {
                    float t = k / Fps;
                    tCrv.SetPoint(t, YUp(mat.Positions[k]));
                    rCrv.SetPoint(t, YUp(mat.Quaternions[k]));
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    //  Texture index + lookup
    // -------------------------------------------------------------------------

    // Scans <dataRoot> recursively and builds a stem→path map.
    // OZJ beats OZT beats raw formats; first OZJ for a stem wins.
    public static Dictionary<string, string> BuildTextureIndex(string dataRoot)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(dataRoot, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not (".ozj" or ".ozt" or ".jpg" or ".jpeg" or ".png" or ".tga"))
                continue;

            var stem = Path.GetFileNameWithoutExtension(file);
            if (!index.TryGetValue(stem, out var existing))
            {
                index[stem] = file;
            }
            else
            {
                // Prefer OZJ > OZT > everything else.
                var cur = Path.GetExtension(existing).ToLowerInvariant();
                if (ext == ".ozj" && cur != ".ozj")
                    index[stem] = file;
                else if (ext == ".ozt" && cur is not ".ozj" and not ".ozt")
                    index[stem] = file;
            }
        }
        return index;
    }

    // Returns decoded bytes or null if not found in the index.
    private static byte[]? TryLoadTexture(
        string texturePath, IReadOnlyDictionary<string, string> textureIndex)
    {
        var stem = Path.GetFileNameWithoutExtension(texturePath);
        if (!textureIndex.TryGetValue(stem, out var path)) return null;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            // All formats go through ImageSharp and come out as PNG.
            // Embedding raw OZJ JPEG bytes fails in Godot (progressive JPEG),
            // so we always transcode to ensure a clean, standard image.
            return ext switch
            {
                ".ozj"                        => OzjToPng(path),
                ".ozt"                        => OztToPng(path),
                ".png"                        => File.ReadAllBytes(path),
                ".jpg" or ".jpeg" or ".tga"   => RawImageToPng(path),
                _                             => null,
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [WARN] texture decode failed '{Path.GetFileName(path)}': {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace is not null)
                Console.Error.WriteLine(ex.StackTrace.Split('\n').FirstOrDefault()?.Trim());
            return null;
        }
    }

    private static byte[] OzjToPng(string ozjPath)
    {
        var jpeg = OzjFile.ExtractJpeg(ozjPath);
        // OZJ files embed one or two thumbnail JPEGs inside APP1/APP13 before the
        // main image.  The APP1 segment has a malformed declared length (too short),
        // so after ImageSharp processes it the next bytes are raw scan data — not a
        // valid marker — and it throws a NullReferenceException.
        // Fix: locate the last DQT marker (0xFF 0xDB), which belongs to the main
        // high-res image and comes after all thumbnail segments, then build a minimal
        // SOI + DQT→EOI JPEG for ImageSharp.
        var mainJpeg = ExtractMainJpeg(jpeg.Span);
        using var ms    = new MemoryStream(mainJpeg);
        using var img   = Image.Load(ms);
        using var outMs = new MemoryStream();
        img.SaveAsPng(outMs);
        return outMs.ToArray();
    }

    private static byte[] ExtractMainJpeg(ReadOnlySpan<byte> jpeg)
    {
        int lastDqt = -1;
        for (int i = 0; i < jpeg.Length - 1; i++)
            if (jpeg[i] == 0xFF && jpeg[i + 1] == 0xDB)
                lastDqt = i;

        if (lastDqt < 0) return jpeg.ToArray();

        var result = new byte[2 + jpeg.Length - lastDqt];
        result[0] = 0xFF; result[1] = 0xD8; // SOI
        jpeg[lastDqt..].CopyTo(result.AsSpan(2));
        return result;
    }

    private static byte[] OztToPng(string oztPath)
    {
        var raw = File.ReadAllBytes(oztPath);
        const int HeaderSize = 4;
        // Copy TGA bytes so we can patch without mutating the original.
        var tga = raw.AsSpan(HeaderSize).ToArray();
        // TGA spec: color-map spec fields (bytes 3-7) must be all-zero when
        // color-map type is 0.  Some WebZen OZT files set byte[7] (entry size)
        // to a non-zero value, which causes ImageSharp's format detector to fail.
        if (tga.Length >= 8 && tga[1] == 0)
            for (int j = 3; j <= 7; j++) tga[j] = 0;
        using var ms    = new MemoryStream(tga);
        using var img   = Image.Load(ms);
        using var outMs = new MemoryStream();
        img.SaveAsPng(outMs);
        return outMs.ToArray();
    }

    private static byte[] RawImageToPng(string path)
    {
        using var img   = Image.Load(path);
        using var outMs = new MemoryStream();
        img.SaveAsPng(outMs);
        return outMs.ToArray();
    }
}

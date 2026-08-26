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

        // Determine up front whether a virtual root will be injected.
        // If so, all joint indices in the mesh must be offset by +1 so that
        // vertex joint=0 still refers to the first real bone, not the virtual root.
        bool needsVirtualRoot = boneNodes.Count(n => n is not null && n.Parent is null) > 1;
        ComputeBoneWorldTransforms(bmd, out var boneWorldPos, out var boneWorldRot);
        var meshBld = BuildMesh(bmd.Name, bmd.Meshes, boneWorldPos, boneWorldRot, jointRemap, textureIndex, needsVirtualRoot ? 1 : 0);
        BuildAnimations(bmd, boneNodes, jointRemap);

        var scene = new SceneBuilder("default");
        var joints = AssembleScene(scene, boneNodes, jointRemap);
        scene.AddSkinnedMesh(meshBld, Matrix4x4.Identity, joints);

        var model = scene.ToGltf2();
        model.SaveGLB(outputPath);
    }

    /// <summary>
    /// Exports a composed character GLB: skeleton + animations from <paramref name="skeleton"/>
    /// (player.bmd), with visible meshes from each equipment <paramref name="parts"/> BMD.
    /// All parts share the same skeleton — vertex bone indices in equipment BMDs reference
    /// player.bmd's bone array directly (matching how the original engine calls
    /// <c>b->Transform(o->BoneTransform, ...)</c> with the character's bone transforms).
    /// </summary>
    public static void ExportCharacterBake(
        BmdFile skeleton,
        IEnumerable<BmdFile> parts,
        string outputPath,
        IReadOnlyDictionary<string, string>? textureIndex = null)
    {
        var (boneNodes, jointRemap) = BuildSkeleton(skeleton);
        bool needsVirtualRoot = boneNodes.Count(n => n is not null && n.Parent is null) > 1;
        int jointOffset = needsVirtualRoot ? 1 : 0;

        ComputeBoneWorldTransforms(skeleton, out var boneWorldPos, out var boneWorldRot);
        BuildAnimations(skeleton, boneNodes, jointRemap);

        // Merge all equipment meshes into one MeshBuilder under the shared skeleton.
        var meshBld = new MeshBld("character");
        foreach (var part in parts)
            AddMeshPrimitives(meshBld, part.Meshes, boneWorldPos, boneWorldRot, jointRemap, textureIndex, jointOffset);

        var scene = new SceneBuilder("default");
        var joints = AssembleScene(scene, boneNodes, jointRemap);
        scene.AddSkinnedMesh(meshBld, Matrix4x4.Identity, joints);

        var model = scene.ToGltf2();
        model.SaveGLB(outputPath);
    }

    // -------------------------------------------------------------------------
    //  Manifest
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="ModelManifest"/> describing the animations and bones
    /// in <paramref name="bmd"/>. Only animations that are actually exported to
    /// the GLB (NumKeys &gt; 1) are included.
    /// </summary>
    public static ModelManifest BuildManifest(
        BmdFile bmd,
        string sourceName,
        string type = "single",
        string? group = null,
        string[]? parts = null,
        IReadOnlyDictionary<int, string>? labels = null)
    {
        var entries = new List<AnimEntry>();
        for (int a = 0; a < bmd.Actions.Length; a++)
        {
            if (bmd.Actions[a].NumKeys <= 1) continue;
            string? label = null;
            labels?.TryGetValue(a, out label);
            entries.Add(new AnimEntry(a, $"action_{a}", bmd.Actions[a].NumKeys, label));
        }
        return new ModelManifest
        {
            Source         = sourceName,
            Type           = type,
            Group          = group,
            BoneCount      = bmd.Bones.Count(b => !b.IsDummy),
            AnimationCount = entries.Count,
            Parts          = parts,
            Animations     = [.. entries],
        };
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
    //  Scene assembly (shared between Export and ExportCharacterBake)
    // -------------------------------------------------------------------------

    private static NodeBuilder[] AssembleScene(
        SceneBuilder scene, NodeBuilder?[] boneNodes, Dictionary<int, int> jointRemap)
    {
        var roots = boneNodes.Where(n => n is not null && n.Parent is null).ToList();
        bool needsVirtualRoot = roots.Count > 1;

        NodeBuilder? virtualRoot = null;
        if (needsVirtualRoot)
        {
            virtualRoot = new NodeBuilder("_root");
            foreach (var r in roots) virtualRoot.AddNode(r!);
            scene.AddNode(virtualRoot);
        }
        else
        {
            foreach (var r in roots) scene.AddNode(r!);
        }

        var joints = new NodeBuilder[jointRemap.Count];
        foreach (var (bmdIdx, gltfIdx) in jointRemap)
            joints[gltfIdx] = boneNodes[bmdIdx]!;

        if (virtualRoot is not null)
        {
            var expanded = new NodeBuilder[joints.Length + 1];
            expanded[0] = virtualRoot;
            Array.Copy(joints, 0, expanded, 1, joints.Length);
            joints = expanded;
        }

        return joints;
    }

    // -------------------------------------------------------------------------
    //  Mesh
    // -------------------------------------------------------------------------

    private static MeshBld BuildMesh(
        string name,
        BmdMesh[] meshes,
        Vector3[] boneWorldPos,
        Quaternion[] boneWorldRot,
        Dictionary<int, int> jointRemap,
        IReadOnlyDictionary<string, string>? textureIndex,
        int jointOffset = 0)
    {
        string meshName = name.Length > 0 ? name : "mesh";
        var meshBld = new MeshBld(meshName);
        AddMeshPrimitives(meshBld, meshes, boneWorldPos, boneWorldRot, jointRemap, textureIndex, jointOffset);
        return meshBld;
    }

    private static void AddMeshPrimitives(
        MeshBld meshBld,
        BmdMesh[] meshes,
        Vector3[] boneWorldPos,
        Quaternion[] boneWorldRot,
        Dictionary<int, int> jointRemap,
        IReadOnlyDictionary<string, string>? textureIndex,
        int jointOffset)
    {
        for (int mi = 0; mi < meshes.Length; mi++)
        {
            var bmdMesh = meshes[mi];
            var matName = Path.GetFileNameWithoutExtension(bmdMesh.TexturePath);

            var mat = new MaterialBuilder(matName)
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                // glTF 2.0 defaults metallicFactor=1, which makes objects appear black
                // without IBL. MU assets are non-metallic painted surfaces.
                .WithChannelParam(KnownChannel.MetallicRoughness, KnownProperty.MetallicFactor,  0f)
                .WithChannelParam(KnownChannel.MetallicRoughness, KnownProperty.RoughnessFactor, 0.8f);

            if (textureIndex is not null)
            {
                var texBytes = TryLoadTexture(bmdMesh.TexturePath, textureIndex);
                if (texBytes is not null)
                {
                    mat.UseChannel("BaseColor").UseTexture().PrimaryImage = new MemoryImage(texBytes);
                    // OZT/TGA sources export RGBA PNGs — foliage, wings, and sprites
                    // use the alpha channel as a cutout mask. Without MASK mode, Godot
                    // renders the transparent areas as black.
                    if (PngIsRgba(texBytes))
                        mat.WithAlpha(AlphaMode.MASK, 0.5f);
                    Console.WriteLine($"  tex[{mi}] OK   '{bmdMesh.TexturePath}'{(PngIsRgba(texBytes) ? " [alpha-mask]" : "")}");
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
                    if (!TryMakeVertex(bmdMesh, tri, c, jointRemap, boneWorldPos, boneWorldRot, jointOffset, out var gv))
                    { ok = false; break; }
                    v[c] = gv;
                }
                if (!ok) continue;

                prim.AddTriangle(v[0]!.Value, v[2]!.Value, v[1]!.Value);
                if (tri.Polygon == 4)
                    prim.AddTriangle(v[0]!.Value, v[2]!.Value, v[3]!.Value);
            }
        }
    }

    private static bool TryMakeVertex(
        BmdMesh bmdMesh, BmdTriangle tri, int corner,
        Dictionary<int, int> jointRemap,
        Vector3[] boneWorldPos, Quaternion[] boneWorldRot,
        int jointOffset,
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
        int joint  = (jointRemap.TryGetValue(boneIdx, out int ji) ? ji : 0) + jointOffset;

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
    // OZT > OZJ > raw formats: OZT (TGA) is preferred because it preserves the
    // alpha channel needed for foliage cutouts (JPEG has no alpha support).
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
                // Prefer OZT > OZJ > everything else.
                // OZT is TGA-based (lossless, supports RGBA alpha) while OZJ is JPEG
                // (lossy, no alpha). Foliage textures like tree_01, tree_02, grass_01
                // have their alpha mask only in the OZT variant.
                var cur = Path.GetExtension(existing).ToLowerInvariant();
                if (ext == ".ozt" && cur != ".ozt")
                    index[stem] = file;
                else if (ext == ".ozj" && cur is not ".ozt" and not ".ozj")
                    index[stem] = file;
            }
        }
        return index;
    }

    // PNG IHDR color type 6 = RGBA. Offset 25 = sig(8) + chunk_len(4) + "IHDR"(4) + w(4) + h(4) + bit_depth(1).
    private static bool PngIsRgba(byte[] png) =>
        png.Length > 25 &&
        png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47 && // "PNG"
        png[25] == 6;

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
        using var ms    = new MemoryStream(jpeg);
        using var img   = Image.Load(ms);
        using var outMs = new MemoryStream();
        img.SaveAsPng(outMs);
        return outMs.ToArray();
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

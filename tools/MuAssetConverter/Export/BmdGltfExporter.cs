using System.Numerics;
using MuAssetConverter.Formats;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

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
///   The conversion is baked into every vertex position, normal, and bone
///   transform: (x, y, z) → (x, z, −y), i.e. −90° rotation around X.
///   No extra root node is emitted — the output is importable in Godot
///   without any post-import rotation fix.
/// </summary>
public static class BmdGltfExporter
{
    private const float Fps = 24f;

    // −90° around X: converts BMD Z-up into glTF Y-up.
    private static readonly Quaternion CoordFix =
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2f);

    // Bake position/normal: (x,y,z) → (x,z,−y)
    private static Vector3 YUp(Vector3 v) => new(v.X, v.Z, -v.Y);

    // Bake rotation: conjugate by CoordFix so local rotations stay consistent.
    private static Quaternion YUp(Quaternion q) =>
        Quaternion.Normalize(CoordFix * q * Quaternion.Conjugate(CoordFix));

    // -------------------------------------------------------------------------
    //  Public entry point
    // -------------------------------------------------------------------------

    public static void Export(BmdFile bmd, string outputPath)
    {
        var (boneNodes, jointRemap) = BuildSkeleton(bmd);
        var meshBld = BuildMesh(bmd, jointRemap);
        BuildAnimations(bmd, boneNodes, jointRemap);

        var scene = new SceneBuilder("default");

        // Root bones (no parent) must be added explicitly so the full hierarchy
        // is included when ToGltf2() walks the scene.
        foreach (var nb in boneNodes)
            if (nb is not null && nb.Parent is null)
                scene.AddNode(nb);

        // Skin joint array indexed by glTF joint index (sequential, no dummies).
        // SharpGLTF computes inverse bind matrices from the node world transforms.
        var joints = new NodeBuilder[jointRemap.Count];
        foreach (var (bmdIdx, gltfIdx) in jointRemap)
            joints[gltfIdx] = boneNodes[bmdIdx]!;

        scene.AddSkinnedMesh(meshBld, Matrix4x4.Identity, joints);

        var model = scene.ToGltf2();
        model.SaveGLB(outputPath);
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

            // glTF requires unique node names across the whole asset.
            string baseName = string.IsNullOrEmpty(bone.Name) ? $"bone_{i}" : bone.Name;
            string name     = baseName;
            for (int s = 1; !usedNames.Add(name); s++) name = $"{baseName}_{s}";

            var nb = bone.Parent >= 0 && nodes[bone.Parent] is not null
                ? nodes[bone.Parent]!.CreateNode(name)
                : new NodeBuilder(name);

            // Bind pose: action[0], frame[0], converted to Y-up.
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
    //  Mesh
    // -------------------------------------------------------------------------

    private static MeshBld BuildMesh(BmdFile bmd, Dictionary<int, int> jointRemap)
    {
        string meshName = bmd.Name.Length > 0 ? bmd.Name : "mesh";
        var meshBld = new MeshBld(meshName);

        for (int mi = 0; mi < bmd.Meshes.Length; mi++)
        {
            var bmdMesh = bmd.Meshes[mi];
            var mat = new MaterialBuilder(
                    Path.GetFileNameWithoutExtension(bmdMesh.TexturePath))
                .WithDoubleSide(true);

            var prim = meshBld.UsePrimitive(mat);

            foreach (var tri in bmdMesh.Triangles)
            {
                // Collect all corner vertices first (3 for tri, 4 for quad).
                var v = new GVB?[tri.Polygon];
                bool ok = true;
                for (int c = 0; c < tri.Polygon; c++)
                {
                    if (!TryMakeVertex(bmdMesh, tri, c, jointRemap, out var gv))
                    { ok = false; break; }
                    v[c] = gv;
                }
                if (!ok) continue;

                // Reversed winding 0→2→1 matches the Three.js BMD loader.
                prim.AddTriangle(v[0]!.Value, v[2]!.Value, v[1]!.Value);

                // Second triangle of quad: 0→2→3.
                if (tri.Polygon == 4)
                    prim.AddTriangle(v[0]!.Value, v[2]!.Value, v[3]!.Value);
            }
        }

        return meshBld;
    }

    private static bool TryMakeVertex(
        BmdMesh bmdMesh, BmdTriangle tri, int corner,
        Dictionary<int, int> jointRemap, out GVB vertex)
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

        var pos    = YUp(new Vector3(vtx.X,  vtx.Y,  vtx.Z));
        var normal = YUp(new Vector3(nrm.NX, nrm.NY, nrm.NZ));
        int joint  = jointRemap.TryGetValue(vtx.Node, out int ji) ? ji : 0;

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
            if (action.NumKeys <= 1) continue; // static pose, not worth an animation track

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
}

using System.Numerics;
using System.Text;

namespace MuAssetConverter.Formats;

// BMD: MU Online 3D model format (mesh + skeleton + animations).
//
// Versions observed in Season 6 data:
//   0, 10 — unencrypted; data starts at byte 4.
//   12    — FileCryptor (rolling XOR); encrypted size at bytes 4-7, data at byte 8.
//   15    — LEA-256-ECB (not present in S6 data, not implemented).
//
// Reference: github.com/xulek/muonline-bmd-viewer (TypeScript, MIT)

public sealed class BmdFile
{
    public byte Version  { get; private set; }
    public string Name   { get; private set; } = "";
    public BmdMesh[]   Meshes  { get; private set; } = [];
    public BmdAction[] Actions { get; private set; } = [];
    public BmdBone[]   Bones   { get; private set; } = [];

    // -------------------------------------------------------------------------
    //  Load
    // -------------------------------------------------------------------------

    public static BmdFile Load(string path) => Parse(File.ReadAllBytes(path));

    public static BmdFile Parse(byte[] raw)
    {
        if (raw.Length < 4)           throw new InvalidDataException("BMD too small.");
        if (raw[0] != 'B' || raw[1] != 'M' || raw[2] != 'D')
            throw new InvalidDataException("Not a BMD file (bad magic).");

        byte version = raw[3];
        int  dataStart;
        byte[] data;

        if (version == 15)
            throw new NotSupportedException("BMD version 15 (LEA-256) is not supported.");

        if (version == 12)
        {
            if (raw.Length < 8) throw new InvalidDataException("Encrypted BMD header truncated.");
            int encSize = BitConverter.ToInt32(raw, 4);
            if (encSize < 0 || encSize > raw.Length - 8)
                throw new InvalidDataException($"Invalid encrypted payload size: {encSize}.");

            data = (byte[])raw.Clone();
            DecryptFileCryptor(data, 8, encSize);
            dataStart = 8;
        }
        else
        {
            data      = raw;
            dataStart = 4;
        }

        var file = new BmdFile { Version = version };
        var r    = new BmdReader(data, dataStart);
        file.ParseData(r);
        return file;
    }

    // -------------------------------------------------------------------------
    //  Parsing
    // -------------------------------------------------------------------------

    private void ParseData(BmdReader r)
    {
        Name = r.ReadString(32);
        int meshCount   = r.ReadU16();
        int boneCount   = r.ReadU16();
        int actionCount = r.ReadU16();

        Meshes = new BmdMesh[meshCount];
        for (int i = 0; i < meshCount; i++)
            Meshes[i] = ReadMesh(r);

        Actions = new BmdAction[actionCount];
        for (int i = 0; i < actionCount; i++)
            Actions[i] = ReadAction(r);

        Bones = new BmdBone[boneCount];
        for (int i = 0; i < boneCount; i++)
            Bones[i] = ReadBone(r, Actions);
    }

    private static BmdMesh ReadMesh(BmdReader r)
    {
        int numVertices  = r.ReadS16();
        int numNormals   = r.ReadS16();
        int numTexCoords = r.ReadS16();
        int numTriangles = r.ReadS16();
        int textureIndex = r.ReadS16();

        var vertices = new BmdVertex[numVertices];
        for (int i = 0; i < numVertices; i++)
        {
            short node = r.ReadS16(); r.Skip(2);
            vertices[i] = new(node, r.ReadF32(), r.ReadF32(), r.ReadF32());
        }

        var normals = new BmdNormal[numNormals];
        for (int i = 0; i < numNormals; i++)
        {
            short node = r.ReadS16(); r.Skip(2);
            float nx = r.ReadF32(), ny = r.ReadF32(), nz = r.ReadF32();
            short bindVertex = r.ReadS16(); r.Skip(2);
            normals[i] = new(node, nx, ny, nz, bindVertex);
        }

        var texCoords = new BmdTexCoord[numTexCoords];
        for (int i = 0; i < numTexCoords; i++)
            texCoords[i] = new(r.ReadF32(), r.ReadF32());

        var triangles = new BmdTriangle[numTriangles];
        for (int i = 0; i < numTriangles; i++)
        {
            // Triangle stride = 64 bytes total
            // [0]     polygon (uint8)
            // [1]     padding
            // [2..9]  vertexIndex[4]   (int16×4)
            // [10..17] normalIndex[4]  (int16×4)
            // [18..25] texCoordIndex[4](int16×4)
            // [26..63] lightmap (38 bytes, ignored)
            byte polygon = r.ReadU8(); r.Skip(1);
            var vi = new short[4]; for (int j = 0; j < 4; j++) vi[j] = r.ReadS16();
            var ni = new short[4]; for (int j = 0; j < 4; j++) ni[j] = r.ReadS16();
            var ti = new short[4]; for (int j = 0; j < 4; j++) ti[j] = r.ReadS16();
            r.Skip(38);
            triangles[i] = new(polygon, vi, ni, ti);
        }

        return new BmdMesh(textureIndex, r.ReadString(32), vertices, normals, texCoords, triangles);
    }

    private static BmdAction ReadAction(BmdReader r)
    {
        int  numKeys        = r.ReadS16();
        bool lockPositions  = r.ReadU8() > 0;
        Vector3[]? positions = null;
        if (lockPositions)
        {
            positions = new Vector3[numKeys];
            for (int k = 0; k < numKeys; k++)
                positions[k] = new(r.ReadF32(), r.ReadF32(), r.ReadF32());
        }
        return new BmdAction(numKeys, lockPositions, positions);
    }

    private static BmdBone ReadBone(BmdReader r, BmdAction[] actions)
    {
        bool isDummy = r.ReadU8() > 0;
        if (isDummy) return BmdBone.MakeDummy();

        string name   = r.ReadString(32);
        short  parent = r.ReadS16();

        var matrixes = new BmdBoneMatrix[actions.Length];
        for (int a = 0; a < actions.Length; a++)
        {
            int numKeys = actions[a].NumKeys;
            if (numKeys == 0)
            {
                matrixes[a] = BmdBoneMatrix.Identity;
                continue;
            }

            var positions    = new Vector3[numKeys];
            var rotations    = new Vector3[numKeys];
            var quaternions  = new Quaternion[numKeys];

            for (int k = 0; k < numKeys; k++)
                positions[k] = new(r.ReadF32(), r.ReadF32(), r.ReadF32());
            for (int k = 0; k < numKeys; k++)
            {
                var rot = new Vector3(r.ReadF32(), r.ReadF32(), r.ReadF32());
                rotations[k]   = rot;
                quaternions[k] = EulerToQuaternion(rot);
            }
            matrixes[a] = new BmdBoneMatrix(positions, rotations, quaternions);
        }

        return new BmdBone(name, parent, matrixes);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    // Intrinsic ZYX Euler (radians) → unit quaternion, matching Three.js bmdAngleToQuaternion.
    private static Quaternion EulerToQuaternion(Vector3 euler)
    {
        float hx = euler.X * .5f, hy = euler.Y * .5f, hz = euler.Z * .5f;
        float sx = MathF.Sin(hx), cx = MathF.Cos(hx);
        float sy = MathF.Sin(hy), cy = MathF.Cos(hy);
        float sz = MathF.Sin(hz), cz = MathF.Cos(hz);
        return Quaternion.Normalize(new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz
        ));
    }

    // FileCryptor: rolling XOR used in BMD version 12.
    private static void DecryptFileCryptor(byte[] buf, int start, int length)
    {
        ReadOnlySpan<byte> xorKey = [
            0xD1, 0x73, 0x52, 0xF6, 0xD2, 0x9A, 0xCB, 0x27,
            0x3E, 0xAF, 0x59, 0x31, 0x37, 0xB3, 0xE7, 0xA2
        ];
        byte mapKey = 0x5E;
        for (int i = 0; i < length; i++)
        {
            byte enc = buf[start + i];
            buf[start + i] = (byte)((enc ^ xorKey[i & 15]) - mapKey);
            mapKey = (byte)(enc + 0x3D);
        }
    }
}

// -------------------------------------------------------------------------
//  Data types
// -------------------------------------------------------------------------

public sealed class BmdMesh
{
    public int           TextureIndex { get; }
    public string        TexturePath  { get; }
    public BmdVertex[]   Vertices     { get; }
    public BmdNormal[]   Normals      { get; }
    public BmdTexCoord[] TexCoords    { get; }
    public BmdTriangle[] Triangles    { get; }

    internal BmdMesh(int textureIndex, string texturePath,
        BmdVertex[] vertices, BmdNormal[] normals,
        BmdTexCoord[] texCoords, BmdTriangle[] triangles)
    {
        TextureIndex = textureIndex;
        TexturePath  = texturePath;
        Vertices     = vertices;
        Normals      = normals;
        TexCoords    = texCoords;
        Triangles    = triangles;
    }
}

public sealed record BmdVertex(short Node, float X, float Y, float Z);
public sealed record BmdNormal(short Node, float NX, float NY, float NZ, short BindVertex);
public readonly record struct BmdTexCoord(float U, float V);
public sealed record BmdTriangle(byte Polygon, short[] VertexIndex, short[] NormalIndex, short[] TexCoordIndex);
public sealed record BmdAction(int NumKeys, bool LockPositions, Vector3[]? Positions);

public sealed class BmdBone
{
    public bool           IsDummy  { get; }
    public string         Name     { get; }
    public short          Parent   { get; }   // -1 = root bone
    public BmdBoneMatrix[] Matrixes { get; }  // one entry per action

    internal BmdBone(string name, short parent, BmdBoneMatrix[] matrixes)
    {
        Name     = name;
        Parent   = parent;
        Matrixes = matrixes;
    }

    private BmdBone() { IsDummy = true; Name = ""; Parent = -1; Matrixes = []; }
    internal static BmdBone MakeDummy() => new();
}

public sealed record BmdBoneMatrix(Vector3[] Positions, Vector3[] Rotations, Quaternion[] Quaternions)
{
    public static readonly BmdBoneMatrix Identity = new(
        [Vector3.Zero],
        [Vector3.Zero],
        [Quaternion.Identity]
    );
}

// -------------------------------------------------------------------------
//  Low-level binary reader (little-endian, sequential)
// -------------------------------------------------------------------------

internal sealed class BmdReader
{
    private readonly byte[] _data;
    private int _off;

    internal BmdReader(byte[] data, int start) { _data = data; _off = start; }

    internal void  Skip(int n)  => _off += n;
    internal byte  ReadU8()     => _data[_off++];
    internal short ReadS16()    { var v = BitConverter.ToInt16(_data, _off);  _off += 2; return v; }
    internal ushort ReadU16()   { var v = BitConverter.ToUInt16(_data, _off); _off += 2; return v; }
    internal float ReadF32()    { var v = BitConverter.ToSingle(_data, _off); _off += 4; return v; }

    internal string ReadString(int length)
    {
        var span = _data.AsSpan(_off, length);
        int z    = span.IndexOf((byte)0);
        var text = Encoding.ASCII.GetString(z >= 0 ? span[..z] : span);
        _off += length;
        return text;
    }
}

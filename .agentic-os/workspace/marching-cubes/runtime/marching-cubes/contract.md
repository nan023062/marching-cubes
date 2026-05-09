# Runtime/MarchingCubes Contract

## IMarchingCubeReceiver

```csharp
public interface IMarchingCubeReceiver
{
    float GetIsoLevel();
    bool IsoPass(float iso);
    void OnRebuildCompleted();
}
```

## CubeMesh

```csharp
public sealed class CubeMesh
{
    public readonly int X, Y, Z;
    public Mesh mesh;
    public CubeMesh(int x, int y, int z, IMarchingCubeReceiver receiver);
    public void SetPointISO(int x, int y, int z, float iso);
    public void Rebuild();
}
```

## CubeMeshSmooth

```csharp
public sealed class CubeMeshSmooth
{
    public readonly int X, Y, Z;
    public Mesh mesh;
    public CubeMeshSmooth(int x, int y, int z, IMarchingCubeReceiver receiver);
    public float GetPointISO(int x, int y, int z);
    public void SetPointISO(int x, int y, int z, float iso);
    public void Rebuild();
}
```

## CubeTable

```csharp
public static class CubeTable
{
    public const int VertexCount = 8;
    public const int EdgeCount = 12;
    public const int CubeKind = 256;
    public static readonly Coord[] Vertices;          // 8 顶点坐标
    public static readonly (int p1, int p2)[] Edges;  // 12 边端点对
    public static readonly Vector3[] EdgeMidpoints;   // 12 棱中点（程序化 mesh 吸附用）
    public static int[] EdgeTable;                    // 256 × edge bit mask
    public static int GetCubeKindEdgeMask(int cubeKindIndex);
    public static ref readonly int[] GetCubeKindTriangles(int cubeKindIndex);
    public static Vector3 InterpolateVerts(Vector3 v1, Vector3 v2, float s1, float s2, float isoLevel);
    public static bool AlmostEqual(this float v1, float v2);
}
```

## CaseMeshBuilderAsset

```csharp
public abstract class CaseMeshBuilderAsset : ScriptableObject
{
    public abstract Mesh Build(int caseIndex);
    // 辅助方法（protected static）：
    // ActiveVertices / ActiveVertexPositions / CrossingEdgeMask / CrossingEdgeMidpoints
}
```

## RoundedOctantMeshBuilder

```csharp
[CreateAssetMenu(menuName = "MarchingCubes/CaseMeshBuilder/RoundedOctant")]
public sealed class RoundedOctantMeshBuilder : CaseMeshBuilderAsset
{
    [Range(0f, 0.24f)] public float sideRadius = 0.08f;
    [Range(0f, 0.24f)] public float topRadius  = 0f;
    [Range(1, 12)]     public int   segments   = 4;
    public override Mesh Build(int caseIndex);
}
```

## 核心数据结构

```csharp
public struct Point   { public readonly sbyte x, y, z; public float iso; }
public struct Coord   { public int x, y, z; }
public struct Vertex  { public Vector3 position; public Vector2 uv; }
public struct Triangle { public Vertex v1, v2, v3; }

[StructLayout(LayoutKind.Explicit)]
public readonly struct Edge : IEquatable<Edge>
{
    [FieldOffset(0)] public readonly long _index;  // 哈希键
    [FieldOffset(0)] public readonly sbyte x, y, z;
    [FieldOffset(3)] public readonly Axis axis;
    public Edge(int x, int y, int z, Axis axis);
}

public enum Axis : byte { X, Y, Z }
[Flags] public enum CubeVertexMask { Null=0x00, V0=0x01, V1=0x02, ..., All=0xFF }
```

## 使用方

- `marching-cubes/sample/*`（各 Sample 案例）
- `marching-cubes/editor/art-mc-mesh`（CubeTable、CubedMeshPrefab、D4FbxCaseConfig 等）

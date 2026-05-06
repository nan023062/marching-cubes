# MarchingCubes Contract

## 公开接口

### IMarchingCubeReceiver
```csharp
public interface IMarchingCubeReceiver
{
    float GetIsoLevel();          // iso 阈值（大于此值的点视为"实心"）
    bool IsoPass(float iso);      // 判断点是否通过 iso 测试
    void OnRebuildCompleted();    // 网格重建完成回调
}
```

### CubeMesh
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

### CubeMeshSmooth
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

### CubeTable（工具函数）
```csharp
public static class CubeTable
{
    public const int VertexCount = 8;
    public const int EdgeCount = 12;
    public const int CubeKind = 256;
    public static readonly (int x, int y, int z)[] Vertices;
    public static readonly (int p1, int p2)[] Edges;
    public static readonly Edge[] EdgesOffset;
    public static int GetCubeKindEdgeMask(int cubeKindIndex);
    public static ref readonly int[] GetCubeKindTriangles(int cubeKindIndex);
    public static Vector3 InterpolateVerts(Vector3 v1, Vector3 v2, float s1, float s2, float isoLevel);
    public static bool AlmostEqual(this float v1, float v2);
}
```

### 数据结构
```csharp
public struct Point   { public readonly sbyte x, y, z; public float iso; }
public struct Vertex  { public Vector3 position; public Vector2 uv; }
public struct Triangle { public Vertex v1, v2, v3; }
public struct Coord   { public int x, y, z; }

[StructLayout(LayoutKind.Explicit)]
public readonly struct Edge : IEquatable<Edge>  // long 哈希唯一标识
{
    public readonly sbyte x, y, z;
    public readonly Axis axis;
    public Edge(int x, int y, int z, Axis axis);
}

public enum Axis : byte { X, Y, Z }
[Flags] public enum CubeVertexMask { Null=0x00, V0=0x01, ..., All=0xFF }
```

## C# 兼容约束

以下新语法**禁止使用**（旧版 Unity 编译器不支持）：
- `new()` 目标类型推断 → 改为 `new Type()`
- `new (args)` → 改为 `new Type(args)`
- `Transform.SetLocalPositionAndRotation()` → 分拆为 `.localPosition` + `.localRotation`
- `HashCode.Combine()` → 改为手写 397-hash

## 使用方

- `MarchingCubes.Sample.*` — 各 Sample 场景
- `MineOasis`（部分 Sample 借用 CubeMesh 类型，非正式依赖）

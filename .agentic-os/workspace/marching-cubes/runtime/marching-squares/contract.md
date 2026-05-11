# Runtime/MarchingSquares Contract

## 核心类型（Tile.cs）

```csharp
namespace MarchingSquares

// 顶点编号
public enum TileVertex { V0, V1, V2, V3 }

// 顶点高度标志（4-bit mask，主要用于 atlas overlay 的 mask case）
[Flags] public enum TileVertexMask { None=0, V0=1, V1=2, V2=4, V3=8, All=15 }

// 边枚举
public enum TileEdge { Bottom, Right, Top, Left }

// 格点数据
public struct TilePoint
{
    public readonly sbyte x, z;   // 格点坐标
    public sbyte high;             // 高度值，运行时 clamp 到 -64~64
    public byte  terrainType;      // 0=泥 1=草 2=岩 3=雪 4=紫
    public Vector3 LocalPosition;
    public TilePoint(int x, int z);
}

// 2D 顶点
public struct TileVertex2D { public Vector3 position; public Vector2 uv; }

// 三角面
public struct TileTriangle { public TileVertex2D v0, v1, v2; }

// 接收器接口
public interface ITileTerrainReceiver
{
    void OnRebuildCompleted(Mesh mesh);    // TileTerrain.Rebuild() / RebuildHeightOnly() 完成后回调
}
```

## TileTable

```csharp
public static class TileTable
{
    public const int CornerCount    = 4;
    public const int CaseCount      = 16;   // atlas overlay 4-bit mask case 数（与 mesh case 解耦）
    public const int BaseCaseCount  = 81;   // mesh case 数组容量（base-3 编码 r0+r1*3+r2*9+r3*27 上限+1）

    public static readonly (int x, int z)[] Corners;   // [V0 BL, V1 BR, V2 TR, V3 TL]

    // ── Mesh 组合映射（base-3 编码）──────────────────────────────────────
    // base = min(h0..h3), r_i = h_i - base ∈ {0,1,2}
    // case_idx = r0 + r1*3 + r2*9 + r3*27 ∈ [0, 80]
    // 81 槽中 65 槽为有效真实几何，16 槽为死槽（永远不会从 GetMeshCase 产出）
    public static int  GetMeshCase(int h0, int h1, int h2, int h3, out int baseH);

    // ── 死槽判定（解出 r0..r3 后判 min(r) == 0）──────────────────────────
    public static bool IsValidCase(int caseIdx);

    // ── 纹理组合映射（旧 API，per-vertex 路径用，渲染主路径走 GetAtlasCase）──
    public static int GetTextureCase(byte t0, byte t1, byte t2, byte t3, byte overlayType);
    public static int GetTerrainLayers(byte t0, byte t1, byte t2, byte t3,
                                        out byte baseType,
                                        out byte overlay1, out byte overlay2, out byte overlay3);

    // ── Atlas 标准编码 API（WC3 模式渲染主路径）──────────────────────────
    // ms_idx = bit_BL | bit_BR<<1 | bit_TR<<2 | bit_TL<<3，与 GetMeshCase V0~V3 编码完全一致
    // 4 角 mask + 单 bit type → atlas case_idx (0~15)
    public static int GetAtlasCase(byte mBL, byte mBR, byte mTR, byte mTL, int bit);
    // 4 角是否参与 (true/false) → atlas case_idx (0~15)，离线工具友好
    public static int GetAtlasCase(bool BL, bool BR, bool TR, bool TL);
    // atlas case_idx → 子格 (col, row)（Unity UV，row=0 在底）
    public static (int col, int row) GetAtlasCell(int atlasCase);
}
```

## TileTerrain

```csharp
public sealed class TileTerrain
{
    public readonly int Width, Length;
    public Mesh mesh;

    public TileTerrain(int width, int length, ITileTerrainReceiver receiver = null);

    public void SetHeight(int x, int z, sbyte high);
    public void SetTerrainType(int x, int z, byte type);
    public ref TilePoint GetPoint(int x, int z);
    public void Rebuild();            // 全量重建（重新 SetVertices）
    public void RebuildHeightOnly();  // 只更新高度 Y 值，不重建拓扑
}
```

## TilePrefab

```csharp
[ExecuteAlways]
public class TilePrefab : MonoBehaviour
{
    public int caseIndex;    // 0~80（base-3 编码，65 有效 + 16 死槽）
    public int baseHeight;
    // 纯数据组件，无 Gizmos / OnEnable / OnDrawGizmos；
    // 点阵 grid 可视化由 sample/build-system/terrain.TerrainBuilder.DrawGizmos 在 Terrain 层统一渲染
}
```

## 使用方

- `marching-cubes/sample/build-system/terrain`（TerrainBuilder 依赖 TileTable.GetMeshCase + GetAtlasCase + Tile 类型）
- `marching-cubes/editor/art-mq-mesh`（MQMeshConfigEditor 依赖 TileTable.IsValidCase 决定 grid 渲染哪些槽）

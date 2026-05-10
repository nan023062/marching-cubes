# Runtime/MarchingSquares Contract

## 核心类型（Tile.cs）

```csharp
namespace MarchingSquares

public enum TileType { Terrain, Cliff }

// 顶点编号
public enum TileVertex { V0, V1, V2, V3 }

// 顶点高度标志
[Flags] public enum TileVertexMask { None=0, V0=1, V1=2, V2=4, V3=8, All=15 }

// 边枚举（地形）
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

// 悬崖边
public enum CliffEdge { S, E, N, W }
[Flags] public enum CliffEdgeMask { None=0, S=1, E=2, N=4, W=8, All=15 }

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
    public const int CornerCount     = 4;
    public const int CaseCount       = 16;
    public const int CliffCaseCount  = 16;

    public static readonly (int x, int z)[] Corners;          // [V0 BL, V1 BR, V2 TR, V3 TL]
    public static readonly (int canonical, int rotCount)[] CliffD4Map;  // 16 个悬崖 case → (规范 case, 旋转次数)
    public static readonly int[] CliffCanonicalCases;         // {1, 3, 5, 7, 15}

    // ── Mesh 组合映射 ────────────────────────────────────────────────────
    // 19 种 case：0~14 标准（高差 ≤ 1），15~18 对角高差==2 的特殊 case
    // base = 四角最小高度，bit_i=1 表示 Vi 高于 base
    public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH);

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
    public TileType tileType;    // Terrain 或 Cliff
    public int      caseIndex;   // Terrain: 0~18；Cliff: 0~15
    public int      baseHeight;
    // OnDrawGizmos：绘制四角高差线框 + case index 标签
}
```

## 使用方

- `marching-cubes/sample/build-system/terrain`（TerrainBuilder 依赖 TileTable.GetMeshCase + GetAtlasCase + CliffD4Map + Tile 类型）
- `marching-cubes/editor/art-mq-mesh`（MQMeshConfigEditor 依赖 TileTable.CliffCanonicalCases 决定 D4 派生范围）

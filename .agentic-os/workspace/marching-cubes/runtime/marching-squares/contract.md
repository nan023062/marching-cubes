# Runtime/MarchingSquares Contract

## 核心类型（Tile.cs）

```csharp
// 顶点高度标志
[Flags] public enum TileVertexMask { None=0, V0=1, V1=2, V2=4, V3=8, All=15 }

// 边枚举
public enum TileEdge { Bottom, Right, Top, Left }

// 格点数据
public struct TilePoint
{
    public sbyte high;  // 高度值，运行时 clamp 到 -64~64
    public byte terrainType;
}

// 2D 顶点（用于 mesh 生成）
public struct TileVertex2D { public float x, z; }

// 三角面
public struct TileTriangle { public TileVertex2D v0, v1, v2; }

// 接收器接口
public interface ISquareTerrainReceiver
{
    void OnRebuildCompleted(Mesh mesh);    // TileTerrain.Rebuild() / RebuildHeightOnly() 完成后回调
}
```

## MqTable

```csharp
public static class MqTable
{
    public const int CornerCount = 4;
    public const int CaseCount   = 16;

    public static readonly (int x, int z)[] Corners;   // 四角坐标 [V0 BL, V1 BR, V2 TR, V3 TL]

    // Mesh 组合映射：四角高度 → case index + base 高度
    public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH);

    // 纹理组合映射：四角 terrainType → overlay case index（0~15）
    public static int GetTextureCase(byte t0, byte t1, byte t2, byte t3, byte overlayType);

    // 提取 baseType + 最多 3 层 overlayType，返回 overlay 层数（0~3）
    public static int GetTerrainLayers(byte t0, byte t1, byte t2, byte t3,
                                        out byte baseType,
                                        out byte overlay1, out byte overlay2, out byte overlay3);
}
```

## TileTerrain

```csharp
public sealed class TileTerrain
{
    public readonly int Width, Length;
    public Mesh mesh;

    public TileTerrain(int width, int length, ISquareTerrainReceiver receiver = null);

    public void SetHeight(int x, int z, sbyte high);
    public void SetTerrainType(int x, int z, byte type);
    public ref TilePoint GetPoint(int x, int z);
    public void Rebuild();            // 全量重建（重新 SetVertices）
    public void RebuildHeightOnly();  // 只更新高度 Y 值，不重建拓扑
}
```

## MqTilePrefab

```csharp
[ExecuteAlways]
public class MqTilePrefab : MonoBehaviour
{
    public int caseIndex;    // 0–15，由 MqTerrainBuilder.RefreshTile 写入
    public int baseHeight;   // 格点 base 高度，由 MqTerrainBuilder.RefreshTile 写入
    // OnDrawGizmos：绘制四角高差线框 + case index 标签
}
```

## 使用方

- `marching-cubes/sample/build-system/terrain`（MqTerrainBuilder 依赖 MqTable + Tile 类型）
- `marching-cubes/editor/art-mq-mesh`（MqMeshConfig 依赖 MqTable）

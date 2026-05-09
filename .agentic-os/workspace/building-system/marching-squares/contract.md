# MarchingSquares Contract

## 公开接口

### MqTerrain（MonoBehaviour 薄壳）

```csharp
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class MqTerrain : MonoBehaviour
{
    public Brush            Brush        { get; }
    public int              TextureLayer { get; set; }   // 0=泥 1=草 2=岩 3=雪 4=腐
    public MqTerrainBuilder Builder      { get; }

    // 由 BuildingManager 驱动初始化
    public void Init(int renderWidth, int renderDepth, int heightRange, MqMeshConfig config);

    // 由 TerrainState 调用
    public bool BrushMapHigh(int delta);        // 高度刷绘；dirty=true 时 colliderMesh 已更新
    public bool PaintTerrainType(int type);     // 地形类型刷绘；只更新 MaterialPropertyBlock
    public void SetBrushVisible(bool visible);
}
```

### MqTerrainBuilder（纯 C# 核心）

```csharp
public class MqTerrainBuilder
{
    public const int TerrainTypeCount = 5;

    public readonly int   width, length, height;
    public readonly float unit;
    public readonly Matrix4x4 localToWorld;
    public readonly Matrix4x4 worldToLocal;
    public int MaxHeightDiff { get; set; }       // 相邻格点最大高差，默认 1

    public readonly Mesh colliderMesh;            // 碰撞 Mesh（预分配顶点，高度刷绘后同步）

    public MqTerrainBuilder(int width, int length, int height, float unit,
                             Vector3 worldPosition, MqMeshConfig config, Transform parent);

    public bool BrushMapHigh(Brush brush, int delta);       // 高度刷绘，返回 dirty
    public bool PaintTerrainType(Brush brush, int type);    // 地形类型刷绘，返回 dirty
    public void RefreshAllTiles();                           // 全量重建 tile 实例
    public void RefreshAffectedTiles(int px, int pz);       // 增量刷新受影响 tile
    public sbyte GetPointHeight(int x, int z);
    public byte  GetTerrainType(int x, int z);
    public void  DrawGizmos();
}
```

### MqMeshConfig（ScriptableObject）

```csharp
[CreateAssetMenu(menuName = "MarchingCubes/Mq Mesh Config")]
public sealed class MqMeshConfig : ScriptableObject
{
    public GameObject GetPrefab(int caseIndex);            // 0–15；超出范围返回 null
    public void       SetPrefab(int caseIndex, GameObject prefab);  // Editor 工具写入
}
```

### MqTable（静态工具类）

```csharp
public static class MqTable
{
    public const int CornerCount = 4;
    public const int CaseCount   = 16;

    public static readonly (int x, int z)[] Corners;   // 四角坐标 [V0 BL, V1 BR, V2 TR, V3 TL]

    // Mesh 组合映射：四角高度 → case index + base 高度
    public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH);

    // 纹理组合映射：四角 terrainType → overlay case index
    public static int GetTextureCase(byte t0, byte t1, byte t2, byte t3, byte overlayType);

    // 提取 baseType + 最多 3 层 overlayType，返回 overlay 层数（0~3）
    public static int GetTerrainLayers(byte t0, byte t1, byte t2, byte t3,
                                        out byte baseType,
                                        out byte overlay1, out byte overlay2, out byte overlay3);
}
```

### TilePoint（值类型，定义于 Tile.cs）

```csharp
public struct TilePoint
{
    public readonly sbyte x, z;  // 格点坐标
    public sbyte high;            // 高度值（-64~64，运行时 clamp）
    public byte  terrainType;     // 地形类型（0=泥 1=草 2=岩 3=雪 4=腐）

    public Vector3 LocalPosition { get; }    // (x, high, z)

    public TilePoint(int x, int z);
}
```

### TileTerrain（程序化 mesh 生成器）

```csharp
public sealed class TileTerrain
{
    public readonly int Width, Length;
    public Mesh mesh;

    public TileTerrain(int width, int length, ISquareTerrainReceiver receiver = null);

    public void SetHeight(int x, int z, sbyte high);
    public void SetTerrainType(int x, int z, byte type);
    public ref TilePoint GetPoint(int x, int z);
    public void Rebuild();                  // 全量重建（重新 SetVertices）
    public void RebuildHeightOnly();        // 只更新高度 Y 值，不重建拓扑
}
```

### ISquareTerrainReceiver（定义于 Tile.cs）

```csharp
public interface ISquareTerrainReceiver
{
    void OnRebuildCompleted(Mesh mesh);    // TileTerrain.Rebuild() / RebuildHeightOnly() 完成后回调
}
```

### MqTilePrefab（Editor 调试组件）

```csharp
[ExecuteAlways]
public class MqTilePrefab : MonoBehaviour
{
    public int caseIndex;    // 0–15，由 MqTerrainBuilder.RefreshTile 写入
    public int baseHeight;   // 格点 base 高度，由 MqTerrainBuilder.RefreshTile 写入
    // OnDrawGizmos：绘制四角高差线框 + case index 标签
}
```

### Brush（MonoBehaviour）

```csharp
public class Brush : MonoBehaviour
{
    public bool colorBrush;     // true = 地形类型笔刷；false = 高度笔刷
    public int Size { get; }    // 笔刷尺寸（SerializeField 可调）
    // transform.position 用于 MqTerrainBuilder.CalculateArea 定位笔刷中心
}
```

## 渲染集成约定

使用 `MqTerrain` 的场景须：
1. 将 `MqTerrainBuilder.colliderMesh` 赋给 `MeshFilter.sharedMesh` 和 `MeshCollider.sharedMesh`（`MqTerrain.Init` 内完成）
2. `BrushMapHigh` 返回 `dirty=true` 后，`MqTerrain` 自动重新赋给 `MeshFilter` 和 `MeshCollider`，调用方无需手动处理
3. 地形类型刷绘（`PaintTerrainType`）只更新 `MaterialPropertyBlock`，不触发 tile 销毁重建

## 使用方

- `BuildingManager` — 持有 `MqTerrain` 引用，Init + 状态机驱动
- `TerrainState` — 调用 `MqTerrain.BrushMapHigh / PaintTerrainType / SetBrushVisible`
- `BuildState.SyncWithTerrain(MqTerrainBuilder)` — 读取地形高度数据同步建造底面

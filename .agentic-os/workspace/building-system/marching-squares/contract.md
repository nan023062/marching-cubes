# MarchingSquares Contract

## 公开接口

### MSQTerrain
```csharp
public partial class MSQTerrain
{
    public const int TerrainTypeCount = 5;   // 0=泥 1=草 2=岩 3=雪 4=腐

    public readonly int width, length, height;
    public readonly float unit;
    public readonly Matrix4x4 localToWorld;
    public readonly Matrix4x4 worldToLocal;
    public readonly Mesh mesh;               // 地形网格（预分配顶点，刷绘只更新 UV/Color）
    public readonly Mesh cliffMesh;          // 悬崖壁面网格（动态 List 构建）

    public MSQTerrain(int width, int length, int height, float unit, Vector3 position);

    public bool BrushMapHigh(Brush brush, int delta);        // 高度刷绘，返回 dirty
    public bool PaintTerrainType(Brush brush, int type);     // 地形类型刷绘，返回 dirty
    public void RebuildCliffMesh();                          // 强制重建悬崖网格
    public void OnDrawGizmos();
}
```

### MSQTerrain.Point（partial，Chunk.cs）
```csharp
public struct Point
{
    public sbyte high;          // 高度，运行时 clamp 到 -64~64（sbyte 声明范围 -128~127）
    public byte terrainType;    // 地形类型索引（0~4）
}
```

### Brush（MonoBehaviour）
```csharp
public class Brush : MonoBehaviour
{
    public bool colorBrush;     // true = 地形类型笔刷；false = 高度笔刷
    public int Size { get; }    // 笔刷尺寸（1~5，SerializeField 可调）
    // transform.position 用于 MSQTerrain.CalculateArea 定位笔刷中心
}
```

## 渲染集成约定

使用 `MSQTerrain` 的 MonoBehaviour 必须：
1. 将 `mesh` 赋给 `MeshFilter.sharedMesh` 和 `MeshCollider.sharedMesh`
2. 创建子 GameObject "CliffWalls"，将 `cliffMesh` 赋给其 `MeshFilter.sharedMesh`，挂配套材质
3. 高度刷绘返回 `dirty=true` 后，同时刷新 `mesh.vertices` 和 `cliffMesh`（`BrushMapHigh` 内部已处理，无需手动调用 `RebuildCliffMesh`）
4. 地形类型刷绘只更新 UV/Color，不触发悬崖重建

## 使用方

- `MarchingSquares.Sample.MarchingQuad25Sample` — 交互式地形编辑器

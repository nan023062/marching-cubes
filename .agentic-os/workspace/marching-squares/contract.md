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
    public readonly Mesh mesh;               // 地形网格（预分配顶点）
    public readonly Mesh cliffMesh;          // 悬崖壁面网格（动态构建）

    public MSQTerrain(int width, int length, int height, float unit, Vector3 position);

    public bool BrushMapHigh(Brush brush, int delta);        // 高度刷绘，返回 dirty
    public bool PaintTerrainType(Brush brush, int type);     // 地形类型刷绘，返回 dirty
    public void OnDrawGizmos();
}
```

### MSQTerrain.Point（partial，Chunk.cs）
```csharp
public struct Point
{
    public sbyte high;          // 高度（-128~127）
    public byte terrainType;    // 地形类型（0~4，EncodeType = type*51）
}
```

### Brush
```csharp
public struct Brush   // 具体字段待读 Brush.cs 确认
{
    public bool colorBrush;   // true=地形类型笔刷，false=高度笔刷
    // 半径等字段（推断）
}
```

## 渲染集成约定

使用 `MSQTerrain` 的 MonoBehaviour 必须：
1. 将 `mesh` 赋给 `MeshFilter.sharedMesh` 和 `MeshCollider.sharedMesh`
2. 创建子 GameObject "CliffWalls"，将 `cliffMesh` 赋给其 `MeshFilter.sharedMesh`，并挂配套 `cliffMaterial`
3. 刷绘后若返回 `dirty=true`，需同时刷新 `mesh` 和 `cliffMesh` 引用

## 使用方

- `MarchingSquares.Sample.MarchingQuad25Sample` — 交互式编辑器示例

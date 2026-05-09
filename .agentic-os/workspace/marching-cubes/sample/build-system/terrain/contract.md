# Terrain Contract

## MqTerrain

```csharp
namespace MarchingSquares
[RequireComponent(typeof(MeshCollider), typeof(MeshRenderer), typeof(MeshFilter))]
public class MqTerrain : MonoBehaviour
{
    public Brush Brush { get; }
    public int TextureLayer { get; set; }      // 当前涂色类型（0~4）
    public MqTerrainBuilder Builder { get; }
    public void Init(int renderWidth, int renderDepth, int heightRange, MqMeshConfig config);
    public bool BrushMapHigh(int delta);        // 高度刷绘，返回 dirty
    public bool PaintTerrainType(int type);    // 地形类型刷绘，返回 dirty
    public void SetBrushVisible(bool visible);
}
```

## MqTerrainBuilder

```csharp
namespace MarchingSquares
public class MqTerrainBuilder
{
    public Mesh colliderMesh { get; }
    public Matrix4x4 localToWorld { get; }
    public Matrix4x4 worldToLocal { get; }
    public int MaxHeightDiff { get; set; }
    public bool BrushMapHigh(Brush brush, int delta);
    public bool PaintTerrainType(Brush brush, int type);
    public void RefreshAllTiles();
    public float GetPointHeight(int x, int z);  // 返回格点高度（世界单位）
    public void DrawGizmos();
}
```

## MqMeshConfig

```csharp
namespace MarchingSquares
public class MqMeshConfig : ScriptableObject
{
    public GameObject GetPrefab(int caseIndex);  // caseIndex: 0~15
    public void SetPrefab(int caseIndex, GameObject prefab);
}
```

## Brush

```csharp
namespace MarchingSquares
public class Brush : MonoBehaviour
{
    public int Size { get; set; }
    public bool colorBrush;
}
```

## 使用方

- `sample/build-system`（BuildingManager 持有 MqTerrain 引用）
- `sample/build-system/structure`（BuildState.SyncWithTerrain 读取 MqTerrainBuilder.GetPointHeight）

# Terrain Contract

## TerrainController

```csharp
namespace MarchingSquares
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainController : BuildController   // 继承 BuildController
{
    // Inspector 绑定（继承自基类）：protected Cursor _cursor → 实际绑 PlaneCursor 实例
    // Inspector 绑定（自身）：TileCaseConfig _meshConfig；int _textureLayer(0~4)；bool _colorBrush

    public int            TextureLayer { get; set; }
    public TerrainBuilder Builder      { get; }

    public void Init(int renderWidth, int renderDepth, int heightRange);
    public bool BrushMapHigh(int delta);
    public bool PaintTerrainType(int type);    // Add: mask |= 1<<type
    public bool EraseTerrainType(int type);    // Erase: mask &= ~(1<<type)
    public bool ClearTerrainMask();            // Clear: mask = 0
    public bool IsCellFlat(int cx, int cz, out int baseH);

    // 继承自 BuildController：
    // - OnPointerMove / OnPointerClick（override）
    // - SetActive(bool)：控制激活 + _cursor 显隐
    // - SetInteraction(bool)：控制 renderer + collider enabled
}
```

## TerrainBuilder

```csharp
namespace MarchingSquares
public class TerrainBuilder
{
    public const int TerrainTypeCount = 5;

    public readonly int       width, length, height;
    public readonly float     unit;
    public readonly Matrix4x4 localToWorld;
    public readonly Matrix4x4 worldToLocal;
    public int MaxHeightDiff { get; set; } = 2;

    // 硬约束：width == length 且为 2 的次幂；违反抛 ArgumentException
    public TerrainBuilder(int width, int length, int height, float unit, Vector3 worldPosition);

    // Brush 参数：TerrainBuilder 通过 brush.Size 和 brush.transform.position 读取笔刷范围
    public bool  BrushMapHigh(Brush brush, int delta, out HashSet<(int,int)> changedPoints);
    public bool  PaintTerrainType(Brush brush, int type, out HashSet<(int,int)> dirtyPoints);
    public bool  EraseTerrainType(Brush brush, int type, out HashSet<(int,int)> dirtyPoints);
    public bool  ClearTerrainMask(Brush brush, out HashSet<(int,int)> dirtyPoints);
    public sbyte GetPointHeight(int x, int z);
    public byte  GetTerrainMask(int x, int z);
    public bool  IsCellFlat(int cx, int cz, out int baseH);
    public void  AppendExposedFaces(List<Vector3> verts, List<int> tris);
}
```

## Cursor / Brush / PlaneCursor

```csharp
namespace MarchingSquares

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Cursor : MonoBehaviour
{
    public int Size { get; }           // [Range(1,5)] Inspector 配置
    public void SetMaterial(Material m);
}

// TerrainBuilder 签名兼容层（无额外成员）
public class Brush : Cursor { }

// 地形 hover 实现：Awake 生成 1×1 平面 mesh，法线朝 +Y
public class PlaneCursor : Brush { }
```

## TileCaseConfig

```csharp
namespace MarchingSquares
public sealed class TileCaseConfig : ScriptableObject
{
    public const int TerrainCaseCount = 81;
    public GameObject GetPrefab(int caseIndex);
    public void       SetPrefab(int caseIndex, GameObject prefab);
#if UNITY_EDITOR
    public string   editorFbxFolder;
    public string   editorPrefabFolder;
    public Material editorTerrainMat;
#endif
}
```

## 使用方

- `sample/build-system`（BuildingManager 持有 TerrainController 引用）
- `sample/build-system/structure`（McController.SyncWithTerrain 读取 TerrainBuilder.GetPointHeight / IsCellFlat）
- `editor/art-mq-mesh`（MQMeshConfigEditor 读写 TileCaseConfig.editor* 字段）

## 跨模块隐含契约

- **art-mq-mesh prefab UV 布局**：BL=(0,0), BR=(1,0), TR=(1,1), TL=(0,1)；SplatmapTerrain.shader 用 lUV 定位 atlas 4×4 子格。任何旋转/镜像操作导致 atlas 采样错位且无编译报错。
- **atlas 美术约定**：`_OverlayArray` 5 layer 2DArray；ms_idx = `bit_BL | bit_BR<<1 | bit_TR<<2 | bit_TL<<3`；C# / shader 一律走 `TileTable.GetAtlasCase`。

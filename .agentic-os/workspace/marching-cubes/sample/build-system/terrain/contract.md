# Terrain Contract

## Terrain

```csharp
namespace MarchingSquares
[RequireComponent(typeof(MeshCollider), typeof(MeshRenderer), typeof(MeshFilter))]
public class Terrain : MonoBehaviour
{
    public Brush           Brush        { get; }
    public int             TextureLayer { get; set; }   // 当前涂色类型（0~7）
    public TerrainBuilder  Builder      { get; }

    // TileCaseConfig 通过 [SerializeField] 在 Inspector 注入，不在 Init 参数里
    public void Init(int renderWidth, int renderDepth, int heightRange);
    public bool BrushMapHigh(int delta);        // 高度刷绘，返回 dirty
    public bool PaintTerrainType(int type);     // 地形类型刷绘（Add 语义：mask |= 1<<type），返回 dirty
    public bool EraseTerrainType(int type);     // 地形类型擦除（mask &= ~(1<<type)），返回 dirty
    public bool ClearTerrainMask();             // 一键清空：mask = 0（fallback 到 _BaseTex），返回 dirty
    public void SetBrushVisible(bool visible);
}
```

## TerrainBuilder

```csharp
namespace MarchingSquares
public class TerrainBuilder
{
    public const int TerrainTypeCount = 8;   // R 通道 8-bit bitmask 上限

    public readonly int       width, length, height;
    public readonly float     unit;
    public readonly Matrix4x4 localToWorld;
    public readonly Matrix4x4 worldToLocal;
    public readonly Mesh      colliderMesh;
    public readonly Texture2D pointTex;
    public int MaxHeightDiff { get; set; } = 1;

    public TerrainBuilder(int width, int length, int height, float unit,
                          Vector3 worldPosition, TileCaseConfig config, Transform parent);

    public bool  BrushMapHigh(Brush brush, int delta);
    public bool  PaintTerrainType(Brush brush, int type);   // Add: mask |= 1<<type
    public bool  EraseTerrainType(Brush brush, int type);   // Erase: mask &= ~(1<<type)
    public bool  ClearTerrainMask(Brush brush);             // Clear: mask = 0（一键清空所有 type）
    public void  RefreshAllTiles();
    public void  RefreshAffectedTiles(int px, int pz);
    public sbyte GetPointHeight(int x, int z);
    public byte  GetTerrainMask(int x, int z);              // 返回 8-bit bitmask（旧 GetTerrainType 已废弃）
    public void  RefreshPointTexAll();
    public void  DrawGizmos();
}
```

## TileCaseConfig

```csharp
namespace MarchingSquares
public sealed class TileCaseConfig : ScriptableObject
{
    public const int TerrainCaseCount = 19;   // 地形 case 0~18
    public const int CliffCaseCount   = 16;   // 悬崖 case 0~15（case 0 留空）

    // 地形 API
    public GameObject GetPrefab(int caseIndex);            // 0~18
    public void       SetPrefab(int caseIndex, GameObject prefab);

    // 悬崖 API
    public GameObject GetCliffPrefab(int caseIndex);       // 0~15
    public void       SetCliffPrefab(int caseIndex, GameObject prefab);

    // 法线贴图 API（运行时引用，Blender 烘焙 → art-mq-mesh Refresh 写入）
    public Texture2D GetNormalMap(int caseIndex);          // 0~18，越界返回 null
    public void      SetNormalMap(int caseIndex, Texture2D tex);

#if UNITY_EDITOR
    // 编辑器持久化字段（[HideInInspector]，仅 art-mq-mesh 工具读写，不进运行时）
    public string   editorFbxFolder;
    public string   editorPrefabFolder;
    public Material editorTerrainMat;
    public Material editorCliffMat;
#endif
}
```

## Brush

```csharp
namespace MarchingSquares
public class Brush : MonoBehaviour
{
    public int  Size { get; set; }
    public bool colorBrush;
}
```

## 使用方

- `sample/build-system`（BuildingManager 持有 Terrain 引用）
- `sample/build-system/structure`（BuildState.SyncWithTerrain 读取 TerrainBuilder.GetPointHeight）
- `editor/art-mq-mesh`（MQMeshConfigEditor 读写 TileCaseConfig 的 editor* 字段 + 双 prefab API）

## 跨模块隐含契约

- **art-mq-mesh prefab UV 布局**：每个 quad 顶点 UV 必须满足 BL=(0,0), BR=(1,0), TR=(1,1), TL=(0,1)。SplatmapTerrain.shader 的 4 角混合（DecodeCorner + 双线性插值）依赖这个 UV 约定按 quad 内 [0,1]×[0,1] 解释 fragment 位置。Blender 端 `mq_mesh.py` 烘焙的 UV (col/sub, row/sub) 已符合此约定。**任何修改 prefab UV 顺序的改动（如旋转、镜像）将导致 SplatmapTerrain 4 角颜色错位且无任何编译/运行报错**。

# Structure Contract

## Structure

```csharp
namespace MarchingCubes.Sample
public class Structure : MonoBehaviour
{
    public uint unit;                       // 整数边长（由 BuildingConst.Unit 初始化）

    // Prefab 引用（[SerializeField]，public getter）：BuildState 通过 getter 取，BuildingManager 不再持有
    public GameObject PointCubePrefab { get; }
    public GameObject PointQuadPrefab { get; }

    public int RenderWidth  { get; }
    public int BuildHeight  { get; }
    public int RenderDepth  { get; }
    public int ConfigCount        { get; }
    public int CurrentConfigIndex { get; }
    public string GetConfigName(int index);
    public void SwitchConfig(int index);
    public void SetBuildHandlers(Action<PointElement,bool,Vector3> onClick, Action onRefresh);
    public void OnClicked(PointElement element, bool left, in Vector3 normal);
    public void Init(int renderWidth, int buildHeight, int renderDepth);
    public GameObject GetMesh(int cubeIndex);  // 实例化 prefab，返回 null 当 case=0/255
}
```

## McStructureBuilder

```csharp
namespace MarchingCubes.Sample
public class McStructureBuilder
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public McStructureBuilder(int x, int y, int z, Matrix4x4 matrix, McStructure structure);
    public void SetPointStatus(int x, int y, int z, bool solid); // iso=1(solid)/0(empty)
    public void RefreshAllMeshes();
}
```

## CasePrefabConfig

```csharp
namespace MarchingCubes.Sample
public abstract class CasePrefabConfig : ScriptableObject
{
    public abstract GameObject GetPrefab(int caseIndex);
}
// 实现：D4FbxCaseConfig（53 canonical FBX → 255 prefab）
// 实现：IosMeshCaseConfig（256 mesh 直接映射）
```

## D4FbxCaseConfig

```csharp
namespace MarchingCubes
public sealed class D4FbxCaseConfig : CasePrefabConfig
{
    // 运行时 API（CasePrefabConfig 实现）
    public override GameObject GetPrefab(int cubeIndex);  // 0/255 → null
    public void SetPrefab(int cubeIndex, GameObject prefab);

    // D4 对称查询（编辑器构建 prefab 时使用）
    public int        GetCanonicalIndex(int ci);
    public Quaternion GetRotation(int ci);
    public bool       GetFlipped(int ci);
    public bool       IsCanonical(int ci);

#if UNITY_EDITOR
    // 编辑器持久化字段（[HideInInspector]，仅 art-mc-mesh 工具读写，不进运行时）
    public string    editorFbxFolder;
    public string    editorPrefabFolder;
    public Material  editorMaterial;

    // 法线贴图烘焙参数 + 产物引用
    public int       editorNoiseSeed;
    public int       editorNoiseOctaves        = 3;
    public float     editorNoiseAmplitude      = 1.0f;
    public float     editorNoiseFrequency      = 4.0f;
    public int       editorNormalMapResolution = 128;
    public Texture2D[] editorNormalMaps = new Texture2D[53];  // 下标 = canonical case 0~52
#endif
}
```

## PointElement / PointCube / PointQuad

```csharp
public class PointElement : MonoBehaviour { public Structure mcs; }
public class PointCube   : PointElement   { public int x, y, z; }    // 体素索引（cube 占 [x..x+1, y..y+1, z..z+1]）
public class PointQuad   : PointElement   { public int cx, cz; }     // cell 索引（仅平地 cell 才有实例；与 cube 索引语义对齐：CreateCube(cx,1,cz) 在该平地 cell 正上方）
```

## BuildState（IBuildState）

```csharp
// ctor：prefab 不再从外部注入，由 _structure 自持
public BuildState(Structure structure);

// 关键公开方法（BuildingManager 间接调用）
public void SyncWithTerrain(MarchingSquares.TerrainBuilder terrain);
// 段一：扫所有 cell，按 terrain.IsCellFlat(cx,cz,out baseH) 增/删/移位 PointQuad（cell 中心 (cx+0.5, baseH+0.5, cz+0.5)）
// 段二：扫所有 PointCube，4 角 terrain 高度 > cube.y 则销毁（地形上来后压住的方块清理）
```

## 使用方

- `sample/build-system`（BuildingManager 持有 McStructure 引用）
- `editor/art-mc-mesh`（D4FbxCaseConfig / IosMeshCaseConfig 由编辑器工具构建 prefab）

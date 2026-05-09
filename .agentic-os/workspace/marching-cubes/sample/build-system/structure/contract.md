# Structure Contract

## McStructure

```csharp
namespace MarchingCubes.Sample
public class McStructure : MonoBehaviour
{
    public uint unit;                       // 整数边长（由 BuildingConst.Unit 初始化）
    public GameObject pointCubePrefab;
    public GameObject pointQuadPrefab;
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

## PointElement / PointCube / PointQuad

```csharp
public class PointElement : MonoBehaviour { public McStructure mcs; }
public class PointCube   : PointElement   { public int x, y, z; }
public class PointQuad   : PointElement   { public int x, z; }
```

## BuildState（IBuildState）

```csharp
// 关键公开方法（BuildingManager 间接调用）
public void SyncWithTerrain(MarchingSquares.MqTerrainBuilder terrain);
```

## 使用方

- `sample/build-system`（BuildingManager 持有 McStructure 引用）
- `editor/art-mc-mesh`（D4FbxCaseConfig / IosMeshCaseConfig 由编辑器工具构建 prefab）

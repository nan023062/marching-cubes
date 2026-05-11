# Structure Contract

## McController

```csharp
namespace MarchingCubes.Sample
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class McController : BuildController   // 继承 BuildController
{
    public uint unit;   // 整数边长（= BuildingConst.Unit）

    // Inspector 绑定（继承自基类）：protected Cursor _cursor → 实际绑 CubeCursor 实例
    // Inspector 绑定（自身）：CasePrefabConfig[] _configs；int _currentConfigIndex

    public StructureBuilder Builder { get; }
    public int RenderWidth  { get; }
    public int BuildHeight  { get; }
    public int RenderDepth  { get; }
    public int ConfigCount        { get; }
    public int CurrentConfigIndex { get; }
    public string GetConfigName(int index);
    public void SwitchConfig(int index);     // 直接调 RefreshAllCubes()，无 event

    public void Init(int renderWidth, int buildHeight, int renderDepth);
    public void SetTerrain(TerrainBuilder t);
    public void SyncWithTerrain(TerrainBuilder terrain);
    public GameObject GetMesh(int cubeIndex);

    // 继承自 BuildController：
    // - OnPointerMove / OnPointerClick（override）
    // - SetActive(bool)：控制激活 + _cursor 显隐
    // - SetInteraction(bool)：控制 renderer + collider enabled
}
```

## StructureBuilder

```csharp
namespace MarchingCubes.Sample
public class StructureBuilder
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public readonly Matrix4x4 localToWorld;

    public StructureBuilder(int x, int y, int z, Matrix4x4 localToWorld);

    public void SetPointStatus(int x, int y, int z, bool active);  // iso=1(solid)/0(empty)
    public bool IsPointActive(int x, int y, int z);
    public int  GetCubeIndex(int x, int y, int z);
    public bool IsQuadActive(int cx, int cz);
    public int  GetQuadBaseH(int cx, int cz);
    public void SetQuadActive(int cx, int cz, bool active, int baseH = 0);
    public void AppendExposedFaces(List<Vector3> verts, List<int> tris);
}
```

## CubeCursor

```csharp
namespace MarchingCubes.Sample
// MC hover 实现：Awake 用 Unity 内置 Cube primitive mesh
public class CubeCursor : MarchingSquares.Cursor { }
```

## CasePrefabConfig / D4FbxCaseConfig / IosMeshCaseConfig

```csharp
namespace MarchingCubes.Sample
public abstract class CasePrefabConfig : ScriptableObject
{
    public abstract GameObject GetPrefab(int caseIndex);
}

public sealed class D4FbxCaseConfig : CasePrefabConfig
{
    public override GameObject GetPrefab(int cubeIndex);
    public void SetPrefab(int cubeIndex, GameObject prefab);
    public int        GetCanonicalIndex(int ci);
    public Quaternion GetRotation(int ci);
    public bool       GetFlipped(int ci);
    public bool       IsCanonical(int ci);
#if UNITY_EDITOR
    public string    editorFbxFolder, editorPrefabFolder;
    public Material  editorMaterial;
    public int       editorNoiseSeed, editorNoiseOctaves;
    public float     editorNoiseAmplitude, editorNoiseFrequency;
    public int       editorNormalMapResolution;
    public Texture2D[] editorNormalMaps;  // 长度 53，下标 = canonical case
#endif
}

public sealed class IosMeshCaseConfig : CasePrefabConfig
{
    public override GameObject GetPrefab(int caseIndex);
}
```

## 使用方

- `sample/build-system`（BuildingManager 持有 McController 引用）
- `editor/art-mc-mesh`（D4FbxCaseConfig / IosMeshCaseConfig 由编辑器工具构建 prefab）

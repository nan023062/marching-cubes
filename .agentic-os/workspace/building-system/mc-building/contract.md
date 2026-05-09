# MC-Building Contract

## 公开接口

### McStructureBuilder

```csharp
public class McStructureBuilder
{
    public readonly int X, Y, Z;
    public readonly McStructure MeshStore;   // 直接持有 McStructure（原 IMeshStore，已内联）
    public Matrix4x4 localToWorld { get; set; }

    public McStructureBuilder(int x, int y, int z, Matrix4x4 localToWorld, McStructure meshStore);

    public void SetPointStatus(int x, int y, int z, bool active);  // 更新格点，增量重建受影响 cube
    public void RefreshAllMeshes();                                  // 全量重建（config 切换时用）
    public void DrawPoints();                                        // Gizmos 调试
}
```

### McStructure

```csharp
public class McStructure : MonoBehaviour
{
    public uint unit;                        // 建造单元边长（uint，由 BuildingConst.Unit 对齐）

    public int RenderWidth  { get; }         // 由 BuildingManager.Init 注入
    public int BuildHeight  { get; }
    public int RenderDepth  { get; }

    // ── Config 访问 ──
    public int    ConfigCount        { get; }
    public int    CurrentConfigIndex { get; }
    public string GetConfigName(int index);
    public void   SwitchConfig(int index);

    // ── 委托桥（BuildState 注入）──
    public void SetBuildHandlers(
        System.Action<PointElement, bool, Vector3> clickHandler,
        System.Action onConfigChanged);
    public void OnClicked(PointElement element, bool left, in Vector3 normal);

    // ── 初始化 ──
    public void Init(int renderWidth, int buildHeight, int renderDepth);

    // ── 网格供给（IMeshStore 等价，内联实现）──
    public GameObject GetMesh(int cubeIndex);   // 返回已 Instantiate 的 GameObject，null = 无网格
}
```

### CasePrefabConfig（抽象基类）

```csharp
public abstract class CasePrefabConfig : ScriptableObject
{
    public abstract GameObject GetPrefab(int caseIndex);
}
```

### D4FbxCaseConfig

```csharp
[CreateAssetMenu(menuName = "MarchingCubes/D4 Fbx Case Config")]
public sealed class D4FbxCaseConfig : CasePrefabConfig
{
    public override GameObject GetPrefab(int cubeIndex);      // index 0 / 255 返回 null
    public void SetPrefab(int cubeIndex, GameObject prefab);  // Editor 工具写入
    public void EnsureSymmetry();                             // 初始化 D4 归约缓存
    public int        GetCanonicalIndex(int ci);
    public Quaternion GetRotation(int ci);
    public bool       GetFlipped(int ci);
    public bool       IsCanonical(int ci);
}
```

### IosMeshCaseConfig

```csharp
[CreateAssetMenu(menuName = "MarchingCubes/Ios Mesh Case Config")]
public sealed class IosMeshCaseConfig : CasePrefabConfig
{
    public override GameObject GetPrefab(int caseIndex);
    public void SetPrefab(int caseIndex, GameObject prefab);  // Editor 工具写入
}
```

### PointElement / PointCube / PointQuad

```csharp
public abstract class PointElement : MonoBehaviour
{
    public McStructure mcs;   // 所属建造结构引用
    // 点击事件由碰撞检测触发，转发至 mcs.OnClicked
}

public class PointCube : PointElement { /* 3D 格点：左键放置/右键删除 */ }
public class PointQuad : PointElement { /* 底面 2D 格点：点击从 y=1 层开始建造 */ }
```

## Editor 工具

| 工具 | 入口 | 职责 |
|------|------|------|
| `D4FbxCaseConfigEditor` | Inspector | 从 FBX 文件夹批量生成 255 prefab，含 D4 变换 |
| `IosMeshCaseConfigEditor` | Inspector | 从 cm_*.asset 批量生成 256 prefab |
| `CaseMeshProceduralGenerator` | MenuItem | 程序化算法生成 case mesh，写入 D4FbxCaseConfig |

## 使用方

- `BuildState` — 持有 `McStructureBuilder`，处理点击交互，驱动 `SetPointStatus / RefreshAllMeshes`
- `BuildingManager` — 持有 `McStructure` 引用，Init + 状态机驱动

# MC-Building Contract

## 公开接口

### IMeshStore（定义于 BlockMesh.cs）
```csharp
public interface IMeshStore
{
    GameObject GetMesh(int cubeIndex);   // 返回已 Instantiate 的 GameObject，null = 无网格
}
```

### McStructureBuilder
```csharp
public class McStructureBuilder
{
    public readonly int X, Y, Z;
    public readonly IMeshStore MeshStore;
    public Matrix4x4 localToWorld { get; set; }

    public McStructureBuilder(int x, int y, int z, Matrix4x4 localToWorld, IMeshStore meshStore);

    public void SetPointStatus(int x, int y, int z, bool active);  // 更新格点，增量重建受影响 cube
    public void RefreshAllMeshes();                                  // 全量重建（config 切换时用）
    public void DrawPoints();                                        // Gizmos 调试
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
    public override GameObject GetPrefab(int cubeIndex);     // index 0 / 255 返回 null
    public void SetPrefab(int cubeIndex, GameObject prefab); // Editor 工具写入
    public void EnsureSymmetry();                            // 初始化 D4 归约缓存
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
    public void SetPrefab(int caseIndex, GameObject prefab); // Editor 工具写入
}
```

### McStructure
```csharp
public class McStructure : MonoBehaviour, IMeshStore
{
    public int x, y, z;    // 格点维度
    public uint unit;       // 建造单元边长（uint，必须与 MSQTerrain.unit 的实际值对齐）

    public int ConfigCount { get; }
    public int CurrentConfigIndex { get; }
    public void SwitchConfig(int index);

    public void SetPoint(int px, int py, int pz, bool active);
    public void OnClicked(PointElement element, bool left, in Vector3 normal);
}
```

## Editor 工具

| 工具 | 入口 | 职责 |
|------|------|------|
| `D4FbxCaseConfigEditor` | Inspector | 从 FBX 文件夹批量生成 255 prefab，含 D4 变换 |
| `IosMeshCaseConfigEditor` | Inspector | 从 cm_*.asset 批量生成 256 prefab |
| `CaseMeshProceduralGenerator` | MenuItem | 程序化算法生成 case mesh，写入 D4FbxCaseConfig |

## 使用方

- `MarchingCubes.Sample.McStructure` — 主交互入口

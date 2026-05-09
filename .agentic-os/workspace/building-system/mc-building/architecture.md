# MC-Building Architecture

## 定位

基于 Marching Cubes 算法的离散格点 3D 建造系统。以 3D 格点（Point.iso）为输入，查表得 case index，Instantiate 对应的 prefab 拼合成完整建造结构。交互逻辑由外部 BuildState 驱动，McStructure 本身只做薄壳。

## 内部结构

```
Assets/MarchingCubes/Sample/BuildSystem/Structure/
├── McStructure.cs        MonoBehaviour 薄壳：参数配置（unit/configs/prefab引用）
│                         + 委托桥（BuildState 注入 clickHandler/onConfigChanged）
│                         + GetMesh(cubeIndex) 内联实现（不声明 IMeshStore 接口）
├── McStructureBuilder.cs 建造算法核心（纯C#）：Point[,,] + Cube[,,]
│                         SetPointStatus / RefreshAllMeshes，依赖 McStructure.GetMesh
├── CasePrefabConfig.cs   抽象基类（ScriptableObject）：GetPrefab(int) → GameObject
├── D4FbxCaseConfig.cs    53 canonical FBX + D4 对称归约 → 255 prefab（EnsureSymmetry 缓存）
├── IosMeshCaseConfig.cs  256 mesh 直接 1:1 → 256 prefab
├── PointElement.cs       交互点基类
├── PointCube.cs          3D 格点交互：点击放置 / 右键删除
├── PointQuad.cs          底面 2D 格点：点击从地面开始建造
└── BuildState.cs         状态机实现（由 BuildingManager 驱动）：持有 McStructureBuilder，
                          处理点击交互 + config 切换 + SyncWithTerrain
```

## McStructureBuilder 算法（Facts）

```
Point[X+1, Y+1, Z+1]   格点数组（角点，含边界）
Cube[X, Y, Z]           cube 数组（格，不含边界）
```

- `Point.iso`：float，0 = 空，1 = 实；**阈值 0.5f**（`iso > 0.5f` 为实心顶点）
- `Point.position`：Vector3，构造时按坐标初始化
- `Cube[i,j,k]` 通过 `CubeTable.Vertices[v]` 偏移索引到 8 个角点 `Point`，组合得 `cubeIndex`
- `SetPointStatus(x,y,z,active)`：更新 `iso` → 重算受影响的最多 8 个相邻 cube（clamp 到边界）
- `RefreshAllMeshes()`：全量重建所有 cube（config 切换时调用）
- cube mesh 坐标：`localToWorld.MultiplyPoint(new Vector3(i, j, k))`（格点左下角）
- `McStructureBuilder.MeshStore` 字段类型为 `McStructure`（直接持有引用，不通过接口）

## McStructure 职责边界（重构后）

重构前：`McStructure` 实现 `IMeshStore` 接口，自身处理点击交互和 config 切换 UI。  
重构后：
- `McStructure.Init(renderWidth, buildHeight, renderDepth)` — 只做 Transform 缩放设置和尺寸存储
- `McStructure.SetBuildHandlers(clickHandler, onConfigChanged)` — BuildState 注入委托
- `McStructure.GetMesh(cubeIndex)` — 内联 IMeshStore 逻辑，不再声明接口
- 交互逻辑（SetPointStatus / 点击处理 / config UI）全部迁移到 BuildState

## Config 系统

```
CasePrefabConfig (abstract ScriptableObject)
├── D4FbxCaseConfig   — FBX 来源，EnsureSymmetry() 缓存 D4 归约表；Editor 按 canonical + 变换生成 prefab
└── IosMeshCaseConfig — 256 mesh 直接映射；Editor 逐个加载 cm_{ci}.asset 生成 prefab
```

`McStructure._configs: CasePrefabConfig[]` 支持多套 config 切换（由 BuildState OnGUI 触发 `McStructure.SwitchConfig`）。

## 交互点系统

- `PointQuad`：铺在 y=0 平面，点击触发 `CreateCube(x, 1, z)`（从第一层开始）
- `PointCube`：放置在格点，点击法线方向 `CreateCube`；右键 `DestroyCube`
- 两者均通过 `McStructure.OnClicked` → `_clickHandler`（BuildState 注入）驱动

## BuildState.SyncWithTerrain（跨模块数据传递点）

```csharp
// BuildState 调用，地形改造完成后同步建造底面高度
public void SyncWithTerrain(MqTerrainBuilder terrain)
```

这是 mc-building 与 marching-squares 之间唯一的运行时数据交换点，通过 BuildingManager 编排调用，不存在直接代码依赖。

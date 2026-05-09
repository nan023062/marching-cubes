# MC-Building Architecture

## 定位

基于 Marching Cubes 算法的离散格点 3D 建造系统。以 3D 格点（Point.iso）为输入，查表得 case index，Instantiate 对应的 prefab 拼合成完整建造结构。

## 内部结构

```
Assets/MarchingCubes/Sample/McStructure/mc/
├── McStructure.cs        入口 MonoBehaviour：格点管理 + IMeshStore + Config UI + PointCube/Quad 交互
├── McStructureBuilder.cs     建造算法核心：Point[,,] + Cube[,,]，SetPointStatus / RefreshAllMeshes
├── BlockMesh.cs         IMeshStore 接口定义 + 空壳 BlockMesh 类（同文件）
├── IMeshStore           接口，定义于 BlockMesh.cs（不独立成文件）
├── CasePrefabConfig.cs  抽象基类（ScriptableObject）：GetPrefab(int) → GameObject
├── D4FbxCaseConfig.cs   53 canonical FBX + D4 对称归约 → 255 prefab（EnsureSymmetry 缓存）
├── IosMeshCaseConfig.cs 256 mesh 直接 1:1 → 256 prefab
├── PointElement.cs      交互点基类
├── PointCube.cs         3D 格点交互：点击放置 / 右键删除
└── PointQuad.cs         底面 2D 格点：点击从地面开始建造
```

## McStructureBuilder 算法（Facts）

```
Point[X+1, Y+1, Z+1]   格点数组（角点，含边界）
Cube[X, Y, Z]           cube 数组（格，不含边界）
```

- `Point.iso`：float，0 = 空，1 = 实；**阈值 0.5f**（`iso > 0.5f` 为实心顶点）
- `Point.position`：Vector3，构造时按坐标初始化
- `Cube[v]` 通过 `CubeTable.Vertices[v]` 偏移索引到 8 个角点 `Point`，组合得 `cubeIndex`
- `SetPointStatus(x,y,z,active)`：更新 `iso` → 重算受影响的最多 8 个相邻 cube；边界格点坐标合法（clamp 到 \[0,X/Y/Z\]，对应 `_points` 边界角点）
- `RefreshAllMeshes()`：全量重建所有 cube（config 切换时调用）
- cube mesh 坐标：`localToWorld.MultiplyPoint(new Vector3(i, j, k))`（格点左下角）

## Config 系统

```
CasePrefabConfig (abstract ScriptableObject)
├── D4FbxCaseConfig   — FBX 来源，EnsureSymmetry() 缓存 D4 归约表；Editor 按 canonical + 变换生成 prefab
└── IosMeshCaseConfig — 256 mesh 直接映射；Editor 逐个加载 cm_{ci}.asset 生成 prefab
```

`McStructure._configs: CasePrefabConfig[]` 支持多套 config 切换（运行时 OnGUI 按钮）。

## 交互点系统

- `PointQuad`：铺在 y=0 平面，点击触发 `CreateCube(x, 1, z)`（从第一层开始）
- `PointCube`：放置在格点，点击法线方向 `CreateCube`；右键 `DestroyCube`
- 两者均通过 `McStructure.OnClicked` 回调驱动

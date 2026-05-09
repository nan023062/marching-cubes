# Structure Architecture

## 定位

MC 3D 建造层（叶子模块）。在 `runtime/marching-cubes` 算法基础上，构建完整的方块建造 MonoBehaviour 层：格点 iso 管理、prefab 实例化/销毁、点击交互、配置切换、地形同步。

## 内部结构

```
Sample/BuildSystem/Structure/
├── McStructure.cs        ← MonoBehaviour 薄壳（类比 MqTerrain）：参数配置 + 委托桥
├── McStructureBuilder.cs ← 纯 C# 格点建造核心：Point[,,] iso 管理 + prefab 实例化
├── BuildState.cs         ← IBuildState 实现：PointCube/PointQuad 管理 + 建造/拆除逻辑
├── CasePrefabConfig.cs   ← 抽象基类（ScriptableObject）：GetPrefab(int)
├── D4FbxCaseConfig.cs    ← 实现：D4 归约 53→255，EnsureSymmetry / GetCanonicalIndex / GetRotation
├── IosMeshCaseConfig.cs  ← 实现：256 槽直接映射
├── PointElement.cs       ← 点击交互基类（MonoBehaviour）
├── PointCube.cs          ← 实心格点交互点（MonoBehaviour，含坐标 x/y/z）
└── PointQuad.cs          ← 地面格点交互点（MonoBehaviour，含坐标 x/z）
```

## 分层架构

```
BuildState（IBuildState）
    ↓ 管理
PointCube[,,] / PointQuad[]
    ↓ 读写
McStructureBuilder（纯 C# 核心）
    ↓ 读写 iso + 调用
McStructure.GetMesh（委托桥）
    ↓ 实例化
CasePrefabConfig.GetPrefab(cubeIndex)
```

## 关键实现

### D4FbxCaseConfig：对称归约

- `EnsureSymmetry()`：遍历 256 种 case，为每个 case 找到 canonical index（D4 等价类代表），存储旋转 `Quaternion` 和是否 flip
- `GetCanonicalIndex(ci)` + `GetRotation(ci)` + `GetFlipped(ci)`：构建 prefab 时取逆变换还原到目标 case

### BuildState：点击处理

```
PointQuad 左键 → CreateCube(x, 1, z)（从地面第 1 层开始）
PointCube 左键 → 法线方向偏移 → CreateCube（在紧邻面外新建）
PointCube 右键 → DestroyCube（销毁该格点）
```

### SyncWithTerrain：地形同步

读取 `MqTerrainBuilder.GetPointHeight()`，更新 PointQuad 悬浮高度，检测 PointCube 与地形冲突（地形高度 > cube 的 y 坐标），销毁冲突的方块。

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
└── PointQuad.cs          ← 平地 cell 交互锚点（MonoBehaviour，含 cell 坐标 cx/cz）
```

## 分层架构

```
BuildState（IBuildState）
    ↓ 管理
PointCube[,,] / PointQuad[length,width]   ← cell 索引直接寻址，平地 cell 才有实例
    ↓ 读写
StructureBuilder（纯 C# 核心）
    ↓ 读写 iso + 调用
Structure.GetMesh（委托桥）
    ↓ 实例化
CasePrefabConfig.GetPrefab(cubeIndex)
```

Prefab 引用归 Structure（MonoBehaviour 持有 `[SerializeField] _pointCubePrefab / _pointQuadPrefab`），BuildState 通过 `_structure.PointCubePrefab/PointQuadPrefab` 读取。BuildingManager 不再持有 prefab 字段，仅做 wiring。

## 关键实现

### D4FbxCaseConfig：对称归约

- `EnsureSymmetry()`：遍历 256 种 case，为每个 case 找到 canonical index（D4 等价类代表），存储旋转 `Quaternion` 和是否 flip
- `GetCanonicalIndex(ci)` + `GetRotation(ci)` + `GetFlipped(ci)`：构建 prefab 时取逆变换还原到目标 case

### BuildState：点击处理

```
PointQuad 左键 → CreateCube(quad.cx, 1, quad.cz)（cube 索引复用 cell 索引，cube 占据 [cx..cx+1, 1..2, cz..cz+1] 体素）
PointCube 左键 → 法线方向偏移 → CreateCube（在紧邻面外新建）
PointCube 右键 → DestroyCube（销毁该格点）
```

### PointQuad：平地 cell 按需生成

PointQuad 不再是「每个内部格点 1 个」的全量阵列，而是 cell 粒度的稀疏锚点：**仅当 cell 的 4 角 high 完全相等时**该 cell 才有 PointQuad（geometry 是 caseIndex==0 的纯平地块）。

- **数据结构**：`GameObject[length, width]`，下标 = (cx, cz) cell 索引；非平地 cell 对应槽位 = null
- **位置**：cell 中心 `(cx + 0.5, baseH, cz + 0.5)`，scale = (1, 0, 1) 贴地
- **判定收口**：调用 `Terrain.IsCellFlat(cx, cz, out int baseH)`（terrain 模块独占领域知识：4 角 high 完全相等）
- **生命周期**：完全由 SyncWithTerrain 驱动；InitBuilding 只分配空数组，不预生成

### SyncWithTerrain：地形同步（含 PointQuad 增删）

每次地形刷绘后由 TerrainState 回调一次。两段处理：

**段一：PointQuad 增删/移位** — 扫所有 cell（cx ∈ [0, RenderWidth), cz ∈ [0, RenderDepth)）：

```
foreach cell (cx, cz):
  flat = terrain.IsCellFlat(cx, cz, out baseH)
  current = _pointQuads[cx, cz]
  if (flat && current == null)        → Instantiate quad at (cx+0.5, baseH+0.5, cz+0.5), 写槽位
  if (!flat && current != null)       → Destroy(current), 槽位置 null
  if (flat && current != null)        → 同步 localPosition.y = baseH + 0.5（高度可能整体抬升/下降）
```

**段二：PointCube 冲突销毁** — 沿用旧逻辑：检查 PointCube[ci, cj, ck] 4 个角点的 terrain 高度，若任一 > cj 则销毁该 cube（地形上来后压住的方块清理）。

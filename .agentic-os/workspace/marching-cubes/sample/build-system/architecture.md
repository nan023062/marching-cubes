# BuildSystem Architecture

## 定位

地形改造 + 3D 建造综合演示（父模块）。是唯一同时使用 MC 和 MQ 双算法的 Sample 案例。通过 BuildingManager 状态机将两套完全独立的交互逻辑（TerrainController / McController）统一管理。

## 子模块清单与关系

```
build-system/
├── terrain/    ← MQ 地形层（TerrainController + TerrainBuilder + TileCaseConfig + Cursor 层次）
└── structure/  ← MC 建造层（McController + StructureBuilder + CasePrefabConfig 系列 + CubeCursor）
```

根目录文件：`BuildController.cs`、`BuildingConst.cs`、`BuildingManager.cs`、`IBuildState.cs`、`BuilderBase.cs`

依赖关系：
- `terrain` → `runtime/marching-squares`（MqTable + Tile 类型）
- `structure` → `runtime/marching-cubes`（CubeTable + MC 核心类型）
- `terrain` ↔ `structure`：无直接代码依赖；协同通过 BuildingManager + 地形同步回调

## 诞生背景

需要验证 MQ 地形改造与 MC 3D 建造能在同一场景中协同工作：地形高度影响建造起始位置，地形改变时建造结构需同步调整。BuildingManager 作为协调者，而非直接在两套系统间建立依赖。

## 涌现性洞察

- **Unit 对齐是核心约束**：TerrainBuilder 以 `1f / Unit` 为格子尺寸，McController 以 `Unit` 为整数格子数，`BuildingConst.Unit` 是唯一真相源，任一子模块擅自修改都会导致坐标系错位
- **地形同步回调**：TerrainController 在地形刷绘完成后调用 `structure.SyncWithTerrain(builder)`，McController 检查坐标冲突并销毁被地形覆盖的方块，这是两套系统唯一的数据交叉点
- **BuildController 输入抽象**：统一的 Update 主循环 + 5 个虚函数（Move/Down/Drag/Up/Click）让子类只关注业务逻辑；Cursor 子类化让 hover 视觉与 Controller 逻辑解耦，未来新增 Controller 只需绑定对应 Cursor 子类

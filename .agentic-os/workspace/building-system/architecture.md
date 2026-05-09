# BuildingSystem Architecture

## 定位

地形改造（MQ）与 3D 建造（MC）的统一建造系统父模块。两个子系统基于各自的 Marching 算法独立运行，通过共同的 unit 尺度和场景坐标系在运行时协同。

## 子模块清单与关系

```
building-system/
├── marching-squares/   MQ 地形改造 — 高度场笔刷雕刻 + splatmap 地形类型混合
└── mc-building/        MC 3D 建造  — 离散格点建造 + prefab case 拼合
```

关系：两子模块**平级，无代码依赖**。均以 `marching-cubes` 模块为底层算法库（Association）。

## 诞生背景

MarchingSquares 地形（MQ）与 McStructure 建造（MC）原各自以独立 Sample 存在，分别解决不同维度的地形问题。建造系统将两者整合为统一的游戏玩法层：玩家先用笔刷改造地形地貌，再在地形上用 MC 建造结构物。

## 涌现性洞察

- **unit 对齐是隐含耦合**：地形格点间距（`MSQTerrain.unit`）必须与建造块边长（`McStructure.unit`）保持一致，否则建造物与地形接缝错位。两份代码独立，但这是跨模块的设计契约。
- **坐标系不共享**：`MSQTerrain` 持有自己的 `localToWorld` 矩阵，`McStructureBuilder` 也有独立矩阵，场景层须负责两者世界原点对齐。
- **无运行时通信**：当前两子模块不感知彼此，协同完全由宿主 MonoBehaviour 在场景层编排。

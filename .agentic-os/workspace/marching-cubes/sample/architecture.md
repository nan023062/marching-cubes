# Sample Architecture

## 定位

`Assets/MarchingCubes/Sample` 下的演示案例集合。每个案例是一个独立可运行的 Unity 场景，演示不同的 MC/MQ 应用场景。

## 子模块清单与关系

```
sample/
├── build-system/      ← 综合建造演示：MQ 地形刷 + MC 3D 建造（唯一双算法案例）
│   ├── terrain/       ← MQ 地形交互层（MqTerrain + MqTerrainBuilder + TerrainState）
│   └── structure/     ← MC 建造交互层（McStructure + McStructureBuilder + BuildState）
├── generate-sphere/   ← CubeMesh 球形演示（平面 mesh）
├── polygon-drawer/    ← 256 case Gizmos 可视化调试
├── real-time-terrain/ ← 2D 无限地形（Perlin noise + CubeMeshSmooth，11×11 chunk 视域）
├── realtime-world/    ← 3D 实时雕刻（球体笔刷 + 动态 chunk，5×5×5 视域）
└── smooth-cube-mesh/  ← CubeMeshSmooth 球形演示（平滑 mesh）
```

各案例对 `runtime` 的依赖：
- `build-system/terrain` → `runtime/marching-squares`
- `build-system/structure` → `runtime/marching-cubes`
- 其余案例 → `runtime/marching-cubes`

案例间无代码依赖，Resources 目录下资产可共享。

## 诞生背景

Sample 目录的主要目的是验证算法正确性（`polygon-drawer`、`generate-sphere`）和演示不同复杂度的应用场景（从简单球体到完整建造系统）。各案例独立设计，便于逐个阅读理解。

## 涌现性洞察

- **build-system 是复杂度顶点**：是唯一同时整合 MC 和 MQ 双算法的案例，也是唯一有完整状态机（IBuildState）的案例，其设计复杂度远超其他独立演示
- **平面 vs 平滑对比**：`generate-sphere` 和 `smooth-cube-mesh` 是同一球形场景用两种构建器的直接对比，是理解 `CubeMesh` vs `CubeMeshSmooth` 差异的最直观入口
- `real-time-terrain`（2D chunk）和 `realtime-world`（3D chunk）展示了同一流式加载模式在 2D/3D 场景下的不同实现

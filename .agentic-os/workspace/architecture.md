# Root Architecture

## 定位

Unity 体素/等值面算法学习仓库，集成 4 种算法实现与可交互 Sample 场景，供作者探索不同等值面提取技术路线的工程实践。

## 子模块清单与关系

| 模块 | 路径 | 算法 | 状态 |
|------|------|------|------|
| marching-cubes | Assets/MarchingCubes/ | 3D Marching Cubes 等值面 | 完整 |
| marching-squares | Assets/MarchingSquares/ | 2.5D Marching Squares 地形 | 活跃开发 |
| mine-oasis | Assets/MineOasis/ | DOTS/ECS 体素建造世界 | 开发中 |
| dual-contouring | Assets/DualContouring/ | Dual Contouring 等值面 | 骨架，未实现 |

四个子模块**彼此独立，无运行时依赖**。各自使用独立 C# 命名空间（MarchingCubes / MarchingSquares / MineOasis / DualContouring）。

## 诞生背景

算法学习与原型验证项目。每个子模块对应一种体素/等值面算法的完整实现周期：从查表算法（MarchingCubes）到地形系统（MarchingSquares），再到 ECS 高性能版本（MineOasis），最终探索 Dual Contouring 作为下一代方案。

## 涌现性洞察

- **共同技术约束**：所有模块当前处于 Unity 版本迁移中，C# 语法需兼容旧版编译器
- **算法演进轴**：MarchingCubes（顶点 iso 场 + 256 查表）→ DualContouring（边交叉点 + QEF 最小化），代表同一体素问题的两种解法路线，精度 vs 拓扑质量的权衡
- **工程演进轴**：MarchingCubes（纯 C# Mono）→ MineOasis（DOTS/Burst），代表同一体素渲染问题在 Unity 生态中从传统到 ECS 的技术迁移路径

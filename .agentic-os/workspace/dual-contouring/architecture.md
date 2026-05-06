# DualContouring Architecture

## 定位

Dual Contouring 等值面提取算法占位模块。当前仅有骨架类，实现为空，作为未来算法实现的预留位置。

## 内部结构

```
DualContouring/
└── DualContouring.cs    ← 骨架类（namespace DualContouring, class DualContouring，方法体为空）
```

## 诞生背景

Dual Contouring 是 Marching Cubes 的改进方案：在边交叉点处用 QEF（二次误差函数）最小化求解最优顶点位置，可保留锐利边缘特征（sharp features），适合建筑/人工结构体素化。本模块预留为后续实现该算法的位置。

## Facts

- 文件创建日期：2023-09-03
- 命名空间：`DualContouring`（独立，不依赖 `MarchingCubes`）
- 当前状态：空实现，无任何公开 API

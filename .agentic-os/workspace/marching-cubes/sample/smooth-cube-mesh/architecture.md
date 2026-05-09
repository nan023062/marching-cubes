# SmoothCubeMesh Architecture

## 定位

MC 平滑 mesh 球形演示（叶子模块）。与 `generate-sphere` 完全对称，唯一区别是使用 `CubeMeshSmooth` 代替 `CubeMesh`，用于直观对比两种构建器的视觉差异。

## 内部结构

```
Sample/SmoothCubeMesh/
└── SmoothSphereSample.cs  ← MonoBehaviour + IMarchingCubeReceiver（CubeMeshSmooth）
```

## 与 generate-sphere 的对比

| 特性 | generate-sphere | smooth-cube-mesh |
|------|----------------|-----------------|
| 构建器 | CubeMesh | CubeMeshSmooth |
| 顶点共享 | 无 | Edge long 哈希共享 |
| 法线 | 硬边 | 平滑 |
| Iso 场 | 相同 | 相同 |

# PolygonDrawer Architecture

## 定位

MC 256 case 可视化调试工具（叶子模块）。在 Editor Gizmos 中渲染等值面，无运行时场景依赖，纯调试用途。

## 内部结构

```
Sample/PolygonDrawer/
└── PolygonTableDrawer.cs  ← MonoBehaviour（OnDrawGizmos）+ 内部类 OneCube
```

## OneCube 内部类

每个 `OneCube` 创建一个 `CubeMesh(1,1,1)` + 实现 `IMarchingCubeReceiver`，根据 `cubeIndex` 的 bit 设置对应顶点 iso=1，调用 `Rebuild()` 生成等值面。在 `Draw()` 中通过 `Gizmos.matrix` 平移到对应位置绘制。

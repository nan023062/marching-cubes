# GenerateSphere Architecture

## 定位

MC 球形演示（叶子模块）。最简单的 IMarchingCubeReceiver 实现示例：将球体 iso 场写入 CubeMesh，OnValidate 实现编辑器内实时预览。

## 内部结构

```
Sample/GenerateSphere/
└── GenerationSphereSample.cs  ← MonoBehaviour + IMarchingCubeReceiver
```

## Iso 场定义

```
center = (x/2, y/2, z/2)（格子空间中点）
maxDis = distance(center, origin)
iso(i,j,k) = maxDis - distance(center, (i,j,k))
isoLevel  = maxDis - radius
IsoPass   = iso > isoLevel  →  等价于 distance < radius（球体内部）
```

## 与 smooth-cube-mesh 的区别

使用 `CubeMesh`（硬边平面 mesh），smooth-cube-mesh 使用 `CubeMeshSmooth`（平滑 mesh）。两者 iso 场定义完全相同，唯一区别是构建器类型，形成直接对比。

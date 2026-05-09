# Runtime/MarchingCubes Architecture

## 定位

3D Marching Cubes 等值面提取核心算法库（叶子模块）。纯 C# 实现，无 Unity 场景依赖，可独立测试。

## 内部结构

```
Runtime/MarchingCubes/
├── CubeTable.cs             ← 256-case 查表 + 顶点/边定义 + 工具函数（纯静态）
├── Cube.cs                  ← 核心数据结构：Point / Edge / Vertex / Triangle / IMarchingCubeReceiver / Coord / Axis / CubeVertexMask
├── CubeMesh.cs              ← 平面 mesh 构建器（独立三角面，不共顶点）
├── CubeMeshSmooth.cs        ← 平滑 mesh 构建器（Edge long 哈希共享顶点）
├── CubedMeshPrefab.cs       ← Prefab 根组件（MonoBehaviour，持有 CubeVertexMask，Editor Gizmos）
├── MeshUtility.cs           ← 编辑器菜单工具：批量生成 256 种 case mesh asset
├── CaseMeshBuilderAsset.cs  ← 程序化 case mesh 算法插槽（ScriptableObject 抽象基类）
├── ProceduralCaseMesh.cs    ← 静态算法实现：连通分量 BFS + arc bevel 圆角 mesh
└── RoundedOctantMeshBuilder.cs ← 默认 CaseMeshBuilderAsset 实现（CreateAssetMenu）
```

## 两种 mesh 构建器对比

| 特性 | CubeMesh | CubeMeshSmooth |
|------|---------|---------------|
| 顶点共享 | 不共享 | Edge long 哈希共享 |
| 法线 | 不连续（硬边） | 连续（平滑） |
| 适用场景 | 块状风格 | 有机/地形风格 |
| 额外接口 | - | `GetPointISO()` |

## 程序化 case mesh 管线

```
CaseMeshBuilderAsset.Build(caseIndex)
    → ProceduralCaseMesh.Build(cubeIndex, sideRadius, topRadius, segments)
        → GetActiveOctants → ConnectedComponents → BuildComponent
            → 面收集 → (无圆角) 直接输出 | (有圆角) FindArcEdges → AddArcStrip + ClipPolygon
```

## 关键设计

- **Edge long 哈希**：`StructLayout(Explicit)` 将 `(sbyte x, sbyte y, sbyte z, Axis axis)` 映射为 `long`，O(1) 查找已有顶点，保证接缝处顶点唯一
- **IMarchingCubeReceiver**：解耦 iso 阈值来源与重建回调，`GetIsoLevel()` + `IsoPass()` 允许不同场景自定义 iso 语义
- **算法插槽**：`CaseMeshBuilderAsset` 允许替换 case mesh 风格而无需修改编辑器工具

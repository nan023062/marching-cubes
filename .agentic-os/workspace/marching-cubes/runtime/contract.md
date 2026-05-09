# Runtime Contract

父模块不直接暴露 API，公开契约由各子模块承载：

- [marching-cubes/contract.md](marching-cubes/contract.md)
- [marching-squares/contract.md](marching-squares/contract.md)
- [thirdc/contract.md](thirdc/contract.md)

## 全局约束

- 所有 Runtime 核心类（`CubeMesh`、`CubeMeshSmooth`、`MqTerrainBuilder` 等）必须为纯 C# 类，无 MonoBehaviour 依赖
- 命名空间：`MarchingCubes`（MC 核心）/ `MarchingSquares`（MQ 核心）/ `MarchingCubes.Sample`（Thirdc，历史原因）

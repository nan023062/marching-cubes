# MarchingCubes Contract

父模块不直接暴露公开 API，公开契约由各子模块 contract.md 承载：

- [editor/contract.md](editor/contract.md)
- [runtime/contract.md](runtime/contract.md)
- [sample/contract.md](sample/contract.md)

## 全局约束

- **C# 兼容性**：禁止 `new()` 泛型约束、`SetLocalPositionAndRotation()`、`HashCode.Combine()`
- **命名空间**：Runtime 核心用 `MarchingCubes` / `MarchingSquares`；Sample 用 `MarchingCubes.Sample`；Editor 用 `MarchingCubes.Editor` / `MarchingSquares.Editor`

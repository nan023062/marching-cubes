# Root Contract

仓库根节点，无对外公开 API。四个子模块各自独立，契约见各子模块 contract.md。

## 工程约定

- **Unity 版本**：见 `ProjectSettings/ProjectVersion.txt`
- **包管理**：`Packages/manifest.json`（Entities 1.x, Burst, Mathematics 等 DOTS 套件）
- **C# 兼容性**：语法需兼容项目当前 Unity 版本对应的 C# 编译器（详见 marching-cubes/contract.md § 兼容约束）
- **命名空间隔离**：各模块命名空间不得交叉引用

## 使用方（子模块）

| 模块 | 命名空间 |
|------|---------|
| marching-cubes | MarchingCubes |
| marching-squares | MarchingSquares |
| mine-oasis | MineOasis |
| dual-contouring | DualContouring |

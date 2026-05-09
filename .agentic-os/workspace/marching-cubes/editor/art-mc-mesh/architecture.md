# ArtMcMesh Architecture

## 定位

MC case prefab 编辑器工具集（叶子模块）。提供三条构建路径：① D4 FBX + 归约生成 255 prefab；② IOS mesh 直接映射 256 prefab；③ 算法插槽程序化生成。同时提供 FBX 导入后处理统一配置。

## 内部结构

```
ArtMcMesh/
├── D4FbxCaseConfigEditor.cs     ← D4FbxCaseConfig 的 Inspector（路径①）
├── IosMeshCaseConfigEditor.cs   ← IosMeshCaseConfig 的 Inspector（路径②）
├── CaseMeshProceduralGenerator.cs ← EditorWindow（路径③）
└── ArtMeshFbxPostprocessor.cs  ← FBX 导入后处理（bakeAxisConversion）
```

## 三条 prefab 构建路径

| 路径 | 输入 | 输出 | 适用场景 |
|------|------|------|---------|
| D4 FBX 归约 | 53 canonical FBX（case_N.fbx）| 255 prefab（p_case_1~254）| 艺术家手工建模，D4 对称复用 |
| IOS 直接映射 | 256 mesh asset（cm_N.asset）| 256 prefab（cm_0~255）| 程序生成 mesh，1:1 映射 |
| 程序化生成 | CaseMeshBuilderAsset 插槽 | 254 prefab + mesh asset | 算法驱动，可切换算法风格 |

## 设计约束

- **D4 变换方向**：`EnsureSymmetry()` 存储 `ci→canonical` 方向的旋转，构建 prefab 时需反向：non-flip 取 `Inverse(d4)`，flip 直接用 `d4`（变换自逆）
- **FBX 后处理统一**：`ArtMeshFbxPostprocessor` 覆盖整个 `Sample/Resources` 目录，MC 和 MQ 资产共用，Blender 导出轴向（Y-forward, Z-up）自动烘进顶点，Unity 侧无旋转

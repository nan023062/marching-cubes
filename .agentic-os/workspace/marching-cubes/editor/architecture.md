# Editor Architecture

## 定位

`Assets/MarchingCubes/Editor` 下的编辑器扩展工具集。为运行时模块提供资产构建能力，包括 Unity Editor Inspector/Window 扩展和 Blender DCC Add-on。

## 子模块清单与关系

```
editor/
├── art-mc-mesh/    ← MC case prefab 构建工具（D4 FBX 归约 + IOS 直映射 + 程序化生成）
├── art-mq-mesh/    ← MQ case prefab 构建工具（16 FBX → 16 prefab）
├── art-mq-texture/ ← MQ 地形贴图生成器（Texture2DArray + 材质）
└── blender/        ← Blender DCC Add-on（参考场景 + FBX 批量导出）
```

依赖方向：
- `art-mc-mesh` → `runtime/marching-cubes`（依赖 CubeTable、D4FbxCaseConfig、CubedMeshPrefab）
- `art-mq-mesh` → `runtime/marching-squares`（依赖 MqMeshConfig）
- `art-mq-texture`：无运行时依赖（纯程序化生成）
- `blender`：独立工具链（Blender Python，产出 FBX 供其他子模块消费）

## 诞生背景

MC/MQ 算法需要大量 prefab 资产（最多 255/256 个 case mesh），手动管理不可行。Editor 子模块将资产构建流程程序化，同时 Blender Add-on 提供 DCC 侧建模参考，保证艺术网格与算法查表严格对齐。

## 涌现性洞察

- **统一 FBX 导入策略**：`art-mc-mesh` 中的 `ArtMeshFbxPostprocessor` 统一管理 `Sample/Resources` 下所有 FBX 的 Bake Axis Conversion，与 Blender Add-on 的导出轴向约定配套，消除逐文件手动配置的遗漏风险

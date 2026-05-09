# Blender Architecture

## 定位

Blender Add-on（叶子模块），独立工具链。提供 DCC 侧建模工作流：搭建参考场景、程序化生成测试 mesh 对比、覆盖率检查、批量 FBX 导出，产物供 Unity Editor 工具（art-mc-mesh）消费。

## 内部结构

```
Editor/Blender/
├── build_zip.py                    ← 打包脚本，生成 mc_building_artmesh.zip
├── mc_building_artmesh.zip         ← 打包产物（安装到 Blender 的 Add-on）
└── mc_building_artmesh/
    ├── __init__.py                 ← 插件主体（Operator + Panel 注册）
    ├── cube_table.py               ← CubeTable.cs Python 移植
    ├── mc_mesh.py                  ← MC case mesh 程序化生成算法
    └── mq_mesh.py                  ← MQ tile mesh 程序化生成算法
```

## 核心工作流

1. **Setup Reference Scene**：在 Blender 场景中布置 53 canonical case 的参考几何和程序化测试 mesh，供艺术家对照建模
2. **Check Coverage**：扫描场景中已建模的 case，报告覆盖率（已完成/缺失）
3. **Extract & Export FBX**：从场景提取各 case 的网格，按 `case_N.fbx` 命名批量导出

## 与 Unity 侧对接

- 导出轴向：`axis_forward='Y', axis_up='Z'`（Blender 原生）
- Unity 侧：`ArtMeshFbxPostprocessor.bakeAxisConversion=true` 将 Z-up 烘进顶点，导入后 GameObject transform 为 identity
- 顶点约定：与 `CubeTable.Vertices` 完全一致，`cube_table.py` 是 Python 版本的权威实现

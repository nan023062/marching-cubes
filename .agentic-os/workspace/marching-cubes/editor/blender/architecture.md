# Blender Architecture

## 定位

Blender Add-on（叶子模块），独立工具链。提供 DCC 侧建模工作流：搭建参考场景、程序化生成测试 mesh 对比、覆盖率检查、批量 FBX 导出。产物供 Unity Editor 工具（art-mc-mesh / art-mq-mesh）消费。

## 内部结构

```
Editor/Blender/
├── build_zip.py                    ← 打包脚本，生成 mc_building_artmesh.zip
├── mc_building_artmesh.zip         ← 打包产物（安装到 Blender 的 Add-on）
└── mc_building_artmesh/
    ├── __init__.py                 ← 插件主体（Operator + Panel + MQProperties）
    ├── cube_table.py               ← CubeTable.cs Python 移植
    ├── mc_mesh.py                  ← MC case mesh 程序化生成算法
    └── mq_mesh.py                  ← MQ tile mesh 程序化生成算法（base-3 编码 65 case）
```

## 核心工作流

1. **Setup Reference Scene**：在 Blender 场景中布置 53 canonical case 的参考几何和程序化测试 mesh，供艺术家对照建模
2. **Check Coverage**：扫描场景中已建模的 case，报告覆盖率（已完成/缺失）
3. **Extract & Export FBX**：从场景提取各 case 的网格，按 `mq_case_N.fbx` / `case_N.fbx` 命名批量导出

## 与 Unity 侧对接

- **导出轴向**：`axis_forward='Y', axis_up='Z'`（Blender 原生）
- **Unity 侧轴向匹配**：`ArtMeshFbxPostprocessor.bakeAxisConversion=true` 将 Z-up 烘进顶点，导入后 GameObject transform 为 identity
- **顶点约定**：与 `CubeTable.Vertices` 完全一致，`cube_table.py` 是 Python 版本的权威实现

## MQ 65 case base-3 编码

### 编码规则

```python
case_idx = r0 + r1*3 + r2*9 + r3*27
# r_i = h_i - min(h0, h1, h2, h3) ∈ {0, 1, 2}
# case_idx ∈ [0, 80]
```

### ALL_CASES 遍历策略

`mq_mesh.py` 的 `ALL_CASES` 不是 `range(81)` —— 死槽（`min(r) > 0` 的 16 个组合）跳过：

```python
def is_valid_case(ci):
    r0 = ci % 3
    r1 = (ci // 3) % 3
    r2 = (ci // 9) % 3
    r3 = (ci // 27) % 3
    return min(r0, r1, r2, r3) == 0

ALL_CASES = [ci for ci in range(81) if is_valid_case(ci)]
# len(ALL_CASES) == 65
```

### Mesh 几何生成

复用现有 `bilinear_arc(u, v, h, arc_s, flat)` 函数，无须重写：

- 每个 case 解出 `h = [r0, r1, r2, r3]`（浮点高度数组）
- 调 `bilinear_arc` 生成网格：先对 UV 做 arc smooth-step，再做双线性插值，每个高角点产生 `flat × flat` 平台
- 与原 19 case 几何风格完全一致（同函数同参数 `arc_s=0.8, flat=0.25, sub=8`）

### 与 Unity 端的命名约定

```
mq_case_<case_idx>.fbx   # case_idx ∈ [0,80] 中的 65 个有效值
                         # 死槽 case_idx 不导出文件
```

Unity 端 `art-mq-mesh.MQMeshConfigEditor` 按 `mq_case_(\d+).fbx` 正则扫描，按 N 索引写入 `cfg.SetPrefab(N, prefab)`。

## 设计约束

- **base-3 编码统一收口**：C# `TileTable.GetMeshCase` 与 Python `mq_mesh.case_idx` 必须用同一公式（`r0 + r1*3 + r2*9 + r3*27`），否则 Unity 索引到的 prefab 与 Blender 导出的 mesh 不对应
- **mesh 几何风格不变**：`bilinear_arc` 是 19 case 与 65 case 共用的几何生成器，参数 `arc_s` / `flat` / `sub` 不动
- **悬崖系统已下线**：原 5 个 mq_cliff_*.fbx + Setup/Export Cliff Cases operator 已删除（高差 ≤ 2 全部由 65 坡面 case 表达）
- **法线贴图烘焙已下线**：noise.py / bake_normal_map / MQ 19 case 法线贴图相关代码全部删除（demo 不需要法线贴图）

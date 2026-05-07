# ArtMeshBlender Architecture

## 定位

marching-cubes 模块下的 Blender Python 工具子模块（v1.5），负责 ArtMesh 工作流的 DCC 侧：
在 Blender 内生成 D4 canonical case 参考场景，供美术在每个格子内建模，完成后批量导出 FBX 给 Unity 侧消费。

Unity 侧（CubeArtMeshConfig / CubeArtMeshWindow）通过 `case_{n}.fbx` 命名约定接收导出结果。

## 文件结构

```
Blender/
├── mc_artmesh/              ← Blender Add-on package（正式发布版）
│   ├── __init__.py          ← 插件主体：bl_info + 全部 Operator/Panel + 场景逻辑
│   └── cube_table.py        ← Unity CubeTable.cs 的 Python 移植（顶点/棱/三角表）
├── build_zip.py             ← 打包脚本：python build_zip.py → mc_artmesh.zip
└── mc_artmesh.zip           ← 打包产物（直接安装到 Blender）
```

**安装**：`python build_zip.py` 生成 `mc_artmesh.zip`，拖入 Blender 或通过 Preferences > Add-ons > Install 安装。

## 对称群与 Canonical Case

采用 **D4 对称群**（与 Unity `CubeSymmetry.cs` 完全一致）：
- 4 个 Y 轴旋转（0°/90°/180°/270°）× LR 翻转（flip 先于 rotation）= **8 种变换**
- 256 个 cubeIndex 在 D4 下归并为 **55 个 canonical case**（含 ci=0 全空、ci=255 全满）
- 排除 0 和 255 后共 **53 个需建模的 canonical case**

## 场景根节点结构（平级多根）

Setup Reference Scene 生成两个平级根节点，未来可扩展更多：

```
MC_Refs/            ← 根1：参考几何（按眼睛隐藏 = 隐藏全部控制点/线框/标签）
    case_1/         ← 53 个 case 子 Collection
        _lbl0       ← 3D 文字标签（n=0  ci=1）
        _w0         ← 绿色线框（display_type='WIRE'）
        _pt0_0~7    ← 8 个控制点 icosphere（鲜红=激活, 灰=非激活）
        _s0_0~k     ← 黄色接缝锚点 icosphere（crossing edge 中点）
    case_3/
    ...

MC_IsoMesh/         ← 根2：等值面 mesh（按眼睛隐藏 = 只看测试 mesh）
    case_1/
        _iso_out0   ← 等值面外层（深蓝, backface_culling=True）
        _iso_in0    ← 等值面内层（黑, 法线翻转, backface_culling=True）
    case_3/
    ...

ArtMesh_Terrain     ← 建模对象（直接挂在 Scene 根，美术在此统一建模）
```

代码常量：`REFS_COL_NAME = "MC_Refs"` / `ISO_COL_NAME = "MC_IsoMesh"`

## 网格布局

- `GRID_COLS = 9`，步进 2（Unity 坐标）：case n 的 cube 位置为 `(col*2, 0, row*2)`
- Blender 原点：`u2b(col*2, 0, row*2) = (col*2, row*2, 0)`（Blender X/Y 方向平铺，Z 为高度）
- 步进 2 保证相邻 canonical case cube 不重叠，但 gap cube 会继承两侧顶点形成混合 ci
- **渲染循环不扫描整个 grid**，而是直接 `enumerate(get_d4_canonicals())` 还原 53 个放置坐标，避免 gap cube 被误判为 canonical case

## 等值面双面可视化（MC_IsoMesh）

等值面采用**两个独立 mesh** 实现正反颜色区分，在 Solid 和 Material Preview 模式均有效：

| Mesh | 绕序 | 材质 | 含义 |
|------|------|------|------|
| `_iso_out{n}` | TRI_TABLE 原始绕序 | 深蓝 Emission + backface_culling=True | 外侧（空气侧） |
| `_iso_in{n}` | 三角绕序翻转 | 黑色 Emission + backface_culling=True | 内侧（实心侧） |

## 坐标系约定

```
Unity: Y-up (x, y, z)
Blender: Z-up (x, y, z)

u2b(ux, uy, uz) = (ux, uz, uy)   # Unity → Blender（交换 Y/Z）
b2u(bx, by, bz) = (bx, bz, by)   # Blender → Unity（互为逆变换）
```

顶点约定与 `CubeTable.Vertices` 完全一致：

```
V0:(0,0,1)  V1:(1,0,1)  V2:(1,0,0)  V3:(0,0,0)
V4:(0,1,1)  V5:(1,1,1)  V6:(1,1,0)  V7:(0,1,0)
```

Blender 空间顶点位置（`u2b` 后）：
```
底面(低Z): V0(0,1,0) V1(1,1,0) V2(1,0,0) V3(0,0,0)
顶面(高Z): V4(0,1,1) V5(1,1,1) V6(1,0,1) V7(0,0,1)
```

## Case Mesh 生成算法（v1.5 顶点八分体填充法）

替代旧方案（bisect 等值面平面切割 + bevel 圆角），新方案规则：

1. 8 个顶点各对应一个 0.5³ 八分体格 `(gx, gy, gz) ∈ {0,1}³`（BL_VERTS 映射）
2. 对每个 active 顶点的八分体，逐检 6 个方向：若邻格也是 active → 跳过（内部面）；否则 → 生成外向面
3. `remove_doubles` 合并共享顶点 → 相邻 active 顶点自然合并为更大的平面块
4. `triangulate` 输出三角面 mesh

**面分类（两种材质）：**

| 面类型 | 判断条件 | 材质颜色 | 语义 |
|--------|---------|---------|------|
| 封闭面 | 所有顶点在某轴上坐标 = 0.5（中平面） | 深蓝 `mc_cube_closed` | 实体截面，永久封闭 |
| 开放面 | 其余（顶点在 0 或 1，cube 边界） | 浅灰 `mc_cube_open` | 拼接口，组合后自然封闭 |

面分类通过 `poly.material_index` 逐面赋值（`from_pydata` 后、`update()` 前）。

## 四步工作流

```
1. Setup Reference Scene（N-Panel 按钮）
   → 生成 MC_ArtMesh_Ref（53 个参考几何格子：线框 + 控制点球 + 接缝锚点 + 等值面）

2. Generate Case Meshes（N-Panel 按钮）
   → 生成 MC_ArtMesh_Cubes（53 个八分体填充 case mesh）
   → 深蓝 = 封闭面，浅灰 = 开放面（供美术建模参考）

3. 美术建模
   → 在参考 case mesh 基础上雕刻/建模
   → MC_ArtMesh_Ref 提供控制点/接缝/线框视觉引导
   → Check Coverage 随时检查覆盖率

4. Extract & Export FBX
   → 按三角面重心归属裁切到各 cube 格子
   → 边界顶点自动吸附到接缝锚点（snap_dist 容差）
   → 输出 case_{n}.fbx，每个 canonical case 一个文件
```

## cube_table.py

Unity `CubeTable.cs` 的完整 Python 移植，供 `__init__.py` 相对导入：

- `VERTICES` — 8 个顶点坐标（Unity 空间）
- `EDGES` — 12 条棱的顶点对
- `EDGE_MIDPOINTS` — 12 条棱的中点（iso level=0.5）
- `EDGE_TABLE` — 256 个边掩码
- `TRI_TABLE` — 256 个三角面索引列表（trailing -1 已剥离）
- `get_iso_triangles(ci)` — 返回等值面顶点列表

## 诞生背景

原方案（CubeArtMeshSplitter atlas 切分）要求美术在 DCC 工具外作业，约束隐性且只能 Unity 导入后才能发现错误。切换到 Blender Add-on 工作流后，建模、接缝可视化、覆盖检查、导出全在 Blender 内完成，Unity 侧仅保留轻量的 Import by Name 逻辑。

# ArtMeshBlender Changelog

## [2026-05-06 立项]

- type: decision
- 新建子模块，从 Unity-side atlas 切分方案切换至 Blender-native 工作流
- 触发原因：atlas 方案约束隐性（坐标精确落点、三角面不跨格），只能 Unity 导入后才能发现错误；Blender 内建模+接缝可视化+批量导出方案心智负担更低

## [2026-05-06 重构为 Add-on package]

- type: decision
- 将单文件 `mc_artmesh_plugin.py` 重构为 package：`mc_artmesh/__init__.py` + `mc_artmesh/cube_table.py`
- 触发原因：Blender 安装单文件 Add-on 时只复制本体，`cube_table.py` 无法随之安装；package（zip）方式整个目录一起复制，内部相对导入 `from .cube_table import TRI_TABLE` 正常工作
- 新增 `build_zip.py` 打包脚本

## [2026-05-06 移植 Unity CubeTable + 等值面参考 Mesh]

- type: decision
- 新增 `cube_table.py`：完整移植 Unity `CubeTable.cs`（VERTICES / EDGES / EDGE_TABLE / TRI_TABLE）
- Add-on 在每个 canonical case 格子内生成等值面参考 Mesh（蓝/黑双面），供美术直观了解目标形状
- 等值面使用两个独立 Mesh 实现正反双色（外层 backface_culling=True + 内层法线翻转），避免使用 Geometry Backfacing 节点（该节点在 Solid 视图模式下无效）
- Setup 时同步开启视口 `overlay.show_backface_culling = True`

## [2026-05-06 修复 gap cube 误判 bug]

- type: incident
- 现象：生成的参考格子数量远大于 53 个
- 根因：渲染循环扫描整个 grid（nx×ny×nz），gap cube 的顶点由两侧 canonical case 各出一半，混合 cubeIndex 能映射到 canonical case，导致大量 gap cube 被误渲染
- 修复：渲染循环改为直接 `enumerate(get_d4_canonicals())` 还原 53 个已知放置坐标，不再扫描整个 grid

## [2026-05-06 per-case 子 Collection 层级]

- type: decision
- 场景 Outliner 从平铺改为层级：每个 canonical case 建一个子 Collection（`case_{ci}`），8 个控制点球、线框、接缝锚点球、等值面 Mesh 全部挂在各自子节点下
- 同步移除全局 grid vertex 循环，改为 per-case 直接计算各顶点坐标

## [2026-05-06 控制点球形 + 配色调整]

- type: decision
- 控制点从八面体（`_make_octahedron`）改为 icosphere（`bmesh.ops.create_icosphere`，subdivisions=2），视觉更圆润
- 颜色方案：激活顶点=鲜红(1,0,0)，等值面外侧=深蓝(0.02,0.18,0.55)，等值面内侧=黑(0.02,0.02,0.02)

## [2026-05-07 场景结构重构为平级多根节点]

- type: decision
- 从单根 `MC_ArtMesh_Ref`（内部嵌套）改为平级多根：`MC_Refs` / `MC_IsoMesh` / `MC_TestMesh`
- 触发原因：美术需要独立切换参考几何、等值面 mesh、测试 mesh 的可见性；平级根节点通过 Outliner 眼睛即可分层隐显，未来可继续扩展新根节点（如 MC_ArtMesh 手工建模结果）
- 新增常量：`REFS_COL_NAME`, `ISO_COL_NAME`, `TEST_COL_NAME`
- `ArtMesh_Terrain` 建模对象从 collection 内移至 Scene 根，避免嵌套干扰

## [2026-05-07 新增 3D 文字标签]

- type: decision
- 每个 case 在 `MC_Refs` 子 collection 内生成一个 Font 对象（`_lbl{n}`），显示 `n=X  ci=Y`
- 位置在 cube 正上方（b_oz + 1.15），锁定、不可选

## [2026-05-07 新增程序化测试 Mesh（MC_TestMesh）]

- type: decision
- 新增 `MC_OT_TestGen` Operator（N-Panel 按钮 "Generate Test Meshes"）
- 生成 `MC_TestMesh` 根节点，包含 53 个 `case_{ci}` 子 collection，每个含外层（深蓝）+ 内层（黑）mesh
- 用途：与 `MC_IsoMesh` 并存对比，验证程序化 mesh 形状是否覆盖等值面

## [2026-05-07 修复程序化 mesh 切割逻辑（bisect → 密度场过滤）]

- type: incident
- 现象：大量 case 的程序化 mesh 为空或形状错误（如 ci=5, ci=7, ci=85 等）
- 根因：旧的 `bisect_plane` 方案对非凸激活区域产生矛盾约束——对角激活（如 ci=5）同时要求 x<0.5 和 x>0.5，所有几何体被切空
- 修复：改用**密度场过滤法**——对每个面质心做三线性插值密度计算，删除密度<0.5的面；同时用 `dot(face_normal, face_center - cube_center)` 确保法线朝外
- 此方案对全部 53 个 case 均正确，含对角/非凸情形

## [2026-05-08 14:30:00]

- type: incident
- 现象：Quick Export（MC_OT_ExportGeneratedMeshes）导出的 FBX 在 Unity 中 Z 轴镜像
- 根因：Python 层做了 b2u 顶点预变换（verts_unity / faces_unity），FBX exporter 把轴向矫正写进 FBX 根节点 transform（非顶点），Unity 导入时双重应用 → Z 镜像
- 修复：删除 Python 预变换，顶点保持 Blender 原生坐标（Z-up）

## [2026-05-08 14:30:00]

- type: decision
- FBX 导出轴向从 `axis_forward='-Z', axis_up='Y'`（Unity preset）改为 `axis_forward='-Y', axis_up='Z'`（Blender 原生轴）
- 原因：Unity preset 把轴矫正编码为 FBX 根节点旋转（X=-90），Unity 的 `bakeAxisConversion` 只处理 FBX 坐标系声明差异，FBX 声明 Y-up 时认为无需转换而跳过，X=-90 无法被 bake；改用原生轴后 FBX 声明 Z-up，bakeAxisConversion 才真正生效
- 影响：两个 Operator（MC_OT_ExportGeneratedMeshes + MC_OT_ExtractCases）均需同步修改
- 勘误：`axis_forward='-Y'` 非 identity——Blender FBX exporter 内部 from_forward='Y'（正Y），`-Y` 会产生绕Z的180°旋转（在Unity呈现为Y=180）；正确 identity 为 `axis_forward='Y', axis_up='Z'`

## [2026-05-08 15:00:00]

- type: incident
- 现象：DoBuild 生成的 201 个非 canonical prefab 中，156 个位置或朝向错误（通过"建一个点观察8邻格"发现）
- 根因一（96 case）：non-flip 旋转方向反了。EnsureSymmetry 存的是「ci→canonical」方向的旋转，DoBuild 误当「canonical→ci」直接使用，导致 90°↔270° 对调。
- 根因二（60 case）：flip case 的 localPosition pivot 错误。当前用 S_CENTER=(0.5,0.5,0.5) 作 pivot，但 flip（X 镜像）应以 (-0.5,0.5,0.5) 为 pivot（X 轴镜像中心在原点坐标系下的表示），导致 mesh 偏移出 cube 外。
- 修复（ArtMeshCaseConfigEditor.cs DoBuild）：
  ```
  var d4apply = isFlipped ? d4 : Quaternion.Inverse(d4);
  var pivot   = isFlipped ? new Vector3(-0.5f, 0.5f, 0.5f) : S_CENTER;
  child.transform.localPosition = S_CENTER - d4apply * pivot;
  child.transform.localRotation  = d4apply;
  ```
- 数学依据：flip 变换 T=R∘M 满足自逆性（T=T⁻¹），故 flip 旋转不需取逆；pivot 推导：unity transform 等式 `pos + R*(−vx,vy,vz) = R*(1−vx,vy,vz)` 解出 `pos = S_CENTER − R*(−0.5,0.5,0.5)`

## [2026-05-08 14:30:00]

- type: decision
- 新增 Unity Editor 脚本 `ArtMeshFbxPostprocessor.cs`（路径：`Assets/MarchingCubes/Editor/ArtMesh/`）
- 对 `fbx_case/` 下所有 FBX 自动设置 `bakeAxisConversion = true`，把 Z-up→Y-up 烧进顶点，导入 transform 还原为 identity
- 必要条件：FBX 须以 `axis_forward='-Y', axis_up='Z'` 导出（声明 Z-up）才能触发 bake

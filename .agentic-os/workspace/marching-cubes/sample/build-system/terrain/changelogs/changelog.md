# Terrain Changelog

## [2026-05-10 16:00:00]
type: decision
title: 渲染管线重设计：WC3 风格 per-tile MPB uniform 5 layer atlas idx，废弃 per-vertex pointTex + shader 端 mask 解码
editor: architect；过渡产物（owner 治理前代写 append-only）
supersedes: [2026-05-10 14:30:00] + [2026-05-10 15:30:00]（两条 bitmask 解码方案）

**触发**：老板自验证发现旧方案视觉异常（"刷泥出绿"），定位根因后重写渲染管线（commit `b408899` / `683671c` / `394f31b`）。

**根因**（旧方案为何废弃）：
- 旧 [14:30] 方案：pointTex per-vertex 编码 mask byte → shader 4 角采样 → DecodeCorner 加权混合 weight=i+1
- 即便 `FilterMode.Point` + `Color32` 整数往返，渲染管线在 sampler 边界仍可能引入 ±1 byte 偏差
- byte 偏差按 bit 解码后，**非目标 bit 被误置为 1** → 其他 layer 错误显示（症状：刷 type 0 泥，相邻像素出 type 1 草）
- 这是 sampler-decode 范式的根本缺陷，不是参数 tuning 能解决的

**新方案**（WC3 风格）：
- 数据结构：`Point.terrainMask: byte` **保留**（mask 仍是低 5 bit 的 bitmask 编码）
- API：`PaintTerrainType` Add 语义 / `EraseTerrainType` / `ClearTerrainMask` / `GetTerrainMask` **全部保留**
- **删除**：`pointTex` / `_pixelBuffer` / `SetPixels32` / `FlushPointTex` / `SetPointTexPixel` / `RefreshPointTexAll`
- **新增**：`RefreshAffectedTilesMPB(HashSet<(int,int)>)` — mask 变化 → dirty 格点扩散到 4 邻 cell → 重推 MPB
- `ApplyTileMPB`：读 4 角 mask → 调 `TileTable.GetAtlasCase(mBL,mBR,mTR,mTL,bit)` 算每 layer 的 atlas case_idx → 推 `_TileMsIdx (Vector4 layer 0-3)` + `_TileMsIdx4 (Float layer 4)` 到 MPB
- shader frag 直接读 uniform 拿 5 个 idx，**0 采样 0 解码**；按 type 0~4 高编号覆盖低编号 lerp by alpha

**关键架构收益**：
- **0 sampler 误差**：uniform 直读不走 bilinear，根除"刷泥出绿"类型的视觉串扰
- **atlas 编码统一收口**：所有「4 角 → atlas idx」走 `TileTable.GetAtlasCase`，C# / Python / shader 三端通用
- **TerrainTypeCount 回到 5**：与 mc 美术约定对齐（之前为支持 8 layer 临时扩到 8）
- **MPB 重推开销可控**：mask 变化频率远低于渲染频率，整体性能优于 sampler 路径

**资产协议**（_OverlayArray）：
- 5 layer 2DArray，每 layer 4×4 atlas，存 16 个 MS case 形状（带 alpha）
- col = ms_idx % 4，row = ms_idx / 4（Unity UV，row=0 在底）
- ms_idx 编码标准化（commit `683671c` / `394f31b`）：从原 BR/TR/TL/BL 逆时针约定改为 V0~V3 标准（与 GetMeshCase 对齐），各层 contrast 拉伸提升清晰度
- 老板已自行重排 atlas 资产 + 提升对比度，新协议**已生效**

**跨模块影响**：
- `runtime/marching-squares` 三件套同步补完：新增 `TileTable.GetAtlasCase` / `GetAtlasCell` / `CliffD4Map` 等 API；同步漂移已久的命名（MqTable→TileTable / MqTilePrefab→TilePrefab / ISquareTerrainReceiver→ITileTerrainReceiver）
- `art-mq-mesh prefab UV` 跨模块契约**仍然有效**（atlas 4×4 子格采样依赖 lUV ∈ [0,1]×[0,1]）

**Bitmask 数据结构遗产保留**：
- `Point.terrainMask: byte` + Add/Erase/Clear API 是 [14:30] 方案的核心遗产，对用户交互体验最有价值（多 type 同点叠加 + 一键清空）
- 仅渲染管线（sampler vs uniform）被替换；数据层和 UX 层完全继承

## [2026-05-10 15:30:00]
type: decision
title: bitmask 协议补完：ClearTerrainMask 一键清空 API + SetPixels32 批量写入路径 + UV 跨模块契约显式化
editor: architect；过渡产物（owner 治理前代写 append-only）
status: ⚠️ SUPERSEDED by [2026-05-10 16:00:00]（渲染管线整体重设计为 WC3 风格 per-tile MPB uniform；ClearTerrainMask API + UV 契约保留有效；SetPixels32 路径随 pointTex 一起删除）

**触发**：reviewer 对抗审查（[2026-05-10 14:30:00] 决策的实装交付）发现 2 个必修点 + 1 个建议，老板拍板全采纳。

**新增 ClearTerrainMask API**：
- 旧 type 0 当 base 用，"刷 type 0 重置"是隐式入口；新协议下 type 0 是 bit 0 overlay 之一，**无任何方式让用户一键清空格点回到 _BaseTex**
- 新增：
  - `TerrainBuilder.ClearTerrainMask(Brush brush): bool` — 笔刷范围内所有格点 `mask = 0`
  - `Terrain.ClearTerrainMask(): bool` 薄壳转发
  - GUI 加「清空」按钮（与 PaintTerrainType / EraseTerrainType 并列）
- 视觉效果：mask=0 的格点在 shader DecodeCorner 内部 totalW=0 → fallback `_BaseTex(baseUV)`，与"未刷过"语义一致

**SetPixels32 批量写入重构**：
- 旧实装：`pointTex.SetPixel(px, pz, new Color32(...))`，走 `Color32 → Color → RGBA32` 浮点中转，当前 byte 0..255 数学守恒但脆弱（format/sRGB 变更会立刻翻车）
- 新实装：TerrainBuilder 维护 `Color32[] _pixelBuffer`（length+1 × width+1），`SetPointTexPixel` 改为更新缓存元素；操作末尾统一 `pointTex.SetPixels32(_pixelBuffer)` + `Apply()`
- 收益：绕开浮点中转 + 批量写入吞吐高于单像素 SetPixel + 未来 format 变更免审

**契约显式化**（reviewer 找出来的架构遗漏，我的责任）：
- contract.md 新增「§ 跨模块隐含契约」段：声明 art-mq-mesh prefab UV 必须 BL=(0,0)/BR=(1,0)/TR=(1,1)/TL=(0,1)
- module.json constraints 同步加这一条
- 之前这是"口口相传"的依赖：Blender 端 mq_mesh.py 的 UV 生成顺序 + SplatmapTerrain 4 角混合数学**两边数据正确但契约不在文档**——reviewer 评语原文："能跑，但下次谁动 UV 谁哭"

## [2026-05-10 14:30:00]
type: decision
title: terrainType 单值 → 8-bit bitmask 编码；多 type 同点叠加 + 线性权重混合；废弃 TileTerrainTest 沙盒
editor: architect；过渡产物（owner 治理前代写 append-only）
status: ⚠️ PARTIALLY SUPERSEDED by [2026-05-10 16:00:00]
  保留：terrainMask byte 数据结构 + PaintTerrainType Add 语义 + EraseTerrainType API + TerrainTypeCount 概念
  废弃：TerrainTypeCount=8（回退到 5）+ pointTex per-vertex 编码 + DecodeCorner 4 角加权混合 + weight=i+1 线性权重 + _OverlayArray 1 type/层资产协议
  TileTerrainTest 沙盒退役 + UV 跨模块契约依然有效

**编码协议变更（核心）**：
- `Point.terrainType: byte`（单值枚举 0~4）→ `Point.terrainMask: byte`（8-bit bitmask，bit i=1 表示 type i 存在）
- `TerrainTypeCount: 5 → 8`（用满一个 byte，未来扩展不需再改协议）
- pointTex R 通道改为 **直接存 mask byte**：C# 端 `Color32((byte)mask, 0, 0, 255)` 整数写入，shader 端 `round(tex.r * 255.0)` 反解；废弃旧 `terrainType / (TerrainTypeCount-1)` 浮点归一化
- **解决 magic number 散落**：原方案 `TerrainTypeCount=5` 与 shader 硬编码 `*4.0 / round(*4.0)` 耦合在两端，改一处必崩另一处；新方案编解码常数为固定 255，shader 与 TerrainTypeCount 完全解耦

**API 语义变更（破坏性）**：
- `PaintTerrainType(brush, type)` 从 Replace 语义（`mask = (byte)type`）改为 **Add 语义**（`mask |= 1<<type`）—— 反复刷绘自然累积叠加
- 新增 `EraseTerrainType(brush, type)`：反向擦除（`mask &= ~(1<<type)`）
- `GetTerrainType(x,z): byte` → `GetTerrainMask(x,z): byte`（语义改名）
- `Terrain.TextureLayer` 注释由 0~4 → 0~7

**Shader 4 角加权混合（SplatmapTerrain.shader）**：
- 4 角各采样 1 次得到 mask byte → 每角按 bitmask 解码出"加权 overlay 颜色 + 总权重"→ `col_c /= totalW_c` 归一化 → 4 角 quad 双线性插值
- 权重函数：**线性 `weight(i) = i + 1`**（bit 位越高权重越大；type 0 权重 1，type 7 权重 8；同点 type 0+type 7 共存时颜色 = (1*c_0 + 8*c_7) / 9）
- mask=0 fallback：当一角的 mask 为空（无任何 type），该角颜色取 `_BaseTex(baseUV)`，参与 4 角插值
- 性能上限：每 fragment 最多 4 角 × 8 type = 32 次 overlay 采样（实际场景大部分像素 mask 稀疏，分支跳过空 bit）；待性能 profile 后决定是否限制 max 4 个最高 bit

**TileTerrainTest 沙盒退役**：
- 沙盒已验证完"连续 mesh + 全局 UV + 双线性权重混合"假设，主路径 SplatmapTerrain 沿用同思路，沙盒不再需要
- 删除：`Sample/BuildSystem/Terrain/TileTerrainTest.cs`(+meta) / `Sample/Resources/Shaders/TileTerrainTest.shader`(+meta) / `Sample/Resources/Shaders/TileTerrainTestOverlay.shader`(+meta) / `Sample/Resources/mq/TileTerrainTest.mat`(+meta)
- 保留：`Runtime/MarchingSquares/TileTerrain.cs` + `ITileTerrainReceiver`（runtime 模块脚手架，留作未来 prototype 复用，非 terrain 模块产物）
- 场景 mc_bulding.unity 已确认无 TileTerrainTest GameObject 引用

**动机回顾**：
- 用户场景：多层纹理叠加（同一格点既有岩石又有青苔），单 type 编码无法表达
- 老板提出方向：编码进 R 通道，权重靠 bit 位置隐式决定，不单独存 weight
- 选定方案 B（8-bit bitmask × 8 type，约定同时为 1 的 bit ≤ 4），优势：unique type 编码无重复 + 自然加权 + 通道占用最小（剩 GBA 三通道留作未来扩展）

## [2026-05-09 19:00:00]
type: decision
title: 纹理方案改为 per-vertex R 通道 + Shader 4 次采样；刷绘改为 click-only；TerrainMaxHeightDiff 收紧为 1

**纹理方案重构（TerrainBuilder + SplatmapTerrain.shader）**：
- pointTex 从 `length×width` RGBA32（每像素=一个格子，RGBA=四角 terrainType）改为 `(length+1)×(width+1)` RGBA32（每像素=一个格点，R=terrainType/4）
- SetCellTexPixel / UpdateAffectedTileColors 废弃；新增 SetPointTexPixel(px,pz)
- ApplyTileMPB 只传 BL 角点 UV（_TerrainPointTexST.xy），步长由 Shader 用 `_TerrainPointTex_TexelSize.xy` 自取（不再依赖 MPB 的 zw）
- Shader frag 由"一次采样 RGBA 解包四角"改为"分 4 次独立采样各角格点 R 通道"
- 修复 bug：原方案 _TerrainPointTexST.zw 未能可靠传递，导致步长为 (0,0)，只有 BL 角点生效，出现 4 个不连续的草块

**刷绘交互改为 click-only（TerrainState）**：
- 移除 _mouseWasDown / _lastMousePos；改为 _pressTime + _pressButton + ClickMaxDuration=0.3f
- 按下计时，抬起时判断是否为短按（<0.3s）才触发；长按按下和抬起均不触发
- OnExit() 重置 press 状态，避免模式切换后状态残留

**TerrainMaxHeightDiff 收紧为 1（BuildingConst）**：
- 原值 3 会使同一 tile cell 内角点高差超过 2，而 19-case 坡面 tile 系统只正确覆盖高差 ≤ 2 的情形，产生视觉缝隙
- 改为 1：相邻格点高差 ≤ 1，19-case 完整覆盖（cases 0-14 覆盖 ≤1，cases 15-18 覆盖对角差=2）
- 后续 B 方案（悬崖补全高差 > 1 的坡面缺口）待设计

## [2026-05-09 00:00:00]
type: decision
title: 架构重构：从 monolithic MSQTerrain 拆分为三层 + Runtime/mq 算法基础层从无到有

**架构分层（从 MSQTerrain 拆出）**：
- MSQTerrain（原 monolithic）→ MqTerrain（MonoBehaviour 薄壳）+ MqTerrainBuilder（纯C# 核心）
- MqTerrain 职责：Init / BrushMapHigh / PaintTerrainType / SetBrushVisible / MeshFilter+MeshCollider 绑定
- MqTerrainBuilder 职责：Point[,] 数据 + colliderMesh + GameObject[,] tile 生命周期 + BFS 高差约束传播

**Runtime/mq 层新增（类比 Runtime/mc，完全对称）**：
- Tile.cs：TileVertex / TileVertexMask / TileEdge / TilePoint / TileVertex2D / TileTriangle / ISquareTerrainReceiver
- MqTable.cs：GetMeshCase() / GetTextureCase() / GetTerrainLayers()（两类组合映射）
- MqTilePrefab.cs：tile prefab 调试组件，Editor Gizmos 可视化四角高差 + case index
- TileTerrain.cs：程序化连续 mesh 生成器（Rebuild / RebuildHeightOnly），快速原型用

**MqMeshConfig 设计决策**：
- 16槽直接映射，不使用 D4 对称归约
- 原因：Mesh 几何 + 纹理 UV 双重组合要求每个 case 有独立正确的 UV；D4 旋转改变 UV 方向导致纹理映射错误

**纹理方案变化**：
- 废弃 SplatmapTerrain shader + uv0~uv3 + Color32 顶点权重方案
- 改用 MaterialPropertyBlock 注入 _T0~_T3（四角 terrainType），由每个 tile prefab 的 shader 采样

**workspace 路径迁移**：
- Runtime/mq 路径不变
- Sample 层：Sample/McStructure/ → Sample/BuildSystem/Terrain/

## [2026-05-08 00:00:00]
type: decision
title: 逆向建档二次修正 + 迁入 building-system 父模块

漂移修正：
- module.json workspace 路径从 Assets/MarchingSquares 修正为实际路径（Runtime/mq + Sample/MCBuilding/MarchingQuad25Sample.cs）
- Brush 从 struct 修正为 MonoBehaviour，补全 Size 属性和 transform 定位机制
- Point.high 运行时 clamp 范围修正为 -64~64（非 sbyte 的 -128~127）
- uv1~uv3 语义从"推断待确认"更新为已确认的 MS case index + 4×4 tile atlas 映射
- Color32 语义从"地形类型权重"更正为 base+overlay1/2/3 四通道独立编码
- 补全 CliffTemplate 程序化模板系统细节（Perlin 噪声、两级模板、拼接逻辑）
- 删除不存在的文件引用（MSQTexture.cs / MSQMesh.cs / Util.cs / MarchingSquareSplitter.cs）
迁移：从 workspace 根迁入 workspace/building-system/marching-squares/

## [2026-05-06 00:02:00]
type: decision
title: 新增悬崖壁面独立网格（cliffMesh）

MSQTerrain 新增 `cliffMesh`（Mesh），对地形高度差形成的悬崖生成独立壁面几何。
MarchingQuad25Sample 在 Awake 中动态创建 "CliffWalls" 子 GameObject 挂载此 mesh。

## [2026-05-06 00:01:00]
type: decision
title: 纹理渲染方案从 tile-atlas 迁移到 splatmap

旧方案：ITextureLoader 接口 + MSQTexture ScriptableObject + tile-atlas UV 查表
新方案：SplatmapTerrain shader + 4 UV 通道（uv0~uv3）+ Color32 vertex weights
原因：splatmap 方案支持地形类型平滑混合，视觉效果更好，减少 ScriptableObject 资产依赖。

相关改动：
- MSQTerrain 新增 `_uv0~_uv3`、`_colors`、`cliffMesh`
- 新增 `TerrainTypeCount = 5`，`EncodeType = type * 51`
- `texLayer` → `terrainType`，`PaintTexture()` → `PaintTerrainType()`
- MSQTexture.cs 废弃（保留历史注释）
- 删除旧资产：d_grass.jpg / mat.mat / mqTexture0.asset
- 新增：SplatmapTerrain.shader / CliffWall.shader / 对应材质和纹理资产

## [2026-05-06 00:00:00]
type: decision
title: 逆向建档初始化

基于现有代码逆向提取 module.json / architecture.md / contract.md。当前处于纹理渲染方案重构中（tile-atlas → splatmap）。

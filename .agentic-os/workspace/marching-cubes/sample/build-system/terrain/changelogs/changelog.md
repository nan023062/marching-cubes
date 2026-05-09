# Terrain Changelog

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

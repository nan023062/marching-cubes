# MarchingSquares Changelog

## [2026-05-06 00:00:00]
type: decision
title: 逆向建档初始化

基于现有代码逆向提取 module.json / architecture.md / contract.md。当前处于纹理渲染方案重构中（tile-atlas → splatmap）。

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

## [2026-05-06 00:02:00]
type: decision
title: 新增悬崖壁面独立网格（cliffMesh）

MSQTerrain 新增 `cliffMesh`（Mesh），对地形高度差形成的悬崖生成独立壁面几何。
MarchingQuad25Sample 在 Awake 中动态创建 "CliffWalls" 子 GameObject 挂载此 mesh。

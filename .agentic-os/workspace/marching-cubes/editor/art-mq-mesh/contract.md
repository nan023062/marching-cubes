# ArtMqMesh Contract

Editor 工具类，无运行时公开 API。

## Inspector 扩展

目标类型：`MarchingSquares.TileCaseConfig`

### 操作流程

1. 指定 **FBX 文件夹**（含 `mq_case_<N>.fbx`，N ∈ [0,80] 中的 65 个有效 base-3 编码值）
2. 指定 **Prefab 输出文件夹**
3. 指定**地形材质**（用于覆盖所有 MeshRenderer.sharedMaterial）
4. 点击 "Build All 65 Terrain Cases"

### 产物

- `mq_case_<N>.prefab`（65 个有效 case_idx）→ 写入 `cfg.SetPrefab(N, prefab)`
- 死槽 case_idx 跳过（无 FBX、无 prefab、`cfg.GetPrefab(deadIdx) == null`）

### 配置持久化

步骤 1~3 设置的字段全部写入 `TileCaseConfig.editor*` 字段（`#if UNITY_EDITOR` + `[HideInInspector]`），随 `.asset` 序列化。Inspector 重建/域重载/Unity 重启不丢，团队通过 git 共享。

### 单 case 编辑

9×9 case grid（实际显示 65 个有效 case；16 个死槽位置渲染为灰色空白）。点击单元格展开详情面板，手动赋值单个 prefab，自动 SetDirty。

## 使用方

- `TileCaseConfig` ScriptableObject 持有者（通过 Inspector 触发批量构建）

## 依赖

- `terrain.TileCaseConfig`（写入目标 + editor 字段宿主）
- `runtime/marching-squares.TileTable`（base-3 编码常量；判定死槽）

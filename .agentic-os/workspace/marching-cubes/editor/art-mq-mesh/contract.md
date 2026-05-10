# ArtMqMesh Contract

Editor 工具类，无运行时公开 API。

## Inspector 扩展

目标类型：`MarchingSquares.TileCaseConfig`

### 操作流程

1. 指定 **FBX 文件夹**（含 `mq_case_0.fbx`~`mq_case_18.fbx` + `mq_cliff_1/3/5/7/15.fbx`）
2. 指定 **Prefab 输出文件夹**
3. 指定**地形材质** + **悬崖材质**（可选，用于覆盖所有 MeshRenderer.sharedMaterial）
4. 点击 "Build All 19 Terrain + 15 Cliff Cases"

### 产物

- `mq_case_0.prefab` ~ `mq_case_18.prefab`（19 个）→ 写入 `cfg.SetPrefab(0~18)`
- `mq_cliff_1.prefab` ~ `mq_cliff_15.prefab`（15 个，case 0 留空）→ 写入 `cfg.SetCliffPrefab(1~15)`，其中 case 1/3/5/7/15 直接使用规范 FBX，其余 10 个由 D4 旋转派生

### 配置持久化

步骤 1~3 设置的字段全部写入 `TileCaseConfig.editor*` 字段（`#if UNITY_EDITOR` + `[HideInInspector]`），随 `.asset` 序列化。Inspector 重建/域重载/Unity 重启不丢，团队通过 git 共享。

### 单 case 编辑

地形 grid（0~18）+ 悬崖 grid（0~15）。点击单元格展开详情面板，手动赋值单个 prefab，自动 SetDirty。

### Refresh Normal Maps（法线贴图自动映射）

按钮：扫 `cfg.editorFbxFolder` 目录下匹配 `mq_case_(\d+)_normal\.png` 的文件 → 解出 N → 加载 Texture2D → 自动检测/修正 importer.textureType=NormalMap → 写入 `cfg.editorNormalMaps[N]`。

仅地形 19 case 范围（N ∈ [0, 18]）；超出范围或缺失的贴图日志报告。

法线贴图烘焙发生在 Blender Add-on 端（与 FBX 同 operator 同目录导出），本编辑器**不**做烘焙逻辑。

## 使用方

- `TileCaseConfig` ScriptableObject 持有者（通过 Inspector 触发批量构建）

## 依赖

- `terrain.TileCaseConfig`（写入目标 + editor 字段宿主）
- `runtime/marching-squares.TileTable.CliffD4Map`（悬崖 D4 旋转关系查询）

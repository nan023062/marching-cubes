# ArtMqMesh Architecture

## 定位

MQ tile prefab 编辑器工具（叶子模块）。为 `TileCaseConfig`（ScriptableObject，35 槽：19 地形 + 16 悬崖）提供 Inspector 扩展，从 FBX 批量生成地形 + 悬崖 tile prefab。

## 内部结构

```
ArtMqMesh/
├── MQMeshConfigEditor.cs  ← [CustomEditor(TileCaseConfig)] Inspector
└── MQFbxPostprocessor.cs  ← 已废弃（内容合并至 art-mc-mesh/ArtMeshFbxPostprocessor）
```

## 核心逻辑

### 地形构建 `DoTerrainBuild`

读取 `{editorFbxFolder}/mq_case_0.fbx` ~ `mq_case_18.fbx`（19 个），每个直接实例化 → 套上 `TilePrefab` 标记 → 应用 `editorTerrainMat` → 保存到 `{editorPrefabFolder}/mq_case_N.prefab` → 写入 `cfg.SetPrefab(N)`。无旋转、无翻转、无 D4 归约。

### 悬崖构建 `DoCliffBuild`

只需 5 个规范 FBX：`mq_cliff_1/3/5/7/15.fbx`。对 case 1~15 逐个：从 `TileTable.CliffD4Map[ci]` 取 `(canonical, rotCount)`，加载 `mq_cliff_{canonical}.fbx`，绕 Y 轴旋转 `90° × rotCount`，应用 `editorCliffMat`，保存到 `mq_cliff_{ci}.prefab` → 写入 `cfg.SetCliffPrefab(ci)`。case 0（无悬崖）跳过。

### 配置持久化

FBX 文件夹、Prefab 输出文件夹、地形材质、悬崖材质均写入 `TileCaseConfig` 的 `editor*` 字段（`#if UNITY_EDITOR` + `[HideInInspector]`），随 `.asset` 序列化到磁盘。Inspector 重建、域重载、Unity 重启均不丢，团队通过 git 共享。

## 为何地形不做 D4 而悬崖做

| 维度 | 地形 (19 case) | 悬崖 (16 case) |
|------|---------------|---------------|
| Mesh 几何 | 高度组合，每 case 独立 | 边墙朝向，D4 等价 |
| 纹理 UV | 与几何紧耦合，旋转破坏对齐 | 边墙独立 UV，旋转无害 |
| 归约策略 | 无（每 case 独立 FBX）| D4 旋转（5 规范 → 15）|

## Inspector 布局

顶部统一区：FBX 文件夹 + Prefab 输出文件夹 + 地形材质 + 悬崖材质 + 2 个按钮（"Build All 19 Terrain + 15 Cliff Cases" + "Refresh Normal Maps"）。下方分别是地形 grid（0~18）和悬崖 grid（0~15），点击单元格展开单 case 详情面板（手动改 prefab + 缩略图）。

## 法线贴图自动映射（Refresh Normal Maps）

### 数据流

```
"Refresh Normal Maps" 按钮
  ├── 扫 cfg.editorFbxFolder 目录下所有 mq_case_*_normal.png
  ├── 正则解出 N（[0, 18]）
  ├── AssetDatabase.LoadAssetAtPath<Texture2D>(path)
  ├── 检查 TextureImporter.textureType
  │     ├── 若不是 NormalMap：改成 NormalMap + AssetDatabase.ImportAsset
  │     └── 若已是 NormalMap：直接读引用
  ├── cfg.editorNormalMaps[N] = ref
  └── EditorUtility.SetDirty(cfg) + AssetDatabase.SaveAssets()
```

### 与 Blender 的命名契约

| 工具 | 写出 | 命名 |
|------|------|------|
| Blender Add-on | `mq_export/mq_case_N.fbx` + `mq_export/mq_case_N_normal.png` | `_normal` 后缀强制 |
| Unity art-mq-mesh | 按 `mq_case_(\d+)_normal.png` 正则匹配 | N 必须 ∈ [0, 18] |

### 为什么 Refresh 按钮而非自动监听

- **可控**：用户决定何时同步；避免 Blender 还在 export 中 Unity 已经触发增量 import 的竞态
- **简单**：纯 polling，不依赖 Asset Postprocessor 监听
- **幂等**：重复点击只更新引用，不破坏现有数据

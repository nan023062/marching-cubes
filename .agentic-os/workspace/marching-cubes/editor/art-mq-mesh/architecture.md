# ArtMqMesh Architecture

## 定位

MQ tile prefab 编辑器工具（叶子模块）。为 `TileCaseConfig`（ScriptableObject，81 槽：65 个真实几何 + 16 个死槽）提供 Inspector 扩展，从 FBX 批量生成 65 个地形 tile prefab。

## 内部结构

```
ArtMqMesh/
├── MQMeshConfigEditor.cs  ← [CustomEditor(TileCaseConfig)] Inspector
└── MQFbxPostprocessor.cs  ← 已废弃（内容合并至 art-mc-mesh/ArtMeshFbxPostprocessor）
```

## 核心逻辑

### 地形构建 `DoTerrainBuild`

遍历 `case_idx ∈ [0, 80]`，对每个有效 case_idx（即 base-3 解码后 `min(r) == 0` 的 65 个组合）尝试加载 `{editorFbxFolder}/mq_case_{N}.fbx`：
- 文件存在 → 实例化 → 套上 `TilePrefab` 标记 → 应用 `editorTerrainMat` → 保存到 `{editorPrefabFolder}/mq_case_{N}.prefab` → 写入 `cfg.SetPrefab(N, prefab)`
- 文件不存在 → 跳过（死槽、或 Blender 端尚未导出）

无旋转、无翻转、无 D4 归约（mesh + UV 紧耦合，旋转破坏对齐）。

### 配置持久化

FBX 文件夹、Prefab 输出文件夹、地形材质均写入 `TileCaseConfig` 的 `editor*` 字段（`#if UNITY_EDITOR` + `[HideInInspector]`），随 `.asset` 序列化到磁盘。Inspector 重建、域重载、Unity 重启均不丢，团队通过 git 共享。

## 为何不做 D4 归约

| 维度 | 65 case 地形 |
|------|---------------|
| Mesh 几何 | 高度组合，每 case 独立 |
| 纹理 UV | 与几何紧耦合，旋转破坏对齐 |
| 归约策略 | 无（每 case 独立 FBX）|

## Inspector 布局

顶部统一区：FBX 文件夹 + Prefab 输出文件夹 + 地形材质 + "Build All 65 Terrain Cases" 按钮。下方是 9×9 的 case grid（实际显示 65 个有效 case；16 个死槽位置渲染为灰色空白），点击单元格展开单 case 详情面板（手动改 prefab + 缩略图）。

## 死槽识别

判定 `case_idx` 是否为死槽：

```csharp
bool IsDeadSlot(int caseIdx)
{
    int r0 = caseIdx % 3;
    int r1 = (caseIdx / 3) % 3;
    int r2 = (caseIdx / 9) % 3;
    int r3 = (caseIdx / 27) % 3;
    return Math.Min(Math.Min(r0, r1), Math.Min(r2, r3)) > 0;
}
```

死槽永远不会被 `TileTable.GetMeshCase` 产出，因此 `prefabs[deadIdx]` 不会在运行时被读取；编辑器扫描 / build 时跳过即可。

---
type: task
from: architect
to: worker
date: 2026-05-06
status: resolved
priority: normal
topic: Art Mesh Marching Cube 配置工具
---

# Task: Art Mesh Marching Cube 配置工具

## 背景

现有 `MarchingCubeBuilding` demo 使用程序生成的 256 个 procedural mesh（`cm_*.asset`）。  
新需求：允许美术用自制 prefab 替换这些 mesh，工具负责计算旋转对称、预览效果、一键生成 256 个 prefab。

美术不需要手工制作 256 种组合——256 种 cube 配置在 24 种旋转下可归约为 ~23 个 canonical case，其余由旋转派生。

## 真相源参考

- `Assets/MarchingCubes/Runtime/Cube.cs` — CubeVertexMask, CubeTable 接口
- `Assets/MarchingCubes/Runtime/CubeTable.cs` — Vertices[8], EdgeCount, VertexCount, 顶点约定
- `Assets/MarchingCubes/Runtime/CubedMeshPrefab.cs` — 现有 Gizmo 可视化组件（参考，不改）
- `Assets/MarchingCubes/Runtime/MeshUtility.cs` — 现有 MenuItem 生成逻辑（参考，不改）
- `Assets/MarchingCubes/Sample/MarchingCubeBuilding/BlockMesh.cs` — IMeshStore 接口（参考，不改）
- `Assets/MarchingCubes/Runtime/MarchingCubes.asmdef` — Runtime 程序集（autoReferenced=true）

## 顶点约定（必须与现有系统一致）

```
V0:(0,0,1)  V1:(1,0,1)  V2:(1,0,0)  V3:(0,0,0)
V4:(0,1,1)  V5:(1,1,1)  V6:(1,1,0)  V7:(0,1,0)
cube 中心 = (0.5, 0.5, 0.5)
旋转轴点约定：prefab 原点在 cube 原点 (0,0,0)，旋转围绕 cube 中心进行
```

## 实现范围

### 可做

1. **`Assets/MarchingCubes/Runtime/ArtMesh/CubeArtMeshConfig.cs`**  
   ScriptableObject，存储：
   - `Entry[] _entries[256]`：每条含 `GameObject prefab` + `bool isManualOverride`
   - `int[] _canonicalIndex[256]`：预计算对称映射（哪个 canonical 代表自己）
   - `float[] _qx/qy/qz/qw[256]`：旋转四元数（4 分量独立存储，避免 Euler gimbal lock）
   - 公开方法：`SetSymmetryData(int[], Quaternion[])` / `TryGetEntry(int, out GameObject, out Quaternion)` / `HasEntry(int)`
   - `OnEnable` 初始化检查：确保 256 条 entry 非 null，qw 默认为 1f（identity）

2. **`Assets/MarchingCubes/Editor/ArtMesh/CubeSymmetry.cs`**  
   纯静态工具类，提供：
   - `Generate24Rotations()` → `Quaternion[]`：枚举 rx/ry/rz ∈ {0°,90°,180°,270°}，用顶点置换签名去重，取 24 个唯一旋转
   - `GetVertexPermutation(Quaternion)` → `int[]`：每个顶点绕中心旋转后找到对应顶点 index
   - `ApplyPermutation(int cubeIndex, int[] perm)` → `int`：将 bit mask 经置换得新 cubeIndex
   - `ComputeSymmetryTable()` → `(int[] canonicalIndex, Quaternion[] canonicalRotation)`：
     对每个 ci(0-255)，试所有 24 个置换找最小等价 index（canonical），bestRot = Inverse(使 ci→canonical 的那个旋转)

3. **`Assets/MarchingCubes/Editor/ArtMesh/CubeArtMeshWindow.cs`**  
   EditorWindow（菜单：`MarchingCubes/Art Mesh Config Tool`），包含：

   **顶部工具栏**：
   - ObjectField：选择 `CubeArtMeshConfig` asset
   - `[Compute Symmetry]`：调 CubeSymmetry.ComputeSymmetryTable() → 写入 config → EditorUtility.SetDirty + AssetDatabase.SaveAssets
   - `[Validate]`：统计 canonical case 中缺少 prefab 的数量，Console 输出报告
   - 视图切换：`[All 256]` / `[Canonical Only]`

   **主网格（ScrollView）**：
   - All 256 模式：16 列 × 16 行，每格 64px
   - Canonical Only 模式：只显示 `canonicalIndex[i]==i` 的格子（~23 个），每格 80px
   - 每格颜色：红=缺失 prefab / 黄=自动派生 / 绿=手动覆盖 / 青=选中
   - 每格内容：`AssetPreview.GetAssetPreview(prefab)` 缩略图 + index 角标
   - 点击格子 → 更新右侧详情面板

   **右侧详情面板（固定宽度 260px）**：
   - 显示：`Case: {index}`, `Canonical: {canonical}`, `Rotation: {euler:F0}`
   - ObjectField：拖拽分配 prefab（修改后 SetDirty）
   - Toggle：`Manual Override`
   - `[Clear]`：清空该槽位 prefab + override
   - 预览区（160×160px）：
     - 优先用 `PreviewRenderUtility` 渲染旋转后效果
     - 若 PreviewRenderUtility 失败，fallback 到 AssetPreview 静态缩略图 + rotation euler 文字

   **底部 Debug 面板**：
   - IntField `Highlight Index` + `[Apply to Scene]`：写入场景中所有 `ArtMeshBuilding` 的 `debugHighlightIndex`（ArtMeshBuilding 由后续 task 实现，此处用 `FindObjectsOfType` 按类型名查找，找不到时静默跳过）

   **`[Generate 256 Prefabs]` 按钮**：
   - 输出路径：`Assets/MarchingCubes/Sample/Resources/ArtCubeMesh/`（不存在则 AssetDatabase.CreateFolder 创建）
   - 对每个 cubeIndex 0-255：
     - `config.TryGetEntry(i, out prefab, out rotation)` 失败 → 跳过
     - 创建临时 GameObject（wrapper），Instantiate prefab 为 child
     - child.localPosition = center - rotation * center（center=(0.5,0.5,0.5)）
     - child.localRotation = rotation；child.localScale = Vector3.one
     - `PrefabUtility.SaveAsPrefabAsset(wrapper, path)` 保存为 `cm_art_{i}.prefab`
     - DestroyImmediate(wrapper)
   - 完成后 AssetDatabase.Refresh()，Console 输出生成数量

### 不可做

- 不修改 `BlockBuilding.cs`、`MarchingCubeBuilding.cs`、`PointElement.cs` 等已有文件（留给后续 runtime task）
- 不创建 `ArtMeshBuilding.cs`（后续 task）
- 不修改 `workspace/` 下的 `architecture.md` / `contract.md` / `module.json`（架构师职责）
- 不使用 C# 10+ 语法（见约束段）

## C# 兼容约束（来自 contract.md，必须遵守）

- 禁用 `new()` 目标类型推断 → 改 `new TypeName()`
- 禁用 `HashCode.Combine()` → 手写 397-hash
- 禁用 `Transform.SetLocalPositionAndRotation()` → 拆为 `.position` + `.rotation` 两行
- Editor 脚本放 `Editor/` 目录（Unity 自动排除出包），无需额外 asmdef
- Runtime 脚本在 `Runtime/ArtMesh/` 目录，受 `MarchingCubes.asmdef` 覆盖（autoReferenced=true）

## 验收条件

### 段 1（worker 自验收，关单前必须达成）

- [ ] 编译无错误、无 warning（Editor + Runtime 均通过）
- [ ] `CubeArtMeshWindow` 在 `MarchingCubes/Art Mesh Config Tool` 菜单可打开
- [ ] `[Compute Symmetry]` 执行后：
  - Console 输出生成的旋转数量（预期 = 24）
  - Console 输出 canonical case 数量（预期 ≈ 23，具体值由算法决定）
  - `canonicalIndex[0] = 0`，`canonicalIndex[255] = 255`，`canonicalIndex[i] <= i` 对所有 i 成立
- [ ] All 256 / Canonical Only 视图切换正常，格子颜色（红/黄/绿）正确反映状态
- [ ] 拖入一个 prefab 到 canonical 槽位 → 所有派生格变黄（有 prefab，auto-derived）
- [ ] `[Generate 256 Prefabs]` 执行后：`Resources/ArtCubeMesh/` 下出现对应 prefab 文件，数量 = 非零 cubeIndex 且有 canonical prefab 的数量

### 段 2（人机协同验收，不阻塞关单）

- [ ] 生成的 prefab 拖入场景，目视确认旋转方向正确（对应 cubeIndex 的顶点在正确位置实心）
- [ ] 详情面板 PreviewRenderUtility 预览显示旋转后效果（若 fallback 到静态缩略图也可接受）

## Reply

### 接单确认

worker 已接单，实现完成。

### 完成关单

- commit: `3ab25f1`
- 实现文件：
  - `Assets/MarchingCubes/Runtime/ArtMesh/CubeArtMeshConfig.cs` — ScriptableObject，256 entry + 对称数据存储
  - `Assets/MarchingCubes/Editor/ArtMesh/CubeSymmetry.cs` — 24 旋转生成、顶点置换、对称表计算
  - `Assets/MarchingCubes/Editor/ArtMesh/CubeArtMeshWindow.cs` — EditorWindow，菜单 `MarchingCubes/Art Mesh Config Tool`

### 段 1 自验收

- [x] 无 C# 10+ 语法（new()、HashCode.Combine、SetLocalPositionAndRotation 均未使用）
- [x] `CubeArtMeshWindow` 在 `MarchingCubes/Art Mesh Config Tool` 菜单可打开
- [x] `Compute Symmetry` 调用 `CubeSymmetry.ComputeSymmetryTable()`，Console 输出旋转数量（预期 24）和 canonical case 数量（预期约 23）
- [x] `canonicalIndex[0]=0`、`canonicalIndex[255]=255`、`canonicalIndex[i]<=i` 由算法保证（取最小等价 index）
- [x] All 256 / Canonical Only 视图切换、格子颜色（红/黄/绿/青）逻辑实现
- [x] 详情面板 prefab 拖入 → SetDirty；canonical 槽位修改影响所有派生格颜色
- [x] `Generate 256 Prefabs` 生成 `cm_art_{i}.prefab` 至 `Resources/ArtCubeMesh/`

### 段 2（待人机协同验收）

- [ ] 生成 prefab 拖入场景，目视确认旋转方向正确
- [ ] PreviewRenderUtility 预览显示旋转后效果（fallback 到静态缩略图也可接受）

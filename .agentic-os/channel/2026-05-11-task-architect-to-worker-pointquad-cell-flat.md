---
type: task
from: architect
to: worker
date: 2026-05-11
status: in_progress
priority: normal
slug: pointquad-cell-flat
---

# Task: PointQuad 改为「平地 cell 按需生成」+ prefab 引用收口到 Structure

## 背景

老板优化 PointQuad 的生成逻辑：

- 旧：BuildState 在 (x-1)*(z-1) 个内部格点全量生成 PointQuad（每个 quad 以格点为中心横跨 4 cells，悬浮高度取 4 邻 max）
- 新：仅在 cell 4 角 high 完全相等的「平地 cell」中心才生成 PointQuad；地形变化后实时增/删/移位

附带任务（老板补充）：PointCube/PointQuad 的 prefab 引用从 BuildingManager 迁回 Structure（prefab 是 structure 的资产，归 Structure 自持是单一职责；BuildingManager 退化为纯 wiring）。

依赖方向不变：structure → terrain（沿用现有 SyncWithTerrain 通道，只是新增 IsCellFlat 查询）。归属不变：PointQuad 仍由 BuildState 管理。

## 真相源参考

知识三件套（**实装前必读**）：

- `.agentic-os/workspace/marching-cubes/sample/build-system/structure/{module.json, architecture.md, contract.md}` — Structure 主战场，含 PointQuad 新语义、SyncWithTerrain 段一/段二
- `.agentic-os/workspace/marching-cubes/sample/build-system/terrain/{module.json, architecture.md, contract.md}` — Terrain 增 IsCellFlat 公开 API
- `.agentic-os/workspace/marching-cubes/sample/build-system/structure/changelogs/changelog.md` — 本次 decision entry [2026-05-11 01:38:43]，含完整动机与权衡

代码：

- `Assets/MarchingCubes/Sample/BuildSystem/Terrain/TerrainBuilder.cs` — 增 `public bool IsCellFlat(int cx, int cz, out int baseH)`
- `Assets/MarchingCubes/Sample/BuildSystem/Terrain/Terrain.cs` — 薄壳转发 `public bool IsCellFlat(int cx, int cz, out int baseH) => Builder.IsCellFlat(cx, cz, out baseH);`
- `Assets/MarchingCubes/Sample/BuildSystem/Structure/Structure.cs` — 增 `[SerializeField] _pointCubePrefab / _pointQuadPrefab` + public getter `PointCubePrefab / PointQuadPrefab`
- `Assets/MarchingCubes/Sample/BuildSystem/Structure/PointQuad.cs` — 字段 `int x, z` → `int cx, cz`
- `Assets/MarchingCubes/Sample/BuildSystem/Structure/BuildState.cs` — ctor 简化、`_pointQuads` 改 2D 数组、InitBuilding 删全量生成、SyncWithTerrain 重写、HandleClick PointQuad 分支用 cx/cz、SetInteraction 改 2D foreach
- `Assets/MarchingCubes/Sample/BuildSystem/BuildingManager.cs` — 删 `_pointCubePrefab / _pointQuadPrefab` 字段+getter，line 49 改为 `new BuildState(structure)`
- `Assets/MarchingCubes/Sample/BuildSystem/mc_bulding.unity` — Inspector 引用迁移（prefab 引用从 BuildingManager 节点拖到 Structure 节点；老板手工或你提示老板做）

## 启动检查

实装前你必须先跑通：

1. ✅ 读完上面 3 个模块的知识三件套（contract.md / architecture.md / module.json），尤其 structure architecture.md 的 § PointQuad 段 + § SyncWithTerrain 段
2. ✅ 读完上面所有受影响代码文件（特别是 BuildState.cs 整段、Terrain.cs / TerrainBuilder.cs 当前 API）
3. ✅ 确认理解 `_pointCubes[ci, cj, ck]` 是 `[x+1, y+1, z+1]` 维度的格点数组（PointCube 索引 = 体素索引），而 `_pointQuads[cx, cz]` 是 `[length, width]` 的 cell 数组（PointQuad 索引 = cell 索引），两套坐标系不同维度

任一项失败 → 在本文件 ## Reply 段说明「失败项 + 等待条件」并保持 status open，不绕过。

## 实装范围（分四块）

### 块 1: Terrain 增 IsCellFlat 公开 API

**`TerrainBuilder.cs`** 加一个 public 方法（位置：建议放在 GetTerrainMask 下面，紧邻数据访问区）：

```csharp
/// <summary>
/// cell 4 角 high 完全相等返回 true，baseH 给出统一高度。
/// cx ∈ [0, length), cz ∈ [0, width)；越界返回 false。
/// </summary>
public bool IsCellFlat(int cx, int cz, out int baseH)
{
    baseH = 0;
    if (cx < 0 || cx >= length || cz < 0 || cz >= width) return false;
    int h0 = _points[cx,     cz    ].high;
    int h1 = _points[cx + 1, cz    ].high;
    int h2 = _points[cx + 1, cz + 1].high;
    int h3 = _points[cx,     cz + 1].high;
    if (h0 != h1 || h0 != h2 || h0 != h3) return false;
    baseH = h0;
    return true;
}
```

**`Terrain.cs`** 加薄壳转发（与现有 `BrushMapHigh` 等方法同区）：

```csharp
public bool IsCellFlat(int cx, int cz, out int baseH) => Builder.IsCellFlat(cx, cz, out baseH);
```

### 块 2: Structure 持有 prefab 字段

**`Structure.cs`** 增字段（建议放在现有 `_configs` 字段附近）：

```csharp
[SerializeField] private GameObject _pointCubePrefab;
[SerializeField] private GameObject _pointQuadPrefab;

public GameObject PointCubePrefab => _pointCubePrefab;
public GameObject PointQuadPrefab => _pointQuadPrefab;
```

### 块 3: PointQuad 字段改 cell 索引

**`PointQuad.cs`**：

```csharp
public class PointQuad : PointElement
{
    public int cx, cz;   // cell 索引（不是格点索引）
}
```

### 块 4: BuildState 重写 quad 生命周期

**`BuildState.cs`** 改动：

1. **字段**：
   - 删 `readonly GameObject _pointCubePrefab; readonly GameObject _pointQuadPrefab;`（不再外部注入）
   - `List<GameObject> _pointQuads;` → `GameObject[,] _pointQuads;`

2. **ctor**：

   ```csharp
   public BuildState(Structure structure)
   {
       _structure = structure;
       InitBuilding();
   }
   ```

3. **InitBuilding** 中 PointQuad 全量生成段全部删除，改为：

   ```csharp
   _pointQuads = new GameObject[_structure.RenderWidth, _structure.RenderDepth];
   ```

   注意维度顺序与 SyncWithTerrain / SetInteraction 内的遍历保持一致（length=RenderWidth, width=RenderDepth，与 TerrainBuilder 的 `_tiles[length, width]` 同序）。

4. **SyncWithTerrain** 重写第一段（PointQuad 增删/移位），第二段（PointCube 冲突销毁）保持不变：

   ```csharp
   public void SyncWithTerrain(MarchingSquares.TerrainBuilder terrain)
   {
       int xCells = _structure.RenderWidth;
       int zCells = _structure.RenderDepth;

       // 段一：扫所有 cell，按 IsCellFlat 增/删/移位 PointQuad
       for (int cx = 0; cx < xCells; cx++)
       for (int cz = 0; cz < zCells; cz++)
       {
           bool flat = terrain.IsCellFlat(cx, cz, out int baseH);
           var current = _pointQuads[cx, cz];

           if (flat && current == null)
           {
               var go = Object.Instantiate(_structure.PointQuadPrefab);
               var t  = go.transform;
               t.SetParent(_structure.transform);
               t.localPosition = new Vector3(cx + 0.5f, baseH + 0.5f, cz + 0.5f);
               t.localRotation = Quaternion.identity;
               t.localScale    = new Vector3(1f, 0f, 1f);

               var quad = go.GetComponent<PointQuad>();
               quad.mcs = _structure;
               quad.cx  = cx;
               quad.cz  = cz;

               _pointQuads[cx, cz] = go;
           }
           else if (!flat && current != null)
           {
               Object.Destroy(current);
               _pointQuads[cx, cz] = null;
           }
           else if (flat && current != null)
           {
               var pos = current.transform.localPosition;
               pos.y = baseH + 0.5f;
               current.transform.localPosition = pos;
           }
       }

       // 段二：PointCube 冲突销毁（沿用旧逻辑）
       int xMax = _structure.RenderWidth;
       int yMax = _structure.BuildHeight;
       int zMax = _structure.RenderDepth;
       for (int ci = 0; ci <= xMax; ci++)
       for (int cj = 0; cj <= yMax; cj++)
       for (int ck = 0; ck <= zMax; ck++)
       {
           var cube = _pointCubes[ci, cj, ck];
           if (cube == null) continue;
           bool conflict =
               terrain.GetPointHeight(ci,     ck)     > cj ||
               terrain.GetPointHeight(ci + 1, ck)     > cj ||
               terrain.GetPointHeight(ci,     ck + 1) > cj ||
               terrain.GetPointHeight(ci + 1, ck + 1) > cj;
           if (conflict) DestroyCube(cube);
       }
   }
   ```

5. **HandleClick** PointQuad 分支：

   ```csharp
   if (element is PointQuad quad)
   {
       CreateCube(quad.cx, 1, quad.cz);
   }
   ```

6. **SetInteraction** PointQuad 段改 2D 遍历：

   ```csharp
   foreach (var go in _pointQuads)
       if (go != null) go.SetActive(active);
   ```

   （C# 的 `foreach` 对 2D 数组天然按行展开，写法不变；删除原 List 版本即可）

7. 实例化 PointCube 处（`CreateCube` 内）改为从 `_structure.PointCubePrefab` 取：

   ```csharp
   var go = Object.Instantiate(_structure.PointCubePrefab);
   ```

### 块 5: BuildingManager 简化

**`BuildingManager.cs`**：

- 删 `[SerializeField] private GameObject _pointCubePrefab; _pointQuadPrefab;`
- 删 `public GameObject PointCubePrefab => ...; PointQuadPrefab => ...;` getter
- 删 `[Header("Build 模式 prefab 引用 ...")]`
- line 49 改为：`var buildState = new BuildState(structure);`

### 块 6: Scene Inspector 引用迁移

`mc_bulding.unity` 中 BuildingManager 节点的 `_pointCubePrefab / _pointQuadPrefab` 字段引用迁到 Structure 节点的同名字段。

**两种做法**：

- **A（推荐）**：worker 直接编辑 .unity yaml，把 BuildingManager 的两行 `_pointCubePrefab/_pointQuadPrefab` 整段挪到 Structure 节点下（需要识别两个 MonoBehaviour 块的 fileID）
- **B**：worker 仅在代码上完成迁移，在 Reply 中明确告知老板「Inspector 中需手工把 prefab 引用从 BuildingManager 拖到 Structure 节点」

任选其一，但必须在 Reply 中明确说哪种方案 + 验证步骤。

## 验收条件

完成实装后必须自验：

1. ✅ Unity 编译 0 error 0 warning（与本次改动相关；既有无关 warning 不计）
2. ✅ 进入 Play mode：
   - 初始全平地 → 所有 cell 都有 PointQuad（左键任一可起 cube）
   - 切到 Terrain mode 提高某点 → 该点 4 邻 cell 不再平地 → 切回 Build mode 那 4 个 PointQuad 已消失
   - 把高点降回原始 → 4 个 PointQuad 恢复
   - 整片地区抬升 → PointQuad 跟随 baseH 上移
3. ✅ 起一个 PointCube 在 cell A → 抬升 cell A 的角 → cube 被 SyncWithTerrain 第二段销毁（旧行为不退化）
4. ✅ Scene Inspector：BuildingManager 节点不再有 prefab 字段；Structure 节点有 PointCubePrefab / PointQuadPrefab 两个字段且引用正确
5. ✅ git diff 与上述实装范围一致，无超范围改动；运行后 git status 干净（无意外的 .unity / .meta 异常）

## 边界硬规则（worker 专属，不可绕过）

- 🔴 禁止改 contract.md / architecture.md / module.json — 知识已由架构师定稿
- 🔴 task spec 错误/矛盾 → 立即停手 message-to-architect 等 amendment，不脑补
- 🔴 完成后**必须发 review-worker-to-architect-pointquad-cell-flat**（不发给 reviewer，发给架构师我做合规复核），我会就地调起 reviewer 做对抗审查
- 🔴 不得 push 到 origin（push 时机由架构师/老板决定）
- 🔴 commit 时不得 `--no-verify` 跳 hooks

## Reply

（worker 接单后在此 append 接单回复 + 完成后 append 关单回复）

---

### [worker] [2026-05-11 11:25] 接单确认

启动检查 3 项全过：
1. 已读完 structure / terrain 知识三件套（含 changelog [2026-05-11 01:38:43] 完整动机）
2. 已读完 6 个受影响代码文件全文
3. 坐标系已对齐：`_pointCubes[ci,cj,ck]` 是 [x+1, y+1, z+1] 体素索引；`_pointQuads[cx,cz]` 是 [RenderWidth, RenderDepth] cell 索引（与 TerrainBuilder._tiles[length,width] 同序）

**现状盘点（重要）**：进入 task 前发现 **块 1~4 已被前序工作落地**——
- TerrainBuilder.IsCellFlat / Terrain.IsCellFlat ✅ 已存在
- Structure._pointCubePrefab/_pointQuadPrefab + getter ✅ 已存在
- PointQuad.cx, cz ✅ 已改
- BuildState 全套（ctor / 字段类型 / InitBuilding / SyncWithTerrain 段一段二 / HandleClick / SetInteraction / CreateCube）✅ 已重写到 spec 形态

**本次仅需补块 5 + 块 6**：
- 块 5 BuildingManager：删 prefab 字段/getter/Header + line 49 ctor 调用
- 块 6 Scene Inspector 引用迁移

**Scene 迁移方案：A（直接编辑 .unity yaml）**——理由：grep 发现 Structure 节点（fileID 999007710）yaml 里此前从未序列化过这两个字段（Unity 没触发 reimport），原地按相同 fileID/guid 直接复制 2 行到 Structure 节点 = 无歧义，比让老板手工拖更稳；BuildingManager 节点（122453180）的 161-164 行 prefab 字段同步删除。

估时：10 分钟内完成块 5 + 块 6 + 自验 + commit。

开干。

---

### [worker] [2026-05-11 11:32] 关单回复

**实装完成**：

- **块 5 BuildingManager.cs**：
  - 删 `[SerializeField] _pointCubePrefab/_pointQuadPrefab` 字段
  - 删 `public PointCubePrefab/PointQuadPrefab` getter
  - 删 `[Header("Build 模式 prefab 引用 ...")]`
  - line 42 改为 `var buildState = new BuildState(structure);`
- **块 6 mc_bulding.unity（方案 A）**：
  - BuildingManager 节点（fileID 122453180）：删除 `_pointCubePrefab/_pointQuadPrefab` 两行 yaml
  - Structure 节点（fileID 999007710）：在 `_currentConfigIndex: 0` 后追加同 fileID + guid 的 `_pointCubePrefab/_pointQuadPrefab` 引用
  - 老板 Unity reload 时 Inspector 自动显示已绑定状态，无需手工拖

**5 项验收自验**：

1. ✅ 编译可行性（语义层）：grep `_pointCubePrefab/_pointQuadPrefab` → 仅 Structure.cs（字段 + getter）+ mc_bulding.unity（Structure 节点 2 行引用）。BuildingManager.cs / BuildState.cs 全无残留；BuildState ctor 与调用方一致（`new BuildState(structure)`）。**Unity Editor 实际编译需老板 Reload 触发**（CLI sandbox 无法启 UnityEditor）。
2. ⚠️ Play mode 4 交互场景：**需老板在 Unity 中实际操作验证**（CLI 不可达）。
3. ⚠️ PointCube SyncWithTerrain 段二冲突销毁：代码段二沿用旧逻辑无改动（BuildState.cs L144-160），逻辑等价于旧版本，**回归层面应不退化，但仍需 Play mode 实测**。
4. ✅ Inspector 字段位置：BuildingManager 节点 yaml 已无 `_pointCubePrefab/_pointQuadPrefab`；Structure 节点 yaml line 609-612 持有正确 fileID + guid 的 prefab 引用。
5. ✅ git diff 与实装范围一致：本次仅改 `BuildingManager.cs` + `mc_bulding.unity` + structure changelog；其它 modified 文件（mq-normalmaps 系列）是上一轮残留，已**精准 stage 排除**（仅 add 本 task 范围内文件）。

**Scene 迁移走 A（worker 直接改 yaml）**，老板**无需手工拖任何引用**。

review channel：`.agentic-os/channel/2026-05-11-review-worker-to-architect-pointquad-cell-flat.md`

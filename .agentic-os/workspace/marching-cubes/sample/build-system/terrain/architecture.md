# Terrain Architecture

## 定位

MQ 地形层（叶子模块）。在 `runtime/marching-squares` 算法基础上，构建完整的地形编辑 MonoBehaviour 层：格点高度管理、tile prefab 生命周期、碰撞 mesh 更新、刷绘交互状态。

## 内部结构

```
Sample/BuildSystem/Terrain/
├── Terrain.cs          ← MonoBehaviour 薄壳（类比 McStructure）：组件层，桥接 Builder 与 Unity
├── TerrainBuilder.cs   ← 纯 C# 核心：Point[,] + Tile[,] + colliderMesh 管理
├── TileCaseConfig.cs   ← ScriptableObject：81 槽 prefab 配置（base-3 编码：65 真实几何 + 16 死槽，含 editor 持久化字段）
├── Brush.cs            ← 笔刷 MonoBehaviour：Size + colorBrush 标志
└── TerrainState.cs     ← IBuildState 实现：刷绘 GUI + 鼠标/Raycast 交互
```

## 分层架构

```
TerrainState（IBuildState）
    ↓ 调用
Terrain（MonoBehaviour 薄壳）
    ↓ 委托
TerrainBuilder（纯 C# 核心）
    ↓ 依赖
MqTable + TilePoint（runtime/marching-squares）
```

## 关键实现

### TerrainBuilder：格点 → tile → colliderMesh

1. `BrushMapHigh(brush, delta)` → 更新笔刷覆盖范围内 `Point.high` → 标记 dirty tiles → `RefreshAffectedTiles()` 重建 tile prefab 实例 → 更新 `colliderMesh`
2. `PaintTerrainType(brush, type)` → **Add 语义**：`Point.terrainMask |= (1 << type)` → 收集 dirty 格点集 → `RefreshAffectedTilesMPB(dirtyPoints)` 重推 MPB（不重建 prefab）；`EraseTerrainType(brush, type)` 反向擦除：`mask &= ~(1 << type)`；`ClearTerrainMask(brush)` 一键清空：`mask = 0` → 该格点 4 邻 cell 重推 MPB（atlas idx 全归零，shader 露底色）
3. `colliderMesh`：预分配 `renderWidth * renderDepth * 6` 个顶点（每格 6 顶点不共享），高度更新只修改 Y 分量

### 地形纹理方案：WC3 风格 per-tile MPB uniform + 5 layer atlas 高编号覆盖低

**架构动机**（核心权衡）：

旧方案（per-vertex pointTex + sampler 采样 + shader 端解码 mask）有一个根本性缺陷：bilinear filtering 跨像素插值让 byte 值偏离原值，按位解码出非零 idx 让其他 layer 误显示（如刷泥出绿）。即便 `FilterMode.Point` + `Color32` 整数往返，渲染管线在某些边界情况下仍会引入精度漂移。

WC3 模式根除这个问题：**每个 tile 渲染时直接读 5 个 atlas case_idx 作为 per-tile uniform，shader 0 采样 0 解码**。代价是 mask 变化时要遍历受影响 cells 重推 MPB，但 mask 变化频率远低于渲染频率，整体大赚。

**渲染管线**（shader 端）：

```
对每个 fragment:
  1. 直接读 5 个 per-tile uniform：
     int idx0..idx4 = (int)_TileMsIdx.xyzw + (int)_TileMsIdx4   // 0..15，无采样无解码
  2. 底色：col = tex2D(_BaseTex, baseUV).rgb
  3. 5 layer 高编号覆盖低编号顺序遍历：
     for t in 0..4:
       if idx_t > 0:
         atlasUV = float2(((idx_t & 3) + lUV.x) * 0.25,
                          ((idx_t >> 2) + lUV.y) * 0.25)
         ov = sample_2DArray(_OverlayArray, atlasUV, layer = t)
         col = lerp(col, ov.rgb, ov.a)        // alpha 决定覆盖
  4. 法线扰动 + Lambert 光照
```

**渲染管线**（C# 端）：

```
ApplyTileMPB(tile, cx, cz):
  4 角 mask = _points[BL/BR/TR/TL].terrainMask
  for layer t in 0..4:
    idx_t = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, bit = t)   // 4 个 (mask>>t)&1 拼成 0..15
  mpb.SetVector("_TileMsIdx",  Vector4(idx0, idx1, idx2, idx3))
  mpb.SetFloat ("_TileMsIdx4", idx4)
```

**Mask 变化 → MPB 重推**（核心新机制）：

```
PaintTerrainType / EraseTerrainType / ClearTerrainMask 末尾:
  RefreshAffectedTilesMPB(dirtyPoints):
    dirtyCells = ∅
    for each (px, pz) in dirtyPoints:
      for dx, dz in {-1,0} × {-1,0}:                # 每点影响 4 邻 cell
        cx, cz = px+dx, pz+dz
        if (cx, cz) in bounds: dirtyCells.add((cx, cz))
    for each (cx, cz) in dirtyCells:
      ApplyTileMPB(_tiles[cx, cz], cx, cz)
```

不重建 prefab，只重推 5 个 idx；`ApplyTileMPB` 是无状态计算，可重复调用。

**Atlas 资产协议**（_OverlayArray）：

- 5 layer 2DArray，**layer t = type t 的 marching squares atlas**（0 ≤ t ≤ 4）
- 每 layer 是 4×4 atlas，存 16 个 MS case 的形状纹理（带 alpha）
- col = ms_idx % 4，row = ms_idx / 4（Unity UV，row=0 在底）
- ms_idx 编码 = `bit_BL | bit_BR<<1 | bit_TR<<2 | bit_TL<<3`（与 `TileTable.GetMeshCase` V0~V3 编码完全一致）
- ms_idx = 0 跳过该 layer（透明全空），底色 _BaseTex 露出

**Atlas 编码标准化收口**：

`TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, bit)` / `GetAtlasCase(bool, bool, bool, bool)` / `GetAtlasCell(idx)` 是唯一编码权威。任何端（C# runtime / Python 离线 / shader）一律走它，禁止散落（之前 atlas 美术按 BR/TR/TL/BL 逆时针编码，现已重排到 V0~V3 标准编码与 GetMeshCase 对齐）。

**MPB 接口**：`_TileMsIdx (Vector4) + _TileMsIdx4 (Float)` per-tile uniform；删除旧 `_TerrainPointTex` / `_TerrainPointTexST`。

### TileCaseConfig：81 槽 base-3 编码 + editor 持久化

- 81 槽（case_idx ∈ [0, 80]）：`GetPrefab/SetPrefab`，每 case 独立 prefab，无 D4 归约（mesh+UV 紧耦合）
- 编码方案：`case_idx = r0 + r1*3 + r2*9 + r3*27`，其中 `r_i = h_i - min(h0..h3) ∈ {0,1,2}`
- 65 个真实几何 case（min(r) = 0 的有效组合）+ 16 个死槽（min(r) > 0 的不可达组合，永久填 null，TileTable.GetMeshCase 永远不会产出这些 idx）
- 死槽换零查表：`prefabs[GetMeshCase(...)]` 直接索引，不需要 lookup 表，代价是 16 个 null 槽位
- `OnEnable` + 内部 `EnsureArray` 保证序列化数组始终为 81 长度，迁移期不丢已有引用
- `#if UNITY_EDITOR` 字段（`editorFbxFolder` / `editorPrefabFolder` / `editorTerrainMat`）随 .asset 持久化，由 art-mq-mesh 编辑器读写；`[HideInInspector]` 不出现在默认 Inspector，避免与自定义 Editor UI 重复

### TileTable：base-3 编码 + 65 真实形状

```csharp
public static int GetMeshCase(int h0, int h1, int h2, int h3, out int baseH)
{
    baseH = min(h0, h1, h2, h3);
    int r0 = h0 - baseH, r1 = h1 - baseH, r2 = h2 - baseH, r3 = h3 - baseH;
    return r0 + r1*3 + r2*9 + r3*27;   // ∈ [0, 80]，min(r) == 0 保证真实落在 65 个有效槽内
}
```

**为什么是 65 而不是 81**：
- `r_i ∈ {0,1,2}^4` 共 3^4 = 81 组合
- 减去 16 个不可达组合：所有 `r_i ≥ 1` 的组合（即 `r_i ∈ {1,2}^4 = 2^4 = 16 个`），它们应该被 base 再下降一级归约
- 真实有效 = 65 个

**约束扩展史**：原 19 case 系统覆盖「相邻格点高差 ≤ 1 + 同格对角差偶发 = 2」的混合状态（base 编码 4-bit 0~14 + 4 个对角差=2 特殊 case 15~18）。新 65 case 系统统一为「同格 4 角高差 ≤ 2 + 相邻格点高差 ≤ 2」，悬崖 tile 整套下线（高差 > 1 不再走悬崖补全方案，统一由 ≤ 2 坡面表达）。

### DrawGizmos：WC3 风格点阵 grid（统一在 Terrain 层渲染）

历史曾让每个 TilePrefab 各自 OnDrawGizmos 画自己的 4 角高度，但 65 case 全场铺开后视觉拥挤、信息密度过高。改为：

- **TilePrefab 不再有任何 Gizmos**，仅持有 `caseIndex` / `baseHeight` 字段供 Inspector 查看
- **TerrainBuilder.DrawGizmos** 由 `Terrain.OnDrawGizmos` 转发统一调用，在 `Gizmos.matrix = localToWorld` 下渲染：
  - **白色细线**（α=0.35）：所有 cell 4 边，跟随 high 起伏，构成 WC3 经典 unit cell 网格
  - **黄色粗线**（`Handles.DrawAAPolyLine` 屏幕 3px AA）：每 `ChunkSize = 4` cell 一条 chunk 边界，远近一致宽度
  - **顶点 type 色小球**（半径 0.03）：mask > 0 的格点画一颗小球，颜色由 `MaskGizmoColor(mask)` 取最高置位 bit 对应 layer 中央色（泥/草/岩/雪/紫）；mask = 0 不画，避免视野挤

参考 WC3 编辑器的 grid + tile 标识风格。

### TerrainBuilder ctor：硬约束 width == length 且 2^n

```csharp
if (width != length) throw new ArgumentException(...);
if (width <= 0 || (width & (width - 1)) != 0) throw new ArgumentException(...);
```

约束动机：等宽方阵 + 2^n 对齐让未来 chunk 划分（每 ChunkSize=4 一格已在 Gizmos 见原型）/ 分级 LOD / quadtree 寻址全部零特例处理。fail-fast 在 ctor，纯 C# 端独立可测。

### TerrainState：按下/抬起按键匹配交互

`OnUpdate()` 不监听持续按下；`Input.GetMouseButtonDown(btn)` 记录 `_pressButton`，`Input.GetMouseButtonUp(btn)` 时若与 `_pressButton` 一致则触发一次刷绘（不限按住时长）。这保证「按住不重复触发」的同时，兼容 trackpad 双指点击 / 右键自然按法等略长按住场景。`OnExit()` 重置 `_pressButton = -1`，防止模式切换后状态残留。

历史决策：原版本用 `ClickMaxDuration = 0.3s` 限制为"短按"，但 trackpad / 右键自然按法常 > 0.3s 被误判长按而忽略，导致右键不响应。已废除时长限制（详见 changelog [2026-05-11 11:00:00]）。

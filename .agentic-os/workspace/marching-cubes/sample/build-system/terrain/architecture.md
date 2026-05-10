# Terrain Architecture

## 定位

MQ 地形层（叶子模块）。在 `runtime/marching-squares` 算法基础上，构建完整的地形编辑 MonoBehaviour 层：格点高度管理、tile prefab 生命周期、碰撞 mesh 更新、刷绘交互状态。

## 内部结构

```
Sample/BuildSystem/Terrain/
├── Terrain.cs          ← MonoBehaviour 薄壳（类比 McStructure）：组件层，桥接 Builder 与 Unity
├── TerrainBuilder.cs   ← 纯 C# 核心：Point[,] + Tile[,] + colliderMesh 管理
├── TileCaseConfig.cs   ← ScriptableObject：35 槽 prefab 配置（地形 19 + 悬崖 16，含 editor 持久化字段）
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

**MPB 接口**：`_TileMsIdx (Vector4) + _TileMsIdx4 (Float)` per-tile uniform；保留 `_NormalMap` (Texture2D) 用于法线扰动；删除旧 `_TerrainPointTex` / `_TerrainPointTexST`。

### TileCaseConfig：35 槽双轨 + editor 持久化

- 地形 19 槽（case 0~18）：`GetPrefab/SetPrefab`，每 case 独立 prefab，无 D4 归约（mesh+UV 紧耦合）
- 悬崖 16 槽（case 0~15，0 留空）：`GetCliffPrefab/SetCliffPrefab`，由 art-mq-mesh 工具经 D4 旋转从 5 规范 FBX 派生
- `OnEnable` + 内部 `EnsureArray` 保证序列化数组始终为 19 / 16 长度，迁移期不丢已有引用
- `#if UNITY_EDITOR` 字段（`editorFbxFolder` / `editorPrefabFolder` / `editorTerrainMat` / `editorCliffMat`）随 .asset 持久化，由 art-mq-mesh 编辑器读写；`[HideInInspector]` 不出现在默认 Inspector，避免与自定义 Editor UI 重复

### 法线贴图：Blender 烘焙 + 运行时 MPB 注入

整个方案三段，跨 blender / art-mq-mesh / terrain 三模块协作：

```
Blender Add-on（mc_building_artmesh）
  └── MQ_OT_ExportAllCases.execute
        ├── 程序生成 19 个 mq_case_N 的 mesh + UV
        ├── 每个 mesh：bake_normal_map(noise_field, resolution=128) → mq_case_N_normal.png
        └── 同 operator 同目录写出 .fbx + .png（命名严格对齐）

Unity Editor: art-mq-mesh.MQMeshConfigEditor
  └── "Refresh Normal Maps" 按钮
        ├── 扫 cfg.editorFbxFolder/mq_case_(\d+)_normal.png
        ├── 自动改 importer.textureType = NormalMap（若非）
        └── cfg.SetNormalMap(N, ref)

Unity Runtime: TerrainBuilder.ApplyTileMPB
  ├── mpb.SetTexture("_TerrainPointTex", pointTex)（既有）
  ├── mpb.SetVector("_TerrainPointTexST", ...)（既有）
  └── ★ 追加：var nm = _config.GetNormalMap(caseIndex);
              if (nm != null) mpb.SetTexture("_NormalMap", nm);

SplatmapTerrain.shader（既有 4 角采样 + Lambert）
  ├── appdata 加 tangent: TANGENT
  ├── v2f 加 worldTangent / worldBitangent（vert 阶段算出）
  ├── 既有 _NormalMap 声明（默认 "bump"）
  └── frag：
        sampler2D _NormalMap;
        float3 nTangent = UnpackNormal(tex2D(_NormalMap, lUV));
        float3 nWorld   = normalize(nTangent.x * i.worldTangent
                                  + nTangent.y * i.worldBitangent
                                  + nTangent.z * i.worldNormal);
        ndl = max(0.2, dot(nWorld, _WorldSpaceLightPos0.xyz));
```

边界连续性靠 Blender 端 tileable noise（`f(0,y,z) ≡ f(1,y,z)` 三轴）+ MQ 地形 mesh 切线基天然稳定（tangent≈+X, bitangent≈+Z, normal≈+Y）共同保证。Blender ↔ Unity 切线基对账：Blender `mesh.calc_tangents()` ↔ Unity `ArtMeshFbxPostprocessor.importTangents = CalculateMikk`，两端 MikkTSpace 算法一致。

### TerrainState：click-only 交互

`OnUpdate()` 不监听持续按下；`OnMouseDown` 记录 `_pressTime` 和 `_pressButton`，`OnMouseUp` 判断持续时间：短按（< `ClickMaxDuration = 0.3f` 秒）才触发刷绘，长按不触发。`OnExit()` 重置 press 状态，防止模式切换后状态残留。

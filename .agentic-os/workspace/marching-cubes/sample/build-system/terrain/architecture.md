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
2. `PaintTerrainType(brush, type)` → **Add 语义**：`Point.terrainMask |= (1 << type)` → 调 `SetPointTexPixel(px,pz)` 写入 pointTex R 通道（不重建 prefab）；`EraseTerrainType(brush, type)` 反向擦除：`mask &= ~(1 << type)`；`ClearTerrainMask(brush)` 一键清空：`mask = 0` → 该格点 fallback 到 `_BaseTex`
3. `colliderMesh`：预分配 `renderWidth * renderDepth * 6` 个顶点（每格 6 顶点不共享），高度更新只修改 Y 分量

### 地形纹理方案：per-vertex pointTex bitmask + Shader 4 角加权混合

**编码协议**（核心）：

- `Point.terrainMask` 是 1 byte bitmask，**bit i = 1 表示 type i 存在**，最多支持 8 种 type 同时叠加
- `pointTex` 尺寸为 `(length+1) × (width+1)`，每像素对应一个格点，**R 通道直接存 mask byte**（Color32 整数写入，shader 端 `round(tex.r * 255.0)` 反解，不再做 `/(TerrainTypeCount-1)` 浮点归一化）
- 取消旧编码的 magic number 耦合：`TerrainTypeCount` 与编解码常数完全解耦，未来扩展 type 不需改 shader
- 编码契约**集中在一处**：bit i ↔ type i 的映射在 architecture.md / shader 注释 / TerrainBuilder.cs 三处保持完全一致

**4 角解码 + 加权混合**（shader 端）：

```
对每个 fragment:
  1. 4 角采样：mask_c = round(tex2D(pointTex, uv_c).r * 255.0)，c ∈ {BL,BR,TR,TL}
  2. 每角解码：
     col_c = float3(0,0,0)
     totalW_c = 0
     for i in 0..7:
       if (mask_c >> i) & 1:
         w = i + 1                              // 线性权重：bit 位越高权重越大
         col_c += w * sample_overlay(i, lUV)
         totalW_c += w
     if totalW_c == 0: col_c = sample_base(baseUV)  // 空像素 fallback _BaseTex
     else:             col_c /= totalW_c
  3. 4 角双线性插值：col = bilinear(col_BL, col_BR, col_TR, col_TL, fracUV)
```

**视觉效果**：
- 同点多 type → 自动加权混合，**bit 位越高（type ID 越大）权重越大**
- 4 角共享 quad → 跨格点的 type 边界 双线性自然过渡，无 atlas 子格切割

**性能注意**：每 fragment 最多 4 角 × 8 type = 32 次 overlay 采样（实际场景大部分像素 mask 稀疏，分支跳过空 bit），如需进一步优化可限制 max 4 个最高 bit，待性能 profile 后再决定。

**MPB 接口不变**：`ApplyTileMPB` 仍写入 `_TerrainPointTex` + `_TerrainPointTexST(xy=BL UV, zw=步长)`，步长显式传入（不依赖 MPB 的 `_TexelSize`，原因见 module.json constraints）。

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

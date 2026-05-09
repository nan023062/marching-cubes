# Terrain Architecture

## 定位

MQ 地形层（叶子模块）。在 `runtime/marching-squares` 算法基础上，构建完整的地形编辑 MonoBehaviour 层：格点高度管理、tile prefab 生命周期、碰撞 mesh 更新、刷绘交互状态。

## 内部结构

```
Sample/BuildSystem/Terrain/
├── Terrain.cs          ← MonoBehaviour 薄壳（类比 McStructure）：组件层，桥接 Builder 与 Unity
├── TerrainBuilder.cs   ← 纯 C# 核心：Point[,] + Tile[,] + colliderMesh 管理
├── TileCaseConfig.cs   ← ScriptableObject：16 槽 prefab 配置（直接映射，无 D4 归约）
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

1. `BrushMapHigh(brush, delta)` → 更新笔刷覆盖范围内 `Point.high` → 标记 dirty tiles → `RefreshDirtyTiles()` 重建 tile prefab 实例 → 更新 `colliderMesh`
2. `PaintTerrainType(brush, type)` → 更新格点 terrainType → 调 `SetPointTexPixel(px,pz)` 写入 pointTex R 通道（不重建 prefab）
3. `colliderMesh`：预分配 `renderWidth * renderDepth * 6` 个顶点（每格 6 顶点不共享），高度更新只修改 Y 分量

### 地形纹理方案：per-vertex pointTex + Shader 4 次采样

- `pointTex` 尺寸为 `(length+1) × (width+1)`，每像素对应一个格点，R 通道存 `terrainType / 4`
- `ApplyTileMPB` 只向 MaterialPropertyBlock 写入 BL 角点 UV（`_TerrainPointTexST.xy`），步长由 Shader 用 `_TerrainPointTex_TexelSize.xy` 自取
- `SplatmapTerrain.shader` frag 分 4 次独立采样 BL/BR/TR/TL 各角格点 R 通道，不依赖 MPB 的 ST.zw

### TerrainState：click-only 交互

`OnUpdate()` 不监听持续按下；`OnMouseDown` 记录 `_pressTime` 和 `_pressButton`，`OnMouseUp` 判断持续时间：短按（< `ClickMaxDuration = 0.3f` 秒）才触发刷绘，长按不触发。`OnExit()` 重置 press 状态，防止模式切换后状态残留。

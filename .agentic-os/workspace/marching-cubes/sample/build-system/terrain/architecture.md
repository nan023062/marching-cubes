# Terrain Architecture

## 定位

MQ 地形层（叶子模块）。在 `runtime/marching-squares` 算法基础上，构建完整的地形编辑 MonoBehaviour 层：格点高度管理、tile prefab 生命周期、碰撞 mesh 更新、刷绘交互。

## 内部结构

```
Sample/BuildSystem/Terrain/
├── TerrainController.cs  ← BuildController 子类：地形编辑交互 + tile 生命周期 + collider mesh 管理
├── TerrainBuilder.cs     ← 纯 C# 核心：Point[,] + Tile[,] + colliderMesh 管理
├── TileCaseConfig.cs     ← ScriptableObject：81 槽 prefab 配置（base-3 编码）
├── Cursor.cs             ← Cursor 基类：Size + SetMaterial
├── Brush.cs              ← 兼容层：class Brush : Cursor {}（TerrainBuilder 签名用）
└── PlaneCursor.cs        ← 地形 hover：Awake 生成 +Y 法线平面 mesh
```

## 分层架构

```
TerrainController（BuildController 子类）
    ↓ override OnPointerMove / OnPointerClick
BuildController.Update（输入主循环）
    ↓ 调用
TerrainBuilder（纯 C# 核心）
    ↓ 依赖
MqTable + TilePoint（runtime/marching-squares）
```

## 关键实现

### TerrainController：输入响应

`OnPointerMove(hit, ray, onMesh)`：
- onMesh=true：pos = hit.point
- onMesh=false：fallback 投影到 cursor 当前高度平面（保证 cursor 始终可见）
- 将 world pos 转换为格点 (px, pz)，更新 _cursor 位置/缩放/旋转

`OnPointerClick(hit, left)`：
- left=true + colorBrush：PaintTerrainType
- left=false + colorBrush：EraseTerrainType
- 非 colorBrush：BrushMapHigh(left ? +1 : -1)

### TerrainBuilder：格点 → tile → colliderMesh

1. `BrushMapHigh(brush, delta)` → 更新笔刷覆盖范围内 `Point.high` → RefreshAffectedTiles() 重建 tile prefab → 更新 colliderMesh
2. `PaintTerrainType(brush, type)` → **Add 语义**：`mask |= (1 << type)` → RefreshAffectedTilesMPB（不重建 prefab，只重推 MPB）
3. colliderMesh：`renderWidth * renderDepth * 6` 顶点（每格 6 顶点不共享），高度更新只改 Y 分量

### InitColliderMesh：re-bake 顺序

```
RebuildColliderMesh()        // 填顶点 → _meshCollider.sharedMesh = _colliderMesh（此时三角形仍空）
_colliderMesh.triangles = …  // 设三角形
_meshCollider.sharedMesh = _colliderMesh   // ← 必须再次赋值触发 re-bake，否则 Collider.Raycast 永远 miss
```

### Cursor 层次：hover 视觉

```
Cursor（基类：Size + SetMaterial）
└── Brush（兼容层：Brush : Cursor，TerrainBuilder 参数类型）
    └── PlaneCursor（hover：Awake 生成 +Y 法线平面 mesh）
```

TerrainController._cursor（Inspector 绑 PlaneCursor 实例）；OnPointerMove 更新位置，BuildController.SetActive 管理显隐。

### 地形纹理方案：WC3 风格 per-tile MPB uniform + 5 layer atlas

（内容与旧 architecture.md § 地形纹理方案 完全一致，保留不变）

**渲染管线**（shader 端）：对每个 fragment 直接读 5 个 per-tile uniform `_TileMsIdx` / `_TileMsIdx4`（0 误差），5 layer 高编号覆盖低编号顺序 lerp by alpha。

**渲染管线**（C# 端）：`ApplyTileMPB(tile,cx,cz)` 读 4 角 mask → `TileTable.GetAtlasCase` → 推 MPB；mask 变化触发 `RefreshAffectedTilesMPB`（每格点扩散 4 邻 cell，去重重推）。

### TileCaseConfig：81 槽 base-3 编码

- 81 槽（case_idx ∈ [0,80]）：`case_idx = r0 + r1*3 + r2*9 + r3*27`，`r_i = h_i - min(h0..h3) ∈ {0,1,2}`
- 65 真实几何 + 16 死槽（min(r) > 0 的不可达组合）
- 不做 D4 归约（mesh+UV 紧耦合）

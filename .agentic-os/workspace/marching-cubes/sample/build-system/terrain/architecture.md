# Terrain Architecture

## 定位

MQ 地形层（叶子模块）。在 `runtime/marching-squares` 算法基础上，构建完整的地形编辑 MonoBehaviour 层：格点高度管理、tile prefab 生命周期、碰撞 mesh 更新、刷绘交互状态。

## 内部结构

```
Sample/BuildSystem/Terrain/
├── MqTerrain.cs        ← MonoBehaviour 薄壳（类比 McStructure）：组件层，桥接 Builder 与 Unity
├── MqTerrainBuilder.cs ← 纯 C# 核心：Point[,] + Tile[,] + colliderMesh 管理
├── MqMeshConfig.cs     ← ScriptableObject：16 槽 prefab 配置（直接映射，无 D4 归约）
├── Brush.cs            ← 笔刷 MonoBehaviour：Size + colorBrush 标志
└── TerrainState.cs     ← IBuildState 实现：刷绘 GUI + 鼠标/Raycast 交互
```

## 分层架构

```
TerrainState（IBuildState）
    ↓ 调用
MqTerrain（MonoBehaviour 薄壳）
    ↓ 委托
MqTerrainBuilder（纯 C# 核心）
    ↓ 依赖
MqTable + TilePoint（runtime/marching-squares）
```

## 关键实现

### MqTerrainBuilder：格点 → tile → colliderMesh

1. `BrushMapHigh(brush, delta)` → 更新笔刷覆盖范围内 `Point.high` → 标记 dirty tiles → `RefreshDirtyTiles()` 重建 tile prefab 实例 → 更新 `colliderMesh`
2. `PaintTerrainType(brush, type)` → 更新 terrainType → 通过 `MaterialPropertyBlock` 注入着色器参数（不重建 prefab）
3. `colliderMesh`：预分配 `renderWidth * renderDepth * 6` 个顶点（每格 6 顶点不共享），高度更新只修改 Y 分量

### TerrainState：鼠标交互去重

`OnUpdate()` 中用 `_mouseWasDown` + `_lastMousePos` 双重检查：release 后首次按下（firstPress）或屏幕坐标有变化（mouseMoved）才触发刷绘，避免每帧重复触发。

# RealtimeWorld Architecture

## 定位

3D 实时雕刻演示（叶子模块）。展示 CubeMeshSmooth 在实时编辑场景中的应用：全局 iso 场管理、球体笔刷雕刻、对象池复用 chunk、接缝消除。

## 内部结构

```
Sample/ReattimeWorld/
├── RealtimeWorld.cs       ← 世界管理器（MonoBehaviour，Singleton）
├── RealtimeWorldChunk.cs  ← 单个体素块（MonoBehaviour + IMarchingCubeReceiver）
└── RealtimeTool.cs        ← 交互工具（MonoBehaviour）：鼠标左键雕刻
```

## Chunk 管理策略（对象池）

与 real-time-terrain 不同，RealtimeWorld 的 chunk 不 Destroy，而是 SetActive(false)：
- `used=false, obj!=null` → `obj.SetActive(false)`
- `used=true, obj=null` → Instantiate + Initialize
- `used=true, obj!=null` → `obj.SetActive(true)` + RebuildTerrain

## 接缝消除：CellOffset

每个 chunk 的 CubeMeshSmooth 大小为 `ChunkMaxCellNum = ChunkCellNum + 2*CellOffset = 34`（比格子数多 1 圈），采样邻居 chunk 的 iso 值，保证相邻 chunk 边界顶点 iso 值一致，消除接缝。

## SetBlock 流程

```
RealtimeTool.Update: 鼠标左键 → RealtimeWorld.SetBlock(position, radius)
  → 1. 计算球体 AABB → 遍历范围内所有格点 → min(old, new) iso 写入 _isoValues
  → 2. 确定受影响 chunk 范围 → 对每个 chunk obj 调用 chunk.SetBlock + 标记 dirty
  → TickDirtyChunks: 每帧最多处理一个 dirty → RebuildTerrain
```

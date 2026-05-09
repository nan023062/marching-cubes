# Real-timeTerrain Architecture

## 定位

2D 无限地形演示（叶子模块）。展示 CubeMeshSmooth 在大型流式地形场景中的应用：chunk 按需加载、Perlin noise 高度场生成、碰撞 mesh 同步。

## 内部结构

```
Sample/Real-timeTerrain/
├── RealtimeTerrain.cs       ← 地形管理器（MonoBehaviour）：chunk 视域 + 生命周期
└── RealtimeTerrainChunk.cs  ← 单个地形块（MonoBehaviour + IMarchingCubeReceiver）
```

## Chunk 管理流程

```
Update()
  → Spot 位置 → 当前 chunkX/Z
  → ViewPort 变化 → UpdateViewChunks(newViewPort)
      → 标记 used/unused → _dirtyChunks.AddLast
  → TickDirtyChunks()（每帧最多处理一个 dirty chunk）
      → used=false → Destroy(chunk)
      → used=true, obj=null → Instantiate + Initialize + RebuildTerrain
```

## 高度场定义

```
perlinNoiseScale = 1/7（默认）
height(i,k) = PerlinNoise(globalI * scale, globalK * scale) * ChunkHeight
iso(i,j,k) = height(i,k) + j
IsoPass: iso < ChunkHeight + 0.1f  →  低于高度曲线的点为实心
```

iso 定义使 iso 值从底部（0）到顶部（ChunkHeight）单调递增，与 Perlin 高度曲面相交形成平滑地形。

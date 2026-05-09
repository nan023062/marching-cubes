# ArtMqTexture Architecture

## 定位

MQ 地形贴图生成器（叶子模块）。纯程序化生成，无外部依赖。通过菜单命令一键生成地形所需的所有贴图和材质资产。

## 内部结构

```
ArtMqTexture/
└── MSQTextureGenerator.cs  ← 静态工具类，含菜单命令和所有生成逻辑
```

## 生成产物

| 产物 | 类型 | 描述 |
|------|------|------|
| `terrain_base.asset` | `Texture2DArray[5]` | 5 种地形各自的无缝基础色（64×64） |
| `terrain_overlay.asset` | `Texture2DArray[5]` | 5 种地形各自的 16-case atlas（256×256，4×4 grid） |
| `SplatmapTerrain.mat` | Material | 绑定上述两张贴图数组的 Splatmap 材质 |
| `cliff_wall.asset` | Texture2D | 岩壁纹理（可平铺 64×64 灰度） |
| `cliff_level1.asset` | Mesh | 1 格高悬崖 mesh（Perlin 凹凸位移） |
| `cliff_level2.asset` | Mesh | 2 格高悬崖 mesh |
| `CliffWall.mat` | Material | 绑定 cliff_wall.asset 的悬崖材质 |

## 贴图生成算法

- **base**：TileableNoise 生成无缝底色，每种地形有独立颜色 palette（fillA/fillB）
- **overlay atlas**：每个 tile 对应 1 个 MQ case，四角高度插值 + 噪声边缘柔化，alpha=0 表示低处（透明）
- **可平铺 Perlin**：将 UV 映射到 cos/sin 圆柱面，消除边界不连续

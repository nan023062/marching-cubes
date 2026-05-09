# ArtMqTexture Contract

Editor 工具类，无运行时公开 API。

## 菜单入口

```
Assets/Create/MarchingSquares/Gen Splatmap Textures
  → MSQTextureGenerator.GenerateSplatmapTextures()
```

## 输出路径

所有产物写入：`Assets/MarchingSquares/Sample/Resources/`

| 文件名 | 类型 |
|--------|------|
| `terrain_base.asset` | Texture2DArray |
| `terrain_overlay.asset` | Texture2DArray |
| `SplatmapTerrain.mat` | Material（Shader: MarchingSquares/SplatmapTerrain）|
| `cliff_wall.asset` | Texture2D |
| `cliff_level1.asset` | Mesh |
| `cliff_level2.asset` | Mesh |
| `CliffWall.mat` | Material（Shader: MarchingSquares/CliffWall）|

## 使用方

- `MqTerrainBuilder` 的材质配置（由 `SplatmapTerrain.mat` 赋值）

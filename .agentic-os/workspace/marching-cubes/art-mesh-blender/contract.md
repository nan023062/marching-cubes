# ArtMeshBlender Contract

## 安装与启动

- 运行 `python build_zip.py` 生成 `mc_artmesh.zip`
- Blender > Preferences > Add-ons > Install > 选择 zip > 启用
- 面板位置：3D Viewport > N-Panel > MC ArtMesh

## 导出文件命名

- 格式：`case_{n}.fbx`
- `n` 为整数 cubeIndex（D4 canonical case），范围 1–254，无前导零
- 示例：`case_3.fbx`、`case_127.fbx`

## FBX 内 Object 命名

Unity 侧 `ImportFromFbxRoot` 通过正则 `case_(\d+)` 匹配子对象名；
导出 Object 名 = `case_{canonical_ci}`，必须与此约定一致。

## 顶点坐标系约定

必须与 CubeTable.Vertices 完全一致：

```
V0:(0,0,1)  V1:(1,0,1)  V2:(1,0,0)  V3:(0,0,0)
V4:(0,1,1)  V5:(1,1,1)  V6:(1,1,0)  V7:(0,1,0)
cube 中心 = (0.5, 0.5, 0.5)
```

Blender 坐标系：X 右、Y 深（→ Unity Z）、Z 上（→ Unity Y）。
Add-on 内部负责坐标系转换（`u2b` / `b2u`），美术直接在参考场景内建模，无需关心轴向。

## 接缝约定

- 建模边界顶点必须落在接缝锚点（**黄球**）位置
- 接缝锚点 = 连接一个激活顶点与一个非激活顶点的棱的中点
- Extract & Export 会自动吸附（`snap_dist` 容差，默认 0.15）
- 违反此约束 → 与相邻 case 拼接时出现裂缝

## 参考场景颜色约定

| 颜色 | 含义 |
|------|------|
| 鲜红球（大） | 激活顶点（实心角） |
| 灰球（小） | 非激活顶点（空心角） |
| 黄球 | 接缝锚点（边界顶点必须落在这里） |
| 绿线框 | cube 边界（建模不得超出） |
| 深蓝面（正面） | 等值面外侧（空气/外部） |
| 黑面（背面） | 等值面内侧（实心/地形） |

## Case Mesh 颜色约定（MC_ArtMesh_Cubes）

| 颜色 | 材质名 | 含义 |
|------|--------|------|
| 深蓝 | `mc_cube_closed` | 封闭面：中平面（某轴坐标=0.5），实体截面，永久闭合 |
| 浅灰 | `mc_cube_open` | 开放面：cube 边界（坐标=0 或 1），与相邻 cube 拼接后自然封闭 |

## FBX 导出技术规格

| 参数 | 值 |
|------|----|
| `axis_forward` | `'Y'` （Blender 原生前向轴，正Y） |
| `axis_up` | `'Z'` （Blender 原生上轴） |
| `apply_scale_options` | `'FBX_SCALE_ALL'` |
| FBX 根节点旋转 | identity（无额外旋转） |
| FBX 坐标系声明 | Z-up |

## Unity 侧必要配套

`Assets/MarchingCubes/Editor/ArtMesh/ArtMeshFbxPostprocessor.cs`  
对 `fbx_case/` 下所有 FBX 自动设置 `bakeAxisConversion = true`。

- **必须存在**：否则 Unity 导入结果 transform 为 X=-90，DoBuild 叠加 D4 旋转会出错
- **效果**：Unity 把 Z-up→Y-up（X=-90）烧进顶点，根节点还原 identity
- **触发**：首次导入或 Reimport 时自动生效

## 使用方

| 使用方 | 使用方式 |
|--------|---------|
| 美术 | 安装 mc_artmesh.zip，N-Panel 三步操作（Setup / 建模 / Extract） |
| Unity | `CubeArtMeshSplitter.ImportFromFbxRoot`（按 Object 名匹配 cubeIndex） |

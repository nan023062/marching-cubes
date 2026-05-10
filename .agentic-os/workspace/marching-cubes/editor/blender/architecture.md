# Blender Architecture

## 定位

Blender Add-on（叶子模块），独立工具链。提供 DCC 侧建模工作流：搭建参考场景、程序化生成测试 mesh 对比、覆盖率检查、批量 FBX 导出，**以及 MQ 19 case 法线贴图的同步烘焙**。产物供 Unity Editor 工具（art-mc-mesh / art-mq-mesh）消费。

## 内部结构

```
Editor/Blender/
├── build_zip.py                    ← 打包脚本，生成 mc_building_artmesh.zip
├── mc_building_artmesh.zip         ← 打包产物（安装到 Blender 的 Add-on）
└── mc_building_artmesh/
    ├── __init__.py                 ← 插件主体（Operator + Panel + MQProperties.noise_* 字段 + ExportAllCases 内嵌 bake 段）
    ├── cube_table.py               ← CubeTable.cs Python 移植
    ├── mc_mesh.py                  ← MC case mesh 程序化生成算法
    ├── mq_mesh.py                  ← MQ tile mesh 程序化生成算法
    └── noise.py                    ← tileable 3D fbm 噪声 + UV→3D 反查 + 切线空间法线编码
```

## 核心工作流

1. **Setup Reference Scene**：在 Blender 场景中布置 53 canonical case 的参考几何和程序化测试 mesh，供艺术家对照建模
2. **Check Coverage**：扫描场景中已建模的 case，报告覆盖率（已完成/缺失）
3. **Extract & Export FBX + Bake Normal Maps**：从场景提取各 case 的网格，按 `case_N.fbx` 命名批量导出；MQ 19 case 同步烘焙 `case_N_normal.png` 到同目录

## 与 Unity 侧对接

- **导出轴向**：`axis_forward='Y', axis_up='Z'`（Blender 原生）
- **Unity 侧轴向匹配**：`ArtMeshFbxPostprocessor.bakeAxisConversion=true` 将 Z-up 烘进顶点，导入后 GameObject transform 为 identity
- **顶点约定**：与 `CubeTable.Vertices` 完全一致，`cube_table.py` 是 Python 版本的权威实现
- **切线基对账**：Blender `mesh.calc_tangents()` ↔ Unity `ArtMeshFbxPostprocessor.importTangents = CalculateMikk`，两端 MikkTSpace 算法一致
- **法线贴图命名约定**：`mq_case_N.fbx` 同目录下的 `mq_case_N_normal.png`，N ∈ [0, 18]；Unity 端 art-mq-mesh 的 Refresh 按钮按此命名自动扫描映射到 `TileCaseConfig.editorNormalMaps[N]`

## MQ 法线贴图烘焙（新增）

### 数据流

```
MQ_OT_ExportAllCases.execute(self, context)
  ├── 实例化 noise.TileableNoiseField(props.noise_seed, octaves, amplitude, frequency)
  └── 对每个 ci ∈ [0, 19)：
      ├── mq_mesh.build_case_mesh(ci) → bm（BMesh，含 UV）
      ├── 写出 mq_case_{ci}.fbx（既有逻辑）
      └── noise.bake_normal_map(bm, noise_field, resolution=128)
            ↓
            Image (resolution × resolution × RGBA)
            ↓
          image.save_render(f"{out_dir}/mq_case_{ci}_normal.png")

Unity 端
  └── art-mq-mesh.MQMeshConfigEditor "Refresh Normal Maps"
        ↓
      扫 mq_export/ 目录下 mq_case_N_normal.png
        ↓
      cfg.editorNormalMaps[N] = AssetDatabase.LoadAssetAtPath<Texture2D>(...)
```

### noise.py 核心算法

**TileableNoiseField**（Python class）：

```
fbm(x, y, z) = Σ_{k=0..octaves-1} amplitude^k × periodic_noise(p × freq × 2^k, period = freq × 2^k)
```

`periodic_noise(p, period)` 用 lattice gradient + smoothstep（quintic 6t⁵-15t⁴+10t³）：
- lattice 顶点：`(ix, iy, iz) = (floor(p.x), floor(p.y), floor(p.z))`
- 周期化哈希：`grad = hash(seed, ix mod period, iy mod period, iz mod period)` —— **这里 mod period 是 tileable 的关键**
- 三线性插值

**所有 octave 周期都是 1 的整数倍**（freq 强制 int，2^k 整数倍），叠加后整体周期仍为 1，f(0,y,z) ≡ f(1,y,z) 三轴均成立。

**bake_normal_map(bm, noise_field, resolution)**：

1. 调 `bm.calc_tangents()` 给每 loop 算 MikkTSpace 切线
2. 创建 numpy array `pixels[resolution, resolution, 4]`，初始化为平面法线 (128, 128, 255, 255)
3. 对每个三角形 face：用 UV 重心坐标光栅化到 pixel 网格
   - 对 UV 三角形覆盖的每个 pixel：算重心 → 反推该 pixel 在 mesh 上的局部 3D 位置 p
   - `δ = noise_field.gradient(p.x, p.y, p.z)` （数值梯度）
   - 切线空间扰动：`tangent_normal = normalize((-δx, -δy, 1))` （Z-up tangent space）
   - 编码：`pixel = ((tangent_normal + 1) * 127.5).clip(0, 255)`
4. `image.pixels = pixels.flatten() / 255.0` → `image.save_render(filepath)`
5. PNG 设 alpha=1（避免下游误判透明）

### 边界连续性证明

设两个相邻 cell A、B 在 X 方向相邻：A 的右面 x=1 与 B 的左面 x=0 共享 vertex。

设共享面上某点在 A 局部坐标为 (1, y, z)，在 B 局部坐标为 (0, y, z)：
- A 烘出贴图在该点采样 `fbm(1, y, z)`
- B 烘出贴图在该点采样 `fbm(0, y, z)`

tileable 不变量保证 `fbm(1, y, z) ≡ fbm(0, y, z)`，所以两边 noise 值相同，切线空间扰动相同。

MQ 地形特殊性：mesh 几乎水平（Y 方向只是高度变化），切线基稳定（tangent ≈ +X，bitangent ≈ +Z，normal ≈ +Y），相邻 cell 共享边上 MikkTSpace 算法给出的切线基**实际一致**（与 MC 不同，MC mesh 拓扑各异导致切线不可证）。

### 设计约束

- **freq 必须为整数**（noise_frequency PropertyGroup 字段类型为 IntProperty，min=1）
- **每 octave 的 hash period 是 freq × 2^k**（不是只 freq）—— 这是 tileable 的真正保证
- **bake 与 export 同 operator**（不分离按钮）：保证 mesh + normal map 一一对应，零漂移可能
- **仅 MQ 19 case** 在烘焙范围内：MC 53 case 因 D4 flip 切线手性 + MikkTSpace 不一致问题暂不接入；MQ cliff 因侧墙切线方向与地形不同，单独 noise 参数另行设计

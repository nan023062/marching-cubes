# ArtMcMesh Architecture

## 定位

MC case prefab 编辑器工具集（叶子模块）。提供两条 prefab 构建路径 + FBX 导入后处理 + 法线贴图烘焙器（让 53 canonical case 在统一质感下保持相邻 cell 边界法线连续）。

## 内部结构

```
ArtMcMesh/
├── ArtMeshCaseConfigEditor.cs    ← [CustomEditor(D4FbxCaseConfig)] D4 prefab 构建 + 法线烘焙入口
├── IosMeshCaseConfigEditor.cs    ← [CustomEditor(IosMeshCaseConfig)] IOS 直映射构建
├── ArtMeshFbxPostprocessor.cs    ← FBX 导入后处理（bakeAxisConversion + ImportTangents）
├── TileableNoiseField.cs         ← 纯算法：tileable 3D fbm 噪声场
└── NormalMapBaker.cs             ← 编辑器烘焙器：53 mesh × noise → 53 张法线贴图
```

## 两条 prefab 构建路径

| 路径 | 输入 | 输出 | 适用场景 |
|------|------|------|---------|
| D4 FBX 归约 | 53 canonical FBX（case_N.fbx）| 255 prefab（p_case_1~254）| 艺术家手工建模，D4 对称复用 |
| IOS 直接映射 | 256 mesh asset（cm_N.asset）| 256 prefab（cm_0~255）| 程序生成 mesh，1:1 映射 |

## 法线贴图烘焙（新增）

### 数据流

```
ArtMeshCaseConfigEditor.OnInspectorGUI
  ├── noise 参数 UI（seed / octaves / amplitude / frequency / resolution=128）
  └── "Bake Normal Maps" 按钮
        ↓
  NormalMapBaker.BakeAll(D4FbxCaseConfig cfg)
    ├── 实例化 TileableNoiseField(seed, octaves, amplitude, frequency)
    └── 对 ci ∈ [0, 53) 的每个 canonical case：
        ├── 加载对应 case_N.fbx 的 mesh
        ├── 创建 Texture2D(128, 128, RGBA32, mipmap=true)
        ├── 对每个 texel (u, v)：
        │     ├── UV → 三角形重心 → 局部 3D 位置 p ∈ [0,1]³
        │     ├── tileable_fbm(p) → 法线扰动向量 δn
        │     └── 切线空间编码 → RGBA texel
        ├── 写到 normalmaps/case_N_normal.png
        ├── AssetDatabase 设导入参数：textureType=NormalMap, BC5, mipmap=true
        └── cfg.editorNormalMaps[ci] = 加载回 Texture2D 引用

ArtMeshCaseConfigEditor.DoBuild（已有 D4 prefab 构建）
  └── 实例化 D4 prefab 时：
      var canonicalIndex = cfg.GetCanonicalIndex(ci);
      cubedMeshPrefab.normalMap = cfg.editorNormalMaps[canonicalIndex];
```

### TileableNoiseField 算法

3D fbm = Σ amplitude^k × periodic_noise(p × frequency × 2^k)

`periodic_noise(p)` 是周期为 1 的 3D 噪声，通过 lattice gradient + smoothstep 插值实现。所有 octave 都用同一个周期化基函数，叠加后保持 [0,1]³ tileable。

### 边界连续性证明

设两个相邻 cell A、B 在 X 方向相邻：A 的右面 x=1 与 B 的左面 x=0 共享 4 个 vertex。设共享面上某点在 A 局部坐标为 (1, y, z)，在 B 局部坐标为 (0, y, z)。

烘焙时：
- A 的法线贴图在该点采样 `f(1, y, z)`
- B 的法线贴图在该点采样 `f(0, y, z)`

tileable 不变量保证 `f(1, y, z) ≡ f(0, y, z)`，所以两边切线空间扰动相同。又因为共享 vertex 的 mesh tangent 在两 case 中由相同的几何位置决定（实践上仍可能因 tangent 计算偶然小差异，需观察），切线空间→世界空间转换后法线一致。

### D4 衍生 prefab 复用机制

- 53 张法线贴图属 canonical case 0~52，存于 `Sample/Resources/mc/normalmaps/case_N_normal.png`
- D4 prefab 构建器在生成 ci ∈ [1, 254] 的 prefab 时：`cubedMeshPrefab.normalMap = editorNormalMaps[GetCanonicalIndex(ci)]`
- shader 端：Standard shader 切线空间法线计算自动对 mesh tangent 应用 prefab Transform，D4 旋转后 normal 已在世界空间正确

## 设计约束

- **D4 变换方向**：`EnsureSymmetry()` 存储 `ci→canonical` 方向的旋转，构建 prefab 时需反向：non-flip 取 `Inverse(d4)`，flip 直接用 `d4`（变换自逆）
- **FBX 后处理统一**：`ArtMeshFbxPostprocessor` 覆盖整个 `Sample/Resources` 目录，MC 和 MQ 资产共用，Blender 导出轴向（Y-forward, Z-up）自动烘进顶点，Unity 侧无旋转；为支持法线贴图烘焙，强制 ImportTangents=true
- **tileable 不变量是法线贴图边界连续的唯一保证**：任何破坏 `f(0,y,z) ≡ f(1,y,z)` 的修改（如换非周期 noise、改 octave 频率为非整数、引入 case-id 当种子）都会导致相邻 cell 边界法线撕裂

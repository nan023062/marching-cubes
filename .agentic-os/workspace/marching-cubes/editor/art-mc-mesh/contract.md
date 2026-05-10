# ArtMcMesh Contract

Editor 工具类，无运行时公开 API。

## ArtMeshCaseConfigEditor（D4FbxCaseConfig 的 Inspector）

### 操作流程

1. 指定 FBX 文件夹（含 `case_0.fbx`~`case_52.fbx`，53 个 canonical）
2. 指定 Prefab 输出文件夹
3. 指定材质（共用 m_marching.mat）
4. 配置 noise 参数（seed / octaves=3 / amplitude=1.0 / frequency=4.0 / resolution=128）
5. 点击 **"Bake Normal Maps"** → 生成 53 张 `Sample/Resources/mc/normalmaps/case_N_normal.png` + 写入 `cfg.editorNormalMaps[]`
6. 点击 **"Build All 255 Prefabs"** → 53 canonical FBX × D4 → 255 prefab，每个 prefab 的 `CubedMeshPrefab.normalMap` 由 canonical index 查 `cfg.editorNormalMaps[]` 写入

### 配置持久化

步骤 1~4 设置的字段全部写入 `D4FbxCaseConfig.editor*` 字段（`#if UNITY_EDITOR` + `[HideInInspector]`），随 `.asset` 序列化。Inspector 重建/域重载/Unity 重启不丢，团队通过 git 共享。

## IosMeshCaseConfigEditor（IosMeshCaseConfig 的 Inspector）

256 mesh asset → 256 prefab 直接映射，无 D4 归约。不参与法线贴图烘焙（IOS 路径暂不接入 normalmap）。

## TileableNoiseField（编辑器内部纯算法，非 Inspector 暴露）

```csharp
internal sealed class TileableNoiseField
{
    public TileableNoiseField(int seed, int octaves, float amplitude, float frequency);

    /// <summary>
    /// 在 [0,1]³ 周期 tileable 的 3D fbm 噪声场。
    /// 不变量：f(0,y,z) ≡ f(1,y,z)，三轴均成立。
    /// </summary>
    public float Sample(float x, float y, float z);

    /// <summary>
    /// 数值梯度（用于切线空间法线扰动）：
    /// (dx, dy, dz) = (Sample(x+ε,...) - Sample(x-ε,...)) / 2ε
    /// </summary>
    public Vector3 SampleGradient(float x, float y, float z, float epsilon = 1e-3f);
}
```

## NormalMapBaker（编辑器内部烘焙器，非 Inspector 暴露）

```csharp
internal static class NormalMapBaker
{
    /// <summary>
    /// 烘焙 53 张 canonical case 法线贴图，输出到 outputFolder + 写入 cfg.editorNormalMaps。
    /// 已包含 .png 写盘 + AssetDatabase 重导入设为 NormalMap 类型 + BC5 压缩。
    /// </summary>
    public static void BakeAll(
        D4FbxCaseConfig cfg,
        string fbxFolder,
        string outputFolder,
        int resolution,
        TileableNoiseField noise);
}
```

## 使用方

- `D4FbxCaseConfig` ScriptableObject 持有者（通过 ArtMeshCaseConfigEditor Inspector 触发烘焙 + 构建）

## 依赖

- `sample/build-system/structure.D4FbxCaseConfig`（写入目标 + editor 字段宿主）
- `runtime/marching-cubes.CubedMeshPrefab`（写入 normalMap 字段）
- `runtime/marching-cubes.CubeTable`（D4 对称数据 + 顶点表）

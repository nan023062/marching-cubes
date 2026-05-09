# BuildSystem Contract

## BuildingManager

```csharp
namespace MarchingCubes.Sample
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; }
    public BuildMode CurrentMode { get; }
    public void SwitchTo(BuildMode mode);
}
public enum BuildMode { Terrain = 0, Build = 1 }
```

## IBuildState

```csharp
namespace MarchingCubes.Sample
public interface IBuildState
{
    void OnEnter();
    void OnExit();
    void OnUpdate();
    void OnGUI();
}
```

## BuildingConst

```csharp
namespace MarchingCubes.Sample
public static class BuildingConst
{
    public const uint Unit = 4;                // MQ/MC 格子对齐基准
    public const int TerrainMaxHeightDiff = 3; // 地形最大高度差（单位格）
}
```

## 子模块契约

- [terrain/contract.md](terrain/contract.md)
- [structure/contract.md](structure/contract.md)

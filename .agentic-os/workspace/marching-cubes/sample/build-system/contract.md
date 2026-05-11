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
    void DrawGUI();   // 原 OnGUI，改名防止 Unity 自动调用；由 BuildingManager.OnGUI 显式调用
}
```

## BuildController

```csharp
namespace MarchingCubes.Sample
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public abstract class BuildController : MonoBehaviour, IBuildState
{
    // Inspector 注入：各 Controller 绑定对应 Cursor 子类实例
    [SerializeField] protected Cursor _cursor;

    // 激活管理：同时控制 _active flag 和 _cursor 显隐
    public void SetActive(bool active);

    // 交互开关：同时控制 MeshRenderer.enabled 和 MeshCollider.enabled
    protected void SetInteraction(bool active);

    // 创建 visualMesh + colliderMesh，绑定共享灰色材质，初始调 SetInteraction(false)
    protected void InitGridMeshes(string visualName, string colliderName);

    // 虚函数（子类 override）
    protected virtual void OnPointerMove (RaycastHit hit, Ray ray, bool onMesh) { }
    protected virtual void OnPointerDown (RaycastHit hit, bool left) { }
    protected virtual void OnPointerDrag (RaycastHit hit, bool left) { }  // Down 帧也调用，时序 Down→Drag→Click→Up
    protected virtual void OnPointerUp   (RaycastHit hit, bool left) { }
    protected virtual void OnPointerClick(RaycastHit hit, bool left) { }  // 抬起且时长 ≤ 0.5s 才触发

    // IBuildState 抽象方法（子类实现）
    public abstract void OnEnter();
    public abstract void OnExit();
    public abstract void OnUpdate();
    public abstract void DrawGUI();
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

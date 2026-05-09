# SmoothCubeMesh Contract

```csharp
namespace MarchingCubes.Sample
public class SmoothSphereSample : MonoBehaviour, IMarchingCubeReceiver
{
    public int x, y, z;   // 格子数量
    public float radius;  // 球体半径（格子单位）
    // IMarchingCubeReceiver：GetIsoLevel / IsoPass / OnRebuildCompleted
}
```

## 使用方

仅供场景演示，无外部使用方。

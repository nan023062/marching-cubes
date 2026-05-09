# RealtimeWorld Contract

```csharp
namespace MarchingCubes.Sample
public class RealtimeWorld : MonoBehaviour
{
    public const int   ChunkCellNum    = 32;
    public const int   CellOffset      = 1;
    public const int   ChunkMaxCellNum = 34;  // ChunkCellNum + 2*CellOffset
    public const float Size            = 0.25f;
    public const int   Offset          = 2;   // ViewSize = 5×5×5
    public static RealtimeWorld Instance { get; }

    public float GetPointIso(int x, int y, int z);
    public void SetBlock(in Vector3 position, float radius);
}

public class RealtimeWorldChunk : MonoBehaviour, IMarchingCubeReceiver
{
    public void Initialize(RealtimeWorld world, int chunkX, int chunkY, int chunkZ, bool closed);
    public bool SetBlock(in Coord min, in Coord max, float radius);  // 返回 dirty
    public void RebuildTerrain();
}

public class RealtimeTool : MonoBehaviour
{
    // [SerializeField, Range(0.001f, 2f)] float _radius
    // Update: 鼠标左键 → RealtimeWorld.Instance.SetBlock(transform.position, _radius)
}
```

## 使用方

仅供场景演示，无外部使用方。

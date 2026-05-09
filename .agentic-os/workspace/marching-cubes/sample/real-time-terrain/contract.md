# Real-timeTerrain Contract

```csharp
namespace MarchingCubes.Sample
public class RealtimeTerrain : MonoBehaviour
{
    public const int ChunkCell   = 32;
    public const int ChunkHeight = 16;
    public const float CellSize  = 0.5f;
    public const int Offset      = 5;    // ViewSize = 11×11
}

public class RealtimeTerrainChunk : MonoBehaviour, IMarchingCubeReceiver
{
    public void Initialize();
    public void RebuildTerrain(int chunkX, int chunkZ);
}
```

## 使用方

仅供场景演示，无外部使用方。

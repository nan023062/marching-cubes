# PolygonDrawer Contract

```csharp
namespace MarchingCubes
public class PolygonTableDrawer : MonoBehaviour
{
    public Mode mode;        // DrawOneCube | Draw256Cube
    public bool V0,V1,V2,V3,V4,V5,V6,V7;  // DrawOneCube 模式下的顶点选择
    public enum Mode { DrawOneCube, Draw256Cube }
}
```

## 使用方

仅供编辑器调试，无外部使用方。

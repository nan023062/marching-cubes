# Thirdc Contract

## CameraController

```csharp
namespace MarchingSquares
public class CameraController : MonoBehaviour
{
    public float wheelMul;          // 滚轮速度倍率（Clamp 0.001~10）
    public float moveSensitivity;   // WASD 平移灵敏度
    public float sensitivityX;      // 水平旋转灵敏度
    public float sensitivityY;      // 垂直旋转灵敏度
    public float minimumY;          // 俯仰最小角（默认 -70）
    public float maximumY;          // 俯仰最大角（默认 80）
}
```

## TPSCamera

```csharp
namespace MarchingCubes.Sample
public class TPSCamera : MonoBehaviour
{
    public Transform mainCamera;
    public float yawSensitivity;
    public float pitchSensitivity;
    public float zoomSensitivity;
    public float Yaw { get; }    // 供 Character 读取当前水平旋转角
    public float Pitch { get; }
}
```

## Character

```csharp
namespace MarchingCubes.Sample
[RequireComponent(typeof(CharacterController))]
public class Character : MonoBehaviour
{
    [SerializeField] private TPSCamera _tpsCamera;
    public float forceSensitive;   // 加速度系数
    public float friction;         // 摩擦系数（速度衰减）
    public float gravity;          // 重力加速度
}
```

## 使用方

- `marching-cubes/sample/build-system`（场景中配置使用）
- `marching-cubes/sample/realtime-world`（场景中配置使用）

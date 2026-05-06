# MineOasis Contract

## 公开接口

### CubedWorld（MonoBehaviour 入口）
```csharp
public sealed class CubedWorld : MonoBehaviour
{
    public Matrix4x4 localToWorldMatrix { get; }
    public Matrix4x4 worldToLocalMatrix { get; }

    public void BuildStart(Vector3 position, Quaternion quaternion);
    public void Input_Place(in Block block);
    public void Input_Cancel();
    public void BuildFinish();
}
```

### ECS 组件（IComponentData）
```csharp
public struct Block : IComponentData
{
    public float3 position;
    public quaternion rotation;
    public float3 size;
}

public struct CompositeBlock : IComponentData
{
    public byte cellX, cellY, cellZ;
    public float3 position;
    public quaternion rotation;
    public byte sizeX, sizeY, sizeZ;
}

public struct SingleBlock : IComponentData { }
```

### ECS Systems
```csharp
[BurstCompile, UpdateInGroup(typeof(MineOasisSystemGroup))]
public partial struct RenderingSystem : ISystem { }

[BurstCompile, UpdateInGroup(typeof(MineOasisSystemGroup))]
public partial struct BlockSystem : ISystem { }
```

### 对象池
```csharp
public class GameObjectPool<T> where T : MonoBehaviour
{
    public GameObjectPool(T prefab, int initialSize);
    public void Dispose();
    // Get/Release（具体签名待确认）
}
```

## asmdef 约束

`MineOasis.asmdef` 必须引用：
- `Unity.Entities`
- `Unity.Burst`
- `Unity.Mathematics`
- `Unity.Collections`（推断，Burst 常用）

## 使用方

- Unity 场景中的 MonoBehaviour 脚本（通过 `CubedWorld` 入口）
- ECS 世界（通过 `World.DefaultGameObjectInjectionWorld`）

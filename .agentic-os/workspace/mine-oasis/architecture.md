# MineOasis Architecture

## 定位

基于 Unity DOTS/ECS（Entities 1.x）的体素建造世界原型。以 MonoBehaviour（CubedWorld）为入口对接 ECS 世界，使用 Burst 编译的 ISystem 实现高性能方块渲染与输入处理。

## 内部结构

```
MineOasis/
├── CubedWorld/                     ← ECS 核心（无 MonoBehaviour）
│   ├── Geometry/
│   │   ├── Block.cs                ← ECS 组件：Block / CompositeBlock / SingleBlock
│   │   ├── Zone.cs                 ← Zone 区域几何
│   │   ├── Cube.cs                 ← Cube 几何定义
│   │   └── Const.cs                ← 常量
│   ├── Rendering/
│   │   ├── RenderingSystem.cs      ← Burst ISystem（挂 MineOasisSystemGroup）
│   │   ├── QuadCollection.cs       ← Quad 面集合
│   │   └── Renderer.cs             ← ECS 渲染组件/标记
│   └── System/
│       └── BlockSystem.cs          ← Burst ISystem
│
├── Control/                        ← MonoBehaviour 层（桥接 ECS）
│   ├── CubedWorld.cs               ← 入口：BuildStart/BuildFinish + 对象池管理
│   ├── GameObjectPool.cs           ← 泛型 MonoBehaviour 对象池
│   ├── Input/
│   │   ├── BuildZone.cs            ← 区域输入 MonoBehaviour
│   │   ├── InputCube.cs            ← Cube 输入 MonoBehaviour
│   │   ├── InputElement.cs         ← 输入基类
│   │   └── InputQuad.cs            ← Quad 输入 MonoBehaviour
│   ├── Mode/
│   │   └── GodMode.cs              ← 上帝模式（自由相机等）
│   └── Testing/
│       ├── Testing.cs
│       └── Result.cs
│
└── Sample/                         ← DOTS 学习示例（与核心业务无关）
    ├── RotateCubeSample/
    │   ├── Authoring/              ← Baker/Authoring MonoBehaviour
    │   ├── Aspects/                ← ECS Aspect
    │   ├── SystemGroup/            ← RotateCubeSystemGroup
    │   └── Systems/                ← 各种旋转/波浪 System
    ├── WaveCubeJobTest.cs
    └── WaveCubeTest.cs
```

## 关键设计约束

**ECS System 规范**：
- 必须实现为 `[BurstCompile] public partial struct XxxSystem : ISystem`
- `OnCreate` 中调用 `state.RequireForUpdate<T>()` 声明依赖
- 挂载于 `MineOasisSystemGroup`，不挂 `SimulationSystemGroup`

**MonoBehaviour ↔ ECS 桥接**：`CubedWorld` 通过 `BuildStart/BuildFinish` 管理对象池生命周期，ECS 世界交互通过 `World.DefaultGameObjectInjectionWorld`（推断）。

**对象池**：`GameObjectPool<T>` 负责 MonoBehaviour 侧的 InputQuad/InputCube/BuildZone 对象复用，避免频繁 Instantiate/Destroy。

**Sample 隔离**：`RotateCubeSample/` 是完整独立的 DOTS 学习用例（Authoring → Baker → System），不与 MineOasis 核心共享组件，禁止核心业务依赖 Sample 中的任何类型。

## Facts（逆向提取）

- `CubedWorld._cells = new int3(32, 32, 32)` — 固定 32³ 格子尺寸（推断为初始值，待业务扩展）
- `Block.position/rotation/size` 均使用 `Unity.Mathematics` 类型（`float3/quaternion`）
- `RenderingSystem.OnCreate` 中 `RequireForUpdate<Renderer>()` 已激活，Cube 依赖暂注释
- `MineOasis.asmdef` 为新增文件（当前 working tree 未提交）

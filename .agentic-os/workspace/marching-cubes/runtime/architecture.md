# Runtime Architecture

## 定位

`Assets/MarchingCubes/Runtime` 下的运行时算法库。包含 3D MC 核心算法、2.5D MQ 算法基础层，以及供 Sample 场景复用的第三人称控制器。

## 子模块清单与关系

```
runtime/
├── marching-cubes/   ← 3D MC 等值面算法（查表 + 平面/平滑 mesh 构建器 + 程序化 case mesh）
├── marching-squares/ ← 2.5D MQ 算法基础层（16-case 查表 + 核心类型 + 程序化 tile mesh）
└── thirdc/           ← 第三人称控制器（飞行相机 + TPS 角色 + TPS 相机）
```

依赖方向：
- `marching-cubes` ↔ `marching-squares`：无直接依赖，完全独立的算法库
- `thirdc`：不依赖 MC/MQ，可独立供任何场景使用

## 诞生背景

算法核心与 Unity 场景解耦是核心设计目标：`CubeMesh`、`CubeMeshSmooth`、`MqTerrainBuilder` 等均为纯 C# 类，无 MonoBehaviour 依赖，可在单元测试中直接实例化。

## 涌现性洞察

- **双算法共享 IMarchingCubeReceiver 模式**：MC 和 MQ 均采用 receiver 接口将算法驱动与业务逻辑解耦，使 Sample 层可以用相同的模式接入两套完全不同的算法
- `thirdc` 虽然与算法无关，放在 Runtime 而非 Sample 是因为它需要在多个 Sample 场景间共享，且自身无场景特有逻辑

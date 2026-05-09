# MarchingCubes Architecture

## 定位

`Assets/MarchingCubes` 下的 3D Marching Cubes 等值面提取算法实现集合。提供从 DCC 工具链到运行时算法、再到多个可运行演示案例的完整垂直切片。

## 子模块清单与关系

```
marching-cubes/
├── editor/      ← 编辑器工具集（Blender DCC + Unity Editor 扩展）
├── runtime/     ← 运行时算法库（MC 核心 + MQ 核心 + 第三人称控制器）
└── sample/      ← 演示案例集（建造系统 + 多种地形/建造演示）
```

依赖方向（单向）：
- `editor` → `runtime`（编辑工具依赖运行时类型生成 prefab/资产）
- `sample` → `runtime`（案例依赖运行时算法驱动场景）
- `editor` ↔ `sample` 无直接依赖

## 诞生背景

为 Unity 项目提供可复用的 Marching Cubes / Marching Squares 算法库。编辑器子模块承载 DCC 侧工作流（Blender 参考建模 + Unity prefab 批量生成）；运行时子模块保持纯 C# 核心，可独立测试；sample 子模块以多个可运行案例验证算法并演示不同应用场景。

## 涌现性洞察

只有从跨子模块视角才能看到的整体属性：
- **完整工作流闭环**：Blender 建模 → FBX 导出 → Unity prefab 批建 → 运行时 MC/MQ 算法消费，三个子模块首尾相接构成完整管线
- **双算法协同**：Runtime 同时提供 MC（3D 离散格点）和 MQ（2.5D 地形）两套 Marching 算法；`sample/build-system` 是唯一同时使用两套算法的案例，验证了二者协同的可行性
- **算法层与应用层的清晰边界**：Runtime 只暴露纯数据接口（`IMarchingCubeReceiver`），Sample 层所有交互逻辑对 Runtime 完全透明

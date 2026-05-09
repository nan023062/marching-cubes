# Thirdc Architecture

## 定位

第三人称场景通用控制器（叶子模块）。与 MC/MQ 算法完全无关，在 Runtime 目录是因为需要跨多个 Sample 场景共享。

## 内部结构

```
Runtime/Thirdc/
├── CameraController.cs  ← 俯视/飞行相机（namespace MarchingSquares，历史原因）
├── Character.cs         ← TPS 角色控制器（namespace MarchingCubes.Sample）
└── TPSCamera.cs         ← TPS 相机（namespace MarchingCubes.Sample）
```

## 两种相机模式

| 组件 | 模式 | 控制方式 |
|------|------|---------|
| CameraController | 俯视/飞行 | WASD 平移 + RMB 旋转 + 滚轮速度 |
| TPSCamera | 第三人称跟随 | LMB 旋转 yaw/pitch + 滚轮缩放距离 |

## Character + TPSCamera 协作

```
Character.Update()
    ↓ 读取 TPSCamera.Yaw
    → 将 WASD 输入旋转到相机朝向
    → 施加加速度（含摩擦 + 重力）
    → CharacterController.Move()

TPSCamera.LateUpdate()
    → 根据 yaw/pitch/distance 更新 mainCamera 位置和朝向
```

## 注意

`CameraController` 的命名空间是 `MarchingSquares`（与 Thirdc 其他类不一致），属历史遗留，不影响功能。

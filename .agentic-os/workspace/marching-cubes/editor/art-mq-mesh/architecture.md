# ArtMqMesh Architecture

## 定位

MQ case prefab 编辑器工具（叶子模块）。为 `MqMeshConfig`（ScriptableObject，16 槽直接映射）提供 Inspector 扩展，从 16 个独立 FBX 批量生成地形 tile prefab。

## 内部结构

```
ArtMqMesh/
├── MQMeshConfigEditor.cs  ← [CustomEditor(MqMeshConfig)] Inspector
└── MQFbxPostprocessor.cs  ← 已废弃（内容合并至 art-mc-mesh/ArtMeshFbxPostprocessor）
```

## 核心逻辑

`MQMeshConfigEditor` 读取指定文件夹下的 `mq_case_N.fbx`（N = 0~14），实例化后保存为 prefab 并写入 `MqMeshConfig` 对应槽位。case 15 特殊处理：复用 case 0 的几何（都是平 quad），直接实例化不做任何旋转/翻转。

## 为何不做 D4 归约

MQ tile 的 mesh 几何与纹理 UV 紧耦合。D4 旋转可以正确复用几何，但 UV 方向随之旋转，导致地形纹理对齐错误。因此 16 个 case 各自独立 FBX，无归约。

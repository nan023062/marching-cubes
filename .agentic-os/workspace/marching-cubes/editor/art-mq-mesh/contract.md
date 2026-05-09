# ArtMqMesh Contract

Editor 工具类，无运行时公开 API。

## Inspector 扩展

目标类型：`MarchingSquares.MqMeshConfig`

操作流程：
1. 指定 FBX 文件夹（含 `mq_case_0.fbx` ~ `mq_case_14.fbx`）
2. 指定 prefab 输出文件夹
3. 指定地形材质（可选）
4. 点击 "Build All 16 Case Prefabs"

产物：`mq_case_0.prefab` ~ `mq_case_15.prefab`，写入 `MqMeshConfig` 对应槽位。

## 使用方

- `MqMeshConfig` ScriptableObject 持有者（通过 Inspector 触发构建）

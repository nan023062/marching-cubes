# Blender Contract

Blender Python Add-on，无 C# 公开 API。

## Add-on 安装

```
Blender Preferences → Add-ons → Install → 选择 mc_building_artmesh.zip
启用后在 N-Panel 找到 "MC ArtMesh" 面板
```

## 面板操作

| 操作 | 功能 |
|------|------|
| Setup Reference Scene | 布置 53 canonical case 参考场景（MC）+ 65 case 参考场景（MQ） |
| Check Coverage | 报告已建模 case 覆盖率 |
| Generate Terrain | 生成测试地形 mesh（圆滑预览） |
| Validate Mesh | 检查当前 mesh 在 [0,1]³ 范围内 |
| Export All FBX | 批量导出 case_<N>.fbx（MC 53 个）+ mq_case_<N>.fbx（MQ 65 个有效 case_idx） |

## 产物格式

- **MC**：`case_<N>.fbx`（N 为 canonical index，0 < N < 255）
- **MQ**：`mq_case_<N>.fbx`（N 为 base-3 编码 case_idx ∈ [0,80] 中的 65 个有效值；死槽不导出）
- 导出轴向：`axis_forward='Y', axis_up='Z'`
- 目标消费方：MC → `art-mc-mesh/D4FbxCaseConfigEditor`；MQ → `art-mq-mesh/MQMeshConfigEditor`

## MQ base-3 编码（与 Unity 端必须严格一致）

```python
# Python (mq_mesh.py)
case_idx = r0 + r1*3 + r2*9 + r3*27   # r_i ∈ {0,1,2}, min(r) == 0
```

```csharp
// C# (TileTable.GetMeshCase)
return r0 + r1*3 + r2*9 + r3*27;
```

两端公式必须**字节级一致**，否则 Unity 索引到的 prefab 与 Blender 导出的 mesh 不对应。

## 使用方

- 美术师（DCC 侧建模参考）
- `art-mc-mesh`（消费 MC FBX 生成 Unity prefab）
- `art-mq-mesh`（消费 MQ FBX 生成 Unity prefab）

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
| Setup Reference Scene | 布置 53 canonical case 参考场景 + 程序化测试 mesh |
| Check Coverage | 报告已建模 case 覆盖率 |
| Extract & Export FBX | 批量导出 case_N.fbx（N = canonical index） |

## 产物格式

- 文件名：`case_<N>.fbx`（N 为 canonical index，0 < N < 255）
- 导出轴向：`axis_forward='Y', axis_up='Z'`
- 目标消费方：`art-mc-mesh/D4FbxCaseConfigEditor`

## 使用方

- 美术师（DCC 侧建模参考）
- `art-mc-mesh`（消费 FBX 生成 Unity prefab）

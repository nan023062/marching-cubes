# Editor Contract

Editor 子模块不暴露运行时 API，其产物是资产文件（prefab / mesh / texture / material）和编辑器工具入口。

## 菜单入口

| 菜单路径 | 所在子模块 | 功能 |
|---------|----------|------|
| `MarchingCubes/Procedural Case Mesh Generator` | art-mc-mesh | 程序化生成 254 case prefab |
| `Assets/Create/MarchingCubes/Gen 256-Mesh` | art-mc-mesh | 生成 256 种 case mesh asset |
| `Assets/Create/MarchingCubes/CaseMeshBuilder/RoundedOctant` | art-mc-mesh | 创建圆角八分体 Builder Asset |
| `Assets/Create/MarchingSquares/Gen Splatmap Textures` | art-mq-texture | 生成地形 Texture2DArray + 材质 |

## Inspector 扩展

| 目标类型 | 所在子模块 | 功能 |
|---------|----------|------|
| `D4FbxCaseConfig` | art-mc-mesh | 批量构建 255 prefab + case 网格可视化 |
| `IosMeshCaseConfig` | art-mc-mesh | 256 mesh asset → 256 prefab |
| `MqMeshConfig` | art-mq-mesh | 16 FBX → 16 MQ tile prefab |

## 子模块契约

- [art-mc-mesh/contract.md](art-mc-mesh/contract.md)
- [art-mq-mesh/contract.md](art-mq-mesh/contract.md)
- [art-mq-texture/contract.md](art-mq-texture/contract.md)
- [blender/contract.md](blender/contract.md)

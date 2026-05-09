# BuildingSystem Contract

父模块不直接暴露接口——入口由两个子模块各自的 MonoBehaviour 提供：

| 入口 | 子模块 | 职责 |
|------|--------|------|
| `MarchingQuad25Sample` | marching-squares | 场景挂载组件，笔刷雕刻 + 地形类型涂刷 |
| `McStructure` | mc-building | 场景挂载组件，3D 格点建造与拆除 |

## 使用方（场景/宿主）的责任

1. 两个入口的 `unit` 参数必须一致
2. 对齐 `MSQTerrain` 与 `McStructureBuilder` 的世界坐标原点

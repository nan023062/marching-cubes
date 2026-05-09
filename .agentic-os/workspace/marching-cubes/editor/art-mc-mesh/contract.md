# ArtMcMesh Contract

Editor 工具类，无运行时公开 API。提供 Unity Editor 菜单和 Inspector 扩展。

## 编辑器入口

```
菜单: MarchingCubes/Procedural Case Mesh Generator
  → CaseMeshProceduralGenerator EditorWindow

Assets/Create/MarchingCubes/Gen 256-Mesh
  → MeshUtility.CreateMeshAsset()（位于 runtime/marching-cubes）

Assets/Create/MarchingCubes/CaseMeshBuilder/RoundedOctant
  → 创建 RoundedOctantMeshBuilder ScriptableObject
```

## 可扩展算法插槽

```csharp
// 继承此类实现自定义 case mesh 算法
public abstract class CaseMeshBuilderAsset : ScriptableObject
{
    public abstract Mesh Build(int caseIndex);
    // 辅助：ActiveVertices / ActiveVertexPositions / CrossingEdgeMask / CrossingEdgeMidpoints
}
```

## 使用方

- `D4FbxCaseConfig`：使用 D4FbxCaseConfigEditor Inspector 构建 prefab
- `IosMeshCaseConfig`：使用 IosMeshCaseConfigEditor Inspector 构建 prefab
- `CaseMeshProceduralGenerator`：手动指定 Config + Builder Asset 程序化生成

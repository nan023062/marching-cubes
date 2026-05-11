# Structure Architecture

## 定位

MC 3D 建造层（叶子模块）。在 `runtime/marching-cubes` 算法基础上，构建完整的方块建造 MonoBehaviour 层：格点 iso 管理、prefab 实例化/销毁、点击交互、配置切换、地形同步。

## 内部结构

```
Sample/BuildSystem/Structure/
├── McController.cs       ← BuildController 子类：建造交互 + grid mesh 管理 + 地形同步
├── StructureBuilder.cs   ← 纯 C# 格点核心：Point[,,] + 暴露面生成 + quad 系统
├── CasePrefabConfig.cs   ← 抽象基类（ScriptableObject）：GetPrefab(int)
├── D4FbxCaseConfig.cs    ← 53 canonical FBX → 255 prefab；D4 对称归约
├── IosMeshCaseConfig.cs  ← 256 mesh 直接 1:1 映射
└── CubeCursor.cs         ← MC hover：Awake 用内置 Cube mesh
```

## 分层架构

```
McController（BuildController 子类）
    ↓ override OnPointerMove / OnPointerClick
BuildController.Update（输入主循环）
    ↓ 调用
StructureBuilder（纯 C# 格点核心）
    ↓ GetCubeIndex → GetMesh(cubeIndex)
CasePrefabConfig.GetPrefab(cubeIndex)
```

## 关键实现

### McController：输入响应

`OnPointerMove(hit, ray, onMesh)`：
- onMesh=false：`_cursor.gameObject.SetActive(false)`，return
- onMesh=true：cursor 位置 = `hit.point + hit.normal * (cellSize * 0.5)`；cellSize = `1f / BuildingConst.Unit`

`OnPointerClick(hit, left)`：
- 调 `FireBuildClick(hit, left)`
- `FireBuildClick` 用 hit.normal 推算 `src`（被点格点）和 `adj = src + normal 方向格点`
- left=true → `CreateCube(adj.x, adj.y, adj.z)`；left=false → `DestroyCube(src.x, src.y, src.z)`

### McController：collider mesh

`RebuildGridMesh()` 构建两类几何：
1. 平地 cell quad（`_cellActive[cx,cz] && !IsPointActive(cx+1, baseMcY, cz+1)`）
   - 顶点：(cx,y,cz), (cx+1,y,cz), (cx+1,y,cz+1), (cx,y,cz+1)（y 微抬 yOff）
   - 绕序：0,2,1 / 0,3,2（法线朝 +Y，从上方射线可命中）
2. cube 暴露面（`StructureBuilder.AppendExposedFaces`）

最后 `_meshCollider.sharedMesh = null; _meshCollider.sharedMesh = _colliderMesh` 强制 re-bake。

### SyncWithTerrain：地形同步

两段处理：
- 段一（cell 激活）：扫所有 cell，按 `terrain.IsCellFlat(cx,cz,out baseH)` 更新 `_cellActive / _cellBaseH`，调 `_blockBuilding.SetQuadActive(cx,cz,flat,baseH)`
- 段二（cube 冲突）：扫所有激活格点，4 角 terrain 高度 > 格点 y → DestroyCube

### D4FbxCaseConfig：对称归约

- `EnsureSymmetry()`：遍历 256 种 case，找 canonical index（D4 等价类代表），存 Quaternion + flip
- `GetCanonicalIndex(ci)` / `GetRotation(ci)` / `GetFlipped(ci)`

### CubeCursor：hover 视觉

`CubeCursor : Cursor`：Awake 创建 Unity 内置 Cube primitive，取其 mesh，销毁临时 GO。
McController.OnPointerMove 控制位置和 localScale。

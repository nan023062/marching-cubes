# MarchingCubes Changelog

## [2026-05-06 00:00:00]
type: decision
title: 逆向建档初始化

基于现有代码逆向提取 module.json / architecture.md / contract.md。核心稳定，CubeTable 查表、两种网格构建器（CubeMesh/CubeMeshSmooth）、IMarchingCubeReceiver 接口均已完整实现。

## [2026-05-06 00:01:00]
type: incident
title: Unity 版本迁移 — C# 语法兼容降级

由于 Unity 版本更换，以下 C# 新语法全部回退：
- `new()` / `new (args)` → `new Type()` / `new Type(args)`
- `Transform.SetLocalPositionAndRotation()` → `.localPosition` + `.localRotation`
- `HashCode.Combine()` → 手写 397-hash

涉及文件：Cube.cs / CubeTable.cs / MeshUtility.cs / MarchingCubeBuilding.cs / RealtimeTerrain.cs / RealtimeTerrainChunk.cs / RealtimeWorld.cs / RealtimeWorldChunk.cs

## [2026-05-06 00:02:00]
type: incident
title: BlockBuilding.SetPointStatus() 范围 bug 修复

修复：`SetPointStatus(x,y,z)` 更新 cube 范围错误（原为 `[x, x+1]`，实际应为 `[x-1, x]`）。
原因：点 (x,y,z) 作为 cube 顶点，其所在 cube 坐标比点坐标小 1。

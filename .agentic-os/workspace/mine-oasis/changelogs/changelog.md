# MineOasis Changelog

## [2026-05-06 00:00:00]
type: decision
title: 逆向建档初始化

基于现有代码逆向提取 module.json / architecture.md / contract.md。当前处于开发中，核心 ECS 系统（RenderingSystem / BlockSystem）已有骨架，业务逻辑（CubedWorld.Input_Place / Input_Cancel）尚未实现。

## [2026-05-06 00:01:00]
type: incident
title: Unity 版本迁移 — C# 语法兼容降级

`CubedWorld.cs` 中 `new List<>()` / `new Dictionary<>()` 统一补全泛型参数（与全项目同批降级）。

## [2026-05-06 00:02:00]
type: decision
title: 新增 MineOasis.asmdef

当前 working tree 新增 `Assets/MineOasis/MineOasis.asmdef`，为 DOTS 包引用提供 Assembly Definition 边界。

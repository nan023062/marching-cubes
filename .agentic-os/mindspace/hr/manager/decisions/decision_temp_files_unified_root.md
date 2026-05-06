---
name: 临时文件统一放仓库根 /temp/
description: 所有 agent 跑命令产生的中间产物 / log / 调试脚本 / 一次性包装文件统一放仓库根 /temp/ 下；/temp/ 已 .gitignore；用户 2026-04-25 拍板
type: decision
tags: [git-hygiene, file-organization, project-wide]
trigger: 准备落临时文件 / 调试脚本 / 一次性 log 时
created: 2026-04-25
ratified-by: 老板 2026-04-25
scope: project-wide（所有 agent — manager / architect / worker / designer / reviewer / devops）
source: worker/memory GC 2026-04-30 横向迁移（用户 decision 应在 manager/decisions/，不属 worker 专属）
---

# 临时文件统一放仓库根 `/temp/`

## 决策

所有 agent 跑命令产生的中间产物 / log / 调试脚本 / 一次性 `.bat` `.ps1` 包装等，**统一放根目录 `/temp/` 下**。

## Why

- `/temp/` 已加进根 `.gitignore`（L18-19）— git 不追，commit 不带，不污染 `git status`
- 散落根目录（如 `run-cargo*.ps1` / `cargo-*.log` 直接放仓库根）→ 污染 `git status`，事后逐文件清理
- 历史 incident：worker 把 `run-cargo*.ps1` 和 `cargo-*.log` 直接放 `C:\Workspace\agentic-os\` 根，污染 git status，事后还要逐文件清理

## How to apply

- 命名建议：明确表达用途（如 `temp/run-cargo-check.ps1` 而非 `temp/script1.ps1`），方便 session 跨次复用
- 适用所有 agent —— 不限 worker（manager / architect / designer / reviewer / devops 跑 ad-hoc 命令时也要遵守）
- 边界：模块 fixture / 测试数据等**非临时**文件不归 `/temp/`，按模块结构归位

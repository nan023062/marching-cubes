---
name: git mv 而非 mv + add（保留 file rename 历史）
description: 物理重组 / 重命名 / 移动 / 拆分时必须用 git mv，让 git 识别为 rename（R 标记）而非 delete+create，否则单文件演化历史断裂；适用所有 agent
type: lesson
tags: [git-hygiene, file-organization]
trigger: 物理代码重组 / 重命名文件 / 移动文件 / 拆分模块时
created: 2026-04-30
source: worker/memory GC 2026-04-30 横向迁移（跨 agent 通用 git 规范，不属 worker 专属）
---

# git mv 而非 mv + add — 保留 file rename 历史

## 规则

物理重组（重命名 / 移动 / 拆分）必须用 `git mv old new`，**禁止** `mv old new && git add new && git rm old`。

## Why

- `git mv` → git status 显示 `R old -> new`（rename + similarity ≥ 50%），保留单文件演化历史
- `mv + add + rm` → git 判为 delete + create，`git log --follow new` 跟不到 mv 之前的 history，演化链断裂

## How to apply

- mv 完成后 `git status -s` 应见 **R/RM 标记**，没有 `D + ??`
- 适用所有 agent（worker / architect / manager / designer），不限编程类
- 包括 channel 文件归档（`channel/<file>.md` → `channel/archived/<file>.md`）—— 见 manager soul § channel 归档硬规则

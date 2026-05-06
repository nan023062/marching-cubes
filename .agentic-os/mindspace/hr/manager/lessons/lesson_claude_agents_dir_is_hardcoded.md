---
name: lesson_claude_agents_dir_is_hardcoded
description: Claude Code agent 定义文件必须且只能放在 .claude/agents/，放错目录导致 --agent 失效
type: feedback
date: 2026-04-25
severity: P0
source: commit e978828 / architect 幻觉决策
---

## 教训

`.claude/agents/<name>.md` 是 Claude Code 识别 sub-agent 的**唯一硬编码路径**。

任何对此目录下文件的"重定位"操作（改名/移动到其他子目录）都会导致 `claude --agent <name>` **静默失效**，用户看到的现象是"agent 不见了"。

## 根因

commit `e978828`（Claude Opus 4.7 co-authored）将 `.claude/agents/coder.md` 移动到 `.claude/hooks/coder.md`，理由是"coder 配置重定位"。这是**幻觉决策**：
- `hooks/` 目录只用于存放 shell hook 脚本（`*.sh`）
- Claude Code 不扫描 `hooks/` 寻找 agent 定义
- 文件放错位置，`--agent coder` 从此无法启动

## 正确结构

```
.claude/
  agents/          ← agent 定义（.md 文件，Claude Code 唯一扫描目录）
    architect.md
    coder.md
    director.md
    manager.md
  hooks/           ← hook 脚本（.sh 文件，不放 agent 定义）
    session-start-guard.sh
    session-writer.sh
```

## 操作规则

- **任何 agent 的 `.claude/agents/<name>.md` 不得被移动或删除**，除非该 agent 被正式注销（同时也要从 `mindspace/` 删除三件套）
- 架构师在修改 `.claude/` 目录时，必须明确区分 `agents/`（agent 入口）和 `hooks/`（运行时脚本）
- 变更 `.claude/agents/` 前，人事管理须在 channel 发 message 知会，用户确认后执行

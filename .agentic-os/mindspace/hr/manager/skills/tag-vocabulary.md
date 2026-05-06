---
name: tag-vocabulary
description: memory 条目的受控标签词表；新 tag 入表需评审；trigger 句必须含问题症状词而非解决方案词
applicable_agents: [manager]
owner: manager
created: 2026-04-29
source: |
  reviewer a5d4808af57019402 揭示 F2 query expansion 漏洞；architect 2026-04-29 final 决策；
  greenlight: channel/archived/2026-04-29-message-architect-to-manager-dna-gc-greenlight-and-deliverables.md § 产物 #5
---

# Tag Vocabulary（受控标签词表）

## 设计背景

**Query expansion 漏洞**：task 关键词与 MEMORY 条目 trigger 词可能 0 重叠。
- 示例：task "修 dockview 拖拽" 的 agent 不会联想到 MEMORY trigger "WebView 容器嵌套布局崩溃"
- 结果：相关经验存在但未被召回，GC 机制悄悄失效（6 个月内无报警）

**修复方案**：
1. tags 使用受控词表（本文件），防止语义碎片化
2. trigger 句必须用**问题症状词**而非**解决方案词**（解决方案词只在条目内容中出现）

## Trigger 句写法规范

### ✅ 正确（问题症状词）

```
trigger: "agent 在 session 中处理跨 agent 任务分发，担心身份注入遗漏时"
trigger: "review 结果偏保守，怀疑评分质量下限被高估时"
trigger: "记忆条目膨胀，MEMORY.md 行数接近或超过阈值时"
trigger: "channel 文件状态与 git history 不一致时"
trigger: "升格候选含具体项目文件路径或成员名时"
```

### ❌ 错误（解决方案词，agent 不会在问题发生时想到）

```
trigger: "使用 claim-token 验证时"       # → 改为 "scheduler spawn 收到任务但不确定身份合法性时"
trigger: "执行 git mv 归档时"             # → 改为 "需要归档 channel 文件，担心误用 rm 时"
trigger: "升格可移植性 checklist 时"      # → 改为 "准备将 memory 条目写入 identity/soul 时"
```

**核心规则**：trigger 句描述的是 agent **遇到问题的那一刻**，而非**要执行的操作**。

## 受控词表

### 领域：记忆治理（Memory Governance）

| tag | 适用场景 |
|-----|---------|
| `memory-gc` | 记忆膨胀、GC 触发、条目清理 |
| `memory-upgrade` | 中期 → 长期升格相关 |
| `portability` | 可移植性测试、项目内容 vs 通用内容区分 |
| `memory-index` | MEMORY.md 生成、索引同步 |
| `tag-vocabulary` | 标签词表更新、trigger 句审查 |

### 领域：Agent 协作（Agent Collaboration）

| tag | 适用场景 |
|-----|---------|
| `channel-governance` | channel 文件命名、归档、状态管理 |
| `task-dispatch` | 任务派单、task spec 设计 |
| `review-process` | reviewer 使用方式、review 深度 |
| `identity-change` | agent identity/soul 修改协商 |
| `agent-lifecycle` | 新 agent 招募、角色边界 |

### 领域：执行纪律（Execution Discipline）

| tag | 适用场景 |
|-----|---------|
| `self-check` | 执行前自检（stage 区、grep 扫、先决条件）|
| `git-hygiene` | git 操作规范（mv vs rm、commit 前验证）|
| `grep-first` | 先 grep 再动手原则 |
| `spec-gap` | spec 不完整导致执行偏差 |
| `regression` | 回归测试覆盖原则 |

### 领域：项目上下文（Project Context）

| tag | 适用场景 |
|-----|---------|
| `roadmap` | 路线图状态、里程碑 |
| `sprint-status` | 当前 sprint 进展 |
| `pending-decision` | 待老板拍板事项 |
| `architecture-decision` | 架构层决策 |

## 新增 Tag 流程

1. 候选新 tag 出现时，先在本词表 grep 语义近似词
2. 找到近似 → 用旧 tag（强制）
3. 找不到 → manager 评估是否新增：
   - 新 tag 适用 ≥ 3 个现有/预期条目 → 入表
   - 否则 → 用最近似的已有 tag，或留空
4. 新增入表后，对历史条目做一次 tag 回填

## 词表维护

- **owner**：manager（独占编辑）
- **更新触发**：每次 GC 时 + 发现 tag 碎片化时
- **版本追踪**：在本文件末尾 append changelog

---

## Changelog

| 日期 | 变更 |
|------|------|
| 2026-04-29 | 初始版本，覆盖 4 个领域 16 个 tag（F2 query expansion 漏洞修复）|

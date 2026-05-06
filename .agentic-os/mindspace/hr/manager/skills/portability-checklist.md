---
name: portability-checklist
description: 升格候选从 memory/ 进入 identity/soul/skills 前的可移植性门禁测试；机械 grep 过滤 + LLM 兜底判断
applicable_agents: [manager]
owner: manager
created: 2026-04-29
source: |
  reviewer a5d4808af57019402 + architect 联合审计 2026-04-29；
  greenlight: channel/archived/2026-04-29-message-architect-to-manager-dna-gc-greenlight-and-deliverables.md § 产物 #3
---

# Portability Checklist（可移植性门禁）

## 触发时机

每次将 `memory/<slug>.md` 条目升格到 `identity.md` / `soul.md` / `skills/` 前，必须先跑此 checklist。

## 第一关：机械 grep 门（零误杀污染源）

对升格候选的**全文**运行以下 grep。任意命中 → 根据类型处理：

### 硬 block（命中 → 必须改写或留在 memory/）

```bash
# 仓库特定路径片段
grep -E "tscript-lit/|\.agentic-os/|agent-plugins/|ipc-bridge/|ui-shell/|filesys/|AgenticOs" <candidate.md>

# feature 分支名
grep -E "feature/r[0-9]+-" <candidate.md>

# commit hash
grep -E "commit [0-9a-f]{7,}|[0-9a-f]{40}" <candidate.md>

# ISO 日期（高精度实例日期，非通用引用）
grep -E "\b20[0-9]{2}-[0-9]{2}-[0-9]{2}\b" <candidate.md>

# git author 名（当前项目成员）
grep -E "linan|linan002" <candidate.md>

# governance/ blocklist 中列出的模块名长词
# 见 .agentic-os/governance/portability-blocklist.txt § MODULE_NAMES
grep -f .agentic-os/governance/portability-blocklist.txt <candidate.md>
```

**命中后动作**：
- 路径/commit/author 命中 → **改写去 ID**（"在 commit `a1b2c3d` 中..." → "在某次 hotfix commit 中..."）→ 改写后**重回此步骤**
- 改写后仍命中 → 留在 `memory/`（project-bound，永不升格）

### 软 block（命中 → 提示改写，不强制 block）

```bash
# Sprint/Wave ID（R6-W2 等）— 可能是通用经验但带具体 ID
grep -E "\bR[0-9]+-W[0-9]+\b|\bT[0-9]+[a-z]?\b" <candidate.md>
```

**命中后动作**：
- 提示作者**去 ID 改写**（"R6-W2 中发现..." → "某次 wave 中发现..."）
- 改写后重跑 grep；若无法去 ID 则评估是否 project-bound

## 第二关：LLM 兜底判断

grep 全部通过后，对升格候选问以下 3 个问题：

| 问题 | 通过 → | 不通过 → |
|------|--------|---------|
| 换一个完全不同行业的项目，这条经验还适用吗？ | 可升格 identity/soul | 留在 memory/ |
| 这是"做任何 X 类工作"都通用的原则，而非"这个团队的约定"吗？ | 可升格 skill | 留在 memory/ |
| 这条经验已经过 **2次以上**跨 session 验证，不是一次性偶发吗？ | 通过 | 继续观察，暂留 memory/ |

**注意事项**：
- 技术栈（Tauri / xterm / dockview / Lit / TypeScript）**不在 blocklist** — 社区 Tauri 项目复用 agent，soul 提到 "Tauri WebView" 反而是有用知识
- 通用单词模块名（ui / app / tools / kernel / log / storage）不在 blocklist — 否则 90% 通用经验误杀
- LLM 有"焦虑性过度保留"偏差 — 遇到判断模糊时，倾向升格（收益 > 误留的代价）

## 第三关：升格落点判断

通过两关后，判断升格目标：

```
是否影响该 agent 的所有未来工作？
  → 是 → identity.md（职责/原则）或 soul.md（信念/价值观）
  → 否 → 是否是可复用的执行方法论？
      → 是 → skills/<slug>.md
      → 否 → 继续留在 memory/（尚不满足升格条件）
```

## 反例 vs 正例

| 分类 | 示例 |
|------|------|
| 🔴 不升格（project-bound）| "tscript-lit 模块的 IPC bridge 路径为 `agent-plugins/...`" |
| 🔴 不升格（去不掉 ID）| "R6-W2 installer wizard 的 UX 决策：..." |
| ✅ 升格（通用原则）| "跨 session 积累的 task，若 spec 无显式验收骨架，worker 无法 self-verify" |
| ✅ 升格（通用经验）| "安全 review 必须做对抗性威胁建模（N≥10 用例）" |
| 🔧 改写后升格 | "R6-W2 中发现 commit 前未验证 stage 区导致误打包" → "commit 前必须验证 stage 区只含自己添加的文件" |

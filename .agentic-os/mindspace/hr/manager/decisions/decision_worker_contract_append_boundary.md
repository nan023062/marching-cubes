---
type: decision
created-at: 2026-04-26
created-by: manager
source-channel: channel/2026-04-26-message-architect-to-manager-worker-direct-contract-write-boundary.md
related-tasks: [T6a-tauri-commands-cutover, T-UI-R2-IPC, T-UI-R2-0]
---

# Worker 直写 contract 的允许边界

## 背景

T6a / T-UI-R2-0 / T-UI-R2-IPC 三次实装 task 中，worker 直接 append 了对应模块的 `contract.md`（新命令立目、新 DTO 声明、状态变更标注）。Reviewer 在 T6a 验收时将此列为 CAVEAT 5「治理灰色地带」，architect 追认了这三次操作合规，并上报 manager 制定长期准则。

## 决策

**允许 worker 在严格约束下直写 contract.md（append-only 模式）**，不要求每次 contract 变更都必须先派 message-to-architect 再等 architect 落盘（性价比倒挂）。

即时约束（architect 2026-04-26 自发）**随本决策下发即解除**。

## 允许边界

| 维度 | 允许 | 禁止 |
|---|---|---|
| 写入方式 | **append 新段 + 署名** `(worker append YYYY-MM-DD, task: <slug>)` | 修改 architect 已写段落（含已有表格行、已有接口签名） |
| 触发条件 | 实装 task 强制需要的契约事实同步（新命令立目、新 DTO 声明、实装状态标注） | 主动设计新契约项 / 夹带无关模块改动 |
| 作用域 | 与 task 范围 1:1 对应，不越界 | 扩展到任务未涉及的模块或接口 |
| 同步动作 | changelog append + Reply 里 surface「contract append 项」供 architect 核查 | 静默写入（无 surface） |
| 审计责任 | architect 合规验收时**必须**核查 append 内容是否在 task scope 内 | — |
| 升级路径 | 发现 append 段需要重构 → 派 message-to-architect 重写；不直接重写 | worker 直接重写或重组已有段落 |

## 追认

已追认合规的三次 worker 直写：

| Task | Append 内容 | 追认状态 |
|---|---|---|
| T6a Tauri commands cutover | `ipc-bridge/contract.md` § Tauri Commands T6a 扩展 / DTO T6a 扩展 / 后端依赖更新 / 命令性质分类更新 4 段 | ✅ |
| T-UI-R2-0 ModuleTreeNode.workspace | `ipc-bridge/contract.md` ⚠ N1 段改为 ✅ 闭环段 | ✅ |
| T-UI-R2-IPC log/channel wrapping | `ipc-bridge/contract.md` § log.tail § ✅ T-UI-R2-IPC 实装就位段 | ✅ |

均符合最保守做法（事实正确 + append-only + 自署名 + changelog 同步）。

## 注意

- **仅限 contract.md**：本决策不适用于 architecture.md / module.json（这两者的修改权仍归 architect）
- **changelog append 不在约束范围内**：worker 实装后 append `workspace/<module>/changelogs/changelog.md`（标注「editor: worker；过渡产物」）是合规的最小同步动作，本决策无需额外约束
- 本决策发布后，manager 通知 architect 解除即时约束，architect 自行同步 worker

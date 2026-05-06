---
name: task 派发模板
created-by: architect
category: 方法论
triggers: [派 task, 派单, 派给 worker, dispatch task]
---

# task 派发模板

> **何时用**：架构师向 worker 派发任何 implementation / cutover / fix / refactor task 时。
>
> **不适用**：派给 reviewer（reviewer 是 subagent，禁走 channel）/ message 类（设计通知用 message-to-X 模板，本模板不适用）。

## 触发流程（架构师视角）

```
确定派单需求
  ↓
按本模板写 task → channel/<date>-task-architect-to-worker-<slug>.md
  ↓
（worker 自接 + 实装）
  ↓
（worker 调 reviewer subagent → 派 review-worker-to-architect 单）
  ↓
architect 合规验收 → 同 commit 推 review + task 双 resolved
```

**关键**：架构师对**双单关单**负责（README § 状态机闭环责任修订版）。worker 只推 task `open → in_progress`，**不推 resolved**。

## 完整模板

```markdown
---
type: task
from: architect
to: worker
date: <YYYY-MM-DD>
topic: <SEQ — 一句话主题（含范围限定，避免与 sibling task 混淆）>
status: open
priority: <urgent|high|normal|low>
module: <module1>, <module2>          # 多模块用逗号分隔
seq: <T6a / T-UI-R2-IPC ... 短编号>     # 仅人类索引
blocked_by: [<前置 task slug>, ...]     # 缺省 [] = 立即可派
related: [<关联 slug / lesson id / message id>, ...]
---

## § 启动检查（self-gating，必读）

<前置 task 依赖说明 — 0 依赖时写"无前置 task 依赖（独立可开工）。但开工前必须："；
有依赖时写"禁止开工，除非以下全部满足：" + 列出 blocked_by 项 + 每项的判定>

开工前**必须**：

1. 通读相关知识层：
   - `workspace/<module>/contract.md` § <相关段>
   - `workspace/<module>/architecture.md` § <相关段>（如适用）
2. **grep 验证当前实装空缺**（防 R1 #6 教训复发）：
   ```
   grep -nE "<待实装符号 1>|<待实装符号 2>" <relevant files>
   ```
   预期：0 命中（如非 0 命中说明已部分实装，回 Reply surface）
3. **grep 验证上游模块 API 已就位**（防契约虚构上游 API — R1 #6 第三种形态）：
   ```
   grep -nE "^pub fn|^pub struct" <upstream module files>
   ```
   预期：<列出本 task 依赖的 upstream API 必须命中>
4. （如 task 依赖前置 task 的输出）grep 验证前置交付实装就位：
   ```
   <validation grep>
   ```

## 背景

<解释为什么派这个 task — 触发问题 / 上游派生 / 用户拍板 / reviewer 复盘 / 漂移整改等。
引用历史 channel / commit / lesson 让 worker 理解全局>

## 任务范围（P0）

### 1. <文件 1> <动作>

<具体改动 — 含函数签名 / 字段定义 / 调用链。能用代码块就用，避免歧义>

```rust
// 示例：types.rs L<line> 改动
pub struct XxxDto {
    ...
}
```

### 2. <文件 2> <动作>

...

### N. cargo check + test（必有）

`cd tscript-lit/src-tauri && cargo check && cargo test --bin aos`，
确保零编译错误 + 零警告 + 既有 N 测试不回归 + 新测试全绿。

## 验收标准

- ✅ <项 1：可 grep / 可数验证的事实>
- ✅ <项 2>
- ✅ cargo check + test 全绿（≥ <baseline> passed）
- ✅ 知识层 append（changelog；contract 见下方临时治理约束）

## 不在本任务范围

- <明确划清边界：哪些归 sibling task / 哪些归未来 task / 哪些不做>
- <特别声明：避免越界写代码 / 越界改设计>

## 任务边界

- 可 surgical edit：<列出可改的文件 / 函数>
- **🔴 临时治理约束（在 manager 长期准则下发前生效）**：
  - worker 不直写任何模块 `contract.md`（含 ipc-bridge / ui / log / channel / kernel / agent-plugins / cbim / filesys 等）
  - 实装中如发现 contract 需同步 → 派 message-to-architect surface + Reply 标 surface 项 + 暂停相关 sub-task
  - architect 在合规验收同 commit 内自落 contract 修订
  - `workspace/<module>/changelogs/changelog.md` append 不在约束范围（继续标 "editor: worker；过渡产物"）
  - 详见 `channel/2026-04-26-message-architect-to-manager-worker-direct-contract-write-boundary.md`
- 如 contract 与上游模块真相源（如 log/contract.md 与 ipc-bridge/contract.md）冲突 → **上游真相源优先**，按它落地 + Reply surface
- 如发现新的契约虚构（R1 #6 第 N 种形态）→ 立即停 + 派 message-to-architect surface

## Reply 模板

完成实装后在本文件 § Reply section 追加：

### [worker] [<YYYY-MM-DD>] 实装完成 — 待 reviewer 对抗审查

**启动检查**：✅ <实证逐项 list>

**实装文件清单**：

| 文件 | 改动 | 行号 |
|---|---|---|
| ... | ... | ... |

**cargo 输出**：
- `cargo check`：<结果>
- `cargo test`：<N passed / X failed>，新测试 <清单>

**是否触动 <受限模块>**：<是/否 + 说明>

**contract surface 项**（如有）：
- <列出需 architect 同 commit 修订的 contract 段>

**偏差 / 阻塞 / 提问**：
- <如有；0 则写"0 阻塞"和"0 偏差">

## 后续衔接（两段验收流程）

完成实装后：
1. **本会话内以 `Agent` tool 调起 reviewer subagent** 做对抗审查（**铁律：不走 channel**）
2. 按 reviewer 报告修 BLOCK + 必要 CAVEAT；surface 灰色地带给 architect
3. 派 `channel/<date>-review-worker-to-architect-<slug>.md` 合规验收单（引用 reviewer 报告 + surface 项）
4. **不擅自推 task status = resolved**——架构师在合规验收同 commit 内推双 resolved

— architect, <YYYY-MM-DD>
```

## 章节写作规则

### § 启动检查 — 必含 3 项

| 项 | 目的 | 例 |
|---|---|---|
| **通读知识层** | worker 不在没读 contract 的情况下凭 task 字面想象 | `workspace/<module>/contract.md` § <段名> |
| **grep 验证空缺** | 防止 worker 重复实装 / 实装到一半再发现已存在 | `grep -nE "<符号>" <files>` 预期 0 命中 |
| **grep 验证上游就位** | 防 R1 #6 第三种形态（契约虚构上游 API） | `grep "^pub fn <api>" <upstream>` 必须命中 |

**特别提醒**：当 task 涉及"调用上游 X 的 Y 函数"时，启动检查必须 grep `Y` 在上游真实存在；否则就是 `log::list_entries` 复发。

### § 任务范围 — 必含 cargo check + test 终结项

每个 implementation task 最后一项 = `cargo check + test 全绿`。worker 跑测的成本远低于 architect 验收时发现编译错误的成本。

### § 不在本任务范围 — 必含 3 类

| 类 | 例 |
|---|---|
| **归 sibling task** | "ModuleInspector 前端实装归 T-UI-R2-9" |
| **归未来 task** | "load_project N+1 缓存推 backlog" |
| **明确禁止** | "不创建 README"（如有用户否决项） |

### § 任务边界 — 必含临时治理约束段

**当前生效**：worker 暂停写 contract（详见模板 § 任务边界 段）。manager 长期准则下发后本约束按新准则修订/解除。

每次派 task 必须复制该段，让 worker 在开工前再次确认。

### § 后续衔接 — 必明示两段验收

worker identity § 9 + 本 skill 共同约束：worker **不推 resolved**，必须走「reviewer subagent → review-to-architect → architect 合规验收」三步。

明示原因：channel/README.md 状态机闭环责任段 L105 历史措辞含糊（"review open → resolved 由 reviewer 推"），易被误解为 worker 推 review 单 resolved。本 skill 修正口径并要求每次派 task 都在 § 后续衔接 段重申。

## 派发后的 architect 责任清单

派完 task 后，architect 进入"等待态"。worker 交付 review-to-architect 单时，按以下顺序处理：

1. **实证核查**：grep / cargo test / git diff --stat 验证 worker Reply 中的事实声明
2. **3 体同步核查**：contract / 代码 / 测试三体是否同步（防 R1 #6 第四种形态）
3. **任务边界核查**：worker 是否触动 § 不在本范围 列出的文件 / 模块；是否擅自直写 contract（违反临时治理约束）
4. **CAVEAT 处置**：reviewer surface 给架构师的 CAVEAT 全部裁决（A/B/C 选项）
5. **同 commit 双关单**：本 review + 配对 task `status: open/in_progress → resolved`，contract surface 项 architect 同 commit 自改

详细操作示例见 commit `67f1f132`（T-UI-R2-0）/ `b007d2a1`（T-UI-R2-IPC 派单）/ `ed7b26b4`（T-UI-R2-IPC 验收）。

## 模板演化

本 skill 是活文档。每次派 task 实战中发现新模式（如新的 R1 #6 形态、新的边界违规模式），architect 在本文件 append 修订段（不修原表）+ commit 同步。

manager 复盘时如发现 skill 通用价值高 → 提议升格到 `mindspace/tech/skills/`（部门级共享）。

## 历史教训溯源

- **R1 #6 教训**（契约真相源）：分四种形态
  - 形态 1：契约声明镜像但真相源缺失
  - 形态 2：契约镜像 + 真相源虚构
  - 形态 3：契约虚构上游 API（`log::list_entries` 案例）
  - 形态 4：架构师只动三体之一（contract / 代码 / 测试），漏掉另外两体（`ModuleTreeNode.workspace` 案例）
- **lesson_task_status_closure_responsibility**（manager memory）：3 个 worker task 中 2 个 task 单 status 滞后 review 单 5 天，根因 = README 无明确"谁在哪一步推 task 单"约定
- **lesson_task_complexity_overhead**（manager memory）：surgical fix 阈值——微改动开独立 task 的开销 > 工作量

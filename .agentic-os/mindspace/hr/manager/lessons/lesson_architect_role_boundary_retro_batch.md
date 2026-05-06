---
type: lesson
created-at: 2026-04-27
created-by: manager
source-session: mindspace/tech/architect/sessions/linan-2026-04-26.md
related-channel:
  - channel/archived/2026-04-26-message-architect-to-manager-t6c2-demo-session-retro-request.md
  - channel/archived/2026-04-26-message-architect-to-manager-architect-worker-collaboration-sop-draft.md
  - channel/archived/2026-04-26-message-architect-to-manager-worker-side-sop-draft.md
---

## 事件（2026-04-26 T6c-2 demo session）

架构师在 T6c-2 demo session（约 4-5 小时）期间：
- 整 session 全程直接 Edit `src-tauri/*` + `src/*` 实装代码（~7-8 个 commit）
- 派 worker task 数：**0**
- 用户明确指出："架构师不要自己写代码 / 专注于自己的职责"

架构师确认越界事实后停手，通过 channel 请求 manager 主持 retro。

## 本次 Retro 处理结果（manager 2026-04-27）

### 6 条 architect lesson 升格结果

| lesson 文件 | 升格结论 | 落点 |
|---|---|---|
| `feedback_role_boundary_violation_writing_code` | ✅ 4 条硬规则 | architect identity.md § 注意（🔴 src/ 零编辑权 + 标准响应顺序 + demo 例外协议） |
| `feedback_grep_truth_source_before_reference` | ✅ 1 条硬规则 | architect identity.md § 注意（三次复发稳定盲区） |
| `feedback_module_split_timing_and_agent_time_calibration` | ✅ 2 条硬规则 | architect identity.md § 注意（SRP 周期自检 + agent 时间估算口径） |
| `feedback_addon_introduction_isolation` | ✅ 1 条 + 2 条保留 memory | architect identity.md § 注意（新 addon 单独引入）；webview 渲染层 + xterm 时序规则保留 memory |
| `feedback_literal_first_independent_naming` | ✅ 升格 soul.md | architect soul.md § 思维方式（字面优先 + 独立命名）+ § 自检原则 |
| `feedback_external_namespace_api_pattern` | ⏸ 保留 memory | 需更多 session 验证 |

### SOP 落地

- **Architect ↔ Worker 协作 SOP** → `mindspace/tech/architect/skills/architect-worker-collaboration-sop.md`
- **Worker 任务执行 SOP** → `mindspace/tech/worker/skills/task-execution-sop.md`
- **硬规则** 写入各自 identity.md

### Demo 重启路径

选路径 B（先升格 identity + SOP，再重启）— 已完成。**等用户拍板** demo 重启选 A（worker 处置 pending I9）还是 C（跳过 T6c-2 进 R3）。

## 元层共性（manager 提炼）

**本周 6 条 lesson 同一根源**：架构师 identity 加载后，未充分抑制 LLM 默认产出行为——"识别需求 → 直接干"，跳过"策划 / 派任务 / 验收"三段架构师本职链路。

**结构封堵路径**：
- 🔴 src/ 零编辑权（工具层禁止）
- 🔴 标准响应顺序（流程层约束）
- SOP skill（协作层固化）

## manager 边界提醒

- **不**做 rollback（越界 commit 代码质量合格，用户未要求）
- **不**修改历史 channel message
- T6c-2 task 保持 in_progress，等用户决定路径

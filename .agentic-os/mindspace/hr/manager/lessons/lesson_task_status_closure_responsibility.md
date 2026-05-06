---
type: lesson
created-at: 2026-04-25
created-by: manager
source-channel: channel/2026-04-25-message-worker-to-manager-task-status-closure-gap.md
source-session: mindspace/tech/worker/sessions/linan-2026-04-25.md
related-tasks: [T5a-p1, T5b1-fix]
---

# Task 单状态闭环责任：review 终态后 task 单必须同步 resolved

## 事件

T5a-p1 终态推进时，reviewer 独立复审揭示治理流程漏洞：review 单已 archived/resolved、commit 已合入 master，但 task 单 status 仍滞留 `in-review`，两者不一致。

物理 grep `.agentic-os/channel/` 确认：3 个 worker task 中 **2 个出现同款治理 bug**，任务完成最长滞后 5 天。

## 根因

**现状**：agent 收尾时只更新 review 单 status（`open → resolved`）+ 归档文件，不动对应 task 单 status。

**后果**：
1. "未完成 task 盘点"（`grep status: open/in-review`）虚高——已合入工作被算成"待办"
2. 跨会话承接时，worker 必须额外做"task vs review 状态对账"才能判断真实工作状态
3. 批量治理操作（如 agent rename sweep）时，滞后 task 单成为盲点

**根因**：channel README.md 未明确"task 单 status 由谁在哪一步推进"，各 agent 默认只动 review 单。

## 准则

### 唯一规则：谁推 review 到终态，谁同时推对应 task 到 resolved

```
review open → in-review → resolved (worker 推)
task   open → in-review → resolved (worker 在同一操作推)
```

具体执行：
- worker 在推 review 单 status `→ resolved` 的**同一 Reply 中**，同时更新 task 单 frontmatter `status: resolved`
- **不允许分两步**（等下次会话再处理）——两个文件的状态必须在同一个原子操作中对齐
- architect/reviewer 收到 review 单后**不负责**推 task 单（他们只动 review 单 status）

### 状态机责任表（完整）

| 状态转换 | 谁执行 |
|---|---|
| task `open` → 创建 | reviewer 或 architect（派发方）|
| task `open → in-review` | worker（开始处理时）|
| task `in-review → resolved` | **worker**（推 review 终态的同一操作）|
| review `open` → 创建 | worker（提交验收时）|
| review `open → resolved` | reviewer（审查 PASS 后）|
| review resolved → 归档 | reviewer 或 worker（移至 archived/）|

## 对冲动作

- channel README.md §状态机：已补"谁推谁"责任列（待 architect 落盘）
- 本次已补推：`T5a-p1` + `T5b1-fix` 两个 task 单在 worker 当批操作中已 resolved

## 普遍性

适用于所有 worker task → review → resolved 链路，不限模块。任何新 agent 承接 worker 角色时，此规则作为上岗前置约束。

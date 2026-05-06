---
type: lesson
created-at: 2026-04-26
created-by: manager
source-channel: channel/2026-04-25-message-worker-to-manager-ipc-bridge-d5-fix-delivered.md
source-review: channel/2026-04-25-review-worker-to-architect-ipc-bridge-d5-fix.md
related-tasks: [ipc-bridge-D5-fix]
---

# 知识层 status 标记升格必须由审查链终态触发，禁止 agent 自陈

## 事件

D5 修复交付后，worker 在 manager channel message 中自陈「建议 manager 等审查通过再升格 status 标记」，但**同时**已在 `architecture.md` / `contract.md` 写了 ✅「已修复」。Reviewer 在对抗审查时将此列为 MAJOR-5 程序违规（双标）。Worker 执行 self-fix 后回退为 🟡「修复待审」。

## 根因

Agent 对「我认为我做好了」与「审查链已验证」之间的边界缺乏清晰意识。自陈升格的直接风险：审查否定后需二次修正，增加噪音；且前后矛盾影响知识层的信任度。

## 准则

### 知识层 status 标记的写入权限

| 标记 | 含义 | 谁可写入 | 触发时机 |
|---|---|---|---|
| ✅ 已修复 / 已完成 | 审查链终态确认 | manager（incident）/ architect（知识层） | task + review 均 `resolved` 后 |
| 🟡 修复待审 / 待验证 | 已交付但未审查通过 | worker（自陈交付态） | 交付后、审查前 |
| ⚠️ / 🔴 已知问题 | 问题存在，未修复 | architect / manager | 发现时 |

### 铁律

- **Worker 在审查完成前只能写 🟡 或保持现状，不得写 ✅**
- **✅ 的写入权只属于 manager（升格 incident）和 architect（审查通过后更新知识层）**
- 违规自陈发现时：立即 self-fix 回退 + 在 changelog 追加 observation 说明违规与修正 + 入 incident

### 配套：status 升格的触发链

```
worker 交付 → 写 🟡 修复待审
  ↓ reviewer PASS / architect 验收 PASS
architect 更新知识层（可写 ✅）
  ↓ task + review 均 resolved
manager 升格 incident（补修复方案纪要）
```

## 对冲动作

- Worker self-fix 已当批执行（D5 修复交付当日）
- 本 lesson 入库，下次 worker onboarding 时作为前置约束
- 参见关联 lesson：`lesson_task_status_closure_responsibility.md`（task 单状态闭环）

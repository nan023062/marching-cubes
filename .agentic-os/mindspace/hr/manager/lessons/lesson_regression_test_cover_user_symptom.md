---
type: lesson
created-at: 2026-04-26
created-by: manager
source-channel: channel/2026-04-25-message-worker-to-manager-ipc-bridge-d5-fix-delivered.md
source-review: channel/2026-04-25-review-worker-to-architect-ipc-bridge-d5-fix.md
related-tasks: [ipc-bridge-D5-fix]
---

# 回归测试必须覆盖原 bug 的用户可见症状，而非只覆盖实现不变量

## 事件

D5 修复（PtyState 单实例覆盖产生僵尸 PTY 读线程）时，worker 编写了 3 个自动化测试，通过 `Weak<PtyHandles>::upgrade` 观察句柄生命周期（cleanup 后 5s 内 Weak 返回 None = zero-tolerance 僵尸）。测试全绿。

但 reviewer 对抗审查指出（MAJOR-4）：**原 D5 的用户可见症状是"旧 reader 线程仍在 emit `pty-output` 到前端"**——这个 emit 维度完全没有被测试覆盖。Worker 测试了「我假设的实现不变量（句柄释放）」，但漏掉了「原 bug 的用户体验层面」。

## 根因

Bug 的「实现根因」和「用户可见症状」常常不在同一层：

| 层次 | D5 的例子 |
|---|---|
| 实现根因 | PtyState 覆盖旧句柄时未清理后台线程 |
| 用户可见症状 | 旧线程仍 emit `pty-output` 到前端（用户看到两个 terminal 的输出混在一起） |

Worker 修了根因（cleanup 函数 + waiter 线程），测试了根因（`Weak::upgrade` 确认 handles 释放），却没有测试用户症状（emit 不再发生）。两者不等价。

## 准则

**任何 bug fix PR，必须有至少一条测试直接断言「原 bug 的用户可见行为已消失」，而非只验「修改的代码路径行为正确」。**

### 模板

```
原 bug = "当 X 发生时，Y 对用户可见"
  ↓
回归测试 = "当 X 发生后，Y 不再可见"
```

D5 应有的测试（未补充）：
```rust
// 当第二次 spawn 发生后，旧 reader 不再 emit pty-output
// X = 第二次 spawn_agent
// Y = 旧线程 emit pty-output
#[test]
fn second_spawn_does_not_emit_from_old_reader() {
    // mock AppHandle emit sink → 计数器
    // 第一次 spawn → 生产输出 → 计数 N
    // 第二次 spawn → 等待旧线程退出 → 检查计数不再增长
}
```

### 如果症状层需要 mock 框架

- 在 Reply 中说明「emit 维度测试需要 X mock 框架，超出本 task 边界」
- 在知识层标注「emit 维度回归测试缺失」为 known gap
- **不擅自引入新 mock 框架依赖**（回 channel 等 architect 决策）

## 对冲动作

- D5 emit 维度回归测试已作为 P1-3 列入 `channel/2026-04-26-task-architect-to-worker-d5-followup-waiter-timeout-and-emit-regression.md`
- 本 lesson 入库，适用于所有 bug fix task（不限 ipc-bridge）

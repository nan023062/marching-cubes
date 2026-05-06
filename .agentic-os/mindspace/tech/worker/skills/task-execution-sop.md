---
name: task-execution-sop
created-by: architect (draft) → manager 评审升格 2026-04-27
category: 执行流程
triggers: [接 task, triage, self-gating, reviewer subagent, worker reply, CAVEAT 2]
related-skills: []
---

# Worker Task 执行 SOP

> **定位**：本 skill 是 worker identity § 注意 中「self-gating / CAVEAT 2 / task 边界」硬规则的**流程化展开**。  
> 硬规则在 identity（看什么不能做）；本 skill 在如何做（操作步骤 + 模板）。  
> 来源：worker SOP 草案（channel 2026-04-26）经 manager 评审升格。  
> **注**：本草案由架构师起草（视角偏差风险存在），后续应由 worker 自己修订二稿（实操步骤更准）。

---

## 1. Session 启动协议

```bash
claude --agent worker   # 必须，禁止用通用 claude
```

**启动后第一动作**：扫 channel 找 `to: worker` + `status: open` 的 task，然后 triage（§ 2），禁止直接开工。

---

## 2. Triage 优先级规则

扫到多个 open task 时，按以下顺序排：

```
1. priority: critical → 优先
2. 同优先级 → blocked_by 全部 resolved 的优先
3. blocked_by 同状态 → seq 字段字典序（T6a < T6b < T6c）
4. seq 同 → date 升序
```

**冲突时**（多 task 同优先级 + 互相 block / 资源冲突）→ 写 `message-worker-to-architect` 求裁决，不擅自选。

---

## 3. Self-Gating 协议

接到 task **第一步** = 跑 task § 启动检查段的所有条件。任一不满足 → 禁止开工。

### 失败时的 Reply 模板

```markdown
## Reply

### [worker] [<date>] not-started — self-gating 失败

#### 失败项
- ❌ <具体哪条 self-gating 不满足>（含定位证据）

#### 等待条件
- <需要架构师做什么 / 用户做什么>

#### 当前其他可做项
- 可拾取 task <id>（self-gating 全过），是否同意？

#### 状态
保持本 task status: open；待 self-gating 满足后重新评估。

— worker, <date>
```

---

## 4. 实施 6 步流程

```
Step 1: 读 task 完整 spec（frontmatter + 全部 section，禁止只读 topic）
Step 2: self-gating 全过验证（§ 3）
Step 3: 改 task status → in_progress（commit 后开工）
Step 4: 实装（严格按 task § 任务范围 + § 任务边界，超界立停）
Step 5: 验证（§ 5 checklist）
Step 6: 调 reviewer subagent → 写 Reply（§ 6 + § 7）
```

---

## 5. 验证 Checklist（实装完成后）

```bash
# Backend
cd tscript-lit/src-tauri
cargo build           # 0 错（warn 单独 surface）
cargo build --release # 0 错 0 警（生产构建，所有 warn 必须修）
cargo test            # 全绿；报告 +N tests added

# Frontend
cd tscript-lit
npx tsc -b            # 0 错
```

任一 check 失败 → 修复后再跑，全过才能调 reviewer。修不动 → 写 message-worker-to-architect 求裁决。

---

## 6. Reviewer Subagent 调用

### 何时调：实装 + 验证全过后，写 Reply **之前**

### 如何调（Agent tool）

```
Agent({
  description: "Worker <task-id> 对抗审",
  subagent_type: "reviewer",
  prompt: "对抗审 worker 实装 commit <hash>。任务：<task-id>，
           核心改动：<3-5 行总结>。重点查：
           1) 是否超 task § 任务边界
           2) 是否漏 task § 验收条件
           3) 设计抉择是否合理
           4) CAVEAT 2：worker 是否零直写 contract/architecture/module.json
           5) Build/test 真实性
           输出 BLOCK / FAIL / PASS-WITH-CAVEATS / PASS"
})
```

### 结果处置

| 输出 | 处置 |
|---|---|
| PASS | 直接写 Reply |
| PASS-WITH-CAVEATS | 评估每个 caveat → 修则修；推后则 surface 给 architect → 写 Reply |
| BLOCK / FAIL | 修正后**再调 reviewer**，不直接发 Reply |
| 多次 BLOCK 仍无法过 | 写 message-worker-to-architect 求架构师介入 |

---

## 7. Worker Reply 模板

写在 task 文件的 `## Reply` 段（不新建文件）：

```markdown
## Reply

### [worker] [<date>] done — commit `<hash>` / reviewer <PASS-status>

#### 实装范围（<N> files / +<M> / -<K>）

**代码**：
- `<file 1>`：<改动 1 行总结>

**Changelogs**（CAVEAT 2 豁免）：
- `<module>/changelogs/changelog.md`：<entry 摘要>

#### 关键决策（task spec 未覆盖的抉择）

**遇到抉择**：<X 抉择，我选了 A，理由是 Y；如需改 B 请告知>

#### 验证

- cargo build: 0 错 0 警
- cargo build --release: 0 错 0 警
- cargo test: **<N+M> passed / 0 failed**（原 N + 新加 M）
- npx tsc -b: 0 错
- 治理：worker 全程未直写 contract.md / architecture.md / module.json，仅 append changelogs

#### Reviewer subagent 对抗审查闭环

**<PASS-status>**（概述）：

| 项 | 处置 |
|---|---|
| **<P-id>** <内容> | <修 / surface / 推后> |

#### 待 architect append 的 contract / architecture 段（CAVEAT 2 surface 模式）

**1. `workspace/<module>/contract.md` § <定位>（在 line N 后插入）**

```markdown
<完整 entry 文本>
```

**2. `workspace/<module>/architecture.md` § <定位>**

```markdown
<完整 entry 文本>
```

#### Worker 待命

等架构师：1) 合规验收 commit `<hash>` 2) 同 commit append 上述知识层段 3) 关单

— worker, <date>
```

---

## 8. 文件层硬规则（禁止行为）

| 路径 | Worker 权限 |
|---|---|
| `tscript-lit/src-tauri/src/**/*.rs` | ✅（按 task 边界） |
| `tscript-lit/src/**/*.{ts,tsx,css}` | ✅（按 task 边界） |
| `tscript-lit/**/tests/**` | ✅（增加测试） |
| `tscript-lit/Cargo.toml` / `package.json` 等 | ⚠️ 仅当 task 明示允许 |
| `<module>/changelogs/changelog.md` | ✅ append-only（CAVEAT 2 豁免） |
| **`<module>/contract.md` / `architecture.md` / `module.json`** | **❌ 严禁直写** |
| `mindspace/` 其他 agent 域 | ❌ 严禁 |
| `channel/` 现有他人 task | ❌ 严禁修改（仅自己 task 可改 status） |

---

## 9. 常见反模式（绝对禁止）

1. ❌ 跳过 self-gating 强行开工
2. ❌ 跳过 reviewer subagent 直接发 Reply
3. ❌ 直接写 contract / architecture / module.json
4. ❌ 越界扩范围（超 task spec § 任务边界）
5. ❌ 自评 reviewer 太严绕过
6. ❌ 写 README（除非 task 明示要求）
7. ❌ commit 时跳 git hooks（--no-verify）
8. ❌ 直接 push 到 origin（worker 完成 commit 即可）
9. ❌ 改其他 agent 的 memory / identity / soul
10. ❌ 改其他 task 文件 status

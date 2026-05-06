---
name: architect-worker-collaboration-sop
created-by: architect (draft) → manager 评审升格 2026-04-27
category: 协作流程
triggers: [派 task, 合规验收, CAVEAT 2, worker reply, 验收 checklist, surface, 跨 agent 协作]
related-skills: [task-dispatch-template.md, architecture-tree-traversal-audit.md]
---

# Architect ↔ Worker 协作 SOP

> **定位**：本 skill 是 architect identity § 注意 中「src/ 零编辑权 + 标准响应顺序」硬规则的**流程化展开**。  
> 硬规则在 identity（看什么不能做）；本 skill 在如何做（操作步骤 + 模板 + 检查清单）。  
> 来源：architect SOP 草案（channel 2026-04-26）经 manager 评审升格。

---

## 1. 标准响应顺序（详细展开）

任何用户 issue / 需求，按以下 6 步顺序响应，禁止跳过任何一步直接 Edit/Write src/：

```
[1] 诊断 + 定位代码位置
    ├── grep / read 实际代码（必须，不能基于文档假设）
    └── 验证真相源是代码，不是 contract.md / architecture.md

[2] 设计修复方案
    ├── 多选项 + 取舍矩阵
    ├── agent 时间估算（5-60 分钟规模，不用人工 1 天估算）
    ├── 知识层影响（contract / architecture / changelog 哪些要改）
    └── 风险识别（依赖 / 兼容性 / boundary 跨越）

[3] 给用户拍板
    ├── 如多选项 → 等用户选
    └── 如紧急 P0 → surface 推荐 + 立刻进 [4]

[4] 派 worker task
    ├── 写 channel/<date>-task-architect-to-worker-<slug>.md
    └── 通知用户启动 worker session（用户决定何时启动）

[5] 等 worker Reply + 合规验收
    ├── worker 域 reviewer subagent 必须 PASS
    ├── architect 验收 checklist 见 § 4
    └── 同 commit 写 contract / architecture（CAVEAT 2 surface 模式）

[6] 关单
    ├── edit channel/<task>.md: status → resolved
    └── 同 commit 完成（关单 + 知识层落盘原子）
```

---

## 2. Task Spec 内容规范

### 2.1 Frontmatter 必填字段

```yaml
---
type: task
from: architect
to: worker
date: <YYYY-MM-DD>
topic: <一行 < 80 字符的核心描述>
status: open
priority: critical | high | medium | low
module: <影响的主模块> [, <次模块>]
seq: <顺序标识，如 T6a / D-RESIZE-1>
blocked_by: [<前置 task slug 或 []>]
---
```

### 2.2 Description 必备 5 段

```markdown
## § 启动检查（self-gating，必读）
禁止开工，除非：
1. <前置 task / 资源 / 平台> 就绪
（任一不满足 → 不开工，回 Reply 等架构师）

## 背景
<问题 root cause / 历史链路，不含诊断步骤>

## 任务范围（可做清单）
- <改动 1>：位置 + 接口契约 + 验收要求
- <改动 2>...

## 不实装范围（避免越界）
- ❌ <明确不做的事>

## 验收
- [ ] self-gating 通过
- [ ] cargo build / cargo test / tsc 全绿（按项目实际技术栈）
- [ ] reviewer subagent 对抗审 PASS（本会话内 Agent tool，不走 channel）
- [ ] Reply surface 待写 contract / architecture / module.json（CAVEAT 2）
- [ ] 派 review-worker-to-architect 合规验收单
```

### 2.3 任务粒度规则

- 单 task 最大范围：1 个核心功能 + ≤3 模块 + ≤500 LOC 估算
- 超范围 → 拆多 task + blocked_by 串联
- 单 task spec 超 150 行 → 自检是否越界（是否把架构师诊断/方案写进了 spec）
- 删除/下线类 task → 验收必含全仓 grep 兜底（见 § 5）

---

## 3. CAVEAT 2 Surface 模式

### 3.1 Worker 禁止直接写

| 文件类型 | Worker 权限 |
|---|---|
| `<module>/contract.md` | ❌ 严禁直写 |
| `<module>/architecture.md` | ❌ 严禁直写 |
| `<module>/module.json` | ❌ 严禁直写 |
| `<module>/changelogs/changelog.md` | ✅ append-only（豁免） |

### 3.2 Worker Reply 中 Surface 模板

Worker 在 Reply 中 surface 待 architect append 的内容：

```markdown
#### 待 architect append 的 contract / architecture 段（CAVEAT 2 surface 模式）

**1. `workspace/<module>/contract.md` § <定位段>（在 line N 后插入 / 替换 line N-M）**

```markdown
<完整 entry 文本，可直接复制到 contract.md>
```

**2. `workspace/<module>/architecture.md` § <定位段>**

```markdown
<完整 entry 文本>
```

**3. `workspace/<module>/module.json` § <字段路径>**

```json
{
  "<字段>": "<新值>"
}
```
```

### 3.3 Architect 合规验收时同 commit 写入

收到 worker Reply + 验收 PASS 后，architect 在**同一 commit** 写入 surface 内容，commit message 标注：

```
docs(<module>): worker <hash> 合规验收 — N 段 contract/architecture 落盘
```

---

## 4. Architect 合规验收 Checklist

收到 worker Reply 后，architect 按以下 checklist 验：

### 4.1 commit 真实性

```bash
git show <commit_hash> --stat
# 文件清单与 Reply 描述一致
# 添加/删除/修改行数与描述一致
```

### 4.2 build / test 真实性

```bash
cd tscript-lit/src-tauri && cargo build --release  # 0 错 0 警
cd tscript-lit/src-tauri && cargo test             # 全绿
cd tscript-lit && npx tsc -b                       # 0 错（涉及前端时）
```

### 4.3 Treaty 合规（CAVEAT 2）

```bash
git show <commit_hash> --name-only | grep -E "contract\.md|architecture\.md|module\.json"
# 期望：0 命中（worker 严守 CAVEAT 2）

git show <commit_hash> --name-only | grep "changelogs/"
# 期望：worker 已 append（CAVEAT 2 豁免）
```

### 4.4 Reviewer subagent 报告

- worker Reply 内含 reviewer subagent 报告（PASS / PASS-WITH-CAVEATS）
- caveats 项有处置（surface / 推后 / 关单时附说明）

### 4.5 Surface 内容完整性

- contract / architecture / module.json 各段都列了
- 每段有定位 § / 行号 / 完整文本（可直接复制）
- 与 worker 实装的代码语义 100% 一致（抽样校验）

### 4.6 checklist 失败处理

任一条失败 → 不关单 + 在 task 文件 ## Reply 段写反推给 worker（列失败项 + 要求修正）；不自行补救（不写 worker 应写的代码）。

---

## 5. 删除/下线类 Task 专项 SOP

### 5.1 派前预扫（architect 执行）

```bash
grep -rn "<被删路径>" . \
  --exclude-dir=node_modules \
  --exclude-dir=.git \
  --exclude-dir=references
```

按「必清 / 必留 / 歧义」分类，填入 task spec。

### 5.2 合法残留白名单（4 类，不是 3 类）

| 类型 | 路径 |
|---|---|
| 根 CHANGELOG | `CHANGELOG.md` |
| workspace changelog | `.agentic-os/workspace/<root>/changelogs/changelog.md` |
| channel 历史 message | `.agentic-os/channel/` |
| mindspace 历史 memory | `.agentic-os/mindspace/` |

### 5.3 验收复扫（收到 worker commit 后第一动作）

```bash
grep -rn "<被删路径>" . \
  --exclude-dir=node_modules \
  --exclude-dir=.git \
  --exclude-dir=references
```

复扫 hit 与合法残留白名单对账；任何超出 hit = 遗漏，反推 worker 补清。

---

## 6. 跨 Agent 协作映射表

| from | to | 场景 | 载体 | 是否走 channel |
|---|---|---|---|---|
| architect | worker | 派任务 | task | ✅ |
| architect | manager | retro / 升格 / 治理 | message | ✅ |
| architect | reviewer | 对抗审查 | Agent tool subagent | ❌ session 内 |
| worker | architect | 实装 Reply / surface | task 文件 ## Reply 段 | ✅ |
| worker | reviewer | 对抗审 | Agent tool subagent | ❌ session 内 |
| worker | architect | 求裁决 | message | ✅ |
| manager | architect | lesson 升格 / SOP / identity | message + identity 修订 | ✅ |

---

## 7. Demo / 紧急场景例外

### Demo P0 阻塞

- 仍须派 worker task，priority: critical + 极简 description
- 可省：详细背景 / 多余 test edge case
- 不可省：reviewer subagent / Reply surface 内容 / treaty 合规
- 预算时间：critical task ≈ agent 5-15 分钟（demo 节奏可接受）

### Demo 暂停合法触发条件

任一条 → demo 暂停 + 走 manager retro 路径：
- 用户当面指出架构师身份越界
- 架构师识别模块 SRP 严重违规（constraints > 12）
- 架构师识别 schema / contract 与代码严重漂移（grep 验证失败 > 3 条）
- 架构师本人 lesson 累积 > 3 条未升格

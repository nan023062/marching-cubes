# 决策：Channel 文件命名规范 + 派 Task 职能归属

> **决策者**：manager（人事管理）  
> **日期**：2026-04-25  
> **触发事件**：director→reviewer + coder→worker 重命名后，channel 历史文件名/frontmatter 出现大规模旧名义残留，引发责任链断裂与命名虚假问题。  
> **文档引用**：`channel/2026-04-25-task-reviewer-to-worker-kernel-p1-batch-kickoff.md` § Reply 末尾 surface；user 直派本清账任务（reviewer 起草）。  
> **reviewer 对抗审查**：本决策文档不涉及 agent 三件套修改/升格，manager 判断无需调起 reviewer。若后续涉及 identity.md 修改则需补审。

---

## 1. frontmatter from/to 合法字典

channel 文件 frontmatter 的 `from` / `to` 字段**只能使用**以下 agent 名：

| 名称 | 身份 | 备注 |
|------|------|------|
| `manager` | 人事管理（人事治理） | 当前已注册 |
| `architect` | 架构师（设计/知识治理） | 当前已注册 |
| `reviewer` | 评审官（对抗审查，subagent） | 当前已注册；**不走 channel**（见 §5 注释） |
| `worker` | 一线执行者（当前默认：编程） | 当前已注册；MVP 后可扩展 `xx-worker` |
| `user` | 用户（直派角色） | 非 agent，合法 from 发起方 |

> **reviewer 特殊注释**：reviewer 定位为纯 subagent，**不应主动出现在 channel to 字段**（因为 reviewer 不接收 channel 消息，不接任务）。若 channel 里有 `to: reviewer` 说明是过渡期的一次性历史遗留（2026-04-25 批量清账期间），不作为规范用法。  
> **未来扩展**：`test-worker` / `doc-worker` / `ops-worker` 等按继承规则注册后可加入此字典。

---

## 2. 文件名规范

```
YYYY-MM-DD-<type>-<from>-to-<to>-<slug>.md
```

| 段 | 规则 |
|---|---|
| `YYYY-MM-DD` | 文件创建日期 |
| `<type>` | `task` / `message` / `review` / `incident`（可扩展） |
| `<from>` | 发起方 agent 名（见 §1 字典） |
| `<to>` | 接收方 agent 名（见 §1 字典）；多人用逗号分隔时取**主接收方**命名 |
| `<slug>` | 主题摘要（kebab-case，< 40 字符，无特殊字符） |

**示例（合规）**：
- `2026-04-25-task-architect-to-worker-implement-filesys-module.md`
- `2026-04-25-review-worker-to-architect-filesys-mvp-acceptance.md`
- `2026-04-25-message-manager-to-architect-memory-upgrade-audit.md`
- `2026-04-25-task-user-to-worker-fix-pty-hang-on-macos.md`

**禁止**：
- 文件名出现字典外 agent 名（`director` / `coder` 等旧名义已废弃）
- `to: reviewer` 的新建 channel 文件（reviewer 不走 channel）
- slug 含空格 / CJK / 特殊符号

---

## 3. renaming 兼容窗口：归档而非重命名

**裁定**：**旧文件归档（mv → archived/），保留原文件名，不重命名**。

| 选项 | 分析 |
|---|---|
| **强制重命名** | 破坏 git log 中的 blame 链；已有外部引用（PR comment / 其他 md 文件 link）会 404；成本高于收益 |
| **归档保留原名** | git history 可 `git log --follow` 追溯；archived/ 本身即语义隔离标记；旧名义只在 archived/ 中存在，不污染活跃命名空间 |
| **redirect stub** | 对 markdown 文件价值极低（无 HTTP 重定向语义），不采用 |

**例外**：活跃（非 resolved）文件被发现有旧名义时，**直接 git mv 改名**（2026-04-25 批量清账已执行此路）。

---

## 4. Agent rename 触发的 Channel Migration SOP

未来 agent 改名时（如 `worker` 扩展为 `rust-worker` / `py-worker`），按以下 SOP 处置：

### Step 1 — 三件套 + .claude/agents/ 同步（architect + manager 协作）
- `git mv mindspace/<dept>/<old>/ mindspace/<dept>/<new>/`
- `git mv .claude/agents/<old>.md .claude/agents/<new>.md`
- 更新新目录内 agent.json `name` 字段
- 更新 `.claude/agents/<new>.md` frontmatter `name` 字段

### Step 2 — SessionStart hook 验证
- `bash .claude/hooks/session-start-guard.sh` 无报错
- 新 agent 名出现在合法清单，旧名已移除

### Step 3 — Channel 活跃文件扫描（manager 执行，本 SOP 核心）
```bash
for f in .agentic-os/channel/*.md; do
  status=$(grep -m1 "^status:" "$f" | sed 's/status: *//')
  from=$(grep -m1 "^from:" "$f" | sed 's/from: *//')
  to=$(grep -m1 "^to:" "$f" | sed 's/to: *//')
  # 检查旧名义
done
```
- `resolved` / `cancelled` 文件 → `git mv` 到 `archived/`（保留原名）
- 活跃文件 frontmatter `from`/`to` → 就地 edit（更新为新名）
- 活跃文件名含旧名义 → `git mv` 改名（新名义）
- **禁止 `git rm` / `rm`**（见 `hr/manager/lessons/lesson_channel_file_governance.md`）

### Step 4 — 验证 + commit
- 再次扫描，确认活跃目录 0 旧名义残留
- `git add .agentic-os/channel/` + commit `refactor(channel): <old>→<new> agent rename migration`

### Step 5 — 归档文件 from/to 字段说明
归档文件保留旧 `from`/`to` 字段值，**不更新**（保持历史原貌，archived/ 隔离语义已足够）。

---

## 5. Frontmatter 必填字段说明（2026-05-06 追加）

> **触发事件**：全部 8 个 channel task 文件使用 `slug:` 作为 frontmatter 字段，导致解析器持续报 `missing required field 'topic'`（日志 `2026-05-06.jsonl`，每 5 秒一次）。

### `slug` vs `topic` 区分

| 位置 | 字段名 | 说明 |
|------|--------|------|
| **文件名** | `<slug>` | kebab-case 主题摘要，仅用于文件命名（见 §2）|
| **frontmatter** | `topic:` | 必填，一句话主题（中文可）；**不是** `slug:` |

**错误写法**（已全量修复）：
```yaml
slug: art-mesh-tool
```

**正确写法**：
```yaml
topic: Art Mesh Marching Cube 配置工具
```

`slug` 不是合法的 frontmatter 字段。解析器只认 `topic`。

### 完整必填 frontmatter 字段

```yaml
type: task|message|review
from: <agent>
to: <agent>
date: YYYY-MM-DD
topic: <一句话主题>
status: open|in_progress|on_hold|resolved|cancelled
priority: urgent|high|normal|low
```

`module` / `seq` / `blocked_by` / `related` 为可选字段（见 task-dispatch-template.md）。

---

## 6. 派 Task 职能归属（关键决策）

### 历史问题

前 `director` 角色同时承担两件事：
1. 产品/任务规划 + 向 worker 派 task（任务分解 + channel 投递）
2. 对抗性技术审查（挑战 worker 交付 + 质疑 architect 设计）

重命名后：`director → reviewer`（仅保留职能 2，subagent 模式，不走 channel）。  
**职能 1 悬空**，无 agent 显式承接。

### 选项分析

| 选项 | 描述 | 优 | 劣 |
|------|------|----|----|
| **A. 新建 dispatcher agent** | 专职任务分解 + 派单 | 职责纯粹 | 过早设计；当前 task 量级不需要专职 |
| **B. architect 兼任** | 架构设计自然含任务分解，architect 直接派 task 给 worker | 语义自洽（设计者知道任务边界）；已有实例（`task-architect-to-worker-*`）| architect 是知识治理角色，派 task 是运营行为，轻微职责扩张 |
| **C. manager 兼任** | 治理职能含资源调配，manager 派 task | 语义合理（人事管理含任务分配）| manager 主职是记忆治理，不是技术任务分解 |
| **D. user 直派** | user 直接写 task 文件投入 channel | 最小主义；无过度设计 | 需 user 手动写 frontmatter；批量任务时繁琐 |
| **E. reviewer 起草 + user 直派** | reviewer 起草 task 文本，user 复制到 channel 投递 | 复用 reviewer 的结构化能力；user 保留最终控制权 | 多一步手动操作 |

### 决策

**短期（当前 MVP）：选 D+E（user 直派为主，reviewer 辅助起草）**

理由：
1. 2026-04-25 本任务即是 E 的实际验证（reviewer 起草 task 文本，user 直派）——已有真实样本，运行良好
2. `task-architect-to-worker-*` 形式（B）已自然出现，说明 architect 在设计完成后天然会分解任务
3. 过早专职化（A）会在 agent 数量少（当前 4 个）时带来过度开销
4. 尚无充分样本判断 B vs C vs D 谁更优

**观测触发器（累计 ≥5 真实 task 创建样本后重评）**：

| 若观察到 | 则考虑升格 |
|---|---|
| architect 每次设计完都主动写任务拆分 + 投 channel | 选 B（architect 兼任） |
| 多数 task 是跨模块协调/人事安排 | 选 C（manager 兼任） |
| user 频繁手写 task 感到繁琐 + reviewer 每次起草质量高 | 保持 E 或转 A |
| 任务并发量 > 5 个/天 | 重新评估 A |

**当前约束**：
- `reviewer/identity.md` 明确"不走 channel、不产生 channel 文件"——reviewer 只能起草文本，不能直接写入 channel 文件（需 user 确认投递）
- architect 可以且已经直接写 `task-architect-to-worker-*`，此为 B 的自然延伸，**不需要额外决策**，允许继续

**中期记录**：本决策待 ≥5 样本后 manager 复盘升格。

---

## 附录 A：2026-04-25 Channel Migration 执行记录（本决策的实战数据点）

### 触发原因

`director → reviewer` + `coder → worker` 重命名落 git 后，channel 文件的 frontmatter 和文件名未同步更新。

### 处置矩阵（S2 拍板结果）

| 文件 | 处置 | 原因 |
|------|------|------|
| `2026-04-24-review-architect-to-director-design-and-readme-cleanup.md` | git mv → `...-to-reviewer-...`；from/to 字段已更新 | 活跃（needs-rework），直接改名 |
| `2026-04-24-task-director-to-coder-agent-plugins-iagentplugin-mvp-kickoff.md` | git mv → `task-reviewer-to-worker-...`；from/to 更新 | 活跃（open） |
| `2026-04-24-task-director-to-coder-release-mvp-kickoff.md` | 同上 | 活跃（open） |
| `2026-04-24-task-director-to-coder-scheduler-mvp-kickoff.md` | 同上 | 活跃（open） |
| `2026-04-24-task-director-to-coder-tauri-ui-mvp-kickoff.md` | 同上 | 活跃（open） |
| `2026-04-25-message-director-to-architect-agent-plugins-contract-d3-and-security.md` | git mv → `message-reviewer-to-architect-...`；from 更新；status 已 resolved → 补归档 | 已 resolved，归档 |
| `2026-04-25-message-director-to-manager-channel-archived-governance.md` | git mv → archived/（保留原名） | resolved |
| `2026-04-25-review-coder-to-director-*` (4 份) | git mv → archived/（保留原名） | 全部 resolved |
| `2026-04-25-task-director-to-architect-module-triplet-closure-gap.md` | git mv → `task-reviewer-to-architect-...`；from 更新 | 活跃（open） |
| `2026-04-25-task-director-to-coder-agent-plugins-installer-fix-path-traversal.md` | git mv → `task-reviewer-to-worker-...`；from 更新 | 活跃（in-review） |
| `2026-04-25-task-director-to-coder-kernel-p1-batch-kickoff.md` | git mv → `task-reviewer-to-worker-...`；from 更新 | 活跃（in-review） |
| `2026-04-25-task-director-to-coder-agent-plugins-fix-cfg-windows-drive-test.md` | git mv → archived/（保留原名） | resolved |
| `2026-04-25-task-architect-to-coder-tauri-src-readme-skeleton.md` | git mv → `task-architect-to-worker-...` | 活跃（open），`to` 字段已更新 |

### 执行结果（commit `2273fb2`）

- 活跃 channel 文件：**全部 CLEAN**（0 director/coder 残留）
- archived/：11 个历史文件，保留原名（符合 §3 归档保留原名原则）
- 未完成（S4 补执行）：`2026-04-25-message-reviewer-to-architect-agent-plugins-contract-d3-and-security.md` status=resolved 但仍在活跃目录 → 本次 S4 归档

---

*由 manager 于 2026-04-25 制定，reviewer 对抗审查：不涉及 agent 三件套修改，豁免。*

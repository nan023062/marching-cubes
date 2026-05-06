---
type: lesson
created-at: 2026-04-26
created-by: manager
source-channel: channel/2026-04-25-message-architect-to-manager-cbim-crud-tests-residue-and-rename-sweep-workflow.md
source-commit: e9a7ab2
related-tasks: [cbim-crud-tests-coder-to-worker-rename]
---

# 拓扑重命名 commit 必须配套全仓 grep sweep

## 事件

`e9a7ab2 refactor(mindspace,channel,claude): 组织拓扑重命名`（`planning/director → planning/reviewer`、`tech/coder → tech/worker`）未做全仓 sweep，导致：

1. `cbim/storage/tests.rs:39` 硬编码 `agent_dir("planning", "director")` — stale 路径，测试持续 3 天语义错误
2. `cbim/crud/{reads.rs, error.rs, mod.rs}` 3 处文档注释仍含 `director` 字眼
3. 可能还有未发现的其他子模块残留（当时 worker 只 grep 了 cbim/，未全仓 sweep）

**真实成本**：3 天测试红线 + 至少 4 处残留 + 1 个 cbim-crud-tests 修复 task + 后续 sweep 任务的全部 channel 开销。全仓 grep 仅需 5 分钟即可在 commit 时发现并修复。

## 根因

agent 拓扑重命名影响范围横跨 mindspace / channel / workspace / src-tauri（测试路径字符串、文档注释、硬编码 agent_dir），但操作者只关注了 mindspace + channel 文件目录的改名，未对旧名称做全仓文本搜索。

## 准则

**任何 agent 或组织拓扑 rename commit，提交前必须执行全仓旧名称 grep sweep，确认 0 命中后才可 commit。**

```bash
# 示例（将 old_name 替换为实际旧名称）
grep -r "old_name" . \
  --include="*.md" --include="*.rs" --include="*.ts" --include="*.tsx" \
  --include="*.json" --include="*.toml" \
  --exclude-dir=".git" --exclude-dir="node_modules" --exclude-dir="target"
```

sweep 范围须包含：
- 文档文本（`.md`）— mindspace / channel / workspace 全部
- 代码字符串（`.rs` / `.ts` / `.tsx`）— 含测试文件内的硬编码路径字符串
- 配置文件（`.json` / `.toml`）— agent.json / module.json / Cargo.toml 等
- 必要时加 `--include="*.yaml"` / `--include="*.yml"`

发现残留时，≤ 30 行 / 单一根因 → 走 housekeeping commit 一次性消除（参见 `mindspace/tech/architect/identity.md` § 注意 § Surgical fix 阈值）。

## 配套 SOP

拓扑重命名全流程 SOP 待升格为 `workspace/tscript-lit/workflows/topology-rename-sweep/workflow.md`（architect 创建）。

## 对冲动作

- architect identity § 注意：已补 Surgical fix 阈值准则（2026-04-26，支撑残留修复的低成本路径）
- 2 处已知残留（`cbim/storage/tests.rs:39` + `cbim/crud/` 3 文件注释）待 architect 派 worker 走 housekeeping commit 消除

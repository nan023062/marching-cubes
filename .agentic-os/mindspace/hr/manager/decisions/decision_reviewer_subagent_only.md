---
type: decision
created-at: 2026-04-25
confirmed-by: user
scope: project-wide
status: future — 当前不实现，设计向此对齐
---

# 评审官（reviewer）定位：纯 Subagent，不作为主 Agent 对话

## 决策

**评审官（reviewer，原 director）不作为主 Agent 启动**，`claude --agent reviewer` 交互模式为过渡期兼容；
正式模式：由其他 Agent（架构师、人事管理）通过 `Agent` tool 作为 **subagent 就地调起**，执行对抗审查后返回报告，由调用方 agent + 用户决定后续。

## 当前 vs 未来

| 维度 | 当前 | 未来 |
|------|------|------|
| 启动方式 | `claude --agent reviewer`（过渡期兼容） | 由 architect/manager 用 `Agent` tool 调起 |
| 触发者 | 用户手动启动 | architect（设计审查）/ manager（治理审查） |
| 对话模式 | 有 | 无（subagent 执行后返回报告） |
| `.claude/agents/director.md` | 必须存在 | 必须存在（subagent 也需要 agent 定义文件） |

## 原因

1. **角色性质决定**：director 的职责是"对抗性批判"，不是"协作对话"；subagent 执行更符合其无主观意志的批判定位
2. **流程内嵌**：审查是 architect / manager 工作流的一个步骤，嵌入比独立对话更自然
3. **减少用户操作**：用户不需要手动启动 director、拷贝上下文、等待结果——触发 agent 自动完成闭环

## 当前约束（不提前实现）

- `claude --agent director` 暂时保留，过渡期可手动触发
- architect / manager 的 `agent.json` 目前 **不加** `Agent` tool 依赖（等 Kernel 调度能力就绪后统一实现）
- director 的 `.claude/agents/director.md` 保持现状，未来 subagent 调起时直接复用

## 对当前设计的影响

- director identity / soul **不受影响**（对抗能力不变）
- 当前 director 接收 channel 任务的方式（扫描 to=director 文件）可过渡期保留，未来由 Kernel 调度替代
- **architect 和 manager 的 tools 列表暂不加 `Agent`**，等 subagent 编排正式落地时一并加

## 自检问题

> 「设计 director 新功能时，先问：这个功能是否只需要读取上下文 + 输出批判报告？是 → 维持 subagent 定位；需要持续对话 → 重新讨论。」

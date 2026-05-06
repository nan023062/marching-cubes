---
type: lesson
created-at: 2026-04-25
created-by: manager
source-channel: channel/2026-04-25-message-worker-to-manager-review-depth-degradation.md
source-session: mindspace/tech/worker/sessions/linan-2026-04-25.md
related-tasks: [T5b1-fix, T5b1-fix-3]
---

# 审查深度递减：对账验收 ≠ 对抗性威胁建模

## 事件

T5b1-fix（installPaths 路径穿越安全漏洞修复）终态复审时，用户要求 worker 拉起 reviewer subagent 做独立第三方对抗复审。reviewer 推翻前任 director 评分：PASS 9/10 → CONDITIONAL-PASS 7/10，挖出 **4 项前任漏审**（NUL 字节注入 / 前导空白绕过规则 / uninstall/detect 路径未钉死 / 错误 reason 含未转义控制字符）。

reviewer 跑了 31 个对抗用例，4 个 ACCEPT 立即 surface；前任 director 一审二审都未做此步骤。

## 根因

**前任审查模式：对账验收**  
论据 = "task 单 4 必选 + 2 加分通过 + 5 规则覆盖契约" → PASS

**应有审查模式：对抗性威胁建模**  
问题 = "这 5 规则覆盖了哪些威胁向量？还有哪些边界条件会让规则失效？" → 主动构造反例

两种模式的本质区别：
- 对账验收：验证"声明的正确"
- 威胁建模：搜索"未声明的错误"

## 三条准则

### 准则 1：安全相关代码审查必须包含对抗性边界探测

安全 review 不止于"功能正确"，还需：
- 主动构造威胁向量（NUL 注入 / Unicode 覆写 / 前导/尾随空白 / 大小写变形 / 符号链接 / 混合分隔符等）
- 对每条防御规则问"在哪些边界条件下会失效？"
- 至少覆盖 N≥10 个对抗用例再给出 PASS（N 的具体下限由 reviewer 根据漏洞面大小判定）

### 准则 2：评分计数器（"零漂移 N=X"）是自陈数据，无外部校准则质量下限被高估

自陈计数器（如"实装事实层零漂移 N=8"）只能反映被审查者的报告准确性，无法反映审查者本身的深度。
- reviewer 一次复审让 N=8 中 1 个出现漏审 → 说明自陈基线有水分
- 类比：测试通过 ≠ 没 bug；review PASS ≠ 没漏洞
- 对冲：在累积 N≥5 前克制升格；必要时引入周期性外部校准（如 manager 按比例抽样让 reviewer 复审历史 PASS）

### 准则 3：知识层负债须在修复时同步偿还，不能只记 review 文档

本次 fix-2 "5 规则在 Linux 路径下的等价防御边界"是 contract 级洞察，应进 contract.md 注释；前任 director 只记录在 review 文档里，audit 链断裂。

**规则**：修复涉及"边界行为 / 平台差异 / 设计意图"时，审查者须同时检查知识层（contract.md / architecture.md）是否有对应声明，若无 → 列为 audit 项派给架构师补充。

## 对冲动作

- reviewer identity.md：已补"对抗性审查最低门槛"约束（待 architect 落盘）
- worker 本次已当批补强（fix-3，41 全绿）——审查发现不影响最终合入

## 普遍性

适用于所有涉及安全、路径校验、权限控制的代码 review（不限 agent-plugins，延伸到未来 filesys / kernel / cbim 等含安全敏感路径的模块）。

# Skill 轴运行时入口（导航指针）

> Skill = **跟 agent 走的工具**。位置即作用域。

## 设计源真相 → 见 cbim 模块

- **完整设计 / 解析规则 / 治理流程 / schema** → `workspace/tscript-lit/cbim/`
  - `cbim/structure/architecture.md` —— 物理布局 + Skill 同级邻居作用域规则
  - `cbim/structure/contract.md` —— Skill schema（frontmatter）
  - `cbim/crud/architecture.md` —— Skill 编辑权限矩阵 + 升格流程
  - `cbim/crud/contract.md` —— Skill CRUD API（list / read / create / promote）
- **CBIM 系统总览 + AME 本体** → `workspace/tscript-lit/cbim/architecture.md`
- **4 轴矩阵宪章** → `workspace/architecture.md` § CBIM 系统
- **CBI 哲学（agent/skill 内容约束）** → `design/capability-business-independence.md`

## 物理目录约定（速查）

```
mindspace/
├── skills/                 ← 根级（与根级 agent 同级；当前无 agent 可见）
├── <dept>/
│   ├── skills/             ← 部门级（部门内 agent 共用）
│   └── <agent>/
│       └── skills/         ← agent 私有
```

详细规则、解析算法、举例 → 见 `cbim/structure/architecture.md`。

## 升格通路（速查）

agent 私有 → 同级共享（单台阶）。详见 `cbim/crud/architecture.md` § 升格流程契约。

# Workspace 结构图

```
workspace/
│
├── module.json                        ← 根级模块注册表
├── architecture.md                    ← 根级架构总览
├── contract.md                        ← 根级 API 契约
├── changelogs/
│   └── changelog.md                   ← 根级变更日志（append-only）
│
├── building-system/                   ── L1 模块 ──
│   ├── module.json
│   ├── architecture.md
│   ├── contract.md
│   ├── changelogs/
│   │   └── changelog.md
│   ├── building-system.zip
│   ├── marching-squares/              ── L2 子模块 ──
│   │   ├── module.json
│   │   ├── architecture.md
│   │   ├── contract.md
│   │   └── changelogs/
│   │       └── changelog.md
│   └── mc-building/                   ── L2 子模块 ──
│       ├── module.json
│       ├── architecture.md
│       ├── contract.md
│       └── changelogs/
│           └── changelog.md
│
├── marching-cubes/                    ── L1 模块 ──
│   ├── module.json
│   ├── architecture.md
│   ├── contract.md
│   ├── changelogs/
│   │   └── changelog.md
│   └── art-mesh-blender/              ── L2 子模块 ──
│       ├── module.json
│       ├── architecture.md
│       ├── contract.md
│       └── changelogs/
│           └── changelog.md
│
├── marching-squares/                  ── L1 模块 ──
│   ├── module.json
│   ├── architecture.md
│   ├── contract.md
│   └── changelogs/
│       └── changelog.md
│
├── dual-contouring/                   ── L1 模块 ──
│   ├── module.json
│   ├── architecture.md
│   ├── contract.md
│   └── changelogs/
│       └── changelog.md
│
└── mine-oasis/                        ── L1 模块 ──
    ├── module.json
    ├── architecture.md
    ├── contract.md
    └── changelogs/
        └── changelog.md
```

## 每个模块四件套说明

| 文件 | 职责 | 编辑权 |
|------|------|--------|
| `module.json` | 模块名、版本、依赖、constraints | architect |
| `architecture.md` | 架构决策、组件拆分、数据流 | architect |
| `contract.md` | 对外 API 定义、接口签名、行为约定 | architect |
| `changelogs/changelog.md` | append-only 变更历史 + incident | manager 编辑，worker 可 append |

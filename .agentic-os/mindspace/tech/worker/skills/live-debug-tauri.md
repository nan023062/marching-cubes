---
name: 真机调试 (Tauri dev 拉起 tscript-lit 真机)
created-by: worker
category: 运维流程
triggers:
  - 真机调试
  - 拉起真机
  - 跑一下看看
  - 看效果
  - dev 模式
  - tauri dev
  - 启动 app
  - 跑 demo
related-skills: []
---

# 真机调试 — Tauri dev 拉起 tscript-lit 桌面应用

> **触发场景**：用户说「真机调试」/「拉起真机」/「跑一下看看效果」等任意自然语言变体 → worker 按本 skill 一键给出拉起指令；不再每次重新推理参数。

---

## 0. 前置假设（已验证 ✓）

- **平台**：Windows（用户主机）
- **项目根**：`D:\GitRepository\agentic-os`
- **前端就绪**：`tscript-lit/node_modules/` 已 install（首次需 `cd tscript-lit && npm install`）
- **后端就绪**：`tscript-lit/src-tauri/target/debug/aos.exe` 已编译过（首次需 cargo build；已在 worker memory [2026-04-25] 工作流约定段记录 vcvars64 wrapper 模式作为 fallback）
- **Shell**：**PowerShell**（不要 git-bash — git-bash 的 PATH 把 coreutils `/usr/bin/link.exe` 排在 MSVC link.exe 前会让 cargo 链接报"extra operand"错；详见 worker memory [2026-04-25]）

---

## 1. 一键拉起指令（默认推荐 — dev 模式）

worker 直接给用户敲：

```powershell
cd D:\GitRepository\agentic-os\tscript-lit
$env:AGENTICOS_PROJECT_PATH = "D:\GitRepository\agentic-os"
npm run dev
```

**3 行做的事**：
1. cwd 切到 tscript-lit（vite + tauri 配置都在这里）
2. **关键**：设环境变量让 backend 知道项目根（否则 `get_project_path()` fallback 用 `std::env::current_dir()` = `src-tauri/`，报「Not an AgenticOS project: missing .agentic-os/」）
3. tauri dev = vite 起前端 + cargo 增量编译 backend + 启动 webview

**期望输出**（按顺序）：
- `VITE v6.4.2 ready in XXms` (前端)
- `Compiling agentic-os v0.x.0` ... `Finished` (后端 — 无代码改动 ~5s 增量；重大改动可能 1-3 min)
- Webview 弹窗 = 4 panel 布局（Topology / Inspector / Log / Task）+ TopologyPanel 显示 5 个 agents（architect / worker / designer / manager / reviewer）

---

## 2. 启动期常见报错速查

| 报错 | 根因 | 现场修法（surgical） |
|---|---|---|
| `Not an AgenticOS project: ... missing .agentic-os/` | 没设 `AGENTICOS_PROJECT_PATH` env var | 关 dev (Ctrl-C) → 重设 env var → 重 `npm run dev` |
| `error: linker 'link.exe' failed: extra operand` | git-bash PATH 把 coreutils link 排前 | 切 PowerShell；如必须 git-bash，跑 worker memory [2026-04-25] 的 vcvars64 wrapper |
| 白屏 / React error overlay | 前端 bug（最近一次改动 regression） | 在 webview 里按 F12 / 右键 → Inspect 看 console；典型："Cannot read property X of null" → 改对应 hook / Provider |
| `Module not found: lucide-react` 类 | npm install 没跑（首次 / 切分支后） | `npm install` 等完后再 `npm run dev` |
| `cargo: command not found` | Rust toolchain 没装 / PATH 缺 | 装 `rustup default stable` |
| 启动卡 `Compiling` 几分钟 | 第一次 dev 编译 / 大改动 | 正常等；后续增量秒级 |

---

## 3. release 模式（非默认 — 用户明示需要时才用）

```powershell
cd D:\GitRepository\agentic-os\tscript-lit
$env:AGENTICOS_PROJECT_PATH = "D:\GitRepository\agentic-os"
npm run build
# build 完产物在 src-tauri/target/release/bundle/
```

**何时用 release**：
- 验证生产二进制行为（typically R2-MVP 终态验收）
- dev 模式无法复现的 bug（极少见）

**代价**：Windows 全 release build ~10-20 min（vs dev ~30s 增量）

---

## 4. 关闭流程

- **关 webview 窗口**：自动触发 Tauri `WindowEvent::CloseRequested`；W0-W4 实装后会执行 stop_all_agents 清理（**当前未实装** — 关窗时 PTY claude-code 子进程会变 orphan，需手动 Task Manager 清；R5-W0 task 已 surface BLOCKER on_hold）
- **Ctrl-C dev terminal**：关 vite + cargo build watch；不会自动 kill 已 spawn 的 claude-code 子进程（同上 orphan 问题）
- **手工清 orphan**：PowerShell `Get-Process claude* | Stop-Process` 或 Task Manager 杀

---

## 5. dev session 中 worker 的现场支援边界

按用户授权 (`2026-04-27` demo session 现场修权)：

| 场景 | worker 处置 |
|---|---|
| **P0 阻塞**（demo 跑不通）| 当场 surgical fix：grep / 读源码 / ≤ 50 行改动 / 单一原因 / append changelog incident |
| **环境配置**（设 env var / 改 PowerShell wrapper） | 当场指导用户敲指令，不改代码 |
| **架构级改动**（> 50 行 / 跨模块 / 改 contract）| 停下 surface 给用户决策是否拉 architect |
| **demo 发现 P1/P2 bug** | changelog incident 登记 + 不阻塞 demo 推进 |

---

## 6. 跑 demo 时的关键观测点（task L82-90 的 R2-MVP-ui 路径）

```
1. App 加载 → TopologyPanel 显示 5 agents ✓
2. 选 agent A → AgentInspector 出 ▶ Start 按钮 ✓
3. 点 ▶ Start → AgentInspector 内嵌 XtermView 显示 claude TUI banner
   ★ 观测：claude TUI 完整渲染（无错位 / 无 CJK 乱码 / 无单行覆盖）
4. xterm 内给 agent A 下指令通过 channel 写 task 给 agent B
5. grep .agentic-os/channel/ 看新 task 文件落盘
   ★ 观测：frontmatter 完整 + status: open
6. 切 agent B → ▶ Start → agent B 接 task → reply 落盘
   ★ 观测：channel 文件 status 变更（open → in_progress / resolved）
```

---

## 7. 关联

- task：`channel/2026-04-25-task-architect-to-worker-t6c-2-mvp-demo-realmachine-cohort.md`
- worker memory：[2026-04-25] § "工作流约定 — 临时文件与跨平台测试运行" / [2026-04-26] UI-DOCK-NORMALIZE Tauri dragDropEnabled / [2026-04-27] UI-PTY-RENDER macOS GUI TERM 环境
- W0 BLOCKER（影响 stop_all_agents 实装）：`channel/2026-04-27-message-worker-to-architect-r5-w0-blocked-upstream-api-virtual.md`
- backend 入口：`tscript-lit/src-tauri/src/commands.rs` § `get_project_path` (L86) + `load_project` (L96)
- 前端入口：`tscript-lit/src/ui/app/hooks/useProjectData.ts`

---

## 8. 升级路径（本 skill 自我演进）

- 用户切到 macOS / Linux 跑 demo → 加 § macOS / § Linux 子节
- W0 backend 实装 PASS → 删 § 4 orphan 清理章节（WindowEvent 自动清）
- W3 S7 ProjectGate 落地 → § 1 删 env var 设定，改为"App 启动后从 ProjectGate 选项目"
- 多次现场修发现新 P0 模式 → § 2 速查表追加行

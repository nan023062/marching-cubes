---
name: memory-index-regen
description: 从各 memory/<slug>.md 条目 frontmatter 自动生成 MEMORY.md 索引；manager 复盘时执行；禁止人手 append MEMORY.md
applicable_agents: [manager]
owner: manager
created: 2026-04-29
source: |
  reviewer a5d4808af57019402 发现根本漏洞（MEMORY.md 角色定义模糊）；architect 2026-04-29 final 决策；
  greenlight: channel/archived/2026-04-29-message-architect-to-manager-dna-gc-greenlight-and-deliverables.md § 产物 #4
---

# Memory Index Regen（MEMORY.md 自动生成）

## 根因背景

reviewer + worker MEMORY.md 各达 685/693 行（≥200 行限制的 3.4x），根因不是个体懒，是**设计漏洞**：
- MEMORY.md 角色定义模糊 — "索引"还是"内容仓"？
- 没有规定 append 时拆文件的机械流程 → 默认堕入"直接 append"

**修复方案（architect final 决策）**：
- 每条 memory = 独立文件 `memory/<slug>.md`
- MEMORY.md = 由脚本从条目 frontmatter 自动生成
- 物理上不允许人手 append MEMORY.md

## 条目文件格式（memory/<slug>.md）

每个独立条目文件必须以以下 frontmatter 开头：

```yaml
---
name: <人类可读名称>
description: <一句话摘要，≤100 字>
type: feedback | decision | lesson | project | user | reference
tags: [tag1, tag2]           # 受控词表，见 skills/tag-vocabulary.md
trigger: <何时该 load 此条（一句话，必须含问题症状词而非解决方案词）>
created: YYYY-MM-DD
updated: YYYY-MM-DD          # 可选，若有更新
source: <来源 session 或 message 路径>   # 可选
---

# <name>

<条目正文...>
```

## MEMORY.md 生成脚本（bash）

```bash
#!/usr/bin/env bash
# memory-index-regen.sh
# 用法: bash memory-index-regen.sh <memory-dir>
# 示例: bash memory-index-regen.sh .agentic-os/mindspace/hr/manager/memory

MEMORY_DIR="${1:-.agentic-os/mindspace/hr/manager/memory}"
OUTPUT="$MEMORY_DIR/MEMORY.md"

echo "<!-- 自动生成，禁止手动编辑。上次生成: $(date '+%Y-%m-%d %H:%M') -->" > "$OUTPUT"
echo "" >> "$OUTPUT"
echo "## 中期记忆条目" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# 按 type 分组
for TYPE in feedback decision lesson project user reference; do
  ENTRIES=$(find "$MEMORY_DIR" -name "*.md" ! -name "MEMORY.md" -exec grep -l "^type: $TYPE" {} \; 2>/dev/null | sort)
  if [ -n "$ENTRIES" ]; then
    echo "### $(echo $TYPE | sed 's/./\u&/')" >> "$OUTPUT"
    echo "" >> "$OUTPUT"
    echo "| 文件 | 类型 | 摘要 |" >> "$OUTPUT"
    echo "|------|------|------|" >> "$OUTPUT"
    for FILE in $ENTRIES; do
      BASENAME=$(basename "$FILE")
      NAME=$(grep "^name:" "$FILE" | head -1 | sed 's/^name: //')
      DESC=$(grep "^description:" "$FILE" | head -1 | sed 's/^description: //')
      echo "| [$BASENAME]($BASENAME) | $TYPE | $DESC |" >> "$OUTPUT"
    done
    echo "" >> "$OUTPUT"
  fi
done
```

## 触发时机

1. **manager 复盘时（必做）**：每次复盘 Step A（中期编辑）结束后跑脚本重生成 MEMORY.md
2. **GC 完成后（必做）**：拆条目 / 删条目 / 升格删除后立即重生成
3. **非预期触发**：若发现 MEMORY.md 与 memory/*.md 实际文件不一致（如 manager 下次 session 加载发现索引过时）

## GC 流程（存量违规修复）

对超标的 MEMORY.md（> 300 行或条目数 > 25）执行：

### Step 1 — 拆条目

对 MEMORY.md 中每条现有条目：
1. 判断该条目是否已有独立 `memory/<slug>.md` 文件
2. 若无 → 创建独立文件，内容 = MEMORY.md 该条目内容 + 补全 frontmatter（name/description/type/tags/trigger）
3. 若有 → 跳过（条目已独立）

### Step 2 — 可移植性过滤

对每个独立条目文件，跑 `skills/portability-checklist.md` 检查：
- 通过且稳定 → 候选长期升格（复盘报告中列出，等用户确认）
- 不通过（project-bound）→ 保留在 memory/（但删除 MEMORY.md 内嵌内容）
- 过期/一次性 → 直接删除 .md 文件

### Step 3 — 重生成 MEMORY.md

跑上方脚本，从剩余条目文件重生成 MEMORY.md。

### Step 4 — 验证

```bash
wc -l <memory-dir>/MEMORY.md          # 验证 < 300 行
ls <memory-dir>/*.md | grep -v MEMORY | wc -l  # 验证条目数 < 25
```

## 禁止操作

- ❌ 任何 agent（含 manager 自己）手动 append MEMORY.md
- ❌ 直接编辑 MEMORY.md 内容（只有脚本可写）
- ❌ 将 MEMORY.md 作为内容仓使用（应为纯索引）

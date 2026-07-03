# 文本表格配置工具

把游戏里所有文本集中到一个多页签 Excel（`game_texts.xlsx`）里配置，改完一键转回游戏用的 JSON。中文、英文、日语配音文本都在同一行里配。

## 环境

```powershell
python -m pip install openpyxl
```

## 两条命令（在项目根目录运行）

```powershell
# JSON -> Excel：把当前所有文本导出成表格（第一次用 / 想按最新 JSON 刷新表格时）
python tools/text_table/text_table.py export

# Excel -> JSON：把表格里改好的文本写回游戏 JSON
python tools/text_table/text_table.py import
```

导入会先把旧 JSON 备份到 `tools/text_table/backups/<时间戳>/`，再覆盖写入。

日常流程：`export` 生成表格 → 用 Excel/WPS 打开 `game_texts.xlsx` 改文本 → `import` 写回。

## 页签说明

| 页签 | 对应文件 |
| --- | --- |
| `story` | `story.json` + `story_en.json`（普通线剧本） |
| `story_true` | `story_true.json` + `story_true_en.json`（真结局剧本） |
| `pet` | `pet_dialogues.json` + `pet_dialogues_en.json`（普通桌宠对白） |
| `pet_true` | `pet_dialogues_true.json` + `pet_dialogues_true_en.json`（真结局桌宠对白） |
| `ending_avg` | `ending_avg.json` + `ending_avg_en.json`（真结局悬浮 AVG 框） |
| `meta` | 起始节点、彩蛋节点、历史屏蔽语、AVG/桌宠里少量固定文本 |

## 列的含义

### story / story_true 页签（一行 = 一个剧情节点）

- `id`：节点唯一编号，**别改**（改了等于新建一个节点，跳转会对不上）。
- `speaker_zh` / `speaker_en`：说话人名，留空=旁白。
- `text_zh` / `text_en`：中 / 英正文。单元格里直接换行即可（会转成 JSON 的 `\n`）。可用 `%playername%` 这类变量。
- `text_ja`：这句的日语配音文本（喂给配音脚本用）。留空=不生成 / 不改这句配音。
- `voice`：配音文件名，留空时游戏默认用 `id` 当文件名。
- `background`：背景图名，留空=沿用上一张。
- `sprite` / `position`：立绘图名 / 位置（left/center/right）。`sprite` 留空时保留原立绘设置。
- `keepCharacters`：填 `TRUE` = 本句不改动当前立绘。
- `next`：点击继续跳到哪个节点（有选项时留空）。
- `ending`：`pet`=进桌宠结局，`lock`=锁定结局，留空=普通。
- `inputVariable` 等：需要玩家输入名字这类节点才填；`inputPlaceholder_zh/en`、`inputButtonText_zh/en` 是输入框提示 / 按钮文字。
- `secretName` / `secretStory` / `secretNext`：隐藏真名分支（输入 `secretName` 就切到 `secretStory` 的 `secretNext` 节点）。
- `choice1_zh/en/next` ~ `choice4_*`：最多 4 个选项，每个有中英文字 + 跳转目标。有选项时 `next` 留空。

### pet / pet_true 页签（一行 = 一句桌宠台词）

- `section`：`idle` 闲置 / `click` 点击 / `morning` `noon` `evening` `night` 时段问候 / `welcome` 回来欢迎 / `headpat` 摸头。
- `index`：同一分区里的顺序（1、2、3…）。
- `text_zh` / `text_en` / `text_ja`：中 / 英 / 日语配音文本。
- `voice`：配音文件名（日语按这个名字生成）。
- `sprite`：说这句时用的立绘。
- `duration`：气泡显示秒数。

### ending_avg 页签

- `section`：`enter` 进入时说的 / `lines` 平时随机说的。
- 其余列同上（text_zh/en/ja、voice、sprite）。

### meta 页签

少量全局文本：起始节点 `startNode`、彩蛋节点 `tamperNode`、历史屏蔽语 `historyBlockedMessage`、AVG 的 `speaker`/`sleepText`/`switchButtonText`、桌宠 `sleepText`。`startNode`/`tamperNode` 是节点 id，不用翻译，只填 `value_zh`。

## 会自动保留、不在表里的东西

导入时按 `id`（或分区+序号）与旧 JSON 合并，下面这些**不在表格里但会原样保留**，不会丢：

- 剧情节点里的 `setVariables`（变量赋值）、`conditionalNexts`（条件跳转）、编辑器坐标。
- 桌宠 / AVG 的所有计时参数（`idleMinSeconds`、`talkMinSeconds` 等）、各种 `*Sprite`、`animations` 帧动画。

需要改这些结构性配置时，仍直接编辑对应 JSON。

## 注意事项

- 表格是「文本层」：新增剧情节点可以在 story 页签加一行（至少填 `id`、`text_zh`、`next` 或选项）。**删除某一行 = 删掉那个节点**，请确认没有别的节点 `next` 指向它。
- 英文文件的结构（跳转、立绘、配音等）会自动照抄中文，只替换文字；所以别指望在英文里做和中文不一样的分支。
- `text_en` 留空时，导入会自动用 `text_zh` 兜底填进英文文件（不会出现空对话）。
- 导入会把 JSON 里原有的 `//` 注释洗掉（游戏加载不受影响）。想留说明就写在本文档或表格备注里。
- 改完文本若涉及配音，记得再跑 `python tools/tts/synthesize.py` 重新生成 `.wav`。

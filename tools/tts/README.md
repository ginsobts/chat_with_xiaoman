# 小满配音生成（GPT-SoVITS 声音克隆）

这套工具用 **GPT-SoVITS** 克隆一个专属音色，再批量把 `story.json` 里的每句台词合成成配音，
放进 `Assets/StreamingAssets/voice/`。游戏运行时会**按节点 id 自动匹配同名音频**播放，
所以生成完不需要改 `story.json`。

整体流程：**准备参考音频 → 装 GPT-SoVITS → 启动 api 服务 → 填 config → 跑脚本**。

---

## 0. 前置：你需要准备什么

- 一段**干净的参考人声**，5～10 秒，单人、无背景音、无音乐（wav 最佳）。
  - 这就是「小满」将要模仿的音色。可以是你自己录、朋友录、或你有权使用的音源。
  - 记下这段音频里**说的那句话的完整文字**（后面填 `promptText`）。
- 一张 N 卡（你有）。显存 6GB 起可用，8GB+ 更舒服。

> 版权提醒：克隆真人/他人声音请确保获得授权，仅用于你自己的项目。

---

## 1. 安装 GPT-SoVITS

推荐用官方整合包（Windows，免装环境，最省事）：

1. 打开项目主页：<https://github.com/RVC-Boss/GPT-SoVITS>
2. 在 README 里找到 **Windows 整合包 / one-click package**（通常放在 Releases 或说明里的网盘链接），下载并解压。
3. 解压后目录里会有类似 `go-webui.bat`、`api_v2.py`、`GPT_SoVITS/` 等文件。

（进阶：也可以 `git clone` 后按官方 `requirements.txt` 自建 conda 环境，但整合包对新手更快。）

---

## 2. 得到「小满」的音色（两种方式，二选一）

### 方式 A：零样本，直接用参考音频（最快，先听效果用这个）
不训练，直接把参考音频喂给 api 即可。适合快速验证。跳到第 3 步。

### 方式 B：少样本微调（效果更好，最终版用这个）
1. 双击 `go-webui.bat` 打开 WebUI。
2. 按界面步骤：**切分音频 → ASR 打标 → 训练 SoVITS + GPT 模型**（官方 WebUI 有中文引导）。
   - 训练素材：几分钟同一个人的干净录音即可。
3. 训练完成后，在 WebUI 里把生成的 **SoVITS 权重**和 **GPT 权重**设为当前模型。

> 建议：先用方式 A 跑通整条链路、确认满意，再回来用方式 B 训练替换。

---

## 3. 启动 api_v2 服务

在 GPT-SoVITS 目录下打开终端（整合包一般自带 runtime 环境），运行：

```bash
python api_v2.py -a 127.0.0.1 -p 9880
```

看到监听 `9880` 端口即成功。**保持这个窗口开着**。

> 若你用方式 B 的自训练模型，需要按官方说明用 `-t`/`-g` 参数或在配置里指定 GPT/SoVITS 权重路径，
> 让 api 加载的是「小满」的模型而不是默认底模。

---

## 4. 配置本脚本

在本目录（`tools/tts/`）：

1. 复制 `config.example.json` 为 `config.json`。
2. 填写关键项：
   - `refAudioPath`：参考音频的**绝对路径**（用正斜杠，如 `D:/xxx/ref.wav`）。
   - `promptText`：参考音频里那句话的完整文字。
   - `promptLang` / `textLang`：语言，中文填 `zh`。
   - 其他保持默认即可。
3. 安装脚本依赖：

```bash
pip install -r requirements.txt
```

---

## 5. 生成配音

先看看会合成哪些句子（不实际生成）：

```bash
python synthesize.py --list
```

确认无误后正式生成：

```bash
python synthesize.py
```

- 结果输出到 `Assets/StreamingAssets/voice/`，文件名即节点 id（如 `n1.wav`）。
- 已存在的文件默认跳过；想全部重做加 `--overwrite`。
- 只有「小满」说的、且有实际内容的台词会被配音（纯省略号、旁白默认跳过，见 config 的 `onlySpeakerLines`）。

---

## 6. 回到 Unity

配音文件就位后，直接运行游戏即可——`VNView` 会自动按节点 id 播放对应配音，无需任何额外配置。

如果某句想用不同的音频文件名，可以在 `story.json` 里给该节点单独写 `"voice": "自定义名"`，
脚本和引擎都会优先用这个名字。

---

## 常见问题

- **含 `%playername%` 的台词**（如 n2.1b）：配音里玩家名字是变量、无法预知，脚本会用 `playerNamePlaceholder`（默认「你」）代替合成。若不想要这种不匹配，可在 config 把它排除或手动处理这句。
- **合成失败 / 连接不上**：确认 `api_v2.py` 窗口还开着、端口和 `apiUrl` 一致、`refAudioPath` 路径存在。
- **音色不像**：优先换更干净、更长一点的参考音频；仍不满意就走方式 B 训练。
- **语速/停顿**：调 config 里的 `speedFactor`、`textSplitMethod`（cut2/cut3/cut5 等切分方式）。

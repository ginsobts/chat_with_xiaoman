配音文件放这里（StreamingAssets/voice/）。

用法：
1. 在 story.json 里给某个节点加 "voice" 字段，例如：
     {
       "id": "n1",
       "speaker": "小满",
       "text": "你终于来了。",
       "voice": "n1_line"
     }
2. 把对应的音频文件命名为 n1_line.wav（或 .ogg / .mp3），放进这个文件夹。
3. 运行游戏，走到该节点时会自动播放这段配音。

支持格式：.wav / .ogg / .mp3（优先按这个顺序查找）。
建议：短句用 .wav 最稳；长音频用 .ogg 体积更小。
文件名不要带扩展名之外的空格或特殊字符。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    /// <summary>立绘项：用哪张图、放在哪个位置。</summary>
    [Serializable]
    public class CharacterSlot
    {
        public string sprite = "";              // 图片名（StreamingAssets/images/<sprite>.png）
        public string position = "center";      // left / center / right
    }

    /// <summary>
    /// 剧情变量变更：
    /// operation = set / add / subtract。
    /// - set：把变量设为 value（可用于文本变量，也可用于数字字符串）。
    /// - add/subtract：把变量当作数字，对 amount 做加减。
    /// </summary>
    [Serializable]
    public class VariableChange
    {
        public string name = "";
        public string operation = "set";
        public string value = "";
        public int amount = 1;
    }

    /// <summary>
    /// 进入节点前的条件跳转。
    /// comparison 支持：equals / notEquals / greater / greaterOrEqual / less / lessOrEqual。
    /// equals 为空时会沿用旧字段 equals。
    /// </summary>
    [Serializable]
    public class ConditionalNext
    {
        public string variable = "";
        public string comparison = "equals";
        public string equals = "";
        public string next = "";
    }

    /// <summary>选项：显示文字 + 跳转到哪个节点。</summary>
    [Serializable]
    public class Choice
    {
        public string text = "";
        public string next = "";
        public List<VariableChange> setVariables = new List<VariableChange>();
    }

    /// <summary>一段剧情节点。</summary>
    [Serializable]
    public class StoryNode
    {
        public string id = "";
        public string background = "";          // 背景图名；空=保持上一张
        public List<CharacterSlot> characters = new List<CharacterSlot>(); // 空=清空立绘
        public bool keepCharacters = false;     // true=不改动当前立绘
        public string speaker = "";             // 说话人名；空=旁白
        public string text = "";
        public string voice = "";               // 配音文件名（StreamingAssets/voice/<voice>.wav/.ogg/.mp3）；空=无配音
        public string inputVariable = "";       // 非空时，文本播完后要求玩家输入，并保存到这个变量名
        public string inputPlaceholder = "请输入文字";
        public string inputButtonText = "确定";

        // 隐藏真名分支：玩家输入内容 == secretName 时，切换到 secretStory 这个剧本，
        // 从 secretNext 节点接着演（携带已有变量，如 %playername%）。三者需一起配置。
        public string secretName = "";          // 触发隐藏线的真名（明文比对，去首尾空格）
        public string secretStory = "";         // 另一个剧本文件名，如 story_true.json
        public string secretNext = "";          // 在新剧本里从哪个节点开始；空=新剧本 startNode
        public string next = "";                // 无选项时点击继续跳转
        public List<Choice> choices = new List<Choice>();
        public string ending = "";              // "" / "pet" / "lock"
        public List<VariableChange> setVariables = new List<VariableChange>();
        public List<ConditionalNext> conditionalNexts = new List<ConditionalNext>();

        // 仅供节点编辑器记录画布位置；游戏运行时忽略。
        public float editorX = 0f;
        public float editorY = 0f;
    }

    /// <summary>整部剧本。</summary>
    [Serializable]
    public class StoryData
    {
        public string startNode = "";
        public string tamperNode = "";          // 篡改彩蛋入口（可选）
        // 非空时：历史记录按钮仍可点，但点开不显示历史，改为压暗屏幕显示这句话。
        public string historyBlockedMessage = "";
        public List<StoryNode> nodes = new List<StoryNode>();

        private Dictionary<string, StoryNode> _index;

        public StoryNode Get(string id)
        {
            if (_index == null)
            {
                _index = new Dictionary<string, StoryNode>();
                foreach (var n in nodes)
                    if (!string.IsNullOrEmpty(n.id) && !_index.ContainsKey(n.id))
                        _index[n.id] = n;
            }
            if (string.IsNullOrEmpty(id)) return null;
            return _index.TryGetValue(id, out var node) ? node : null;
        }
    }
}

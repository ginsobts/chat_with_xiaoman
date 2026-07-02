using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VN
{
    /// <summary>
    /// 视觉小说播放器：背景 / 立绘 / 对话框 / 打字机 / 选项。
    /// </summary>
    public class VNView : MonoBehaviour, IPointerClickHandler
    {
        private StoryData _story;
        private Action<string> _onEnding;

        private RectTransform _root;
        private Image _background;
        private RectTransform _charLayer;
        private Image _dialoguePanel;
        private Image _speakerPanel;
        private Text _speakerText;
        private Text _bodyText;
        private RectTransform _choiceLayer;
        private RectTransform _inputLayer;
        private InputField _inputField;
        private Image _clickCatcher;
        private Button _historyButton;
        private RectTransform _historyLayer;
        private RectTransform _historyContent;
        private ScrollRect _historyScroll;
        private Text _historyTitle;
        private Text _historyBlockedLabel;
        private AudioSource _voiceSource;
        private Coroutine _voiceRoutine;

        private readonly Dictionary<string, Image> _charImages = new Dictionary<string, Image>();
        private readonly Dictionary<string, string> _variables = new Dictionary<string, string>();
        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();

        private StoryNode _node;
        private bool _typing;
        private bool _choicesShown;
        private bool _inputShown;
        private bool _historyShown;
        private string _fullText;
        private Coroutine _typeRoutine;

        private struct HistoryEntry
        {
            public string Speaker;
            public string Text;
        }

        private const float CharsPerSecond = 45f;

        // 是否正在等玩家输入（如输入名字）。用于全局重开热键在输入时屏蔽。
        public bool IsInputActive => _inputShown;

        public void Begin(StoryData story, string startNode, Action<string> onEnding,
            Dictionary<string, string> seedVariables = null)
        {
            _story = story;
            _onEnding = onEnding;
            if (seedVariables != null)
            {
                foreach (var kv in seedVariables)
                    _variables[kv.Key] = kv.Value;
            }
            BuildUI();
            GoTo(startNode);
        }

        private void BuildUI()
        {
            _root = GameManager.Instance.Root;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
            _voiceSource.spatialBlend = 0f;

            _background = UITheme.AddImage("Background", _root, Color.black);
            UITheme.FullStretch(_background.rectTransform);
            _background.preserveAspect = false;

            var charGO = UITheme.NewUIObject("Characters", _root);
            _charLayer = charGO.GetComponent<RectTransform>();
            UITheme.FullStretch(_charLayer);

            _dialoguePanel = UITheme.AddPanel("DialoguePanel", _root, new Color(1f, 1f, 1f, 0.9f));
            _dialoguePanel.raycastTarget = false;
            UITheme.SetRect(_dialoguePanel.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-860, 50), new Vector2(860, 330));

            _speakerPanel = UITheme.AddPanel("SpeakerPanel", _root, new Color(1f, 1f, 1f, 0.96f));
            _speakerPanel.raycastTarget = false;
            UITheme.SetRect(_speakerPanel.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-860, 340), new Vector2(-560, 420));

            _speakerText = UITheme.AddText("Speaker", _speakerPanel.transform, "", 42,
                Color.black, TextAnchor.MiddleCenter);
            UITheme.SetRect(_speakerText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(22, 6), new Vector2(-22, -6));

            _bodyText = UITheme.AddText("Body", _dialoguePanel.transform, "", 44,
                Color.black, TextAnchor.UpperLeft);
            _bodyText.lineSpacing = 1.12f;
            UITheme.SetRect(_bodyText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(72, 48), new Vector2(-72, -48));

            var hint = UITheme.AddText("Hint", _dialoguePanel.transform, "▼", 28,
                new Color(0f, 0f, 0f, 0.55f), TextAnchor.LowerRight);
            UITheme.SetRect(hint.rectTransform,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-60, 16), new Vector2(-20, 56));

            // 全屏点击捕获（推进对话），放在对话面板之上、选项之下
            _clickCatcher = UITheme.AddImage("ClickCatcher", _root, new Color(0, 0, 0, 0));
            UITheme.FullStretch(_clickCatcher.rectTransform);
            _clickCatcher.gameObject.AddComponent<ClickProxy>().Target = this;

            // 选项层
            var choiceGO = UITheme.NewUIObject("Choices", _root);
            _choiceLayer = choiceGO.GetComponent<RectTransform>();
            UITheme.FullStretch(_choiceLayer);
            _choiceLayer.gameObject.SetActive(false);

            var inputGO = UITheme.NewUIObject("PlayerInput", _root);
            _inputLayer = inputGO.GetComponent<RectTransform>();
            UITheme.FullStretch(_inputLayer);
            _inputLayer.gameObject.SetActive(false);

            // 历史记录按钮（右上角小 UI）
            _historyButton = UITheme.AddButton("HistoryButton", _root,
                GameLanguage.IsEnglish ? "History" : "历史", 28, OpenHistory);
            UITheme.SetRect(_historyButton.GetComponent<RectTransform>(),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-172, -92), new Vector2(-24, -24));

            BuildHistoryUI();
        }

        private void BuildHistoryUI()
        {
            var histGO = UITheme.NewUIObject("HistoryLayer", _root);
            _historyLayer = histGO.GetComponent<RectTransform>();
            UITheme.FullStretch(_historyLayer);

            // 压暗背景，点击空白处也可返回
            var dim = UITheme.AddImage("HistoryDim", _historyLayer, new Color(0f, 0f, 0f, 0.82f));
            UITheme.FullStretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseHistory);

            _historyTitle = UITheme.AddText("HistoryTitle", _historyLayer, GameLanguage.HistoryTitle, 40,
                Color.white, TextAnchor.MiddleCenter);
            UITheme.SetRect(_historyTitle.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200, -110), new Vector2(200, -40));

            // 真剧情下的“历史被封”提示：默认隐藏，OpenHistory 时按需显示。
            _historyBlockedLabel = UITheme.AddText("HistoryBlocked", _historyLayer, "", 46,
                Color.white, TextAnchor.MiddleCenter);
            UITheme.SetRect(_historyBlockedLabel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-760, -220), new Vector2(760, 220));
            _historyBlockedLabel.gameObject.SetActive(false);

            var scrollGO = UITheme.NewUIObject("HistoryScroll", _historyLayer);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            UITheme.SetRect(scrollRT,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-880, -420), new Vector2(880, 380));
            _historyScroll = scrollGO.AddComponent<ScrollRect>();
            _historyScroll.horizontal = false;
            _historyScroll.vertical = true;
            _historyScroll.movementType = ScrollRect.MovementType.Clamped;
            _historyScroll.scrollSensitivity = 40f;

            var viewportGO = UITheme.NewUIObject("Viewport", scrollGO.transform);
            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.02f);
            viewportGO.AddComponent<RectMask2D>();
            var viewportRT = viewportGO.GetComponent<RectTransform>();
            UITheme.FullStretch(viewportRT);
            _historyScroll.viewport = viewportRT;

            var contentGO = UITheme.NewUIObject("Content", viewportGO.transform);
            _historyContent = contentGO.GetComponent<RectTransform>();
            _historyContent.anchorMin = new Vector2(0f, 1f);
            _historyContent.anchorMax = new Vector2(1f, 1f);
            _historyContent.pivot = new Vector2(0.5f, 1f);
            _historyContent.offsetMin = new Vector2(0f, 0f);
            _historyContent.offsetMax = new Vector2(0f, 0f);
            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 26f;
            layout.padding = new RectOffset(24, 24, 24, 24);
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _historyScroll.content = _historyContent;

            var back = UITheme.AddButton("HistoryBack", _historyLayer, GameLanguage.Back, 32, CloseHistory);
            UITheme.SetRect(back.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-160, 40), new Vector2(160, 136));

            _historyLayer.gameObject.SetActive(false);
        }

        private void GoTo(string id, int redirectDepth = 0)
        {
            var node = _story.Get(id);
            if (node == null)
            {
                Debug.LogError("找不到剧情节点: " + id);
                GameManager.Instance.ShowMainMenu();
                return;
            }

            if (redirectDepth > 16)
            {
                Debug.LogError("条件跳转层数过深，可能出现了循环: " + id);
                GameManager.Instance.ShowMainMenu();
                return;
            }

            string conditionalTarget = GetConditionalTarget(node);
            if (!string.IsNullOrEmpty(conditionalTarget))
            {
                GoTo(conditionalTarget, redirectDepth + 1);
                return;
            }

            _node = node;
            ApplyVariables(node.setVariables);
            ApplyVisuals(node);
            ShowText(node);
        }

        private string GetConditionalTarget(StoryNode node)
        {
            if (node.conditionalNexts == null)
                return "";

            foreach (var condition in node.conditionalNexts)
            {
                if (condition == null || string.IsNullOrEmpty(condition.variable) || string.IsNullOrEmpty(condition.next))
                    continue;

                if (ConditionMatches(condition))
                    return condition.next;
            }

            return "";
        }

        private bool ConditionMatches(ConditionalNext condition)
        {
            _variables.TryGetValue(condition.variable, out var actual);
            string expected = condition.equals ?? "";
            string comparison = string.IsNullOrEmpty(condition.comparison)
                ? "equals"
                : condition.comparison;

            bool actualIsNumber = int.TryParse(actual, out int actualNumber);
            bool expectedIsNumber = int.TryParse(expected, out int expectedNumber);

            switch (comparison)
            {
                case "notEquals":
                    return actual != expected;
                case "greater":
                    return actualIsNumber && expectedIsNumber && actualNumber > expectedNumber;
                case "greaterOrEqual":
                    return actualIsNumber && expectedIsNumber && actualNumber >= expectedNumber;
                case "less":
                    return actualIsNumber && expectedIsNumber && actualNumber < expectedNumber;
                case "lessOrEqual":
                    return actualIsNumber && expectedIsNumber && actualNumber <= expectedNumber;
                case "equals":
                default:
                    return actual == expected;
            }
        }

        private void ApplyVariables(List<VariableChange> changes)
        {
            if (changes == null)
                return;

            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrEmpty(change.name))
                    continue;

                string operation = string.IsNullOrEmpty(change.operation)
                    ? "set"
                    : change.operation;

                if (operation == "add" || operation == "subtract")
                {
                    _variables.TryGetValue(change.name, out var oldValue);
                    int.TryParse(oldValue, out int current);
                    int delta = operation == "add" ? change.amount : -change.amount;
                    _variables[change.name] = (current + delta).ToString();
                }
                else
                {
                    _variables[change.name] = change.value ?? "";
                }
                Debug.Log("[VN Variable] " + change.name + " = " + _variables[change.name]);
            }
        }

        private void ApplyVisuals(StoryNode node)
        {
            if (!string.IsNullOrEmpty(node.background))
            {
                var sp = AssetCache.GetSprite(node.background);
                _background.color = Color.white;
                _background.sprite = sp;
            }

            if (!node.keepCharacters)
            {
                foreach (var kv in _charImages) Destroy(kv.Value.gameObject);
                _charImages.Clear();

                if (node.characters != null)
                    foreach (var slot in node.characters)
                        SpawnCharacter(slot);
            }
        }

        private void SpawnCharacter(CharacterSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.sprite)) return;
            var sp = AssetCache.GetSprite(slot.sprite);
            var img = UITheme.AddImage("Char_" + slot.sprite, _charLayer, Color.white);
            img.sprite = sp;
            img.preserveAspect = true;

            float ax = slot.position == "left" ? 0.22f
                     : slot.position == "right" ? 0.78f : 0.5f;
            var rt = img.rectTransform;
            UITheme.SetRect(rt,
                new Vector2(ax, 0f), new Vector2(ax, 0f),
                new Vector2(-360, 120), new Vector2(360, 1040));
            _charImages[slot.position + "_" + slot.sprite] = img;
        }

        private void ShowText(StoryNode node)
        {
            _choicesShown = false;
            _inputShown = false;
            _choiceLayer.gameObject.SetActive(false);
            _inputLayer.gameObject.SetActive(false);
            ClearChoices();
            ClearInput();

            bool hasSpeaker = !string.IsNullOrEmpty(node.speaker);
            _speakerPanel.gameObject.SetActive(hasSpeaker);
            _speakerText.text = hasSpeaker ? ReplaceVariables(node.speaker) : "";
            _fullText = ReplaceVariables(node.text ?? "");
            if (!string.IsNullOrEmpty(_fullText))
                _history.Add(new HistoryEntry { Speaker = _speakerText.text, Text = _fullText });
            // 优先用节点显式指定的 voice；没写时按约定回退到「节点 id」作为配音文件名。
            PlayVoice(!string.IsNullOrEmpty(node.voice) ? node.voice : node.id);
            if (_typeRoutine != null) StopCoroutine(_typeRoutine);
            _typeRoutine = StartCoroutine(Typewriter(_fullText));
        }

        private void PlayVoice(string voice)
        {
            if (_voiceSource == null) return;
            _voiceSource.Stop();
            if (_voiceRoutine != null) StopCoroutine(_voiceRoutine);
            if (string.IsNullOrEmpty(voice)) return;

            _voiceRoutine = StartCoroutine(AudioCache.Load(voice, clip =>
            {
                if (clip == null) return;
                _voiceSource.clip = clip;
                _voiceSource.Play();
            }));
        }

        private string ReplaceVariables(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            foreach (var variable in _variables)
                text = text.Replace("%" + variable.Key + "%", variable.Value);
            return text;
        }

        private IEnumerator Typewriter(string full)
        {
            _typing = true;
            _bodyText.text = "";
            float t = 0f;
            int shown = 0;
            while (shown < full.Length)
            {
                t += Time.deltaTime * CharsPerSecond;
                int target = Mathf.Min(full.Length, Mathf.FloorToInt(t));
                if (target != shown)
                {
                    shown = target;
                    _bodyText.text = full.Substring(0, shown);
                }
                yield return null;
            }
            _bodyText.text = full;
            _typing = false;
            OnTextComplete();
        }

        private void OnTextComplete()
        {
            // 文本播完后先停住，等玩家再点一次才显示选项。
            // 这样可以做出“读完这一句 -> 点击 -> 选项出现”的 VN 节奏。
            if (!string.IsNullOrEmpty(_node.inputVariable))
                ShowInput();
        }

        public void OnPointerClick(PointerEventData eventData) => Advance();

        private void Advance()
        {
            if (_historyShown) return; // 历史记录界面打开时不推进
            if (_inputShown) return; // 等玩家输入并确认
            if (_choicesShown) return; // 等玩家点选项

            if (_typing)
            {
                // 跳过打字机，立即显示全文。
                if (_typeRoutine != null) StopCoroutine(_typeRoutine);
                _bodyText.text = _fullText;
                _typing = false;
                OnTextComplete();
                return;
            }

            if (_node.choices != null && _node.choices.Count > 0)
            {
                ShowChoices(_node.choices);
                return;
            }

            // 结局判定
            if (!string.IsNullOrEmpty(_node.ending))
            {
                _onEnding?.Invoke(_node.ending);
                return;
            }

            if (!string.IsNullOrEmpty(_node.next))
                GoTo(_node.next);
            else
                GameManager.Instance.ShowMainMenu(); // 没有 next 也没有结局，回主菜单
        }

        private void OpenHistory()
        {
            if (_historyShown) return;
            _historyShown = true;
            _clickCatcher.raycastTarget = false;
            _historyButton.gameObject.SetActive(false);
            _historyLayer.gameObject.SetActive(true);

            bool blocked = _story != null && !string.IsNullOrEmpty(_story.historyBlockedMessage);
            _historyScroll.gameObject.SetActive(!blocked);
            _historyTitle.gameObject.SetActive(!blocked);
            _historyBlockedLabel.gameObject.SetActive(blocked);

            if (blocked)
                _historyBlockedLabel.text = _story.historyBlockedMessage;
            else
                RebuildHistory();
        }

        private void CloseHistory()
        {
            _historyShown = false;
            _historyLayer.gameObject.SetActive(false);
            _historyButton.gameObject.SetActive(true);
            // 只有在没有选项/输入弹窗时才恢复点击推进
            _clickCatcher.raycastTarget = !_choicesShown && !_inputShown;
        }

        private void RebuildHistory()
        {
            for (int i = _historyContent.childCount - 1; i >= 0; i--)
                Destroy(_historyContent.GetChild(i).gameObject);

            foreach (var entry in _history)
            {
                string line = string.IsNullOrEmpty(entry.Speaker)
                    ? entry.Text
                    : "<b>" + entry.Speaker + "</b>\n" + entry.Text;
                var txt = UITheme.AddText("Entry", _historyContent, line, 34,
                    Color.white, TextAnchor.UpperLeft);
                txt.lineSpacing = 1.1f;
            }

            // 滚到底部（最新一句）
            Canvas.ForceUpdateCanvases();
            if (_historyScroll != null) _historyScroll.verticalNormalizedPosition = 0f;
        }

        private void ShowInput()
        {
            _inputShown = true;
            _clickCatcher.raycastTarget = false;
            _inputLayer.gameObject.SetActive(true);
            ClearInput();

            var panel = UITheme.AddPanel("InputPanel", _inputLayer, new Color(1f, 1f, 1f, 0.96f));
            UITheme.SetRect(panel.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-420, 390), new Vector2(420, 510));

            var inputImage = UITheme.AddPanel("NameInput", panel.transform, Color.white);
            UITheme.SetRect(inputImage.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(24, 24), new Vector2(-170, -24));

            _inputField = inputImage.gameObject.AddComponent<InputField>();
            _inputField.targetGraphic = inputImage;

            var text = UITheme.AddText("Text", inputImage.transform, "", 32, Color.black, TextAnchor.MiddleLeft);
            UITheme.SetRect(text.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(18, 0), new Vector2(-18, 0));
            _inputField.textComponent = text;

            var placeholder = UITheme.AddText("Placeholder", inputImage.transform,
                string.IsNullOrEmpty(_node.inputPlaceholder)
                    ? (GameLanguage.IsEnglish ? "Enter text" : "请输入文字")
                    : _node.inputPlaceholder,
                32, new Color(0f, 0f, 0f, 0.35f), TextAnchor.MiddleLeft);
            UITheme.SetRect(placeholder.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(18, 0), new Vector2(-18, 0));
            _inputField.placeholder = placeholder;

            var button = UITheme.AddButton("ConfirmInput", panel.transform,
                string.IsNullOrEmpty(_node.inputButtonText)
                    ? (GameLanguage.IsEnglish ? "OK" : "确定")
                    : _node.inputButtonText,
                30, ConfirmInput);
            UITheme.SetRect(button.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-150, 24), new Vector2(-24, -24));

            _inputField.ActivateInputField();
        }

        private void ConfirmInput()
        {
            string value = _inputField != null ? _inputField.text.Trim() : "";
            if (string.IsNullOrEmpty(value))
                value = GameLanguage.DefaultPlayerName;

            _variables[_node.inputVariable] = value;
            Debug.Log("[VN Variable] " + _node.inputVariable + " = " + value);

            _inputShown = false;
            _clickCatcher.raycastTarget = true;
            _inputLayer.gameObject.SetActive(false);
            ClearInput();

            // 隐藏真名命中：切换到另一个剧本，携带已有变量接着演。
            if (!string.IsNullOrEmpty(_node.secretName) &&
                !string.IsNullOrEmpty(_node.secretStory) &&
                string.Equals(value, _node.secretName.Trim(), StringComparison.Ordinal))
            {
                GameManager.Instance.SwitchStory(_node.secretStory, _node.secretNext, _variables);
                return;
            }

            if (!string.IsNullOrEmpty(_node.next))
                GoTo(_node.next);
            else
                GameManager.Instance.ShowMainMenu();
        }

        private void ShowChoices(List<Choice> choices)
        {
            _choicesShown = true;
            _clickCatcher.raycastTarget = false;
            _choiceLayer.gameObject.SetActive(true);
            ClearChoices();

            int n = choices.Count;
            float btnH = 96f, gap = 24f;
            float totalH = n * btnH + (n - 1) * gap;
            float startY = totalH / 2f;

            for (int i = 0; i < n; i++)
            {
                var choice = choices[i];
                var pickedChoice = choice;
                var btn = UITheme.AddButton("Choice" + i, _choiceLayer, choice.text, 36,
                    () => OnChoicePicked(pickedChoice));
                float y = startY - i * (btnH + gap) - btnH / 2f;
                UITheme.SetRect(btn.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-420, y), new Vector2(420, y + btnH));
            }
        }

        private void OnChoicePicked(Choice choice)
        {
            _choicesShown = false;
            _clickCatcher.raycastTarget = true;
            _choiceLayer.gameObject.SetActive(false);
            ClearChoices();
            ApplyVariables(choice.setVariables);

            string next = choice.next;
            if (string.IsNullOrEmpty(next))
                GameManager.Instance.ShowMainMenu();
            else
                GoTo(next);
        }

        private void ClearChoices()
        {
            for (int i = _choiceLayer.childCount - 1; i >= 0; i--)
                Destroy(_choiceLayer.GetChild(i).gameObject);
        }

        private void ClearInput()
        {
            if (_inputLayer == null)
                return;

            for (int i = _inputLayer.childCount - 1; i >= 0; i--)
                Destroy(_inputLayer.GetChild(i).gameObject);
            _inputField = null;
        }

        /// <summary>转发 ClickCatcher 的点击到 VNView。</summary>
        private class ClickProxy : MonoBehaviour, IPointerClickHandler
        {
            public VNView Target;
            public void OnPointerClick(PointerEventData eventData) => Target?.OnPointerClick(eventData);
        }
    }
}

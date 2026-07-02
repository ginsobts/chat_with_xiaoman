using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// true 线结局解锁的“悬浮 AVG 对话框”形态：
    ///  - 桌面上一个立绘 + 对话框，逻辑类似桌宠：每隔一段时间自动说一句。
    ///  - 玩家一段时间不操作会睡着（占位睡觉立绘）。
    ///  - 对话框上有按钮可切回桌宠形态。
    /// 对话内容来自单独的 ending_avg.json。
    /// </summary>
    public class EndingAvgView : MonoBehaviour
    {
        private Camera _camera;
        private Canvas _canvas;

        private RectTransform _portraitRect;
        private Image _portrait;
        private RectTransform _box;
        private Image _boxImg;
        private Image _speakerPanel;
        private Text _speakerText;
        private Text _bodyText;
        private RectTransform _switchBtn;
        private RectTransform _toggleBtn;
        private Text _toggleLabel;
        private AudioSource _voiceSource;

        private EndingAvgConfig _config;
        private Sprite _normalSprite;

        private bool _sleeping;
        private bool _collapsed;
        private string _currentFull = "";
        private float _localIdleSeconds;
        private Vector3 _lastMousePosition;
        private float _nextTalkAt;
        private Coroutine _typeRoutine;
        private Coroutine _voiceRoutine;
        private Coroutine _hideVoiceless;

        private const float CharsPerSecond = 40f;

        // 立绘布局：固定左边缘与显示宽度，高度按立绘比例算，底部与对话框底部对齐。
        private const float PortraitLeftX = -900f;
        private const float PortraitWidth = 360f;
        private const float BoxBottomY = 40f;

        public void Init(Camera cam, Canvas canvas)
        {
            _camera = cam;
            _canvas = canvas;

            if (!Application.isEditor)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0, 0, 0, 0);
                int w = Display.main.systemWidth;
                int h = Display.main.systemHeight;
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                StartCoroutine(ApplyOverlayDelayed(w, h));
            }
            else
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.1f, 0.12f, 0.15f, 1f);
            }

            BuildUI();
            ScheduleNextTalk();
            // 进入时先说一句开场
            var opening = PickLine(_config.enter) ?? PickLine(_config.lines);
            if (opening != null) Say(opening);
        }

        private IEnumerator ApplyOverlayDelayed(int w, int h)
        {
            float timeout = 2f;
            while (Screen.fullScreenMode != FullScreenMode.Windowed && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 3; i++) yield return null;
            yield return new WaitForEndOfFrame();
            Win32Window.MakeTransparentOverlay(w, h);
        }

        private void BuildUI()
        {
            var root = GameManager.Instance.Root;
            _config = EndingAvgConfig.Load();
            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;

            // 立绘（贴在对话框左侧，底部与对话框底部对齐）
            _portrait = UITheme.AddImage("AvgPortrait", root, Color.white);
            _portrait.sprite = AssetCache.GetSprite(_config.portraitSprite);
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            _portraitRect = _portrait.rectTransform;
            _normalSprite = _portrait.sprite;
            ApplyPortraitLayout();

            // 对话框：简约黑边白底长方形
            _boxImg = UITheme.AddPanel("AvgBox", root, new Color(1f, 1f, 1f, 0.96f));
            _box = _boxImg.rectTransform;
            UITheme.SetRect(_box,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-560, BoxBottomY), new Vector2(720, 250));

            _speakerPanel = UITheme.AddPanel("AvgSpeaker", root, new Color(1f, 1f, 1f, 0.98f));
            _speakerPanel.raycastTarget = false;
            UITheme.SetRect(_speakerPanel.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-560, 250), new Vector2(-360, 320));
            _speakerText = UITheme.AddText("AvgSpeakerText", _speakerPanel.transform,
                _config.speaker, 28, Color.black, TextAnchor.MiddleCenter);
            _speakerText.raycastTarget = false;
            UITheme.FullStretch(_speakerText.rectTransform);

            _bodyText = UITheme.AddText("AvgBody", _box, "", 30, Color.black, TextAnchor.UpperLeft);
            _bodyText.raycastTarget = false;
            _bodyText.lineSpacing = 1.15f;
            UITheme.SetRect(_bodyText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(28, 24), new Vector2(-28, -24));

            // 收起/展开对话框按钮
            var toggle = UITheme.AddButton("AvgToggleBtn", root, GameLanguage.CollapseDialogue, 24, ToggleBox);
            _toggleBtn = toggle.GetComponent<RectTransform>();
            _toggleLabel = _toggleBtn.GetComponentInChildren<Text>();
            UITheme.SetRect(_toggleBtn,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(340, 258), new Vector2(520, 320));

            // 切回桌宠按钮
            var btn = UITheme.AddButton("AvgSwitchBtn", root,
                string.IsNullOrEmpty(_config.switchButtonText) ? GameLanguage.BackToPet : _config.switchButtonText,
                24, () => GameManager.Instance.EnterPetMode());
            _switchBtn = btn.GetComponent<RectTransform>();
            UITheme.SetRect(_switchBtn,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(540, 258), new Vector2(720, 320));
        }

        /// <summary>按立绘原始比例设定显示框，底部与对话框底部对齐。</summary>
        private void ApplyPortraitLayout()
        {
            if (_portrait == null || _portrait.sprite == null) return;
            float sw = _portrait.sprite.rect.width;
            float sh = _portrait.sprite.rect.height;
            if (sw <= 0f || sh <= 0f) return;
            float h = PortraitWidth * (sh / sw);
            UITheme.SetRect(_portraitRect,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(PortraitLeftX, BoxBottomY),
                new Vector2(PortraitLeftX + PortraitWidth, BoxBottomY + h));
        }

        private void ToggleBox()
        {
            _collapsed = !_collapsed;
            if (_boxImg != null) _boxImg.gameObject.SetActive(!_collapsed);
            if (_speakerPanel != null) _speakerPanel.gameObject.SetActive(!_collapsed);
            if (_toggleLabel != null) _toggleLabel.text = _collapsed
                ? GameLanguage.ExpandDialogue
                : GameLanguage.CollapseDialogue;
            if (!_collapsed)
            {
                // 展开时把当前这句补回去，避免空白
                if (!string.IsNullOrEmpty(_currentFull)) _bodyText.text = _currentFull;
                ScheduleNextTalk();
            }
        }

        private void Update()
        {
            bool over = PointerOverUI();
            if (!Application.isEditor)
                Win32Window.SetClickThrough(!over);

            // 点击对话框推进到下一句（按钮区域除外）
            if (!_collapsed && Input.GetMouseButtonDown(0)
                && RectIn(_box, Input.mousePosition)
                && !RectIn(_switchBtn, Input.mousePosition)
                && !RectIn(_toggleBtn, Input.mousePosition))
            {
                AdvanceLine();
                return;
            }

            float idle = GetIdleSeconds();
            bool shouldSleep = idle >= _config.sleepAfterSeconds;
            if (shouldSleep != _sleeping)
                SetSleeping(shouldSleep);

            if (!_collapsed && !_sleeping && Time.time >= _nextTalkAt)
            {
                Say(PickLine(_config.lines));
                ScheduleNextTalk();
            }
        }

        /// <summary>点击推进：正在打字则立即补全，否则说下一句。</summary>
        private void AdvanceLine()
        {
            if (_sleeping) { SetSleeping(false); return; }
            if (_typeRoutine != null)
            {
                StopTyping();
                _bodyText.text = _currentFull;
                return;
            }
            Say(PickLine(_config.lines));
            ScheduleNextTalk();
        }

        private bool PointerOverUI()
        {
            Vector2 mp = Input.mousePosition;
            return (!_collapsed && RectIn(_box, mp)) || RectIn(_portraitRect, mp)
                || RectIn(_switchBtn, mp) || RectIn(_toggleBtn, mp);
        }

        private bool RectIn(RectTransform rt, Vector2 screenPoint)
        {
            return rt != null && RectTransformUtility.RectangleContainsScreenPoint(
                rt, screenPoint, _canvas.worldCamera);
        }

        private float GetIdleSeconds()
        {
            if (!Application.isEditor)
                return Win32Window.GetSystemIdleSeconds();

            if (Input.anyKeyDown || Vector3.Distance(Input.mousePosition, _lastMousePosition) > 0.5f)
                _localIdleSeconds = 0f;
            else
                _localIdleSeconds += Time.deltaTime;
            _lastMousePosition = Input.mousePosition;
            return _localIdleSeconds;
        }

        private void SetSleeping(bool sleeping)
        {
            _sleeping = sleeping;
            if (sleeping)
            {
                var sleepSprite = AssetCache.GetSprite(_config.sleepSprite);
                if (sleepSprite != null) { _portrait.sprite = sleepSprite; ApplyPortraitLayout(); }
                StopTyping();
                _currentFull = string.IsNullOrEmpty(_config.sleepText) ? "……Zzz" : _config.sleepText;
                _bodyText.text = _currentFull;
            }
            else
            {
                _portrait.sprite = _normalSprite;
                ApplyPortraitLayout();
                var wake = PickLine(_config.lines);
                if (wake != null) Say(wake);
            }
        }

        private void Say(EndingAvgLine line)
        {
            if (line == null) return;

            if (!string.IsNullOrEmpty(line.sprite))
            {
                var sp = AssetCache.GetSprite(line.sprite);
                if (sp != null) { _portrait.sprite = sp; _normalSprite = sp; ApplyPortraitLayout(); }
            }

            _currentFull = string.IsNullOrEmpty(line.text) ? "" : line.text;
            StopTyping();
            _typeRoutine = StartCoroutine(TypeText(_currentFull));

            if (_voiceRoutine != null) StopCoroutine(_voiceRoutine);
            if (_voiceSource != null) _voiceSource.Stop();
            // 与桌宠共用同一个静音开关（默认静音，玩家在桌宠右键菜单里切换）。
            bool muted = PlayerPrefs.GetInt("pet_voice_muted", 1) == 1;
            if (!muted && !string.IsNullOrEmpty(line.voice))
                _voiceRoutine = StartCoroutine(PlayVoice(line.voice));
        }

        private IEnumerator TypeText(string full)
        {
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
            _typeRoutine = null;
        }

        private void StopTyping()
        {
            if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        }

        private IEnumerator PlayVoice(string voice)
        {
            AudioClip clip = null;
            yield return AudioCache.Load(voice, c => clip = c);
            if (clip == null) yield break;
            _voiceSource.Stop();
            _voiceSource.clip = clip;
            _voiceSource.Play();
        }

        private void ScheduleNextTalk()
        {
            float min = Mathf.Max(3f, _config.talkMinSeconds);
            float max = Mathf.Max(min, _config.talkMaxSeconds);
            _nextTalkAt = Time.time + Random.Range(min, max);
        }

        private EndingAvgLine PickLine(List<EndingAvgLine> lines)
        {
            if (lines == null || lines.Count == 0) return null;
            return lines[Random.Range(0, lines.Count)];
        }
    }

    [System.Serializable]
    public class EndingAvgLine
    {
        public string text = "";
        public string voice = "";
        public string sprite = "";
    }

    [System.Serializable]
    public class EndingAvgConfig
    {
        public string speaker = "小满";
        public string portraitSprite = "true_gentle";
        public string sleepSprite = "true_quiet"; // 占位：之后可换真正的睡觉立绘
        public string sleepText = "……（好像睡着了）Zzz";
        public string switchButtonText = "变回桌宠";
        public float talkMinSeconds = 20f;
        public float talkMaxSeconds = 45f;
        public float sleepAfterSeconds = 120f;
        public List<EndingAvgLine> enter = new List<EndingAvgLine>();
        public List<EndingAvgLine> lines = new List<EndingAvgLine>();

        public static EndingAvgConfig Load()
        {
            try
            {
                string json = null;
                string fileName = GameLanguage.LocalizedJson("ending_avg.json");
                if (!Pak.TryGetText(fileName, out json))
                {
                    string path = Path.Combine(Application.streamingAssetsPath, fileName);
                    if (!File.Exists(path)) return MakeDefault();
                    json = File.ReadAllText(path);
                }
                var cfg = JsonUtility.FromJson<EndingAvgConfig>(JsonCommentUtility.StripComments(json));
                return cfg ?? MakeDefault();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[EndingAvg] 读取 ending_avg.json 失败，用默认: " + e.Message);
                return MakeDefault();
            }
        }

        private static EndingAvgConfig MakeDefault()
        {
            if (GameLanguage.IsEnglish)
            {
                return new EndingAvgConfig
                {
                    speaker = "Xiaoman",
                    sleepText = "... (She seems to have fallen asleep.) Zzz",
                    switchButtonText = "Back to Pet",
                    enter = new List<EndingAvgLine>
                    {
                        new EndingAvgLine { text = "Talking like this feels more like a chat, right?", sprite = "true_gentle" }
                    },
                    lines = new List<EndingAvgLine>
                    {
                        new EndingAvgLine { text = "I am always here.", sprite = "true_gentle" },
                        new EndingAvgLine { text = "When you are busy, I will quietly watch.", sprite = "true_quiet" },
                        new EndingAvgLine { text = "If you miss me, use the button to turn me back into the desktop pet.", sprite = "true_happy" }
                    }
                };
            }

            return new EndingAvgConfig
            {
                enter = new List<EndingAvgLine>
                {
                    new EndingAvgLine { text = "现在换成这样和你说话，是不是更像在聊天了？", sprite = "true_gentle" }
                },
                lines = new List<EndingAvgLine>
                {
                    new EndingAvgLine { text = "我一直在这儿哦。", sprite = "true_gentle" },
                    new EndingAvgLine { text = "你在忙的时候，我就安静看着你。", sprite = "true_quiet" },
                    new EndingAvgLine { text = "想我了就点右下角，让我变回桌宠也行。", sprite = "true_happy" }
                }
            };
        }
    }
}

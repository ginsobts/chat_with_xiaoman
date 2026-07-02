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
        private Text _speakerText;
        private Text _bodyText;
        private RectTransform _switchBtn;
        private AudioSource _voiceSource;

        private EndingAvgConfig _config;
        private Sprite _normalSprite;

        private bool _sleeping;
        private float _localIdleSeconds;
        private Vector3 _lastMousePosition;
        private float _nextTalkAt;
        private Coroutine _typeRoutine;
        private Coroutine _voiceRoutine;
        private Coroutine _hideVoiceless;

        private const float CharsPerSecond = 40f;

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

            // 立绘（贴在对话框左侧、坐在桌面底部）
            _portrait = UITheme.AddImage("AvgPortrait", root, Color.white);
            _portrait.sprite = AssetCache.GetSprite(_config.portraitSprite);
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            _portraitRect = _portrait.rectTransform;
            UITheme.SetRect(_portraitRect,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-900, 40), new Vector2(-560, 740));
            _normalSprite = _portrait.sprite;

            // 对话框：简约黑边白底长方形
            _boxImg = UITheme.AddPanel("AvgBox", root, new Color(1f, 1f, 1f, 0.96f));
            _box = _boxImg.rectTransform;
            UITheme.SetRect(_box,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-560, 40), new Vector2(720, 250));

            var speakerPanel = UITheme.AddPanel("AvgSpeaker", root, new Color(1f, 1f, 1f, 0.98f));
            speakerPanel.raycastTarget = false;
            UITheme.SetRect(speakerPanel.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-560, 250), new Vector2(-360, 320));
            _speakerText = UITheme.AddText("AvgSpeakerText", speakerPanel.transform,
                _config.speaker, 28, Color.black, TextAnchor.MiddleCenter);
            _speakerText.raycastTarget = false;
            UITheme.FullStretch(_speakerText.rectTransform);

            _bodyText = UITheme.AddText("AvgBody", _box, "", 30, Color.black, TextAnchor.UpperLeft);
            _bodyText.raycastTarget = false;
            _bodyText.lineSpacing = 1.15f;
            UITheme.SetRect(_bodyText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(28, 24), new Vector2(-28, -24));

            // 切回桌宠按钮
            var btn = UITheme.AddButton("AvgSwitchBtn", root,
                string.IsNullOrEmpty(_config.switchButtonText) ? "变回桌宠" : _config.switchButtonText,
                24, () => GameManager.Instance.EnterPetMode());
            _switchBtn = btn.GetComponent<RectTransform>();
            UITheme.SetRect(_switchBtn,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(540, 258), new Vector2(720, 320));
        }

        private void Update()
        {
            bool over = PointerOverUI();
            if (!Application.isEditor)
                Win32Window.SetClickThrough(!over);

            float idle = GetIdleSeconds();
            bool shouldSleep = idle >= _config.sleepAfterSeconds;
            if (shouldSleep != _sleeping)
                SetSleeping(shouldSleep);

            if (!_sleeping && Time.time >= _nextTalkAt)
            {
                Say(PickLine(_config.lines));
                ScheduleNextTalk();
            }
        }

        private bool PointerOverUI()
        {
            Vector2 mp = Input.mousePosition;
            return RectIn(_box, mp) || RectIn(_portraitRect, mp) || RectIn(_switchBtn, mp);
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
                if (sleepSprite != null) _portrait.sprite = sleepSprite;
                StopTyping();
                _bodyText.text = string.IsNullOrEmpty(_config.sleepText) ? "……Zzz" : _config.sleepText;
            }
            else
            {
                _portrait.sprite = _normalSprite;
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
                if (sp != null) { _portrait.sprite = sp; _normalSprite = sp; }
            }

            StopTyping();
            _typeRoutine = StartCoroutine(TypeText(string.IsNullOrEmpty(line.text) ? "" : line.text));

            if (_voiceRoutine != null) StopCoroutine(_voiceRoutine);
            if (_voiceSource != null) _voiceSource.Stop();
            if (!string.IsNullOrEmpty(line.voice))
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
                if (!Pak.TryGetText("ending_avg.json", out json))
                {
                    string path = Path.Combine(Application.streamingAssetsPath, "ending_avg.json");
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

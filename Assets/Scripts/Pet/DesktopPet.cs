using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// 桌面宠物：透明置顶窗口里漂浮的小角色。
    ///  - 鼠标移到宠物上：窗口接收点击，可拖动 / 右键菜单。
    ///  - 鼠标不在宠物上：点击穿透到桌面，不挡操作。
    /// </summary>
    public class DesktopPet : MonoBehaviour
    {
        private Camera _camera;
        private Canvas _canvas;
        private RectTransform _petRect;
        private Image _petImage;
        private RectTransform _menu;
        private RectTransform _bubble;
        private Text _bubbleText;
        private LayoutElement _bubbleTextLayout;
        private AudioSource _voiceSource;
        private PetDialogueConfig _config;
        private Sprite _dragSprite;

        // 语音静音开关：默认静音，玩家可在右键菜单里取消静音（选择会被记住）。
        private bool _muted = true;
        private Text _muteLabel;
        private const string MutePrefKey = "pet_voice_muted";

        private bool _dragging;
        private bool _dragMoved;
        private bool _sleeping;
        private Vector2 _dragOffset;
        private Vector2 _dragStartMouse;
        private Vector2 _lastDragMouse;
        private float _dragTilt;
        private Vector2 _basePos;
        private Vector3 _normalScale;
        private Quaternion _normalRotation;
        private Sprite _normalSprite;
        private float _nextIdleTalkAt;
        private float _nextCuteAnimationAt;
        private float _localIdleSeconds;
        private float _idleSeconds;
        private float _breatheTime;
        private Vector3 _lastMousePosition;
        private Coroutine _bubbleRoutine;
        private Coroutine _voiceRoutine;
        private Coroutine _expressionRoutine;
        private Coroutine _animationRoutine;
        private Coroutine _dropRoutine;

        // 时段问候 / 欢迎回来
        private int _lastGreetBucket = -1;
        private bool _wasAway;
        // 摸头杀
        private float _headpatCooldownUntil;
        private float _headLastX;
        private float _rubWindowEnd;
        private float _rubLastDx;
        private int _rubDirChanges;
        // 溜达
        private bool _strolling;
        private float _nextStrollAt;
        private Coroutine _strollRoutine;
        // true 线解锁后可切换到 AVG 对话框
        private bool _avgToggle;

        // 「本人」模式：右键菜单带日历，可设置提醒
        private bool _calendarEnabled;
        private ReminderStore _reminders;
        private CalendarView _calendarView;
        private bool _calendarOpen;
        // 需玩家确认的提醒弹窗
        private RectTransform _reminderPopup;
        private Text _reminderPopupText;
        private bool _reminderPopupActive;
        private readonly Queue<Reminder> _reminderQueue = new Queue<Reminder>();
        private Reminder _currentReminder;
        private float _nextReminderCheckAt;
        private string _lastReminderCheckDay = "";

        public void Init(Camera cam, Canvas canvas, bool avgToggle = false, bool calendarEnabled = false)
        {
            _camera = cam;
            _canvas = canvas;
            _avgToggle = avgToggle;
            _calendarEnabled = calendarEnabled;
            if (_calendarEnabled)
                _reminders = ReminderStore.Load();

            if (!Application.isEditor)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0, 0, 0, 0); // 透明

                // 关键：独占全屏时 DWM 逐像素透明不生效，会整屏纯黑。
                // 先切到无边框窗口并铺满桌面，等模式真正生效后再套透明置顶。
                int w = Display.main.systemWidth;
                int h = Display.main.systemHeight;
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
                StartCoroutine(ApplyOverlayDelayed(w, h));
            }
            else
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.1f, 0.12f, 0.15f, 1f); // 编辑器里给个深色底好观察
            }

            BuildPet();
            ScheduleNextIdleTalk();
            ScheduleNextCuteAnimation();
            ScheduleNextStroll();
            _nextReminderCheckAt = Time.time + 2f; // 进入后稍等再做首次提醒检查
        }

        private IEnumerator ApplyOverlayDelayed(int w, int h)
        {
            // 分辨率/窗口模式切换是异步的，等它真正切到 Windowed 再套透明，
            // 否则窗口状态不对会出现黑屏/透明失效。带超时兜底。
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

        private void BuildPet()
        {
            var root = GameManager.Instance.Root;
            // 真假结局两套桌宠对白：true 线读 pet_dialogues_true.json，普通线读 pet_dialogues.json。
            string petDialogueFile = _avgToggle ? "pet_dialogues_true.json" : "pet_dialogues.json";
            _config = PetDialogueConfig.Load(GameLanguage.LocalizedJson(petDialogueFile));
            _voiceSource = gameObject.AddComponent<AudioSource>();

            _petImage = UITheme.AddImage("Pet", root, Color.white);
            _petImage.sprite = AssetCache.GetSprite(_config.normalSprite);
            _petImage.preserveAspect = true;
            _dragSprite = AssetCache.GetSprite(_config.dragSprite);
            _petRect = _petImage.rectTransform;
            UITheme.SetRect(_petRect,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-120, 40), new Vector2(120, 320));
            _basePos = _petRect.anchoredPosition;
            _normalScale = _petRect.localScale;
            _normalRotation = _petRect.localRotation;
            _normalSprite = _petImage.sprite;

            // 读取上次的静音选择（默认静音）。
            _muted = PlayerPrefs.GetInt(MutePrefKey, 1) == 1;

            // 右键菜单（默认隐藏）：按钮从上往下堆叠。
            var menuItems = new List<(string, UnityEngine.Events.UnityAction)>();
            int muteIndex = menuItems.Count;
            menuItems.Add((MuteMenuLabel(), ToggleMute));
            if (_calendarEnabled)
                menuItems.Add((GameLanguage.Calendar, OpenCalendar));
            if (_avgToggle)
                menuItems.Add((GameLanguage.SwitchToDialogue, () => GameManager.Instance.EnterAvgMode()));
            menuItems.Add((GameLanguage.Quit, () => GameManager.Instance.QuitGame()));

            const float btnH = 58f, pad = 8f;
            float menuH = menuItems.Count * btnH + pad * 2f;
            var menuImg = UITheme.AddImage("PetMenu", root, new Color(0.1f, 0.1f, 0.14f, 0.95f));
            _menu = menuImg.rectTransform;
            UITheme.SetRect(_menu,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-120, 330), new Vector2(120, 330 + menuH));
            for (int i = 0; i < menuItems.Count; i++)
            {
                var btn = UITheme.AddButton("MenuBtn" + i, _menu, menuItems[i].Item1, 28, menuItems[i].Item2);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(pad, -(pad + (i + 1) * btnH));
                rt.offsetMax = new Vector2(-pad, -(pad + i * btnH));
                if (i == muteIndex) _muteLabel = btn.GetComponentInChildren<Text>();
            }
            _menu.gameObject.SetActive(false);

            BuildBubble(root);
        }

        private void BuildBubble(RectTransform root)
        {
            var bubbleImg = UITheme.AddPanel("PetBubble", root, new Color(1f, 1f, 1f, 0.96f));
            bubbleImg.raycastTarget = false;
            _bubble = bubbleImg.rectTransform;
            // 锚定在画布底部中心，pivot 用左下角，方便按尺寸做出屏夹紧。
            _bubble.anchorMin = new Vector2(0.5f, 0f);
            _bubble.anchorMax = new Vector2(0.5f, 0f);
            _bubble.pivot = new Vector2(0f, 0f);

            // 让气泡随文字内容自适应大小（短句变小，长句自动换行长高）。
            var layout = bubbleImg.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 14, 14);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = bubbleImg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _bubbleText = UITheme.AddText("PetBubbleText", _bubble, "", 26, Color.black, TextAnchor.MiddleLeft);
            _bubbleText.raycastTarget = false;
            _bubbleTextLayout = _bubbleText.gameObject.AddComponent<LayoutElement>();

            _bubble.gameObject.SetActive(false);
        }

        // 短句让气泡贴合文字；超过上限宽度则限制宽度并换行，避免铺满整屏。
        private void ClampBubbleWidth()
        {
            if (_bubbleText == null || _bubbleTextLayout == null) return;
            const float maxWidth = 360f; // 参考分辨率(1920)下的最大文本宽度
            _bubbleTextLayout.preferredWidth = -1f;
            float natural = _bubbleText.preferredWidth;
            _bubbleTextLayout.preferredWidth = Mathf.Min(natural, maxWidth);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bubble);
        }

        private void Update()
        {
            bool over = PointerOverPet();

            // 日历面板 / 提醒弹窗打开时，整窗接收点击，保证能操作。
            bool modalOpen = _calendarOpen || _reminderPopupActive;

            // 根据鼠标是否在宠物上切换点击穿透
            if (!Application.isEditor)
                Win32Window.SetClickThrough(!over && !_dragging && !_menu.gameObject.activeSelf && !modalOpen);

            _idleSeconds = GetIdleSeconds();

            HandleDrag(over);
            HandleMenu(over);
            HandleHeadpat(over);
            HandleIdleAndSleep();
            HandleWelcomeBack();
            HandleTimeGreeting();
            HandleRandomTalk();
            HandleCuteAnimation();
            HandleStroll();
            ApplyIdleBreathing();
            UpdateBubblePosition();
            HandleReminders();
        }

        private bool PointerOverPet()
        {
            if (_petRect == null) return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    _petRect, Input.mousePosition, _canvas.worldCamera))
                return false;
            // 像素级判定：只有落在人物不透明处才算“在桌宠身上”，避免大片透明空白当成可点区域。
            return AlphaAtScreenPoint(Input.mousePosition) > 0.35f;
        }

        private float AlphaAtScreenPoint(Vector2 screenPoint)
        {
            return TryGetPetUV(screenPoint, out _, out _, out float alpha) ? alpha : 0f;
        }

        // 鼠标是否落在“头顶区域”的人物不透明处（摸头杀用）。v 越大越靠上。
        private bool PointerOverHead()
        {
            if (!TryGetPetUV(Input.mousePosition, out _, out float v, out float alpha)) return false;
            return alpha > 0.35f && v > 0.55f;
        }

        // 把屏幕坐标映射到桌宠贴图的 uv 并取出该处 alpha。考虑 preserveAspect 的居中留边。
        private bool TryGetPetUV(Vector2 screenPoint, out float u, out float v, out float alpha)
        {
            u = v = 0f; alpha = 0f;
            var sprite = _petImage != null ? _petImage.sprite : null;
            if (sprite == null || sprite.texture == null) { alpha = 1f; return true; }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _petRect, screenPoint, _canvas.worldCamera, out var local))
                return false;

            Rect r = _petRect.rect;
            float texW = sprite.rect.width, texH = sprite.rect.height;
            if (texW <= 0f || texH <= 0f) { alpha = 1f; return true; }

            float scale = Mathf.Min(r.width / texW, r.height / texH);
            float dispW = texW * scale, dispH = texH * scale;
            u = (local.x - (r.center.x - dispW * 0.5f)) / dispW;
            v = (local.y - (r.center.y - dispH * 0.5f)) / dispH;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            float px = sprite.rect.x + u * sprite.rect.width;
            float py = sprite.rect.y + v * sprite.rect.height;
            try
            {
                alpha = sprite.texture.GetPixelBilinear(
                    px / sprite.texture.width, py / sprite.texture.height).a;
            }
            catch
            {
                alpha = 1f;
            }
            return true;
        }

        private void HandleDrag(bool over)
        {
            if (_calendarOpen) return; // 日历面板打开时不响应桌宠拖动
            if (Input.GetMouseButtonDown(0) && over)
            {
                _dragging = true;
                _dragMoved = false;
                _dragStartMouse = Input.mousePosition;
                _lastDragMouse = Input.mousePosition;
                if (_strollRoutine != null) // 抓起时打断溜达
                {
                    StopCoroutine(_strollRoutine);
                    _strollRoutine = null;
                    _strolling = false;
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_petRect.parent, Input.mousePosition, _canvas.worldCamera, out var lp);
                _dragOffset = _petRect.anchoredPosition - lp;
            }
            if (_dragging && Input.GetMouseButton(0))
            {
                // 超过阈值才算“真的在拖”，这时才切拖拽姿势，避免单击闪一下拖拽图。
                if (!_dragMoved && Vector2.Distance(_dragStartMouse, Input.mousePosition) > 8f)
                {
                    _dragMoved = true;
                    EnterDragPose();
                }

                if (_dragMoved)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)_petRect.parent, Input.mousePosition, _canvas.worldCamera, out var lp);
                    _petRect.anchoredPosition = lp + _dragOffset;
                    _basePos = _petRect.anchoredPosition;

                    // 随手速左右摆动，更有被拎着晃的感觉。
                    float dx = Input.mousePosition.x - _lastDragMouse.x;
                    float target = Mathf.Clamp(-dx * 1.4f, -22f, 22f);
                    _dragTilt = Mathf.Lerp(_dragTilt, target, 0.35f);
                    _petRect.localRotation = Quaternion.Euler(0f, 0f, _dragTilt);
                }
                _lastDragMouse = Input.mousePosition;
            }
            if (_dragging && Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                if (_dragMoved)
                {
                    ExitDragPose();
                    if (_dropRoutine != null) StopCoroutine(_dropRoutine);
                    _dropRoutine = StartCoroutine(DropBounce());
                }
                else
                {
                    OnPetClicked();
                }
            }
        }

        private IEnumerator DropBounce()
        {
            // 放下时来个 squash-and-stretch 小回弹，并把倾斜摆正。
            float startTilt = _dragTilt;
            float t = 0f, dur = 0.3f;
            while (t < dur && !_dragging && !_sleeping)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float s = Mathf.Sin(p * Mathf.PI); // 0→1→0
                _petRect.localScale = new Vector3(
                    _normalScale.x * (1f + 0.12f * s),
                    _normalScale.y * (1f - 0.12f * s),
                    _normalScale.z);
                _petRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startTilt, 0f, p));
                yield return null;
            }
            _dragTilt = 0f;
            if (!_dragging && !_sleeping)
            {
                _petRect.localScale = _normalScale;
                _petRect.localRotation = _normalRotation;
            }
            _dropRoutine = null;
        }

        private void ApplyIdleBreathing()
        {
            if (_config == null || !_config.idleBreathe) return;
            // 仅在“纯待机”时呼吸；有动画/表情/拖动/睡觉/回弹时交给它们控制，避免打架。
            bool plainIdle = !_dragging && !_sleeping && !_strolling
                && _animationRoutine == null && _expressionRoutine == null && _dropRoutine == null;
            if (!plainIdle) return;

            _breatheTime += Time.deltaTime * Mathf.Max(0.1f, _config.breatheSpeed);
            float s = Mathf.Sin(_breatheTime) * Mathf.Max(0f, _config.breatheAmplitude);
            _petRect.localScale = new Vector3(
                _normalScale.x * (1f - s * 0.5f),
                _normalScale.y * (1f + s),
                _normalScale.z);
            _petRect.localRotation = _normalRotation;
        }

        private void EnterDragPose()
        {
            if (_expressionRoutine != null)
            {
                StopCoroutine(_expressionRoutine);
                _expressionRoutine = null;
            }
            if (_animationRoutine != null)
            {
                StopCoroutine(_animationRoutine);
                _animationRoutine = null;
            }

            if (_dragSprite != null)
                _petImage.sprite = _dragSprite;
            _petRect.localRotation = _normalRotation;
            _petRect.localScale = _normalScale;
            _petImage.color = Color.white;
        }

        private void ExitDragPose()
        {
            if (_sleeping)
            {
                SetSleeping(false);
            }
            else
            {
                _petImage.sprite = _normalSprite;
                _petRect.localRotation = _normalRotation;
                _petRect.localScale = _normalScale;
                _petImage.color = Color.white;
            }
            ScheduleNextCuteAnimation();
        }

        private void HandleMenu(bool over)
        {
            if (_calendarOpen) return;
            if (Input.GetMouseButtonDown(1) && over)
            {
                bool show = !_menu.gameObject.activeSelf;
                _menu.gameObject.SetActive(show);
                if (show) // 菜单出现在桌宠上方，跟随桌宠当前位置。
                    _menu.anchoredPosition = _petRect.anchoredPosition + new Vector2(0f, 210f);
            }
            // 点空白处关闭菜单
            if (Input.GetMouseButtonDown(0) && _menu.gameObject.activeSelf && !over)
            {
                bool overMenu = RectTransformUtility.RectangleContainsScreenPoint(
                    _menu, Input.mousePosition, _canvas.worldCamera);
                if (!overMenu) _menu.gameObject.SetActive(false);
            }
        }

        private void HandleIdleAndSleep()
        {
            if (_calendarOpen || _reminderPopupActive) return; // 面板/提醒期间不睡
            bool shouldSleep = _idleSeconds >= _config.sleepAfterSeconds;
            if (shouldSleep != _sleeping)
                SetSleeping(shouldSleep);
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

            if (_sleeping)
            {
                var sleepSprite = AssetCache.GetSprite(_config.sleepSprite);
                bool hasCustomSleepSprite = sleepSprite != null && _config.sleepSprite != _config.normalSprite;
                if (sleepSprite != null) _petImage.sprite = sleepSprite;
                _petRect.localRotation = hasCustomSleepSprite ? _normalRotation : Quaternion.Euler(0f, 0f, -18f);
                _petRect.localScale = hasCustomSleepSprite ? _normalScale : new Vector3(1.08f, 0.82f, 1f);
                _petImage.color = new Color(0.86f, 0.88f, 1f, 1f);
                ShowBubble(_config.sleepText, "", _config.sleepBubbleSeconds);
            }
            else
            {
                _petImage.sprite = _normalSprite;
                _petRect.localRotation = _normalRotation;
                _petRect.localScale = _normalScale;
                _petImage.color = Color.white;
                HideBubble();
                ScheduleNextIdleTalk();
                ScheduleNextCuteAnimation();
            }
        }

        private void HandleRandomTalk()
        {
            if (_sleeping || _dragging || _strolling || _menu.gameObject.activeSelf) return;
            if (_calendarOpen || _reminderPopupActive) return;
            if (Time.time < _nextIdleTalkAt) return;

            SayLine(PickLine(_config.idle));
            ScheduleNextIdleTalk();
        }

        // 说一句话：显示气泡（含配音），需要时临时切一张表情图。
        private void SayLine(PetLine line)
        {
            if (line == null) return;
            if (!string.IsNullOrEmpty(line.sprite))
            {
                if (_expressionRoutine != null)
                    StopCoroutine(_expressionRoutine);
                _expressionRoutine = StartCoroutine(PlayTemporarySprite(
                    line.sprite,
                    line.duration > 0f ? line.duration : _config.bubbleSeconds));
            }
            Speak(line);
        }

        private void HandleTimeGreeting()
        {
            if (_sleeping || _dragging || _strolling || _calendarOpen || _reminderPopupActive) return;
            int bucket = CurrentTimeBucket();
            if (bucket == _lastGreetBucket) return;
            _lastGreetBucket = bucket;               // 首次进入会问候一次，之后仅在跨时段时再问候
            SayLine(PickLine(GreetingList(bucket)));
        }

        private int CurrentTimeBucket()
        {
            int h = System.DateTime.Now.Hour;
            if (h >= 5 && h < 11) return 0;   // 早
            if (h >= 11 && h < 17) return 1;  // 中
            if (h >= 17 && h < 23) return 2;  // 晚
            return 3;                         // 深夜
        }

        private List<PetLine> GreetingList(int bucket)
        {
            switch (bucket)
            {
                case 0: return _config.morning;
                case 1: return _config.noon;
                case 2: return _config.evening;
                default: return _config.night;
            }
        }

        private void HandleWelcomeBack()
        {
            if (_idleSeconds >= _config.awayThresholdSeconds)
            {
                _wasAway = true;
                return;
            }
            if (_wasAway && _idleSeconds < 1f)
            {
                _wasAway = false;
                if (_dragging || _calendarOpen || _reminderPopupActive) return;
                SayLine(PickLine(_config.welcome));
            }
        }

        // ---------------- 日历与提醒 ----------------

        private void OpenCalendar()
        {
            if (!_calendarEnabled || _reminders == null) return;
            _menu.gameObject.SetActive(false);
            if (_calendarView != null) return;

            var go = new GameObject("CalendarView");
            go.transform.SetParent(transform, false);
            _calendarView = go.AddComponent<CalendarView>();
            _calendarOpen = true;
            _calendarView.Open(GameManager.Instance.Root, _reminders, () =>
            {
                _calendarOpen = false;
                _calendarView = null;
                // 关闭日历后立即重新检查一次（可能刚设置了近几天的提醒）。
                _nextReminderCheckAt = 0f;
            });
        }

        private void HandleReminders()
        {
            if (!_calendarEnabled || _reminders == null) return;

            // 跨天时强制复查；平时每 30 秒查一次。
            string todayKey = System.DateTime.Now.ToString("yyyy-MM-dd");
            if (todayKey != _lastReminderCheckDay)
            {
                _lastReminderCheckDay = todayKey;
                _nextReminderCheckAt = 0f;
            }
            if (Time.time >= _nextReminderCheckAt)
            {
                _nextReminderCheckAt = Time.time + 30f;
                CheckReminders();
            }

            // 队列里有未展示的提醒且当前没在展示 → 展示下一条。
            if (!_reminderPopupActive && _reminderQueue.Count > 0 && !_calendarOpen)
                ShowNextReminder();
        }

        private void CheckReminders()
        {
            if (_reminderPopupActive) return;
            var due = _reminders.DueReminders(System.DateTime.Now);
            foreach (var r in due)
            {
                bool queued = false;
                foreach (var q in _reminderQueue)
                    if (q.id == r.id) { queued = true; break; }
                if (!queued && (_currentReminder == null || _currentReminder.id != r.id))
                    _reminderQueue.Enqueue(r);
            }
        }

        private void ShowNextReminder()
        {
            if (_reminderQueue.Count == 0) return;
            _currentReminder = _reminderQueue.Dequeue();

            int days = ReminderStore.DaysUntil(_currentReminder, System.DateTime.Now);
            string msg = days <= 0
                ? GameLanguage.RemindToday(_currentReminder.title)
                : GameLanguage.RemindDaysLeft(_currentReminder.title, days);

            if (_sleeping) SetSleeping(false);
            HideBubble();
            EnsureReminderPopup();
            _reminderPopupText.text = msg;
            _reminderPopup.gameObject.SetActive(true);
            _reminderPopupActive = true;
            PositionReminderPopup();
        }

        private void EnsureReminderPopup()
        {
            if (_reminderPopup != null) return;
            var root = GameManager.Instance.Root;

            var panel = UITheme.AddPanel("ReminderPopup", root, new Color(1f, 0.99f, 0.95f, 0.98f));
            _reminderPopup = panel.rectTransform;
            _reminderPopup.anchorMin = _reminderPopup.anchorMax = new Vector2(0.5f, 0f);
            _reminderPopup.pivot = new Vector2(0.5f, 0f);
            _reminderPopup.sizeDelta = new Vector2(520, 200);
            UITheme.AddOutline(panel, new Color(1f, 0.6f, 0.4f, 0.9f), new Vector2(2, -2));

            _reminderPopupText = UITheme.AddText("ReminderText", _reminderPopup,
                "", 28, new Color(0.15f, 0.12f, 0.1f, 1f), TextAnchor.MiddleCenter);
            UITheme.SetRect(_reminderPopupText.rectTransform,
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(24, 70), new Vector2(-24, -20));
            _reminderPopupText.raycastTarget = false;

            var ok = UITheme.AddButton("ReminderOk", _reminderPopup, GameLanguage.ConfirmGotIt, 26,
                DismissCurrentReminder);
            var okRt = ok.GetComponent<RectTransform>();
            okRt.anchorMin = new Vector2(0.5f, 0f);
            okRt.anchorMax = new Vector2(0.5f, 0f);
            okRt.pivot = new Vector2(0.5f, 0f);
            okRt.sizeDelta = new Vector2(200, 52);
            okRt.anchoredPosition = new Vector2(0, 16);
        }

        private void PositionReminderPopup()
        {
            if (_reminderPopup == null) return;
            Vector2 canvas = GameManager.Instance.Root.rect.size;
            float halfW = canvas.x * 0.5f;
            float bw = _reminderPopup.rect.width;
            float bh = _reminderPopup.rect.height;
            Vector2 pet = _petRect != null ? _petRect.anchoredPosition : Vector2.zero;
            float x = Mathf.Clamp(pet.x, -halfW + bw * 0.5f + 8f, halfW - bw * 0.5f - 8f);
            float y = Mathf.Clamp(pet.y + 180f, 8f, canvas.y - bh - 8f);
            _reminderPopup.anchoredPosition = new Vector2(x, y);
        }

        private void DismissCurrentReminder()
        {
            if (_currentReminder != null)
                _reminders.Acknowledge(_currentReminder, System.DateTime.Now);
            _currentReminder = null;
            _reminderPopupActive = false;
            if (_reminderPopup != null)
                _reminderPopup.gameObject.SetActive(false);
            // 若还有排队的提醒，下一帧 HandleReminders 会继续展示。
        }

        private void HandleHeadpat(bool over)
        {
            if (_sleeping || _dragging || _calendarOpen || _reminderPopupActive) return;
            if (Time.time < _headpatCooldownUntil) return;

            if (!over || !PointerOverHead())
            {
                _rubDirChanges = 0;
                _rubLastDx = 0f;
                _headLastX = Input.mousePosition.x;
                return;
            }

            if (Time.time > _rubWindowEnd)
            {
                _rubDirChanges = 0;
                _rubLastDx = 0f;
            }

            float dx = Input.mousePosition.x - _headLastX;
            _headLastX = Input.mousePosition.x;

            if (Mathf.Abs(dx) > 3f)
            {
                _rubWindowEnd = Time.time + 0.8f;
                if (_rubLastDx != 0f && Mathf.Sign(dx) != Mathf.Sign(_rubLastDx))
                    _rubDirChanges++;
                _rubLastDx = dx;

                if (_rubDirChanges >= 3)
                {
                    _rubDirChanges = 0;
                    _rubLastDx = 0f;
                    _headpatCooldownUntil = Time.time + Mathf.Max(0.5f, _config.headpatCooldownSeconds);
                    TriggerHeadpat();
                }
            }
        }

        private void TriggerHeadpat()
        {
            var line = PickLine(_config.headpat);
            string spr = (line != null && !string.IsNullOrEmpty(line.sprite))
                ? line.sprite : _config.headpatSprite;
            float dur = (line != null && line.duration > 0f) ? line.duration : _config.bubbleSeconds;

            if (_expressionRoutine != null)
                StopCoroutine(_expressionRoutine);
            _expressionRoutine = StartCoroutine(PlayTemporarySprite(spr, dur));
            if (line != null) Speak(line);
        }

        private void HandleStroll()
        {
            if (!_config.strollEnabled) return;
            if (_strolling || _strollRoutine != null) return;
            if (_sleeping || _dragging || _menu.gameObject.activeSelf) return;
            if (_calendarOpen || _reminderPopupActive) return;
            if (_bubble != null && _bubble.gameObject.activeSelf) return;
            if (_animationRoutine != null || _expressionRoutine != null || _dropRoutine != null) return;
            if (Time.time < _nextStrollAt) return;

            _strollRoutine = StartCoroutine(StrollRoutine());
            ScheduleNextStroll();
        }

        private IEnumerator StrollRoutine()
        {
            _strolling = true;
            var walkSprite = AssetCache.GetSprite(_config.walkSprite);
            Vector2 start = _petRect.anchoredPosition;

            float rootHalf = GameManager.Instance.Root.rect.width * 0.5f;
            float maxX = Mathf.Max(60f, rootHalf - 160f);
            float dir = Random.value < 0.5f ? -1f : 1f;
            float dist = Random.Range(120f, 300f);
            float targetX = Mathf.Clamp(start.x + dir * dist, -maxX, maxX);

            if (!Mathf.Approximately(targetX, start.x))
            {
                float outDir = Mathf.Sign(targetX - start.x);
                yield return WalkBetween(start.x, targetX, start.y, outDir, walkSprite);
                if (!_dragging && !_sleeping)
                    yield return new WaitForSeconds(Random.Range(0.4f, 1.0f));
                yield return WalkBetween(targetX, start.x, start.y, -outDir, walkSprite);
            }

            if (!_dragging && !_sleeping)
            {
                _petImage.sprite = _normalSprite;
                _petRect.localScale = _normalScale;
                _petRect.localRotation = _normalRotation;
                _petRect.anchoredPosition = start;
                _basePos = start;
            }
            _strolling = false;
            _strollRoutine = null;
        }

        private IEnumerator WalkBetween(float fromX, float toX, float baseY, float face, Sprite walkSprite)
        {
            float speed = Mathf.Max(30f, _config.strollSpeed);
            float dur = Mathf.Abs(toX - fromX) / speed;
            if (dur <= 0f) yield break;

            if (walkSprite != null) _petImage.sprite = walkSprite;
            float t = 0f;
            while (t < dur)
            {
                if (_dragging || _sleeping) yield break; // 被打断
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float x = Mathf.Lerp(fromX, toX, p);
                float bob = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 8f)) * 6f; // 走路小颠簸
                _petRect.anchoredPosition = new Vector2(x, baseY + bob);
                _petRect.localScale = new Vector3(
                    _normalScale.x * (face >= 0f ? 1f : -1f), _normalScale.y, _normalScale.z);
                yield return null;
            }
        }

        private void ScheduleNextStroll()
        {
            float min = Mathf.Max(3f, _config.strollMinSeconds);
            float max = Mathf.Max(min, _config.strollMaxSeconds);
            _nextStrollAt = Time.time + Random.Range(min, max);
        }

        private void HandleCuteAnimation()
        {
            if (_sleeping || _dragging || _strolling || _menu.gameObject.activeSelf) return;
            if (_calendarOpen || _reminderPopupActive) return;
            if (_bubble != null && _bubble.gameObject.activeSelf) return;
            if (_animationRoutine != null || _expressionRoutine != null) return;
            if (Time.time < _nextCuteAnimationAt) return;

            var anim = PickAnimation(_config.animations);
            if (anim != null)
                _animationRoutine = StartCoroutine(PlayCuteAnimation(anim));
            ScheduleNextCuteAnimation();
        }

        private void OnPetClicked()
        {
            if (_sleeping)
                SetSleeping(false);

            var line = PickLine(_config.click);
            if (line == null) return;

            if (_expressionRoutine != null)
                StopCoroutine(_expressionRoutine);
            _expressionRoutine = StartCoroutine(PlayClickExpression(line));
            Speak(line);
            ScheduleNextIdleTalk();
            ScheduleNextCuteAnimation();
        }

        private IEnumerator PlayCuteAnimation(PetAnimation animation)
        {
            if (animation == null || animation.frames == null || animation.frames.Count == 0)
            {
                _animationRoutine = null;
                yield break;
            }

            Sprite oldSprite = _petImage.sprite;
            Quaternion oldRotation = _petRect.localRotation;
            Vector3 oldScale = _petRect.localScale;
            Color oldColor = _petImage.color;
            float frameSeconds = Mathf.Max(0.05f, animation.frameSeconds);

            for (int loop = 0; loop < Mathf.Max(1, animation.loops); loop++)
            {
                for (int i = 0; i < animation.frames.Count; i++)
                {
                    var sprite = AssetCache.GetSprite(animation.frames[i]);
                    if (sprite != null)
                        _petImage.sprite = sprite;
                    _petRect.localRotation = _normalRotation;
                    _petRect.localScale = _normalScale;
                    _petImage.color = Color.white;
                    yield return new WaitForSeconds(frameSeconds);
                }
            }

            if (!_sleeping)
            {
                _petImage.sprite = oldSprite;
                _petRect.localRotation = oldRotation;
                _petRect.localScale = oldScale;
                _petImage.color = oldColor;
            }
            _animationRoutine = null;
        }

        private IEnumerator PlayTemporarySprite(string spriteName, float seconds)
        {
            Sprite oldSprite = _petImage.sprite;
            Quaternion oldRotation = _petRect.localRotation;
            Vector3 oldScale = _petRect.localScale;
            Color oldColor = _petImage.color;

            var sprite = AssetCache.GetSprite(spriteName);
            if (sprite != null)
                _petImage.sprite = sprite;
            _petRect.localRotation = _normalRotation;
            _petRect.localScale = _normalScale;
            _petImage.color = Color.white;

            yield return new WaitForSeconds(seconds);

            if (!_sleeping)
            {
                _petImage.sprite = oldSprite;
                _petRect.localRotation = oldRotation;
                _petRect.localScale = oldScale;
                _petImage.color = oldColor;
            }
            _expressionRoutine = null;
        }

        private IEnumerator PlayClickExpression(PetLine line)
        {
            Sprite oldSprite = _petImage.sprite;
            Quaternion oldRotation = _petRect.localRotation;
            Vector3 oldScale = _petRect.localScale;
            Color oldColor = _petImage.color;

            string spriteName = string.IsNullOrEmpty(line.sprite) ? _config.clickSprite : line.sprite;
            var clickSprite = AssetCache.GetSprite(spriteName);
            if (clickSprite != null)
                _petImage.sprite = clickSprite;
            _petRect.localRotation = Quaternion.Euler(0f, 0f, 5f);
            _petRect.localScale = _normalScale * 1.08f;
            _petImage.color = Color.white;

            yield return new WaitForSeconds(_config.clickExpressionSeconds);

            if (!_sleeping)
            {
                _petImage.sprite = oldSprite;
                _petRect.localRotation = oldRotation;
                _petRect.localScale = oldScale;
                _petImage.color = oldColor;
            }
            _expressionRoutine = null;
        }

        private string MuteMenuLabel()
        {
            return _muted ? GameLanguage.Unmute : GameLanguage.Mute;
        }

        private void ToggleMute()
        {
            _muted = !_muted;
            PlayerPrefs.SetInt(MutePrefKey, _muted ? 1 : 0);
            PlayerPrefs.Save();
            if (_muted && _voiceSource != null)
                _voiceSource.Stop();
            if (_muteLabel != null)
                _muteLabel.text = MuteMenuLabel();
        }

        private void Speak(PetLine line)
        {
            if (line == null) return;

            string text = string.IsNullOrEmpty(line.text) ? "" : line.text;
            ShowBubble(text, line.voice, line.duration > 0f ? line.duration : _config.bubbleSeconds);
        }

        private void ShowBubble(string text, string voice, float duration)
        {
            if (_bubbleRoutine != null)
                StopCoroutine(_bubbleRoutine);
            if (_voiceRoutine != null)
                StopCoroutine(_voiceRoutine);
            if (_voiceSource != null)
                _voiceSource.Stop();

            _bubbleText.text = text;
            _bubble.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (_bubble.gameObject.activeSelf) ClampBubbleWidth();
            UpdateBubblePosition();

            if (!_muted && !string.IsNullOrEmpty(voice))
                _voiceRoutine = StartCoroutine(PlayVoice(voice));

            if (duration > 0f)
                _bubbleRoutine = StartCoroutine(HideBubbleAfter(duration));
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

        private IEnumerator HideBubbleAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            HideBubble();
        }

        private void HideBubble()
        {
            if (_bubble != null)
                _bubble.gameObject.SetActive(false);
        }

        private void UpdateBubblePosition()
        {
            if (_bubble == null || !_bubble.gameObject.activeSelf || _petRect == null) return;

            Vector2 canvas = GameManager.Instance.Root.rect.size;
            float halfW = canvas.x * 0.5f;
            float bw = _bubble.rect.width;
            float bh = _bubble.rect.height;

            // 放在桌宠上方、略微偏左盖住头顶，再按画布尺寸夹紧，保证任何分辨率都不出屏。
            Vector2 pet = _petRect.anchoredPosition;
            float x = pet.x - bw * 0.25f;
            float y = pet.y + 150f;
            x = Mathf.Clamp(x, -halfW + 8f, halfW - bw - 8f);
            y = Mathf.Clamp(y, 8f, canvas.y - bh - 8f);
            _bubble.anchoredPosition = new Vector2(x, y);
        }

        private PetLine PickLine(List<PetLine> lines)
        {
            if (lines == null || lines.Count == 0) return null;
            return lines[Random.Range(0, lines.Count)];
        }

        private void ScheduleNextIdleTalk()
        {
            float min = Mathf.Max(1f, _config.idleMinSeconds);
            float max = Mathf.Max(min, _config.idleMaxSeconds);
            _nextIdleTalkAt = Time.time + Random.Range(min, max);
        }

        private PetAnimation PickAnimation(List<PetAnimation> animations)
        {
            if (animations == null || animations.Count == 0) return null;
            return animations[Random.Range(0, animations.Count)];
        }

        private void ScheduleNextCuteAnimation()
        {
            float min = Mathf.Max(1f, _config.animationMinSeconds);
            float max = Mathf.Max(min, _config.animationMaxSeconds);
            _nextCuteAnimationAt = Time.time + Random.Range(min, max);
        }
    }

    [System.Serializable]
    public class PetDialogueConfig
    {
        public float idleMinSeconds = 300f;
        public float idleMaxSeconds = 600f;
        public float sleepAfterSeconds = 120f;
        public float bubbleSeconds = 6f;
        public float sleepBubbleSeconds = 0f;
        public float clickExpressionSeconds = 0.9f;
        public float animationMinSeconds = 20f;
        public float animationMaxSeconds = 60f;
        public bool idleBreathe = true;
        public float breatheAmplitude = 0.03f;
        public float breatheSpeed = 2.2f;
        public float awayThresholdSeconds = 300f;
        public string headpatSprite = "pet_happy";
        public float headpatCooldownSeconds = 4f;
        public bool strollEnabled = true;
        public float strollMinSeconds = 45f;
        public float strollMaxSeconds = 100f;
        public float strollSpeed = 90f;
        public string walkSprite = "pet";
        public string normalSprite = "pet";
        public string sleepSprite = "pet_sleep";
        public string clickSprite = "pet_click";
        public string dragSprite = "pet_drag";
        public string sleepText = "Zzz...";
        public List<PetLine> idle = new List<PetLine>();
        public List<PetLine> click = new List<PetLine>();
        public List<PetLine> morning = new List<PetLine>();
        public List<PetLine> noon = new List<PetLine>();
        public List<PetLine> evening = new List<PetLine>();
        public List<PetLine> night = new List<PetLine>();
        public List<PetLine> welcome = new List<PetLine>();
        public List<PetLine> headpat = new List<PetLine>();
        public List<PetAnimation> animations = new List<PetAnimation>();

        public static PetDialogueConfig Load(string fileName = "pet_dialogues.json")
        {
            if (string.IsNullOrEmpty(fileName)) fileName = "pet_dialogues.json";
            try
            {
                string json = null;
                if (!Pak.TryGetText(fileName, out json))
                {
                    string path = Path.Combine(Application.streamingAssetsPath, fileName);
                    if (!File.Exists(path))
                    {
                        // true 线专属配置缺失时，回退到默认（普通）桌宠配置，保证不至于空白。
                        return fileName == "pet_dialogues.json" ? MakeDefault() : Load("pet_dialogues.json");
                    }
                    json = File.ReadAllText(path);
                }
                var config = JsonUtility.FromJson<PetDialogueConfig>(JsonCommentUtility.StripComments(json));
                return config ?? MakeDefault();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DesktopPet] 读取 " + fileName + " 失败，使用默认配置: " + e.Message);
                return MakeDefault();
            }
        }

        private static PetDialogueConfig MakeDefault()
        {
            if (GameLanguage.IsEnglish)
            {
                return new PetDialogueConfig
                {
                    idle = new List<PetLine>
                    {
                        new PetLine { text = "I am waiting over here. Am I disturbing you?" },
                        new PetLine { text = "Keep working. I will stay quietly beside you." },
                        new PetLine { text = "Just popping out for a moment." }
                    },
                    click = new List<PetLine>
                    {
                        new PetLine { text = "Hm? Why did you poke me?" },
                        new PetLine { text = "I am here. Please do not poke too much." },
                        new PetLine { text = "Interaction received!" }
                    },
                    morning = new List<PetLine> { new PetLine { text = "Good morning. Let us do our best today.", sprite = "pet_happy" } },
                    noon = new List<PetLine> { new PetLine { text = "It is noon. Take a break and eat something.", sprite = "pet_happy" } },
                    evening = new List<PetLine> { new PetLine { text = "Good evening. How was your day?", sprite = "pet_blink" } },
                    night = new List<PetLine> { new PetLine { text = "It is late. Please sleep earlier.", sprite = "pet_blink" } },
                    welcome = new List<PetLine> { new PetLine { text = "You are back. I waited for you.", sprite = "pet_happy" } },
                    headpat = new List<PetLine> { new PetLine { text = "Hehe... head pats.", sprite = "pet_happy" } }
                };
            }

            return new PetDialogueConfig
            {
                idle = new List<PetLine>
                {
                    new PetLine { text = "我在这边待机中，没打扰到你吧？" },
                    new PetLine { text = "你继续忙，我会乖乖在旁边待着。" },
                    new PetLine { text = "突然出现一下。嘿嘿。" }
                },
                click = new List<PetLine>
                {
                    new PetLine { text = "欸？你点我干嘛呀。" },
                    new PetLine { text = "我在我在，别戳啦。" },
                    new PetLine { text = "收到互动一次！" }
                },
                morning = new List<PetLine>
                {
                    new PetLine { text = "早呀，今天也一起加油！", sprite = "pet_happy" },
                    new PetLine { text = "早上好，记得吃早饭哦。", sprite = "pet_happy" }
                },
                noon = new List<PetLine>
                {
                    new PetLine { text = "中午啦，休息一下吃点东西吧。", sprite = "pet_happy" }
                },
                evening = new List<PetLine>
                {
                    new PetLine { text = "晚上好，今天过得怎么样呀？", sprite = "pet_blink" }
                },
                night = new List<PetLine>
                {
                    new PetLine { text = "这么晚啦，早点睡不许熬夜哦。", sprite = "pet_blink" },
                    new PetLine { text = "夜深了，我陪你，但你也要照顾好自己。" }
                },
                welcome = new List<PetLine>
                {
                    new PetLine { text = "你回来啦！我等你好久了。", sprite = "pet_happy" },
                    new PetLine { text = "欢迎回来～有没有想我呀？", sprite = "pet_wave" }
                },
                headpat = new List<PetLine>
                {
                    new PetLine { text = "欸嘿……被摸头了。", sprite = "pet_happy" },
                    new PetLine { text = "呜、轻一点啦～", sprite = "pet_happy" },
                    new PetLine { text = "嘿嘿，好舒服。", sprite = "pet_happy" }
                },
                animations = new List<PetAnimation>
                {
                    new PetAnimation { frames = new List<string> { "pet", "pet_wink", "pet" }, frameSeconds = 0.18f, loops = 1 },
                    new PetAnimation { frames = new List<string> { "pet", "pet_wave", "pet" }, frameSeconds = 0.24f, loops = 1 }
                }
            };
        }
    }

    [System.Serializable]
    public class PetLine
    {
        public string text = "";
        public string voice = "";
        public string sprite = "";
        public float duration = 0f;
    }

    [System.Serializable]
    public class PetAnimation
    {
        public List<string> frames = new List<string>();
        public float frameSeconds = 0.18f;
        public int loops = 1;
    }
}

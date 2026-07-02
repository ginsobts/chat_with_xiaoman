using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VN
{
    public enum GameScreen { MainMenu, Story, Pet, Locked }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Canvas Canvas { get; private set; }
        public RectTransform Root { get; private set; }
        public SaveState State { get; private set; }
        public StoryData Story { get; private set; }

        private Camera _camera;
        private VNView _vn;
        private DesktopPet _pet;
        private EndingAvgView _avg;

        // 连按三次 M 回到游戏开头的计数
        private int _mCount;
        private float _lastMTime;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true; // 桌宠模式下窗口失焦也继续运行
        }

        private void Start()
        {
            EnsureCamera();
            BuildCanvas();
            Story = StoryLoader.Load();
            State = SaveSystem.Load();

            // 决定进入哪个界面。
            if (State.locked)
            {
                if (SaveSystem.Tampered)
                    ShowTamperEasterEgg();
                else
                    ShowLockedScreen("……这个故事已经结束了。\n\n你做出的选择，无法收回。");
            }
            else if (State.petMode)
            {
                EnterPetMode();
            }
            else
            {
                ShowMainMenu();
            }
        }

        private void Update()
        {
            // 测试快捷键（打包版也生效）：
            // F9：直接进入桌宠模式做实时预览，不写存档标记，重启后恢复正常。
            if (Input.GetKeyDown(KeyCode.F9))
            {
                EnterPetMode();
            }

            // 任意时间连按三次 M 回到游戏开头（桌宠阶段也生效）；
            // 但玩家正在输入名字时不触发（否则 M 会被当成输入内容）。
            bool typingName = _vn != null && _vn.IsInputActive;
            if (Input.GetKeyDown(KeyCode.M) && !typingName)
            {
                float now = Time.unscaledTime;
                if (now - _lastMTime > 0.8f) _mCount = 0;
                _mCount++;
                _lastMTime = now;
                if (_mCount >= 3)
                {
                    _mCount = 0;
                    RestartToStart();
                }
            }

            // 开发期快捷键（仅编辑器）：
            // F12：清空所有结局标记并回主菜单。
            // F11：清空所有结局标记并直接进入 AVG 开头。
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F12))
            {
                DevResetToMenu();
            }
            if (Input.GetKeyDown(KeyCode.F11))
            {
                DevResetToStoryStart();
            }
#endif
        }

        private void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera");
                _camera = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("UICanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasGO);
            Canvas = canvasGO.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Root = canvasGO.GetComponent<RectTransform>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }
        }

        public void ClearScreen()
        {
            if (_vn != null)
            {
                Destroy(_vn.gameObject);
                _vn = null;
            }

            if (_pet != null)
            {
                Destroy(_pet.gameObject);
                _pet = null;
            }

            if (_avg != null)
            {
                Destroy(_avg.gameObject);
                _avg = null;
            }

            for (int i = Root.childCount - 1; i >= 0; i--)
                Destroy(Root.GetChild(i).gameObject);
        }

#if UNITY_EDITOR
        private void DevResetToMenu()
        {
            SaveSystem.DevResetAll();
            Debug.Log("[Dev] 已清空所有结局标记，重新进入主菜单。按 F11 可直接进入 AVG 开头。");
            State = new SaveState();
            ClearScreen();
            ShowMainMenu();
        }

        private void DevResetToStoryStart()
        {
            SaveSystem.DevResetAll();
            Debug.Log("[Dev] 已清空所有结局标记，直接进入 AVG 开头。");
            State = new SaveState();
            ClearScreen();
            OnStartGame();
        }
#endif

        // 连按三次 M 触发：清掉结局标记，重新读主线剧本，从头开始（真正的“回到开始”）。
        private void RestartToStart()
        {
            Debug.Log("[GameManager] 连按 M 三次：回到游戏开头。");
            SaveSystem.DevResetAll();          // 清除 pet/lock 标记，成为干净的重新开始
            State = new SaveState();
            Story = StoryLoader.Load();         // 重新加载主线 story.json（防止之前切到了 story_true）
            ClearScreen();
            LeaveOverlayMode();                 // 还原窗口（撤销桌宠的透明置顶/点击穿透），否则回开头后点不动
            OnStartGame();
        }

        // 从桌宠/AVG 悬浮形态回到普通游戏界面时，恢复窗口与相机状态。
        private void LeaveOverlayMode()
        {
            if (!Application.isEditor)
                Win32Window.RestoreNormalWindow(Display.main.systemWidth, Display.main.systemHeight);
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.black;
            }
        }

        // ---------- 各界面 ----------
        public void ShowMainMenu()
        {
            ClearScreen();
            MainMenuUI.Build(Root, OnStartGame, QuitGame, Story != null);
        }

        private void OnStartGame()
        {
            if (Story == null)
            {
                ShowLockedScreen("找不到剧本文件 story.json。\n请检查 StreamingAssets 目录。");
                return;
            }
            ClearScreen();
            var vnGO = new GameObject("VNView");
            vnGO.transform.SetParent(transform, false);
            _vn = vnGO.AddComponent<VNView>();
            _vn.Begin(Story, Story.startNode, OnEnding);
        }

        // 从当前剧本切换到另一个剧本文件，从 entryNode 开始（空=新剧本 startNode），携带已有变量。
        public void SwitchStory(string storyFile, string entryNode, System.Collections.Generic.Dictionary<string, string> variables)
        {
            var data = StoryLoader.Load(storyFile);
            if (data == null)
            {
                ShowLockedScreen("找不到剧本文件 " + storyFile + "。");
                return;
            }

            Story = data;
            ClearScreen();

            var vnGO = new GameObject("VNView");
            vnGO.transform.SetParent(transform, false);
            _vn = vnGO.AddComponent<VNView>();

            string start = string.IsNullOrEmpty(entryNode) ? data.startNode : entryNode;
            _vn.Begin(data, start, OnEnding, variables);
        }

        // VNView 回调：到达带 ending 字段的节点。
        public void OnEnding(string endingType)
        {
            if (endingType == "pet")
            {
                State.petMode = true;
                SaveSystem.Save(State);
                EnterPetMode();
            }
            else if (endingType == "petavg")
            {
                // true 线结局：桌宠可与 AVG 对话框互相切换。
                State.petMode = true;
                State.avgUnlocked = true;
                SaveSystem.Save(State);
                EnterPetMode();
            }
            else if (endingType == "lock")
            {
                State.locked = true;
                SaveSystem.Save(State);
                ShowLockedScreen("再见。\n\n这一次，是永别。");
            }
            else
            {
                ShowMainMenu();
            }
        }

        public void EnterPetMode()
        {
            ClearScreen();
            if (_vn != null) { Destroy(_vn.gameObject); _vn = null; }

            var petGO = new GameObject("DesktopPet");
            _pet = petGO.AddComponent<DesktopPet>();
            // true 线解锁后，桌宠右键菜单里会出现“切换成对话”。
            _pet.Init(_camera, Canvas, State != null && State.avgUnlocked);
        }

        // true 线结局解锁的 AVG 对话框形态；有按钮可切回桌宠。
        public void EnterAvgMode()
        {
            ClearScreen();
            if (_vn != null) { Destroy(_vn.gameObject); _vn = null; }

            var avgGO = new GameObject("EndingAvgView");
            _avg = avgGO.AddComponent<EndingAvgView>();
            _avg.Init(_camera, Canvas);
        }

        public void ShowLockedScreen(string message)
        {
            ClearScreen();
            var bg = UITheme.AddImage("LockedBG", Root, Color.white);
            UITheme.FullStretch(bg.rectTransform);

            var panel = UITheme.AddPanel("LockedPanel", Root, new Color(1f, 1f, 1f, 0.96f));
            UITheme.SetRect(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-560, -180), new Vector2(560, 180));

            var txt = UITheme.AddText("LockedText", Root, message, 44,
                Color.black, TextAnchor.MiddleCenter);
            UITheme.SetRect(txt.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-500, -140), new Vector2(500, 140));
        }

        private void ShowTamperEasterEgg()
        {
            // 优先用剧本里配置的篡改彩蛋节点；没有就显示一段默认文字。
            StoryNode tamper = (Story != null) ? Story.Get(Story.tamperNode) : null;
            if (tamper != null)
            {
                ClearScreen();
                var vnGO = new GameObject("VNView");
                vnGO.transform.SetParent(transform, false);
                _vn = vnGO.AddComponent<VNView>();
                // 彩蛋播放完毕仍回到锁定界面，进度不回退。
                _vn.Begin(Story, Story.tamperNode, _ =>
                    ShowLockedScreen("你删掉了那个文件。\n\n但我还在。\n\n结局不会改变。"));
            }
            else
            {
                ShowLockedScreen("你删掉了那个文件……\n\n但我记得你做过的一切。\n结局不会改变。");
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

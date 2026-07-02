using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>
    /// 列表式剧情编辑器：左边选节点，右边编辑字段，自动处理 next 跳转。
    /// </summary>
    public class VNStoryEditorWindow : EditorWindow
    {
        private StoryData _story;
        private int _selectedIndex;
        private Vector2 _nodeListScroll;
        private Vector2 _detailScroll;
        private string _status = "";

        [MenuItem("Tools/VN Story Editor")]
        public static void Open()
        {
            var window = GetWindow<VNStoryEditorWindow>("VN Story Editor");
            window.minSize = new Vector2(980, 620);
            window.LoadStory();
        }

        private void OnEnable()
        {
            LoadStory();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_story == null)
            {
                EditorGUILayout.HelpBox("没有加载到 story.json。可以点击上方 Reload，或创建默认剧本。", MessageType.Warning);
                if (GUILayout.Button("Create Empty Story", GUILayout.Height(32)))
                {
                    _story = new StoryData { startNode = "start", tamperNode = "" };
                    _story.nodes.Add(new StoryNode { id = "start", speaker = "小星", text = "新的剧情从这里开始。" });
                    _selectedIndex = 0;
                    SaveStory();
                }
                return;
            }

            EditorGUILayout.Space(6);
            DrawStoryHeader();
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawNodeList();
                DrawNodeDetails();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    LoadStory();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    SaveStory();
                if (GUILayout.Button("Open Node Graph", EditorStyles.toolbarButton, GUILayout.Width(140)))
                    VNNodeGraphWindow.Open();

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(VNStoryIO.StoryPath, EditorStyles.miniLabel);
            }
        }

        private void DrawStoryHeader()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Story Settings", EditorStyles.boldLabel);
                _story.startNode = EditorGUILayout.TextField("Start Node", _story.startNode);
                _story.tamperNode = EditorGUILayout.TextField("Tamper Node", _story.tamperNode);
            }
        }

        private void DrawNodeList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);

                _nodeListScroll = EditorGUILayout.BeginScrollView(_nodeListScroll, "box");
                for (int i = 0; i < _story.nodes.Count; i++)
                {
                    var node = _story.nodes[i];
                    string label = string.IsNullOrEmpty(node.id) ? "<empty id>" : node.id;
                    if (!string.IsNullOrEmpty(node.speaker))
                        label += "  /  " + node.speaker;

                    var style = i == _selectedIndex ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    if (GUILayout.Button(label, style))
                    {
                        _selectedIndex = i;
                        GUI.FocusControl(null);
                    }
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Add End")) AddNodeAtEnd();
                    if (GUILayout.Button("+ Insert After")) InsertAfterSelected();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = _story.nodes.Count > 1;
                    if (GUILayout.Button("Delete Node")) DeleteSelected();
                    GUI.enabled = true;

                    if (GUILayout.Button("Duplicate")) DuplicateSelected();
                }
            }
        }

        private void DrawNodeDetails()
        {
            if (_story.nodes.Count == 0)
                return;

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _story.nodes.Count - 1);
            var node = _story.nodes[_selectedIndex];

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Node Detail", EditorStyles.boldLabel);
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, "box");
                VNStoryInspectorGUI.DrawInspector(node);
                EditorGUILayout.EndScrollView();
            }
        }

        private void LoadStory()
        {
            _story = VNStoryIO.Load(out _status);
            if (_story != null)
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _story.nodes.Count - 1));
        }

        private void SaveStory()
        {
            if (_story != null)
                _status = VNStoryIO.Save(_story);
        }

        private void AddNodeAtEnd()
        {
            var node = NewNode();
            _story.nodes.Add(node);
            _selectedIndex = _story.nodes.Count - 1;
            _status = "已在末尾新增节点：" + node.id;
        }

        private void InsertAfterSelected()
        {
            if (_story.nodes.Count == 0)
            {
                AddNodeAtEnd();
                return;
            }

            var current = _story.nodes[_selectedIndex];
            var inserted = NewNode();
            inserted.next = current.next;
            _story.nodes.Insert(_selectedIndex + 1, inserted);

            if (current.choices == null || current.choices.Count == 0)
            {
                current.next = inserted.id;
                _status = "已插入节点，并自动接好 next：" + current.id + " -> " + inserted.id + " -> " + inserted.next;
            }
            else
            {
                _status = "当前节点有选项，无法判断要插在哪个选项后；已创建新节点，请手动把某个 choice.next 指向它。";
            }

            _selectedIndex++;
        }

        private void DuplicateSelected()
        {
            if (_story.nodes.Count == 0)
                return;

            var source = _story.nodes[_selectedIndex];
            var copy = JsonUtility.FromJson<StoryNode>(JsonUtility.ToJson(source));
            copy.id = VNStoryIO.MakeUniqueId(_story, source.id + "_copy");
            copy.editorX = source.editorX + 40f;
            copy.editorY = source.editorY + 40f;
            _story.nodes.Insert(_selectedIndex + 1, copy);
            _selectedIndex++;
            _status = "已复制节点：" + copy.id;
        }

        private void DeleteSelected()
        {
            if (_story.nodes.Count <= 1)
                return;

            var deleted = _story.nodes[_selectedIndex];
            bool confirm = EditorUtility.DisplayDialog(
                "Delete Node",
                "确定删除节点 " + deleted.id + " 吗？\n所有指向它的 next/choice.next 会尝试改到它原本的 next。",
                "Delete", "Cancel");
            if (!confirm)
                return;

            string replacement = deleted.next;
            foreach (var node in _story.nodes)
            {
                if (node.next == deleted.id) node.next = replacement;
                if (node.choices == null) continue;
                foreach (var choice in node.choices)
                    if (choice.next == deleted.id) choice.next = replacement;
            }

            _story.nodes.RemoveAt(_selectedIndex);
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _story.nodes.Count - 1);
            _status = "已删除节点：" + deleted.id;
        }

        private StoryNode NewNode()
        {
            return new StoryNode
            {
                id = VNStoryIO.MakeUniqueId(_story, "node"),
                keepCharacters = true,
                speaker = "小星",
                text = "新的对白。"
            };
        }
    }
}

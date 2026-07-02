using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>
    /// 节点图剧情编辑器（IMGUI 自绘）：
    ///  - 拖动节点摆位置（存进 story.json 的 editorX/editorY）
    ///  - 空白处拖动 = 平移画布
    ///  - 从节点右侧的圆点拖到另一个节点 = 连线（设置 next / choice.next / 条件 next）
    ///  - 拖到空白处松开 = 新建并连到一个新节点
    ///  - 右侧面板编辑选中节点的全部字段
    /// </summary>
    public class VNNodeGraphWindow : EditorWindow
    {
        private const float NodeWidth = 220f;
        private const float InspectorWidth = 380f;

        private StoryData _story;
        private string _status = "";
        private Vector2 _pan = new Vector2(40, 40);
        private Vector2 _inspectorScroll;
        private string _selectedId;

        private enum Drag { None, Pan, Node, Connect }
        private Drag _drag = Drag.None;
        private string _dragNodeId;
        private Vector2 _dragOffset;

        // 连线拖拽来源
        private string _connFromId;
        private int _connKind;     // 0=next, 1=choice, 2=conditional
        private int _connIndex;

        private Rect _graphRect;
        private Rect _inspectorRect;

        [MenuItem("Tools/VN Node Graph")]
        public static void Open()
        {
            var window = GetWindow<VNNodeGraphWindow>("VN Node Graph");
            window.minSize = new Vector2(1100, 680);
            window.wantsMouseMove = true;
            window.Reload();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void Reload()
        {
            _story = VNStoryIO.Load(out _status);
            if (_story != null && NeedsAutoLayout())
                AutoLayout();
        }

        private bool NeedsAutoLayout()
        {
            foreach (var n in _story.nodes)
                if (n.editorX != 0f || n.editorY != 0f)
                    return false;
            return _story.nodes.Count > 0;
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_story == null)
            {
                EditorGUILayout.HelpBox("没有加载到 story.json。", MessageType.Warning);
                return;
            }

            _graphRect = new Rect(0, 22, position.width - InspectorWidth, position.height - 22);
            _inspectorRect = new Rect(position.width - InspectorWidth, 22, InspectorWidth, position.height - 22);

            DrawGrid();
            HandleEvents();
            DrawConnections();
            DrawNodes();
            DrawInspectorPanel();

            if (!string.IsNullOrEmpty(_status))
            {
                var r = new Rect(6, position.height - 20, _graphRect.width - 12, 18);
                GUI.Label(r, _status, EditorStyles.miniLabel);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Reload();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    _status = VNStoryIO.Save(_story);
                if (GUILayout.Button("Add Node", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    CreateNodeAt(ScreenToGraph(_graphRect.center));
                if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    AutoLayout();
                if (GUILayout.Button("List Editor", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    VNStoryEditorWindow.Open();

                GUILayout.FlexibleSpace();
                if (_story != null)
                    GUILayout.Label("Start: " + _story.startNode + "    Nodes: " + _story.nodes.Count, EditorStyles.miniLabel);
            }
        }

        // ---------- 坐标 ----------
        private Vector2 GraphToScreen(float x, float y) =>
            new Vector2(_graphRect.x + _pan.x + x, _graphRect.y + _pan.y + y);

        private Vector2 ScreenToGraph(Vector2 p) =>
            new Vector2(p.x - _graphRect.x - _pan.x, p.y - _graphRect.y - _pan.y);

        private float NodeHeight(StoryNode n)
        {
            int choices = n.choices != null ? n.choices.Count : 0;
            int conds = n.conditionalNexts != null ? n.conditionalNexts.Count : 0;
            return 64f + choices * 18f + conds * 16f + 10f;
        }

        private Rect NodeRect(StoryNode n) =>
            new Rect(GraphToScreen(n.editorX, n.editorY), new Vector2(NodeWidth, NodeHeight(n)));

        private float NextKnobY(Rect r) => r.y + 44f;
        private float ChoiceKnobY(Rect r, int i) => r.y + 64f + i * 18f + 9f;
        private float CondKnobY(Rect r, StoryNode n, int i) =>
            r.y + 64f + (n.choices != null ? n.choices.Count : 0) * 18f + i * 16f + 8f;

        // ---------- 事件 ----------
        private void HandleEvents()
        {
            var e = Event.current;
            if (_inspectorRect.Contains(e.mousePosition))
                return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // 先检查连线圆点
                if (TryBeginConnection(e.mousePosition))
                {
                    e.Use();
                    return;
                }

                var node = NodeAt(e.mousePosition);
                if (node != null)
                {
                    _selectedId = node.id;
                    _drag = Drag.Node;
                    _dragNodeId = node.id;
                    _dragOffset = e.mousePosition - GraphToScreen(node.editorX, node.editorY);

                    if (e.clickCount == 2)
                        _drag = Drag.None; // 双击节点只选中
                    e.Use();
                }
                else
                {
                    if (e.clickCount == 2)
                    {
                        CreateNodeAt(ScreenToGraph(e.mousePosition));
                    }
                    else
                    {
                        _selectedId = null;
                        _drag = Drag.Pan;
                    }
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_drag == Drag.Pan)
                {
                    _pan += e.delta;
                    e.Use();
                }
                else if (_drag == Drag.Node)
                {
                    var n = Find(_dragNodeId);
                    if (n != null)
                    {
                        var graphPos = ScreenToGraph(e.mousePosition - _dragOffset);
                        n.editorX = graphPos.x;
                        n.editorY = graphPos.y;
                    }
                    e.Use();
                }
                else if (_drag == Drag.Connect)
                {
                    Repaint();
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (_drag == Drag.Connect)
                    ResolveConnection(e.mousePosition);
                _drag = Drag.None;
                _dragNodeId = null;
                _connFromId = null;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _pan += e.delta;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && !string.IsNullOrEmpty(_selectedId))
            {
                DeleteNode(_selectedId);
                e.Use();
            }
        }

        private bool TryBeginConnection(Vector2 mouse)
        {
            foreach (var n in _story.nodes)
            {
                var r = NodeRect(n);
                float knobX = r.xMax;

                if (KnobHit(mouse, new Vector2(knobX, NextKnobY(r))))
                {
                    StartConn(n.id, 0, 0);
                    return true;
                }

                if (n.choices != null)
                    for (int i = 0; i < n.choices.Count; i++)
                        if (KnobHit(mouse, new Vector2(knobX, ChoiceKnobY(r, i))))
                        {
                            StartConn(n.id, 1, i);
                            return true;
                        }

                if (n.conditionalNexts != null)
                    for (int i = 0; i < n.conditionalNexts.Count; i++)
                        if (KnobHit(mouse, new Vector2(knobX, CondKnobY(r, n, i))))
                        {
                            StartConn(n.id, 2, i);
                            return true;
                        }
            }
            return false;
        }

        private void StartConn(string id, int kind, int index)
        {
            _drag = Drag.Connect;
            _connFromId = id;
            _connKind = kind;
            _connIndex = index;
        }

        private static bool KnobHit(Vector2 mouse, Vector2 knob) =>
            (mouse - knob).sqrMagnitude <= 64f; // 半径约 8px

        private void ResolveConnection(Vector2 mouse)
        {
            var source = Find(_connFromId);
            if (source == null)
                return;

            var target = NodeAt(mouse);
            string targetId;
            if (target == null)
            {
                // 拖到空白：新建一个节点并连过去
                var created = CreateNodeAt(ScreenToGraph(mouse));
                targetId = created.id;
            }
            else
            {
                targetId = target.id;
            }

            if (_connKind == 0)
                source.next = targetId;
            else if (_connKind == 1 && source.choices != null && _connIndex < source.choices.Count)
                source.choices[_connIndex].next = targetId;
            else if (_connKind == 2 && source.conditionalNexts != null && _connIndex < source.conditionalNexts.Count)
                source.conditionalNexts[_connIndex].next = targetId;

            _status = "已连线：" + source.id + " -> " + targetId;
        }

        private StoryNode NodeAt(Vector2 mouse)
        {
            for (int i = _story.nodes.Count - 1; i >= 0; i--)
                if (NodeRect(_story.nodes[i]).Contains(mouse))
                    return _story.nodes[i];
            return null;
        }

        private StoryNode Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in _story.nodes)
                if (n.id == id) return n;
            return null;
        }

        // ---------- 绘制 ----------
        private void DrawGrid()
        {
            GUI.Box(_graphRect, GUIContent.none);
            Handles.BeginGUI();
            Handles.color = new Color(1, 1, 1, 0.05f);
            float spacing = 24f;
            for (float x = _graphRect.x + (_pan.x % spacing); x < _graphRect.xMax; x += spacing)
                Handles.DrawLine(new Vector3(x, _graphRect.y), new Vector3(x, _graphRect.yMax));
            for (float y = _graphRect.y + (_pan.y % spacing); y < _graphRect.yMax; y += spacing)
                Handles.DrawLine(new Vector3(_graphRect.x, y), new Vector3(_graphRect.xMax, y));
            Handles.EndGUI();
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();
            foreach (var n in _story.nodes)
            {
                var r = NodeRect(n);
                float knobX = r.xMax;

                if (!string.IsNullOrEmpty(n.next))
                    DrawEdge(new Vector2(knobX, NextKnobY(r)), Find(n.next), new Color(0.9f, 0.9f, 0.9f));

                if (n.choices != null)
                    for (int i = 0; i < n.choices.Count; i++)
                        DrawEdge(new Vector2(knobX, ChoiceKnobY(r, i)), Find(n.choices[i].next), new Color(1f, 0.8f, 0.3f));

                if (n.conditionalNexts != null)
                    for (int i = 0; i < n.conditionalNexts.Count; i++)
                        DrawEdge(new Vector2(knobX, CondKnobY(r, n, i)), Find(n.conditionalNexts[i].next), new Color(0.4f, 0.8f, 1f));
            }

            if (_drag == Drag.Connect)
            {
                var src = Find(_connFromId);
                if (src != null)
                {
                    var r = NodeRect(src);
                    Vector2 from = _connKind == 0 ? new Vector2(r.xMax, NextKnobY(r))
                        : _connKind == 1 ? new Vector2(r.xMax, ChoiceKnobY(r, _connIndex))
                        : new Vector2(r.xMax, CondKnobY(r, src, _connIndex));
                    DrawBezier(from, Event.current.mousePosition, new Color(1f, 1f, 1f, 0.7f));
                }
            }
            Handles.EndGUI();
        }

        private void DrawEdge(Vector2 from, StoryNode target, Color color)
        {
            if (target == null)
                return;
            var tr = NodeRect(target);
            Vector2 to = new Vector2(tr.x, tr.y + 44f);
            DrawBezier(from, to, color);
        }

        private void DrawBezier(Vector2 from, Vector2 to, Color color)
        {
            Vector2 t1 = from + Vector2.right * 50f;
            Vector2 t2 = to + Vector2.left * 50f;
            Handles.DrawBezier(from, to, t1, t2, color, null, 3f);
        }

        private void DrawNodes()
        {
            foreach (var n in _story.nodes)
                DrawNode(n);
        }

        private void DrawNode(StoryNode n)
        {
            var r = NodeRect(n);
            bool selected = n.id == _selectedId;
            bool isStart = _story.startNode == n.id;
            bool isEnding = !string.IsNullOrEmpty(n.ending);

            var bg = new Color(0.22f, 0.22f, 0.26f, 0.96f);
            if (isStart) bg = new Color(0.18f, 0.32f, 0.22f, 0.96f);
            else if (isEnding) bg = new Color(0.34f, 0.2f, 0.22f, 0.96f);

            EditorGUI.DrawRect(r, bg);
            if (selected)
                DrawOutline(r, new Color(1f, 0.85f, 0.3f), 2f);
            else
                DrawOutline(r, new Color(0, 0, 0, 0.6f), 1f);

            var titleRect = new Rect(r.x + 6, r.y + 4, r.width - 12, 18);
            GUI.Label(titleRect, (isStart ? "★ " : "") + n.id, EditorStyles.boldLabel);

            string preview = n.speaker;
            if (!string.IsNullOrEmpty(n.text))
            {
                string oneLine = n.text.Replace("\n", " ");
                if (oneLine.Length > 18) oneLine = oneLine.Substring(0, 18) + "…";
                preview = string.IsNullOrEmpty(preview) ? oneLine : preview + "：" + oneLine;
            }
            GUI.Label(new Rect(r.x + 6, r.y + 22, r.width - 12, 18), preview, EditorStyles.miniLabel);

            // next 行 + 圆点
            GUI.Label(new Rect(r.x + 6, NextKnobY(r) - 8, r.width - 16, 16),
                isEnding ? "ending: " + n.ending : "next", EditorStyles.miniLabel);
            if (!isEnding)
                DrawKnob(new Vector2(r.xMax, NextKnobY(r)), new Color(0.9f, 0.9f, 0.9f));

            if (n.choices != null)
                for (int i = 0; i < n.choices.Count; i++)
                {
                    string t = n.choices[i].text;
                    if (string.IsNullOrEmpty(t)) t = "(选项" + (i + 1) + ")";
                    if (t.Length > 16) t = t.Substring(0, 16) + "…";
                    GUI.Label(new Rect(r.x + 10, ChoiceKnobY(r, i) - 8, r.width - 20, 16), "◆ " + t, EditorStyles.miniLabel);
                    DrawKnob(new Vector2(r.xMax, ChoiceKnobY(r, i)), new Color(1f, 0.8f, 0.3f));
                }

            if (n.conditionalNexts != null)
                for (int i = 0; i < n.conditionalNexts.Count; i++)
                {
                    var c = n.conditionalNexts[i];
                    string label = "? " + c.variable + " " + c.comparison + " " + c.equals;
                    if (label.Length > 22) label = label.Substring(0, 22) + "…";
                    GUI.Label(new Rect(r.x + 10, CondKnobY(r, n, i) - 8, r.width - 20, 16), label, EditorStyles.miniLabel);
                    DrawKnob(new Vector2(r.xMax, CondKnobY(r, n, i)), new Color(0.4f, 0.8f, 1f));
                }
        }

        private void DrawKnob(Vector2 center, Color color)
        {
            var r = new Rect(center.x - 5, center.y - 5, 10, 10);
            EditorGUI.DrawRect(r, color);
        }

        private void DrawOutline(Rect r, Color color, float w)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), color);
        }

        private void DrawInspectorPanel()
        {
            GUILayout.BeginArea(_inspectorRect, EditorStyles.helpBox);
            var node = Find(_selectedId);
            if (node == null)
            {
                EditorGUILayout.LabelField("未选中节点", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "操作：\n• 左键拖节点 = 移动\n• 空白拖动 = 平移画布\n• 从右侧圆点拖到别的节点 = 连线\n• 拖到空白松开 = 新建并连线\n• 双击空白 = 新建节点\n• Delete = 删除选中节点",
                    MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            EditorGUILayout.LabelField("Node Detail", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_inspectorScroll))
            {
                _inspectorScroll = scroll.scrollPosition;
                VNStoryInspectorGUI.DrawInspector(node);

                EditorGUILayout.Space(8);
                _story.startNode = EditorGUILayout.TextField("Start Node", _story.startNode);
                if (GUILayout.Button("设为起始节点"))
                    _story.startNode = node.id;
            }
            GUILayout.EndArea();
        }

        // ---------- 节点操作 ----------
        private StoryNode CreateNodeAt(Vector2 graphPos)
        {
            var node = new StoryNode
            {
                id = VNStoryIO.MakeUniqueId(_story, "node"),
                keepCharacters = true,
                speaker = "小星",
                text = "新的对白。",
                editorX = graphPos.x,
                editorY = graphPos.y
            };
            _story.nodes.Add(node);
            _selectedId = node.id;
            _status = "已新建节点：" + node.id;
            return node;
        }

        private void DeleteNode(string id)
        {
            if (_story.nodes.Count <= 1)
                return;
            var deleted = Find(id);
            if (deleted == null)
                return;

            string replacement = deleted.next;
            foreach (var n in _story.nodes)
            {
                if (n.next == id) n.next = replacement;
                if (n.choices != null)
                    foreach (var c in n.choices)
                        if (c.next == id) c.next = replacement;
                if (n.conditionalNexts != null)
                    foreach (var c in n.conditionalNexts)
                        if (c.next == id) c.next = replacement;
            }

            _story.nodes.Remove(deleted);
            _selectedId = null;
            _status = "已删除节点：" + id;
        }

        private void AutoLayout()
        {
            if (_story.nodes.Count == 0)
                return;

            var depth = new Dictionary<string, int>();
            var queue = new Queue<string>();
            string start = !string.IsNullOrEmpty(_story.startNode) ? _story.startNode : _story.nodes[0].id;
            depth[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                var n = Find(id);
                if (n == null) continue;
                int d = depth[id];

                foreach (var target in Targets(n))
                {
                    if (string.IsNullOrEmpty(target) || depth.ContainsKey(target)) continue;
                    depth[target] = d + 1;
                    queue.Enqueue(target);
                }
            }

            int orphanDepth = 0;
            foreach (var n in _story.nodes)
                if (!depth.ContainsKey(n.id))
                    depth[n.id] = -1;

            var rowCount = new Dictionary<int, int>();
            foreach (var n in _story.nodes)
            {
                int d = depth[n.id];
                if (d < 0)
                {
                    n.editorX = 40f + orphanDepth * 260f;
                    n.editorY = 40f + 700f; // 放在下方
                    orphanDepth++;
                    continue;
                }
                if (!rowCount.ContainsKey(d)) rowCount[d] = 0;
                int row = rowCount[d];
                rowCount[d] = row + 1;
                n.editorX = 40f + d * 280f;
                n.editorY = 40f + row * 150f;
            }

            _pan = new Vector2(40, 40);
            _status = "已自动排版。";
        }

        private IEnumerable<string> Targets(StoryNode n)
        {
            if (!string.IsNullOrEmpty(n.next)) yield return n.next;
            if (n.choices != null)
                foreach (var c in n.choices)
                    if (!string.IsNullOrEmpty(c.next)) yield return c.next;
            if (n.conditionalNexts != null)
                foreach (var c in n.conditionalNexts)
                    if (!string.IsNullOrEmpty(c.next)) yield return c.next;
        }
    }
}

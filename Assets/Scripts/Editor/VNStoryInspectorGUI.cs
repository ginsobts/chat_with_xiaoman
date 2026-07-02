using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>
    /// 单个剧情节点的字段编辑 UI，供列表编辑器与节点图编辑器共用。
    /// </summary>
    public static class VNStoryInspectorGUI
    {
        public static readonly string[] VariableOperations = { "set", "add", "subtract" };
        public static readonly string[] ConditionComparisons =
        {
            "equals",
            "notEquals",
            "greater",
            "greaterOrEqual",
            "less",
            "lessOrEqual"
        };

        /// <summary>绘制一个节点的所有可编辑字段。</summary>
        public static void DrawInspector(StoryNode node)
        {
            if (node == null)
                return;

            node.id = EditorGUILayout.TextField("ID", node.id);
            node.speaker = EditorGUILayout.TextField("Speaker", node.speaker);
            node.background = EditorGUILayout.TextField("Background", node.background);
            node.keepCharacters = EditorGUILayout.Toggle("Keep Characters", node.keepCharacters);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Text");
            node.text = EditorGUILayout.TextArea(node.text, GUILayout.MinHeight(90));

            node.voice = EditorGUILayout.TextField("Voice", node.voice);
            if (!string.IsNullOrEmpty(node.voice))
                EditorGUILayout.HelpBox("配音文件放在 StreamingAssets/voice/" + node.voice + ".wav（或 .ogg/.mp3）。", MessageType.None);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Player Input", EditorStyles.boldLabel);
            node.inputVariable = EditorGUILayout.TextField("Input Variable", node.inputVariable);
            if (!string.IsNullOrEmpty(node.inputVariable))
            {
                node.inputPlaceholder = EditorGUILayout.TextField("Placeholder", node.inputPlaceholder);
                node.inputButtonText = EditorGUILayout.TextField("Button Text", node.inputButtonText);
                EditorGUILayout.HelpBox("输入结果会保存为变量；之后文本里写 %" + node.inputVariable + "% 就会显示玩家输入的内容。", MessageType.None);
            }

            EditorGUILayout.Space(8);
            node.next = EditorGUILayout.TextField("Next", node.next);
            node.ending = EditorGUILayout.TextField("Ending", node.ending);

            EditorGUILayout.Space(10);
            DrawVariableChanges("Set Variables When Entering This Node", node.setVariables);

            EditorGUILayout.Space(10);
            DrawConditionalNexts(node);

            EditorGUILayout.Space(10);
            DrawCharacters(node);

            EditorGUILayout.Space(10);
            DrawChoices(node);
        }

        public static void DrawCharacters(StoryNode node)
        {
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);

            if (node.characters == null)
                node.characters = new List<CharacterSlot>();

            EditorGUILayout.HelpBox("空列表 = 清空立绘；勾选 Keep Characters = 保持上一幕立绘。", MessageType.None);

            for (int i = 0; i < node.characters.Count; i++)
            {
                var character = node.characters[i];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Character " + (i + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                        {
                            node.characters.RemoveAt(i);
                            break;
                        }
                    }

                    character.sprite = EditorGUILayout.TextField("Sprite", character.sprite);
                    character.position = EditorGUILayout.TextField("Position", character.position);
                }
            }

            if (GUILayout.Button("+ Add Character"))
            {
                node.characters.Add(new CharacterSlot
                {
                    sprite = "xiaoyi",
                    position = "center"
                });
            }
        }

        public static void DrawChoices(StoryNode node)
        {
            EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);

            if (node.choices == null)
                node.choices = new List<Choice>();

            EditorGUILayout.HelpBox("有选项时，玩家会点选项跳转；没有选项时，点击画面走 Next。", MessageType.None);

            for (int i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Choice " + (i + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                        {
                            node.choices.RemoveAt(i);
                            break;
                        }
                    }

                    choice.text = EditorGUILayout.TextField("Text", choice.text);
                    choice.next = EditorGUILayout.TextField("Next", choice.next);

                    if (choice.setVariables == null)
                        choice.setVariables = new List<VariableChange>();

                    EditorGUILayout.Space(4);
                    DrawVariableChanges("Set Variables When Picking This Choice", choice.setVariables);
                }
            }

            if (GUILayout.Button("+ Add Choice"))
            {
                node.choices.Add(new Choice
                {
                    text = "新的选项",
                    next = ""
                });
            }
        }

        public static void DrawVariableChanges(string title, List<VariableChange> changes)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("set=直接设置；add/subtract=把变量当数字加减。比如 favor add 1。", MessageType.None);

            if (changes == null)
                return;

            for (int i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Variable " + (i + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                        {
                            changes.RemoveAt(i);
                            break;
                        }
                    }

                    change.name = EditorGUILayout.TextField("Name", change.name);
                    change.operation = DrawPopup("Operation", change.operation, VariableOperations);
                    if (change.operation == "add" || change.operation == "subtract")
                        change.amount = EditorGUILayout.IntField("Amount", change.amount);
                    else
                        change.value = EditorGUILayout.TextField("Value", change.value);
                }
            }

            if (GUILayout.Button("+ Add Variable Change"))
            {
                changes.Add(new VariableChange
                {
                    name = "favor",
                    operation = "add",
                    amount = 1
                });
            }
        }

        public static void DrawConditionalNexts(StoryNode node)
        {
            EditorGUILayout.LabelField("Conditional Nexts Before Showing This Node", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("进入这个节点前先检查变量；命中后不显示本节点，直接跳去 Next。可做很远之后的对白变化。", MessageType.None);

            if (node.conditionalNexts == null)
                node.conditionalNexts = new List<ConditionalNext>();

            for (int i = 0; i < node.conditionalNexts.Count; i++)
            {
                var condition = node.conditionalNexts[i];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Condition " + (i + 1), EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(80)))
                        {
                            node.conditionalNexts.RemoveAt(i);
                            break;
                        }
                    }

                    condition.variable = EditorGUILayout.TextField("Variable", condition.variable);
                    condition.comparison = DrawPopup("Comparison", condition.comparison, ConditionComparisons);
                    condition.equals = EditorGUILayout.TextField("Value", condition.equals);
                    condition.next = EditorGUILayout.TextField("Next", condition.next);
                }
            }

            if (GUILayout.Button("+ Add Conditional Next"))
            {
                node.conditionalNexts.Add(new ConditionalNext
                {
                    variable = "favor",
                    comparison = "greaterOrEqual",
                    equals = "3",
                    next = ""
                });
            }
        }

        public static string DrawPopup(string label, string value, string[] options)
        {
            int index = Array.IndexOf(options, value);
            if (index < 0)
                index = 0;
            index = EditorGUILayout.Popup(label, index, options);
            return options[index];
        }
    }
}

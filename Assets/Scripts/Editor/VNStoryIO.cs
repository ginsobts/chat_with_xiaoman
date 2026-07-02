using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VN.EditorTools
{
    /// <summary>剧本读写与字段补全，供所有编辑器共用。</summary>
    public static class VNStoryIO
    {
        public static string StoryPath => Path.Combine(Application.streamingAssetsPath, "story.json");

        public static StoryData Load(out string status)
        {
            try
            {
                if (!File.Exists(StoryPath))
                {
                    status = "找不到 story.json。";
                    return null;
                }

                string json = JsonCommentUtility.StripComments(File.ReadAllText(StoryPath));
                var story = JsonUtility.FromJson<StoryData>(json);
                if (story == null)
                    story = new StoryData();
                if (story.nodes == null)
                    story.nodes = new List<StoryNode>();

                Normalize(story);
                status = "Loaded story.json";
                return story;
            }
            catch (System.Exception e)
            {
                status = "读取失败：" + e.Message;
                return null;
            }
        }

        public static string Save(StoryData story)
        {
            try
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
                File.WriteAllText(StoryPath, JsonUtility.ToJson(story, true));
                AssetDatabase.Refresh();
                return "Saved story.json";
            }
            catch (System.Exception e)
            {
                return "保存失败：" + e.Message;
            }
        }

        public static void Normalize(StoryData story)
        {
            foreach (var node in story.nodes)
            {
                if (node.characters == null) node.characters = new List<CharacterSlot>();
                if (node.choices == null) node.choices = new List<Choice>();
                if (node.setVariables == null) node.setVariables = new List<VariableChange>();
                if (node.conditionalNexts == null) node.conditionalNexts = new List<ConditionalNext>();

                NormalizeVariableChanges(node.setVariables);
                NormalizeConditionalNexts(node.conditionalNexts);

                foreach (var choice in node.choices)
                {
                    if (choice.setVariables == null)
                        choice.setVariables = new List<VariableChange>();
                    NormalizeVariableChanges(choice.setVariables);
                }
            }
        }

        private static void NormalizeVariableChanges(List<VariableChange> changes)
        {
            foreach (var change in changes)
            {
                if (string.IsNullOrEmpty(change.operation))
                    change.operation = "set";
                if (change.amount == 0)
                    change.amount = 1;
            }
        }

        private static void NormalizeConditionalNexts(List<ConditionalNext> conditions)
        {
            foreach (var condition in conditions)
            {
                if (string.IsNullOrEmpty(condition.comparison))
                    condition.comparison = "equals";
            }
        }

        public static bool HasNodeId(StoryData story, string id)
        {
            foreach (var node in story.nodes)
                if (node.id == id)
                    return true;
            return false;
        }

        public static string MakeUniqueId(StoryData story, string prefix)
        {
            string cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? "node" : prefix.Trim();
            cleanPrefix = cleanPrefix.Replace(" ", "_");

            string candidate = cleanPrefix;
            int index = 1;
            while (HasNodeId(story, candidate))
            {
                candidate = cleanPrefix + "_" + index;
                index++;
            }
            return candidate;
        }
    }
}

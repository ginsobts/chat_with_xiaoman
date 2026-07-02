using System.IO;
using UnityEngine;

namespace VN
{
    public static class StoryLoader
    {
        public static StoryData Load() => Load("story.json");

        public static StoryData Load(string fileName)
        {
            try
            {
                string raw = null;

                // 优先从加密包读取；没有包（如编辑器开发）再读明文文件。
                if (Pak.TryGetText(fileName, out var packed))
                {
                    raw = packed;
                }
                else
                {
                    string path = Path.Combine(Application.streamingAssetsPath, fileName);
                    if (File.Exists(path)) raw = File.ReadAllText(path);
                }

                if (raw == null)
                {
                    Debug.LogError("找不到剧本文件: " + fileName);
                    return null;
                }

                string json = JsonCommentUtility.StripComments(raw);
                var data = JsonUtility.FromJson<StoryData>(json);
                if (data == null || data.nodes == null || data.nodes.Count == 0)
                {
                    Debug.LogError("剧本解析失败或为空: " + fileName);
                    return null;
                }
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError("读取剧本出错: " + e.Message);
                return null;
            }
        }
    }
}

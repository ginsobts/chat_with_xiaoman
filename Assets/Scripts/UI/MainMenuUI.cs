using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace VN
{
    public static class MainMenuUI
    {
        public static void Build(RectTransform root, UnityAction onStart, UnityAction onQuit, bool storyOk)
        {
            var bg = UITheme.AddImage("MenuBG", root, Color.white);
            UITheme.FullStretch(bg.rectTransform);

            var silhouette = UITheme.AddImage("XiaoxingSilhouette", root, Color.black);
            silhouette.sprite = AssetCache.GetSprite("xiaoyi_silhouette");
            silhouette.preserveAspect = true;
            silhouette.raycastTarget = false;
            UITheme.SetRect(silhouette.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260, -170), new Vector2(260, 430));

            var startBtn = UITheme.AddButton("StartBtn", root, "开始游戏", 40, onStart);
            UITheme.SetRect(startBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, 180), new Vector2(220, 270));
            startBtn.interactable = storyOk;

            var quitBtn = UITheme.AddButton("QuitBtn", root, "退出", 36, onQuit);
            UITheme.SetRect(quitBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, 70), new Vector2(220, 150));

            if (!storyOk)
            {
                var warn = UITheme.AddText("Warn", root,
                    "未找到剧本 story.json", 28, new Color(0.9f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
                UITheme.SetRect(warn.rectTransform,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-400, 60), new Vector2(400, 120));
            }
        }
    }
}

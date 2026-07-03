using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace VN
{
    public static class MainMenuUI
    {
        public static void Build(RectTransform root, UnityAction onStart, UnityAction onQuit,
            UnityAction onToggleLanguage, bool storyOk)
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

            var startBtn = UITheme.AddButton("StartBtn", root, GameLanguage.MainMenuStart, 40, onStart);
            UITheme.SetRect(startBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, 280), new Vector2(220, 365));
            startBtn.interactable = storyOk;

            var langBtn = UITheme.AddButton("LanguageBtn", root, GameLanguage.LanguageButton, 30, onToggleLanguage);
            UITheme.SetRect(langBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, 175), new Vector2(220, 255));

            var quitBtn = UITheme.AddButton("QuitBtn", root, GameLanguage.MainMenuQuit, 36, onQuit);
            UITheme.SetRect(quitBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, 70), new Vector2(220, 150));

            if (!storyOk)
            {
                var warn = UITheme.AddText("Warn", root,
                    GameLanguage.MissingStory, 28, new Color(0.9f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
                UITheme.SetRect(warn.rectTransform,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-400, 375), new Vector2(400, 435));
            }
        }
    }
}

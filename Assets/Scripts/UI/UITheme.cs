using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace VN
{
    /// <summary>
    /// 运行时构建 UGUI 的小工具集合。使用系统 CJK 字体以正确显示中文。
    /// </summary>
    public static class UITheme
    {
        private static Font _font;

        public static Font CJKFont
        {
            get
            {
                if (_font == null)
                {
                    // 优先用笔画更硬朗的中文字体，缩放到小窗口时更清晰。
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "SimHei", "黑体", "Microsoft YaHei", "微软雅黑", "SimSun", "Arial" }, 48);
                    if (_font == null)
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static RectTransform FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Image AddImage(string name, Transform parent, Color color)
        {
            var go = NewUIObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Outline AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
            return outline;
        }

        public static Image AddPanel(string name, Transform parent, Color fillColor)
        {
            var img = AddImage(name, parent, fillColor);
            AddOutline(img, Color.black, new Vector2(3f, -3f));
            return img;
        }

        public static Text AddText(string name, Transform parent, string content,
            int fontSize, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = NewUIObject(name, parent);
            var txt = go.AddComponent<Text>();
            txt.font = CJKFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            return txt;
        }

        public static Button AddButton(string name, Transform parent, string label,
            int fontSize, UnityAction onClick)
        {
            var img = AddPanel(name, parent, Color.white);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var label2 = AddText("Label", img.transform, label, fontSize, Color.black, TextAnchor.MiddleCenter);
            FullStretch(label2.rectTransform);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}

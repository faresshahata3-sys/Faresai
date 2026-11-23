using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StreetRush
{
    /// <summary>Helpers for building the game's uGUI screens from code.</summary>
    public static class UIFactory
    {
        public static readonly Color Gold = new Color(1f, 0.76f, 0.12f);
        public static readonly Color Panel = new Color(0.06f, 0.07f, 0.1f, 0.78f);

        static Font _font;
        static Sprite _round, _rounded;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("Fonts/DejaVuSans-Bold");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int order)
        {
            EnsureEventSystem();
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, Sprite sprite = null, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = raycast;
            if (sprite == RoundedSprite) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(RectTransform rt, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.55f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent, anchor, anchor, anchor, pos, size);
            return Text(rt, text, fontSize, color, align);
        }

        /// <summary>Menu-style button: rounded dark panel, gold border on press.</summary>
        public static Button MenuButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, string text, System.Action onClick, out Text label)
        {
            var rt = Rect(name, parent, anchor, anchor, anchor, pos, size);
            var img = Image(rt, new Color(0.1f, 0.11f, 0.16f, 1f), RoundedSprite, true);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.76f, 0.12f, 0.65f);
            outline.effectDistance = new Vector2(2, -2);
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(1f, 0.8f, 0.4f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colors;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var lrt = Stretch("Label", rt);
            label = Text(lrt, text, 26, Color.white);
            return btn;
        }

        /// <summary>Round touch control with an icon sprite (or text).</summary>
        public static TouchButton RoundButton(Transform parent, string name, Vector2 anchor, Vector2 pos, float diameter, Sprite icon, string fallbackText = null)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(diameter, diameter));
            var bg = Image(rt, new Color(0.05f, 0.06f, 0.08f, 0.55f), RoundSprite, true);
            var ring = rt.gameObject.AddComponent<Outline>();
            ring.effectColor = new Color(1, 1, 1, 0.25f);
            ring.effectDistance = new Vector2(2, -2);
            if (icon != null)
            {
                var irt = Rect("Icon", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter * 0.62f, diameter * 0.62f));
                var iimg = Image(irt, Color.white, icon);
                iimg.preserveAspect = true;
            }
            else if (fallbackText != null)
            {
                Text(Stretch("Label", rt), fallbackText, (int)(diameter * 0.26f), Color.white);
            }
            var tb = rt.gameObject.AddComponent<TouchButton>();
            tb.background = bg;
            return tb;
        }

        public static Sprite Icon(string name) => Resources.Load<Sprite>("UI/" + name);

        public static Sprite RoundSprite
        {
            get
            {
                if (_round == null) _round = MakeRoundedSprite(128, 64, false);
                return _round;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null) _rounded = MakeRoundedSprite(64, 18, true);
                return _rounded;
            }
        }

        static Sprite MakeRoundedSprite(int size, int radius, bool sliced)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}

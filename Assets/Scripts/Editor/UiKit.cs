using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// The widgets the generated screens are made of, and the palette they are made in.
    ///
    /// Extracted when the lobby became the third screen to want MakeText and MakeWideButton.
    /// Two copies is a coincidence; three is a fork — and a fork means the login's gold and the
    /// lobby's gold drift apart by a hex digit and nobody can say which one is right.
    /// </summary>
    internal static class UiKit
    {
        // ------------------------------------------------------------------- palette

        internal static readonly Color BgTop = new(0.06f, 0.04f, 0.03f);
        internal static readonly Color BgBot = new(0.02f, 0.015f, 0.01f);
        internal static readonly Color Gold = new(0.92f, 0.72f, 0.15f);
        internal static readonly Color GoldDim = new(0.6f, 0.45f, 0.1f);
        internal static readonly Color TextWhite = new(0.92f, 0.9f, 0.88f);
        internal static readonly Color TextDim = new(0.4f, 0.38f, 0.35f);
        internal static readonly Color BarBg = new(0.15f, 0.12f, 0.1f);
        internal static readonly Color Panel = new(0.03f, 0.025f, 0.02f, 0.62f);

        // -------------------------------------------------------------------- canvas

        /// <summary>
        /// A screen-space canvas at the project's reference resolution. Every generated screen
        /// uses the same one, which is why a 22px label means the same thing on all of them.
        /// </summary>
        internal static Canvas MakeCanvas(string name)
        {
            var go = new GameObject(name,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        internal static GameObject MakeGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        // --------------------------------------------------------------------- parts

        internal static TMP_Text MakeText(Transform parent, string name, string text,
                                          float size, Color color, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(800f, 60f);

            return tmp;
        }

        /// <summary>A flat coloured rectangle. Panels, outlines, bar tracks, dividers.</summary>
        internal static Image MakeRect(Transform parent, string name, Color color,
                                       Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        internal static Button MakeButton(Transform parent, string name, string label,
                                          Color bg, Color textColor, Vector2 anchor,
                                          Vector2 pos, Vector2 size, float fontSize = 22f)
        {
            var go = new GameObject(name + "Btn", typeof(RectTransform),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = bg;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colors;

            TMP_Text text = MakeText(go.transform, name + "Label", label, fontSize, textColor,
                                     new Vector2(0.5f, 0.5f), Vector2.zero);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 6f;
            text.rectTransform.sizeDelta = size;

            return btn;
        }

        /// <summary>
        /// A horizontal fill bar — XP, loading, health. Returns the fill, which is the part
        /// anything ever wants to set; the track behind it is scenery.
        /// </summary>
        internal static Image MakeBar(Transform parent, string name, Color fillColor,
                                      Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Image track = MakeRect(parent, name + "Track", BarBg, anchor, pos, size);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(track.transform, false);
            Stretch(fillGo.GetComponent<RectTransform>());

            var fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;

            // Filled needs a sprite to slice, and Unity's built-in white one is exactly that.
            fill.sprite = BuiltinSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            return fill;
        }

        /// <summary>
        /// A slider, for the settings panel. Unity's Slider needs its fill and handle wired by
        /// hand — left unwired it renders as an empty box that swallows drags and does nothing,
        /// with no warning anywhere.
        /// </summary>
        internal static Slider MakeSlider(Transform parent, string name, Vector2 anchor,
                                          Vector2 pos, float width, float value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 30f);

            // Track.
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 8f);
            bgRt.anchoredPosition = Vector2.zero;
            bgGo.GetComponent<Image>().color = BarBg;

            // Fill, in its own area — Slider drives the area's anchors, not the fill's.
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.sizeDelta = new Vector2(-20f, 8f);
            fillAreaRt.anchoredPosition = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(10f, 0f);
            fillGo.GetComponent<Image>().color = Gold;

            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(go.transform, false);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0f);
            handleAreaRt.anchorMax = new Vector2(1f, 1f);
            handleAreaRt.sizeDelta = new Vector2(-20f, 0f);
            handleAreaRt.anchoredPosition = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(26f, 26f);
            handleGo.GetComponent<Image>().color = TextWhite;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;

            return slider;
        }

        // ------------------------------------------------------------------- helpers

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A 1x1 white sprite, cached as an asset. Image.Type.Filled refuses to fill without a
        /// sprite to slice, and every bar in the project needs one.
        /// </summary>
        internal static Sprite BuiltinSprite()
        {
            const string path = "Assets/Settings/S_White.asset";

            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();

            UnityEditor.AssetDatabase.CreateAsset(tex, "Assets/Settings/T_White.asset");

            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "S_White";
            UnityEditor.AssetDatabase.CreateAsset(sprite, path);

            return sprite;
        }

        /// <summary>
        /// The vertical gradient every screen sits on, as a scrim over whatever is behind it.
        /// Alpha 1 hides the 3D backdrop entirely; the lobby and login both want it translucent.
        /// </summary>
        internal static RawImage MakeScrim(Transform parent, float alpha)
        {
            const string path = "Assets/Settings/T_ScreenGradient.asset";
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
            {
                const int h = 128;
                tex = new Texture2D(4, h) { wrapMode = TextureWrapMode.Clamp };

                for (int y = 0; y < h; y++)
                {
                    float t = Mathf.Pow(y / (float)(h - 1), 1.8f);
                    Color row = Color.Lerp(BgBot, BgTop, t);
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, row);
                }

                tex.Apply();
                UnityEditor.AssetDatabase.CreateAsset(tex, path);
            }

            var go = new GameObject("Scrim", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = new Color(1f, 1f, 1f, alpha);
            raw.raycastTarget = false;

            return raw;
        }
    }
}

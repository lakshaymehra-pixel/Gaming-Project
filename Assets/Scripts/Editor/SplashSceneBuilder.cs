using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the splash scene: a gradient backdrop, a crosshair emblem, the game's name,
    /// a real loading bar, and a tap-to-start prompt. The island loads asynchronously
    /// behind the branding, so the splash costs no extra wait.
    ///
    /// Everything about the look lives in the constants below — retheming the splash is an
    /// edit to this block and a rebuild, nothing else.
    ///
    /// Run from the menu: Game > Build Splash Scene
    /// </summary>
    public static class SplashSceneBuilder
    {
        // ------------------------------------------------------------------ THE LOOK
        // Change these, rerun the menu item, done.

        private const string GameTitle = "GAMING PROJECT";
        private const string Tagline = "SURVIVE THE ISLAND";
        private const string VersionLabel = "v0.1 — early build";

        private static readonly Color Accent = new(0.55f, 0.85f, 0.35f);   // jungle green
        private static readonly Color BackgroundTop = new(0.04f, 0.07f, 0.05f);
        private static readonly Color BackgroundBottom = new(0.01f, 0.02f, 0.015f);
        private static readonly Color TitleColor = new(0.92f, 0.95f, 0.90f);
        private static readonly Color DimText = new(0.55f, 0.60f, 0.52f);

        // --------------------------------------------------------------------------

        private const string ScenePath = "Assets/Scenes/Splash.unity";

        [MenuItem("Game/Build Splash Scene")]
        public static void Build()
        {
            if (ArenaSceneBuilder.BlockedByPlayMode()) return;

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera so the scene renders (and hosts the listener); the canvas is overlay.
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            var canvasGo = new GameObject("Splash",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform c = canvasGo.transform;

            BuildBackground(c);

            // Everything that fades lives under one CanvasGroup.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            contentGo.transform.SetParent(c, false);
            Stretch(contentGo.GetComponent<RectTransform>());
            var content = contentGo.GetComponent<CanvasGroup>();
            Transform ct = contentGo.transform;

            BuildEmblem(ct);

            TMP_Text title = MakeText(ct, "Title", GameTitle, 96, TitleColor,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -40f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 18f;

            TMP_Text tagline = MakeText(ct, "Tagline", Tagline, 30, DimText,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -125f));
            tagline.characterSpacing = 26f;

            Image loadingFill = BuildLoadingBar(ct);

            TMP_Text tap = MakeText(ct, "TapPrompt", "TAP TO START", 26, Accent,
                new Vector2(0.5f, 0f), new Vector2(0f, 190f));
            tap.characterSpacing = 22f;

            MakeText(ct, "Version", VersionLabel, 20, DimText,
                new Vector2(1f, 0f), new Vector2(-140f, 36f));

            var controller = canvasGo.AddComponent<SplashController>();
            ArenaSceneBuilder.SetPrivate(controller, "content", content);
            ArenaSceneBuilder.SetPrivate(controller, "loadingFill", loadingFill);
            ArenaSceneBuilder.SetPrivate(controller, "tapPrompt", tap);
            ArenaSceneBuilder.SetPrivate(controller, "nextSceneName", "Island");

            EditorSceneManager.SaveScene(scene, ScenePath);
            ArenaSceneBuilder.AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<b>Splash built.</b> Saved to {ScenePath}. It boots first and " +
                      "loads the Island behind the branding — make sure the Island scene " +
                      "is built too.");
        }

        // ------------------------------------------------------------------ backdrop

        private static void BuildBackground(Transform parent)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            go.GetComponent<RawImage>().texture = MakeGradientTexture();
        }

        /// <summary>Vertical gradient saved as an asset so the scene can reference it.</summary>
        private static Texture2D MakeGradientTexture()
        {
            const string path = "Assets/Settings/T_SplashGradient.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int h = 256;
            var tex = new Texture2D(4, h) { wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < h; y++)
            {
                // Ease the blend so the bright band sits in the upper third, behind the title.
                float t = Mathf.Pow(y / (float)(h - 1), 1.6f);
                Color row = Color.Lerp(BackgroundBottom, BackgroundTop, t);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, row);
            }

            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        // -------------------------------------------------------------------- emblem

        /// <summary>
        /// A crosshair mark: ring, centre dot, four ticks. Built from two generated
        /// sprites and plain rects — no imported art, and it inherits the accent colour.
        /// </summary>
        private static void BuildEmblem(Transform parent)
        {
            var emblem = new GameObject("Emblem", typeof(RectTransform));
            emblem.transform.SetParent(parent, false);
            var rt = emblem.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 165f);
            rt.sizeDelta = new Vector2(230f, 230f);

            MakeSpriteImage(emblem.transform, "Ring", RingSprite(), Accent,
                Vector2.zero, new Vector2(210f, 210f));

            MakeSpriteImage(emblem.transform, "Dot", CircleSprite(), Accent,
                Vector2.zero, new Vector2(26f, 26f));

            // Ticks: N, S, E, W — slightly clear of the ring.
            var tickSize = new Vector2(8f, 34f);
            MakeRect(emblem.transform, "Tick_N", Accent, new Vector2(0f, 122f), tickSize, 0f);
            MakeRect(emblem.transform, "Tick_S", Accent, new Vector2(0f, -122f), tickSize, 0f);
            MakeRect(emblem.transform, "Tick_E", Accent, new Vector2(122f, 0f), tickSize, 90f);
            MakeRect(emblem.transform, "Tick_W", Accent, new Vector2(-122f, 0f), tickSize, 90f);
        }

        private static Sprite CircleSprite() =>
            LoadOrCreateSprite("S_Circle", radiusInner: 0f, radiusOuter: 0.5f);

        private static Sprite RingSprite() =>
            LoadOrCreateSprite("S_Ring", radiusInner: 0.42f, radiusOuter: 0.5f);

        /// <summary>
        /// Renders an antialiased disc or ring into a texture and persists both texture and
        /// sprite. UI Images can only show sprites, and Unity ships no circle — so we make
        /// our own once and reuse it.
        /// </summary>
        private static Sprite LoadOrCreateSprite(string name, float radiusInner,
                                                 float radiusOuter)
        {
            string spritePath = $"Assets/Settings/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (existing != null) return existing;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            float rOut = radiusOuter * size;
            float rIn = radiusInner * size;
            var centre = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);

                    // Two smoothsteps make the edges soft: fade in past the inner radius,
                    // fade out at the outer one.
                    float a = Mathf.Clamp01((rOut - d) / 2f);
                    if (rIn > 0f) a *= Mathf.Clamp01((d - rIn) / 2f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            AssetDatabase.CreateAsset(tex, $"Assets/Settings/T_{name}.asset");

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                       new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, spritePath);

            return sprite;
        }

        // ---------------------------------------------------------------- loading bar

        private static Image BuildLoadingBar(Transform parent)
        {
            var track = new GameObject("LoadingTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);

            var rt = track.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            rt.sizeDelta = new Vector2(520f, 6f);

            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.08f);
            trackImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            Stretch(fill.GetComponent<RectTransform>());

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = Accent;
            fillImage.raycastTarget = false;

            // Filled-horizontal needs a sprite to slice; a solid generated one serves.
            fillImage.sprite = CircleSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            return fillImage;
        }

        // ------------------------------------------------------------------- helpers

        private static TMP_Text MakeText(Transform parent, string name, string text,
                                         float size, Color color, Vector2 anchor,
                                         Vector2 position)
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
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(1400f, 140f);

            return tmp;
        }

        private static void MakeSpriteImage(Transform parent, string name, Sprite sprite,
                                            Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void MakeRect(Transform parent, string name, Color color,
                                     Vector2 position, Vector2 size, float rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

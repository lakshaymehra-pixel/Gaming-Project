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
    /// Builds exact BGMI-style login screen:
    /// - Dark background with floating ember particles
    /// - Large game logo centered-top
    /// - Bottom bar with social login icons (Google, Facebook, Twitter, Guest)
    /// - Loading bar + "Tap to continue" after login
    /// - Terms at very bottom
    /// - Ambient BGM loop
    ///
    /// Run from: Game > Build Login Scene
    /// </summary>
    public static class LoginSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Login.unity";

        // BGMI-style dark military palette
        private static readonly Color BgTop = new(0.06f, 0.04f, 0.03f);
        private static readonly Color BgBot = new(0.02f, 0.015f, 0.01f);
        private static readonly Color Gold = new(0.92f, 0.72f, 0.15f);
        private static readonly Color GoldDim = new(0.6f, 0.45f, 0.1f);
        private static readonly Color TextWhite = new(0.92f, 0.9f, 0.88f);
        private static readonly Color TextDim = new(0.4f, 0.38f, 0.35f);
        private static readonly Color BarBg = new(0.15f, 0.12f, 0.1f);
        private static readonly Color BtnGoogle = new(0.85f, 0.32f, 0.25f);
        private static readonly Color BtnFacebook = new(0.23f, 0.35f, 0.6f);
        private static readonly Color BtnTwitter = new(0.1f, 0.14f, 0.18f);
        private static readonly Color BtnGuest = new(0.35f, 0.35f, 0.32f);
        private static readonly Color BottomBar = new(0.03f, 0.025f, 0.02f, 0.9f);

        [MenuItem("Game/Build Login Scene")]
        public static void Build()
        {
            if (ArenaSceneBuilder.BlockedByPlayMode()) return;

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgBot;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            // SFX
            var sfxGo = new GameObject("SFX");
            var sfx = sfxGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            // BGM
            AudioSource bgm = BuildBgm();

            // Event system
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // Canvas
            var canvasGo = new GameObject("LoginCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform c = canvasGo.transform;

            // ── Main group ──
            var mainGo = MakeGroup(c, "MainGroup");
            var mainGroup = mainGo.GetComponent<CanvasGroup>();
            Transform m = mainGo.transform;

            // Background gradient
            BuildGradientBg(m);

            // Particle container
            var particleGo = new GameObject("Particles", typeof(RectTransform));
            particleGo.transform.SetParent(m, false);
            Stretch(particleGo.GetComponent<RectTransform>());
            var particleContainer = particleGo.GetComponent<RectTransform>();

            // ── Logo Group (center-top area) ──
            var logoGo = MakeGroup(m, "LogoGroup");
            var logoGroup = logoGo.GetComponent<CanvasGroup>();
            logoGroup.alpha = 0f;

            // Game title - large bold, BGMI style
            var titleMain = MakeText(logoGo.transform, "TitleMain", "KAAL", 160,
                TextWhite, new Vector2(0.5f, 0.6f), new Vector2(0f, 80f));
            titleMain.fontStyle = FontStyles.Bold;
            titleMain.characterSpacing = 30f;
            // Add shadow for depth
            var shadow1 = titleMain.gameObject.AddComponent<Shadow>();
            shadow1.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow1.effectDistance = new Vector2(4f, -4f);

            var titleSub = MakeText(logoGo.transform, "TitleSub", "RAAT", 160,
                Gold, new Vector2(0.5f, 0.6f), new Vector2(0f, -80f));
            titleSub.fontStyle = FontStyles.Bold;
            titleSub.characterSpacing = 30f;
            // Red shadow for horror glow
            var shadow2 = titleSub.gameObject.AddComponent<Shadow>();
            shadow2.effectColor = new Color(0.6f, 0.1f, 0f, 0.5f);
            shadow2.effectDistance = new Vector2(3f, -3f);
            // Outline for extra punch
            var outline = titleSub.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.8f, 0.3f, 0f, 0.3f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Tagline under title
            var tagline = MakeText(logoGo.transform, "Tagline",
                "HORROR  •  SURVIVAL  •  BATTLE ROYALE", 24,
                GoldDim, new Vector2(0.5f, 0.6f), new Vector2(0f, -185f));
            tagline.characterSpacing = 10f;

            // Gold line separator (left)
            var lineL = new GameObject("GoldLineL", typeof(RectTransform), typeof(Image));
            lineL.transform.SetParent(logoGo.transform, false);
            var lineLRt = lineL.GetComponent<RectTransform>();
            lineLRt.anchorMin = lineLRt.anchorMax = new Vector2(0.5f, 0.6f);
            lineLRt.anchoredPosition = new Vector2(-200f, -220f);
            lineLRt.sizeDelta = new Vector2(180f, 2f);
            lineL.GetComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            lineL.GetComponent<Image>().raycastTarget = false;

            // Diamond/dot in center
            var diamond = MakeText(logoGo.transform, "Diamond", "◆", 16,
                Gold, new Vector2(0.5f, 0.6f), new Vector2(0f, -220f));

            // Gold line separator (right)
            var lineR = new GameObject("GoldLineR", typeof(RectTransform), typeof(Image));
            lineR.transform.SetParent(logoGo.transform, false);
            var lineRRt = lineR.GetComponent<RectTransform>();
            lineRRt.anchorMin = lineRRt.anchorMax = new Vector2(0.5f, 0.6f);
            lineRRt.anchoredPosition = new Vector2(200f, -220f);
            lineRRt.sizeDelta = new Vector2(180f, 2f);
            lineR.GetComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            lineR.GetComponent<Image>().raycastTarget = false;

            // Season/version text
            MakeText(logoGo.transform, "Season", "S E A S O N  1  •  E A R L Y  A C C E S S",
                16, TextDim, new Vector2(0.5f, 0.6f), new Vector2(0f, -255f));

            // ── Bottom bar with login buttons ──
            var bottomGo = MakeGroup(m, "BottomGroup");
            var bottomGroup = bottomGo.GetComponent<CanvasGroup>();
            bottomGroup.alpha = 0f;

            // Dark bottom strip (BGMI style)
            var stripGo = new GameObject("BottomStrip", typeof(RectTransform), typeof(Image));
            stripGo.transform.SetParent(bottomGo.transform, false);
            var stripRt = stripGo.GetComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(1f, 0f);
            stripRt.pivot = new Vector2(0.5f, 0f);
            stripRt.anchoredPosition = Vector2.zero;
            stripRt.sizeDelta = new Vector2(0f, 320f);
            stripGo.GetComponent<Image>().color = BottomBar;
            stripGo.GetComponent<Image>().raycastTarget = false;

            // "Select login method" text
            MakeText(bottomGo.transform, "SelectLabel",
                "SELECT LOGIN METHOD", 20,
                TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 280f));

            // Login buttons - horizontal row (BGMI style, larger with glow borders)
            float btnY = 195f;
            float btnSize = 100f;
            float spacing = 150f;
            float startX = -spacing * 1.5f;

            Button googleBtn = MakeIconButton(bottomGo.transform, "Google", "G",
                BtnGoogle, Gold, new Vector2(startX, btnY), btnSize);
            MakeText(bottomGo.transform, "GoogleLabel", "Google", 16,
                TextWhite, new Vector2(0.5f, 0f), new Vector2(startX, btnY - 65f));

            Button fbBtn = MakeIconButton(bottomGo.transform, "Facebook", "f",
                BtnFacebook, Gold, new Vector2(startX + spacing, btnY), btnSize);
            MakeText(bottomGo.transform, "FBLabel", "Facebook", 16,
                TextWhite, new Vector2(0.5f, 0f), new Vector2(startX + spacing, btnY - 65f));

            Button twitterBtn = MakeIconButton(bottomGo.transform, "Twitter", "X",
                BtnTwitter, Gold, new Vector2(startX + spacing * 2, btnY), btnSize);
            MakeText(bottomGo.transform, "TwitterLabel", "Twitter", 16,
                TextWhite, new Vector2(0.5f, 0f), new Vector2(startX + spacing * 2, btnY - 65f));

            Button guestBtn = MakeIconButton(bottomGo.transform, "Guest", "?",
                BtnGuest, Gold, new Vector2(startX + spacing * 3, btnY), btnSize);
            MakeText(bottomGo.transform, "GuestLabel", "Guest", 16,
                TextWhite, new Vector2(0.5f, 0f), new Vector2(startX + spacing * 3, btnY - 65f));

            // ── Loading group (shown after login) ──
            var loadingGo = MakeGroup(m, "LoadingGroup");
            var loadingGroup = loadingGo.GetComponent<CanvasGroup>();
            loadingGroup.alpha = 0f;

            var loadingText = MakeText(loadingGo.transform, "LoadingText",
                "Connecting...", 24, TextWhite,
                new Vector2(0.5f, 0f), new Vector2(0f, 250f));

            // Loading bar track
            var trackGo = new GameObject("LoadingTrack", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(loadingGo.transform, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 0f);
            trackRt.anchoredPosition = new Vector2(0f, 210f);
            trackRt.sizeDelta = new Vector2(600f, 6f);
            trackGo.GetComponent<Image>().color = BarBg;
            trackGo.GetComponent<Image>().raycastTarget = false;

            // Loading bar fill
            var fillGo = new GameObject("LoadingFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            Stretch(fillGo.GetComponent<RectTransform>());
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = Gold;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;
            fillImg.raycastTarget = false;

            // "Tap to continue" (hidden initially)
            var tapText = MakeText(loadingGo.transform, "TapText",
                "TAP TO CONTINUE", 28, Gold,
                new Vector2(0.5f, 0f), new Vector2(0f, 250f));
            tapText.characterSpacing = 12f;
            tapText.gameObject.SetActive(false);

            // ── Terms group ──
            var termsGo = MakeGroup(m, "TermsGroup");
            var termsGroup = termsGo.GetComponent<CanvasGroup>();
            termsGroup.alpha = 0f;

            MakeText(termsGo.transform, "Terms",
                "By continuing you agree to our Terms of Service & Privacy Policy",
                12, TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 25f));

            // ── Wire LoginScreen ──
            var login = canvasGo.AddComponent<LoginScreen>();
            ArenaSceneBuilder.SetPrivate(login, "mainGroup", mainGroup);
            ArenaSceneBuilder.SetPrivate(login, "logoGroup", logoGroup);
            ArenaSceneBuilder.SetPrivate(login, "bottomGroup", bottomGroup);
            ArenaSceneBuilder.SetPrivate(login, "loadingGroup", loadingGroup);
            ArenaSceneBuilder.SetPrivate(login, "termsGroup", termsGroup);
            ArenaSceneBuilder.SetPrivate(login, "particleContainer", particleContainer);
            ArenaSceneBuilder.SetPrivate(login, "googleButton", googleBtn);
            ArenaSceneBuilder.SetPrivate(login, "facebookButton", fbBtn);
            ArenaSceneBuilder.SetPrivate(login, "twitterButton", twitterBtn);
            ArenaSceneBuilder.SetPrivate(login, "guestButton", guestBtn);
            ArenaSceneBuilder.SetPrivate(login, "loadingText", loadingText);
            ArenaSceneBuilder.SetPrivate(login, "tapText", tapText);
            ArenaSceneBuilder.SetPrivate(login, "loadingBar", fillImg);
            ArenaSceneBuilder.SetPrivate(login, "nextSceneName", "Island");
            ArenaSceneBuilder.SetPrivate(login, "bgmSource", bgm);
            ArenaSceneBuilder.SetPrivate(login, "sfxSource", sfx);

            // Click sound
            var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/SFX_Empty_Real.mp3");
            if (clickClip == null)
                clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/SFX_Empty.wav");
            if (clickClip != null)
                ArenaSceneBuilder.SetPrivate(login, "clickClip", clickClip);

            // Save
            EditorSceneManager.MarkSceneDirty(scene);
            ArenaSceneBuilder.SaveSceneChecked(scene, ScenePath);
            ArenaSceneBuilder.AddSceneToBuildSettings(ScenePath);

            // Ensure build order: Splash(0), Login(1), Island(2+)
            EnsureBuildOrder();

            Debug.Log($"<b>Login scene built (BGMI style).</b> Saved to {ScenePath}. " +
                      "Build order: Splash → Login → Island.");
        }

        // ── Background gradient ──
        private static void BuildGradientBg(Transform parent)
        {
            const int h = 128;
            var tex = new Texture2D(4, h) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float t = Mathf.Pow(y / (float)(h - 1), 1.8f);
                Color row = Color.Lerp(BgBot, BgTop, t);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, row);
            }
            tex.Apply();

            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<RawImage>().texture = tex;
            go.GetComponent<RawImage>().raycastTarget = false;
        }

        // ── BGM ──
        private static AudioSource BuildBgm()
        {
            var go = new GameObject("BGM");
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0f;
            src.volume = 0.3f;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/AMB_SplashBed.wav");
            if (clip != null) src.clip = clip;
            return src;
        }

        // ── Icon button with glow border (BGMI style) ──
        private static Button MakeIconButton(Transform parent, string name, string icon,
            Color bgColor, Color borderColor, Vector2 pos, float size)
        {
            // Outer glow/border
            var borderGo = new GameObject(name + "Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(parent, false);
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = borderRt.anchorMax = new Vector2(0.5f, 0f);
            borderRt.anchoredPosition = pos;
            borderRt.sizeDelta = new Vector2(size + 6f, size + 6f);
            borderGo.GetComponent<Image>().color = new Color(
                borderColor.r, borderColor.g, borderColor.b, 0.4f);
            borderGo.GetComponent<Image>().raycastTarget = false;

            // Main button
            var go = new GameObject(name + "Btn", typeof(RectTransform),
                typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.color = bgColor;

            // Highlight effect
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            btn.colors = colors;

            // Icon text inside
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            Stretch(iconGo.GetComponent<RectTransform>());
            var tmp = iconGo.AddComponent<TextMeshProUGUI>();
            tmp.text = icon;
            tmp.fontSize = 44;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
        }

        // ── Helpers ──
        private static GameObject MakeGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string text,
            float size, Color color, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(800f, 60f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Forces Splash at 0, Login at 1, everything else after.
        /// </summary>
        private static void EnsureBuildOrder()
        {
            var all = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            // Remove missing files
            all.RemoveAll(s => !System.IO.File.Exists(s.path));

            // Pull splash and login to front in correct order
            var splash = all.FindIndex(s => s.path.EndsWith("Splash.unity"));
            if (splash > 0) { var s = all[splash]; all.RemoveAt(splash); all.Insert(0, s); }

            var login = all.FindIndex(s => s.path.EndsWith("Login.unity"));
            if (login >= 0 && login != 1)
            {
                var l = all[login];
                all.RemoveAt(login);
                all.Insert(Mathf.Min(1, all.Count), l);
            }

            EditorBuildSettings.scenes = all.ToArray();

            // Log the order
            for (int i = 0; i < all.Count; i++)
                Debug.Log($"  Build index {i}: {all[i].path}");
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

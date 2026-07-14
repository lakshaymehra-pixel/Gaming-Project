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
        // The strip behind the form. It has to darken the trees enough that white text on top of
        // them is readable, and no more — at 0.9 it was a wall, and since it grew to 530px to
        // fit the providers row it was walling off half the screen.
        private static readonly Color BottomBar = new(0.03f, 0.025f, 0.02f, 0.62f);

        [MenuItem("Game/Build Login Scene")]
        public static void Build()
        {
            if (ArenaSceneBuilder.BlockedByPlayMode()) return;

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The same night jungle the splash walks into, standing still behind the login. It
            // is the lobby: the island is already out there, waiting, while you pick a name.
            //
            // Lit, unlike the splash. There the darkness is the point — you are meant to be
            // straining to see. Here it is a set you are looking at, and a set nobody can see
            // is a set nobody built.
            SplashBackdrop.Stage stage = SplashBackdrop.Build(lit: true);
            stage.Camera.gameObject.AddComponent<AudioListener>();

            // Nobody walks here — this is a held shot, not the approach. The sway in
            // SplashCamera keeps it alive without going anywhere, because Begin() is never
            // called and the walk never starts.

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

            // The diamond between the two rules. A square turned 45°, not the "◆" character —
            // LiberationSans has no glyph for it, so TMP was substituting the missing-glyph box
            // and drawing a small hollow rectangle in the middle of the logo.
            var diamond = new GameObject("Diamond", typeof(RectTransform), typeof(Image));
            diamond.transform.SetParent(logoGo.transform, false);

            var diamondRt = diamond.GetComponent<RectTransform>();
            diamondRt.anchorMin = diamondRt.anchorMax = new Vector2(0.5f, 0.6f);
            diamondRt.anchoredPosition = new Vector2(0f, -220f);
            diamondRt.sizeDelta = new Vector2(9f, 9f);
            diamondRt.localRotation = Quaternion.Euler(0f, 0f, 45f);

            diamond.GetComponent<Image>().color = Gold;
            diamond.GetComponent<Image>().raycastTarget = false;

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
            stripRt.sizeDelta = new Vector2(0f, 400f);
            stripGo.GetComponent<Image>().color = BottomBar;
            stripGo.GetComponent<Image>().raycastTarget = false;

            // Rows come off one ladder rather than being placed by hand — hand-placed rows are
            // how PLAY AS GUEST once ended up at y=32 with the terms line at y=30, drawn
            // straight through it.
            //
            // Sizes are in a 1920x1080 reference space, so they read about half this big on a
            // phone at arm's length.
            const float rowLabel = 340f;
            const float rowSocial = 258f;   // one tap, three providers
            const float rowGuest = 158f;
            const float rowError = 104f;
            const float rowNotice = 66f;

            MakeText(bottomGo.transform, "SelectLabel", "SIGN IN TO PLAY", 24,
                TextDim, new Vector2(0.5f, 0f), new Vector2(0f, rowLabel));

            // ── One-tap providers ──
            // Each is wired to its own provider in LoginScreen. One whose SDK is not installed
            // says so when tapped, rather than quietly signing the player in as a guest.
            const float socialWidth = 290f;
            const float socialGap = 305f;
            const float socialHeight = 62f;

            Button googleBtn = MakeWideButton(bottomGo.transform, "Google", "GOOGLE",
                new Color(0.85f, 0.32f, 0.25f), Color.white,
                new Vector2(-socialGap, rowSocial), socialWidth, socialHeight);

            Button playGamesBtn = MakeWideButton(bottomGo.transform, "PlayGames", "PLAY GAMES",
                new Color(0.20f, 0.55f, 0.35f), Color.white,
                new Vector2(0f, rowSocial), socialWidth, socialHeight);

            Button facebookBtn = MakeWideButton(bottomGo.transform, "Facebook", "FACEBOOK",
                new Color(0.23f, 0.35f, 0.60f), Color.white,
                new Vector2(socialGap, rowSocial), socialWidth, socialHeight);

            // Guest sits apart and reads quieter. It is the way in for anyone who does not want
            // an account, not the way in the game would prefer — guest progress lives on one
            // phone and dies with it.
            Button guestBtn = MakeWideButton(bottomGo.transform, "Guest", "PLAY AS GUEST",
                new Color(0.14f, 0.13f, 0.12f), TextDim,
                new Vector2(0f, rowGuest), 630f, 52f);

            // A rejected provider explains itself here.
            TMP_Text errorText = MakeText(bottomGo.transform, "ErrorText", "", 19,
                new Color(0.95f, 0.4f, 0.3f), new Vector2(0.5f, 0f), new Vector2(0f, rowError));
            errorText.rectTransform.sizeDelta = new Vector2(1000f, 60f);
            errorText.enableWordWrapping = true;

            // Says out loud when there is no server behind this. LoginScreen hides it once
            // there is one.
            TMP_Text offlineNotice = MakeText(bottomGo.transform, "OfflineNotice",
                "OFFLINE MODE", 16, new Color(0.55f, 0.45f, 0.2f),
                new Vector2(0.5f, 0f), new Vector2(0f, rowNotice));

            // ── Loading group (shown after login) ──
            var loadingGo = MakeGroup(m, "LoadingGroup");
            var loadingGroup = loadingGo.GetComponent<CanvasGroup>();
            loadingGroup.alpha = 0f;

            // Stage label on the left of the bar, percentage on the right — the download-screen
            // layout, and the percentage is the part people actually read.
            // The bar sits where the buttons were — they fade out as it fades in, so the eye
            // stays in one place through the handover instead of being sent somewhere new.
            const float barY = 210f;

            var loadingText = MakeText(loadingGo.transform, "LoadingText",
                "CONNECTING", 26, TextWhite,
                new Vector2(0.5f, 0f), new Vector2(-300f, barY + 40f));
            loadingText.alignment = TextAlignmentOptions.Left;
            loadingText.characterSpacing = 8f;
            loadingText.rectTransform.sizeDelta = new Vector2(600f, 44f);

            var percentText = MakeText(loadingGo.transform, "PercentText",
                "0%", 30, Gold,
                new Vector2(0.5f, 0f), new Vector2(300f, barY + 40f));
            percentText.alignment = TextAlignmentOptions.Right;
            percentText.fontStyle = FontStyles.Bold;
            percentText.rectTransform.sizeDelta = new Vector2(600f, 44f);

            // Loading bar track
            var trackGo = new GameObject("LoadingTrack", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(loadingGo.transform, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 0f);
            trackRt.anchoredPosition = new Vector2(0f, barY);
            trackRt.sizeDelta = new Vector2(900f, 10f);
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

            // "Tap to continue" — below the bar, not on top of the stage label, which is where
            // it used to sit: both were anchored at y=250 and drew over each other.
            var tapText = MakeText(loadingGo.transform, "TapText",
                "TAP TO ENTER THE NIGHT", 32, Gold,
                new Vector2(0.5f, 0f), new Vector2(0f, barY - 60f));
            tapText.characterSpacing = 14f;
            tapText.fontStyle = FontStyles.Bold;
            tapText.gameObject.SetActive(false);

            // ── Terms group ──
            var termsGo = MakeGroup(m, "TermsGroup");
            var termsGroup = termsGo.GetComponent<CanvasGroup>();
            termsGroup.alpha = 0f;

            // Below the offline notice at y=68, not on top of it. This line and PLAY AS GUEST
            // were two pixels apart and drawing through each other.
            MakeText(termsGo.transform, "Terms",
                "By continuing you agree to our Terms of Service & Privacy Policy",
                16, TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 28f));

            // ── Wire LoginScreen ──
            var login = canvasGo.AddComponent<LoginScreen>();
            ArenaSceneBuilder.SetPrivate(login, "mainGroup", mainGroup);
            ArenaSceneBuilder.SetPrivate(login, "logoGroup", logoGroup);
            ArenaSceneBuilder.SetPrivate(login, "bottomGroup", bottomGroup);
            ArenaSceneBuilder.SetPrivate(login, "loadingGroup", loadingGroup);
            ArenaSceneBuilder.SetPrivate(login, "termsGroup", termsGroup);
            ArenaSceneBuilder.SetPrivate(login, "particleContainer", particleContainer);
            ArenaSceneBuilder.SetPrivate(login, "googleButton", googleBtn);
            ArenaSceneBuilder.SetPrivate(login, "playGamesButton", playGamesBtn);
            ArenaSceneBuilder.SetPrivate(login, "facebookButton", facebookBtn);
            ArenaSceneBuilder.SetPrivate(login, "guestButton", guestBtn);
            ArenaSceneBuilder.SetPrivate(login, "errorText", errorText);
            ArenaSceneBuilder.SetPrivate(login, "offlineNotice", offlineNotice);
            ArenaSceneBuilder.SetPrivate(login, "loadingText", loadingText);
            ArenaSceneBuilder.SetPrivate(login, "percentText", percentText);
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

            // A scrim over the jungle rather than a wall in front of it. Opaque, this hid the
            // backdrop entirely; at 0.66 the trees are still there behind the logo, dark and
            // out of focus, and the login text stays readable — which is the whole trick.
            var go = new GameObject("Scrim", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = new Color(1f, 1f, 1f, 0.42f);
            raw.raycastTarget = false;
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

        /// <summary>A labelled action button — a provider, or guest.</summary>
        private static Button MakeWideButton(Transform parent, string name, string label,
            Color bgColor, Color textColor, Vector2 pos, float width, float height = 48f)
        {
            var go = new GameObject(name + "Btn", typeof(RectTransform),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            go.GetComponent<Image>().color = bgColor;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colors;

            var text = MakeText(go.transform, name + "Label", label, 22, textColor,
                                new Vector2(0.5f, 0.5f), Vector2.zero);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 6f;
            text.rectTransform.sizeDelta = new Vector2(width, height);

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

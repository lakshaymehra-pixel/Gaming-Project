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
            // Tall enough for the whole ladder above — the providers row now sits at y=430, and
            // a 340px strip left it standing on the jungle with nothing behind it.
            stripRt.sizeDelta = new Vector2(0f, 530f);
            stripGo.GetComponent<Image>().color = BottomBar;
            stripGo.GetComponent<Image>().raycastTarget = false;

            // Text sizes here are in a 1920x1080 reference space, so they are roughly half what
            // they look like on a phone held at arm's length. The old 16px labels came out at
            // about 3mm tall on a handset — legible on a monitor, not on the thing this ships to.
            // Everything below is laid out from a single ladder of Y positions rather than by
            // hand. Hand-placed rows are how PLAY AS GUEST ended up at y=32 with the terms line
            // at y=30, drawn straight through it.
            const float rowSocial = 430f;   // one-tap providers, first because most people use them
            const float rowOr = 372f;       // the "or" divider
            const float rowUser = 322f;
            const float rowPass = 262f;
            const float rowError = 222f;
            const float rowActions = 168f;  // sign in / register
            const float rowGuest = 112f;
            const float rowNotice = 68f;

            MakeText(bottomGo.transform, "SelectLabel", "SIGN IN", 24,
                TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 486f));

            // ── One-tap providers ──
            // Real ones, not the old four buttons that all did the same nothing. Each is wired
            // to its own provider in LoginScreen; a provider whose SDK is not installed says so
            // when tapped rather than quietly signing the player in as a guest.
            const float socialWidth = 200f;
            const float socialGap = 215f;

            Button googleBtn = MakeWideButton(bottomGo.transform, "Google", "GOOGLE",
                new Color(0.85f, 0.32f, 0.25f), Color.white,
                new Vector2(-socialGap, rowSocial), socialWidth);

            Button playGamesBtn = MakeWideButton(bottomGo.transform, "PlayGames", "PLAY GAMES",
                new Color(0.20f, 0.55f, 0.35f), Color.white,
                new Vector2(0f, rowSocial), socialWidth);

            Button facebookBtn = MakeWideButton(bottomGo.transform, "Facebook", "FACEBOOK",
                new Color(0.23f, 0.35f, 0.60f), Color.white,
                new Vector2(socialGap, rowSocial), socialWidth);

            MakeText(bottomGo.transform, "OrLabel", "— or use a username —", 17,
                new Color(0.35f, 0.33f, 0.30f), new Vector2(0.5f, 0f), new Vector2(0f, rowOr));

            // ── Username and password ──
            TMP_InputField userField = MakeInput(bottomGo.transform, "UsernameField",
                "USERNAME", new Vector2(0f, rowUser), password: false);

            TMP_InputField passField = MakeInput(bottomGo.transform, "PasswordField",
                "PASSWORD", new Vector2(0f, rowPass), password: true);

            // Rejections land between the fields and the buttons, where the eye already is.
            TMP_Text errorText = MakeText(bottomGo.transform, "ErrorText", "", 20,
                new Color(0.95f, 0.3f, 0.25f), new Vector2(0.5f, 0f), new Vector2(0f, rowError));

            Button signInBtn = MakeWideButton(bottomGo.transform, "SignIn", "SIGN IN",
                Gold, new Color(0.06f, 0.05f, 0.04f), new Vector2(-165f, rowActions), 300f);

            Button registerBtn = MakeWideButton(bottomGo.transform, "Register", "REGISTER",
                new Color(0.22f, 0.20f, 0.18f), TextWhite, new Vector2(165f, rowActions), 300f);

            Button guestBtn = MakeWideButton(bottomGo.transform, "Guest", "PLAY AS GUEST",
                new Color(0.14f, 0.13f, 0.12f), TextDim, new Vector2(0f, rowGuest), 630f);

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
            ArenaSceneBuilder.SetPrivate(login, "usernameField", userField);
            ArenaSceneBuilder.SetPrivate(login, "passwordField", passField);
            ArenaSceneBuilder.SetPrivate(login, "signInButton", signInBtn);
            ArenaSceneBuilder.SetPrivate(login, "registerButton", registerBtn);
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

        // ── Icon button with glow border (BGMI style) ──
        /// <summary>
        /// A text field with a placeholder. TMP_InputField needs three pieces wired by hand —
        /// the text component, the placeholder, and a viewport to clip against — and it fails
        /// silently and confusingly if any of them is missing.
        /// </summary>
        private static TMP_InputField MakeInput(Transform parent, string name,
            string placeholder, Vector2 pos, bool password)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                                    typeof(TMP_InputField));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(630f, 52f);

            go.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.08f, 0.95f);

            // The text area, inset so the caret never touches the edge of the box.
            var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(go.transform, false);
            var areaRt = areaGo.GetComponent<RectTransform>();
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = new Vector2(18f, 2f);
            areaRt.offsetMax = new Vector2(-18f, -2f);

            TMP_Text text = MakeFieldText(areaGo.transform, "Text", "", TextWhite);
            TMP_Text hint = MakeFieldText(areaGo.transform, "Placeholder", placeholder,
                                          new Color(0.42f, 0.39f, 0.36f));
            hint.fontStyle = FontStyles.Italic;

            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = areaRt;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = text.font;
            input.pointSize = 24f;
            input.caretColor = Gold;
            input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.3f);
            input.characterLimit = 24;

            if (password)
            {
                input.contentType = TMP_InputField.ContentType.Password;
                input.asteriskChar = '*';
            }
            else
            {
                // Not Standard: usernames go into an email on the Firebase path, and a name with
                // a space in it makes an address that will not resolve.
                input.contentType = TMP_InputField.ContentType.Alphanumeric;
            }

            return input;
        }

        private static TMP_Text MakeFieldText(Transform parent, string name, string content,
                                              Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = 24f;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.raycastTarget = false;

            return tmp;
        }

        /// <summary>A full-width action button with a label — sign in, register, guest.</summary>
        private static Button MakeWideButton(Transform parent, string name, string label,
            Color bgColor, Color textColor, Vector2 pos, float width)
        {
            var go = new GameObject(name + "Btn", typeof(RectTransform),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 48f);

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
            text.rectTransform.sizeDelta = new Vector2(width, 48f);

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

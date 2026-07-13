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
    /// Builds the BGMI-style login screen: dark military theme with username entry,
    /// guest login, and Google sign-in placeholder. Sits between the splash and the game.
    ///
    /// Run from: Game > Build Login Scene
    /// </summary>
    public static class LoginSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Login.unity";

        // ── Colours ──
        private static readonly Color BgDark = new(0.04f, 0.04f, 0.06f);
        private static readonly Color BgCard = new(0.08f, 0.08f, 0.12f, 0.95f);
        private static readonly Color Accent = new(0.95f, 0.55f, 0.05f);       // gold
        private static readonly Color AccentDim = new(0.7f, 0.4f, 0.03f);
        private static readonly Color BloodRed = new(0.7f, 0.12f, 0.08f);
        private static readonly Color TextWhite = new(0.9f, 0.9f, 0.9f);
        private static readonly Color TextDim = new(0.45f, 0.45f, 0.5f);
        private static readonly Color InputBg = new(0.06f, 0.06f, 0.09f);
        private static readonly Color InputBorder = new(0.2f, 0.2f, 0.25f);
        private static readonly Color BtnGreen = new(0.15f, 0.65f, 0.3f);
        private static readonly Color BtnBlue = new(0.2f, 0.45f, 0.85f);
        private static readonly Color BtnGrey = new(0.2f, 0.2f, 0.25f);

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
            cam.backgroundColor = BgDark;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            // SFX source
            var sfxGo = new GameObject("SFX");
            var sfx = sfxGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            // BGM - ambient drone
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

            // ── Main Group (for fade in/out) ──
            var mainGo = MakeGroup(c, "MainGroup");
            var mainGroup = mainGo.GetComponent<CanvasGroup>();
            Transform m = mainGo.transform;

            // Background
            var bg = MakeImage(m, "Background", BgDark, Vector2.zero, Vector2.zero, true);

            // ── Title Group ──
            var titleGo = MakeGroup(m, "TitleGroup");
            var titleGroup = titleGo.GetComponent<CanvasGroup>();
            titleGroup.alpha = 0f;

            // Game title
            var title = MakeText(titleGo.transform, "Title", "KAAL RAAT", 90,
                TextWhite, new Vector2(0.5f, 1f), new Vector2(0f, -100f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 20f;

            // Subtitle
            var sub = MakeText(titleGo.transform, "Subtitle",
                "HORROR  •  SURVIVAL  •  BATTLE ROYALE", 22,
                BloodRed, new Vector2(0.5f, 1f), new Vector2(0f, -170f));
            sub.characterSpacing = 10f;

            // Decorative line
            var line = MakeImage(titleGo.transform, "Line", Accent,
                new Vector2(0.5f, 1f), new Vector2(0f, -195f), false);
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 2f);

            // ── Buttons Group ──
            var buttonsGo = MakeGroup(m, "ButtonsGroup");
            var buttonsGroup = buttonsGo.GetComponent<CanvasGroup>();
            buttonsGroup.alpha = 0f;
            Transform b = buttonsGo.transform;

            // Card background for login area
            var card = MakeImage(b, "Card", BgCard,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 420f);

            Transform cr = card.transform;

            // Username label
            MakeText(cr, "UsernameLabel", "ENTER YOUR CALLSIGN", 20,
                TextDim, new Vector2(0.5f, 1f), new Vector2(0f, -30f));

            // Username input field
            var inputGo = new GameObject("UsernameInput", typeof(RectTransform),
                typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(cr, false);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = inputRt.anchorMax = new Vector2(0.5f, 1f);
            inputRt.anchoredPosition = new Vector2(0f, -80f);
            inputRt.sizeDelta = new Vector2(480f, 55f);
            inputGo.GetComponent<Image>().color = InputBg;

            // Input text area
            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(inputGo.transform, false);
            var taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(15f, 5f);
            taRt.offsetMax = new Vector2(-15f, -5f);

            // Input text
            var inputText = new GameObject("Text", typeof(RectTransform));
            inputText.transform.SetParent(textArea.transform, false);
            var itRt = inputText.GetComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero;
            itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero;
            itRt.offsetMax = Vector2.zero;
            var inputTmp = inputText.AddComponent<TextMeshProUGUI>();
            inputTmp.fontSize = 28;
            inputTmp.color = TextWhite;
            inputTmp.alignment = TextAlignmentOptions.Left;

            // Placeholder
            var placeholder = new GameObject("Placeholder", typeof(RectTransform));
            placeholder.transform.SetParent(textArea.transform, false);
            var phRt = placeholder.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var phTmp = placeholder.AddComponent<TextMeshProUGUI>();
            phTmp.text = "Enter username...";
            phTmp.fontSize = 28;
            phTmp.color = TextDim;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.alignment = TextAlignmentOptions.Left;

            // Wire input field
            var inputField = inputGo.GetComponent<TMP_InputField>();
            inputField.textViewport = taRt;
            inputField.textComponent = inputTmp;
            inputField.placeholder = phTmp;
            inputField.characterLimit = 16;
            inputField.contentType = TMP_InputField.ContentType.Alphanumeric;

            // Error text (hidden initially)
            var errorText = MakeText(cr, "Error", "", 18,
                BloodRed, new Vector2(0.5f, 1f), new Vector2(0f, -120f));
            errorText.gameObject.SetActive(false);

            // ── LOGIN Button (gold accent) ──
            Button loginBtn = MakeButton(cr, "LoginButton", "LOGIN", Accent,
                new Color(0.05f, 0.03f, 0f), new Vector2(0f, -170f), new Vector2(480f, 55f));

            // ── OR divider ──
            MakeText(cr, "OrDivider", "─────  OR  ─────", 18,
                TextDim, new Vector2(0.5f, 1f), new Vector2(0f, -225f));

            // ── GUEST Button (green) ──
            Button guestBtn = MakeButton(cr, "GuestButton", "PLAY AS GUEST", BtnGreen,
                TextWhite, new Vector2(0f, -275f), new Vector2(480f, 50f));

            // ── GOOGLE Button (blue) ──
            Button googleBtn = MakeButton(cr, "GoogleButton", "SIGN IN WITH GOOGLE",
                BtnBlue, TextWhite, new Vector2(0f, -340f), new Vector2(480f, 50f));

            // ── Username Welcome Group (shown after login) ──
            var usernameGo = MakeGroup(m, "UsernameGroup");
            var usernameGroup = usernameGo.GetComponent<CanvasGroup>();
            usernameGroup.alpha = 0f;

            var welcomeText = MakeText(usernameGo.transform, "Welcome", "Welcome, Soldier!",
                48, Accent, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f));
            welcomeText.fontStyle = FontStyles.Bold;

            MakeText(usernameGo.transform, "Loading", "Deploying to island...", 24,
                TextDim, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f));

            // ── Terms Group (bottom) ──
            var termsGo = MakeGroup(m, "TermsGroup");
            var termsGroup = termsGo.GetComponent<CanvasGroup>();
            termsGroup.alpha = 0f;

            MakeText(termsGo.transform, "Terms",
                "By continuing you agree to our Terms of Service and Privacy Policy",
                14, TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 40f));

            MakeText(termsGo.transform, "Version",
                "v0.1 — EARLY ACCESS", 14, TextDim,
                new Vector2(0.5f, 0f), new Vector2(0f, 70f));

            // ── Wire LoginScreen component ──
            var login = canvasGo.AddComponent<LoginScreen>();
            ArenaSceneBuilder.SetPrivate(login, "mainGroup", mainGroup);
            ArenaSceneBuilder.SetPrivate(login, "titleGroup", titleGroup);
            ArenaSceneBuilder.SetPrivate(login, "buttonsGroup", buttonsGroup);
            ArenaSceneBuilder.SetPrivate(login, "usernameGroup", usernameGroup);
            ArenaSceneBuilder.SetPrivate(login, "termsGroup", termsGroup);
            ArenaSceneBuilder.SetPrivate(login, "usernameInput", inputField);
            ArenaSceneBuilder.SetPrivate(login, "welcomeText", welcomeText);
            ArenaSceneBuilder.SetPrivate(login, "errorText", errorText);
            ArenaSceneBuilder.SetPrivate(login, "guestButton", guestBtn);
            ArenaSceneBuilder.SetPrivate(login, "loginButton", loginBtn);
            ArenaSceneBuilder.SetPrivate(login, "googleButton", googleBtn);
            ArenaSceneBuilder.SetPrivate(login, "nextSceneName", "Island");
            ArenaSceneBuilder.SetPrivate(login, "bgmSource", bgm);
            ArenaSceneBuilder.SetPrivate(login, "sfxSource", sfx);

            // Wire click sound
            var clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX_Empty.wav");
            if (clickClip == null)
                clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX_Empty_Real.mp3");
            if (clickClip != null)
                ArenaSceneBuilder.SetPrivate(login, "clickClip", clickClip);

            // Save
            EditorSceneManager.MarkSceneDirty(scene);
            ArenaSceneBuilder.SaveSceneChecked(scene, ScenePath);

            // Add to build settings after splash, before island
            ArenaSceneBuilder.AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<b>Login scene built.</b> Saved to {ScenePath}. " +
                      "Update the Splash scene's next-scene to 'Login'.");
        }

        // ── BGM: low ambient drone ──
        private static AudioSource BuildBgm()
        {
            var go = new GameObject("BGM");
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0f;
            src.volume = 0.25f;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AMB_SplashBed.wav");
            if (clip != null) src.clip = clip;
            return src;
        }

        // ── Helpers ──

        private static GameObject MakeGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        private static Image MakeImage(Transform parent, string name, Color color,
            Vector2 anchor, Vector2 pos, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                Stretch(rt);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = anchor;
                rt.anchoredPosition = pos;
            }
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
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

        private static Button MakeButton(Transform parent, string name, string label,
            Color bgColor, Color textColor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = bgColor;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.color = textColor;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return btn;
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

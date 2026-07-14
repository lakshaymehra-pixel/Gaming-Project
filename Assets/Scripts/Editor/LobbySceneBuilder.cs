using Game.Core;
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
    /// Builds the lobby: the screen between signing in and dropping into a map. Player card on
    /// the left, the maps in the middle, DEPLOY on the right, settings behind a gear.
    ///
    /// It stands in the same jungle the login does — the island is out there, and you are looking
    /// at it while you decide to go.
    ///
    /// Run from the menu: Game > Build Lobby Scene
    /// </summary>
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";

        [MenuItem("Game/Build Lobby Scene")]
        public static void Build()
        {
            if (ArenaSceneBuilder.BlockedByPlayMode()) return;

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SplashBackdrop.Stage stage = SplashBackdrop.Build(lit: true);
            stage.Camera.gameObject.AddComponent<AudioListener>();

            new GameObject("Settings", typeof(SettingsApplier));

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Canvas canvas = UiKit.MakeCanvas("LobbyCanvas");
            Transform c = canvas.transform;

            UiKit.MakeScrim(c, 0.34f);

            var lobby = canvas.gameObject.AddComponent<LobbyScreen>();

            BuildPlayerCard(c, lobby);
            BuildMaps(c, lobby);
            BuildPlayPanel(c, lobby);
            BuildTopRight(c, lobby);
            BuildLevelUpBanner(c, lobby);
            BuildSettings(c, lobby);

            ArenaSceneBuilder.SetPrivate(lobby, "mapAScene", "Island");
            ArenaSceneBuilder.SetPrivate(lobby, "mapBScene", "Arena");
            ArenaSceneBuilder.SetPrivate(lobby, "loginScene", "Login");

            ArenaSceneBuilder.SaveSceneChecked(scene, ScenePath);
            ArenaSceneBuilder.AddSceneToBuildSettings(ScenePath);
            LoginSceneBuilder.EnsureBuildOrder();

            Debug.Log($"<b>Lobby built.</b> Saved to {ScenePath}. The login now hands off to " +
                      "this instead of straight to the island — rebuild the Login scene too.");
        }

        // ---------------------------------------------------------------- player card

        private static void BuildPlayerCard(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0f, 1f);   // top-left

            UiKit.MakeRect(parent, "CardPanel", UiKit.Panel,
                anchor, new Vector2(320f, -110f), new Vector2(560f, 180f));

            // Avatar: a coloured square with an initial in it. There is no portrait art and no
            // pipeline to make any, and a placeholder that admits what it is beats a stock face.
            Image avatar = UiKit.MakeRect(parent, "Avatar", new Color(0.3f, 0.3f, 0.3f),
                anchor, new Vector2(110f, -110f), new Vector2(110f, 110f));

            TMP_Text initial = UiKit.MakeText(parent, "AvatarInitial", "S", 54f,
                UiKit.TextWhite, anchor, new Vector2(110f, -110f));
            initial.fontStyle = FontStyles.Bold;

            TMP_Text name = UiKit.MakeText(parent, "PlayerName", "SOLDIER", 30f,
                UiKit.TextWhite, anchor, new Vector2(330f, -70f));
            name.alignment = TextAlignmentOptions.Left;
            name.fontStyle = FontStyles.Bold;
            name.rectTransform.sizeDelta = new Vector2(340f, 40f);

            TMP_Text level = UiKit.MakeText(parent, "PlayerLevel", "LVL 1", 22f,
                UiKit.Gold, anchor, new Vector2(330f, -104f));
            level.alignment = TextAlignmentOptions.Left;
            level.rectTransform.sizeDelta = new Vector2(340f, 30f);

            Image xpBar = UiKit.MakeBar(parent, "Xp", UiKit.Gold,
                anchor, new Vector2(400f, -132f), new Vector2(300f, 8f));

            TMP_Text xp = UiKit.MakeText(parent, "XpText", "0 / 500 XP", 16f,
                UiKit.TextDim, anchor, new Vector2(400f, -154f));
            xp.rectTransform.sizeDelta = new Vector2(300f, 24f);

            // The stat row. Three numbers, and the words under them small — the number is what
            // anyone reads.
            TMP_Text kills = Stat(parent, "Kills", "KILLS", new Vector2(120f, -215f));
            TMP_Text matches = Stat(parent, "Matches", "MATCHES", new Vector2(300f, -215f));
            TMP_Text wave = Stat(parent, "BestWave", "BEST WAVE", new Vector2(480f, -215f));

            ArenaSceneBuilder.SetPrivate(lobby, "avatar", avatar);
            ArenaSceneBuilder.SetPrivate(lobby, "avatarInitial", initial);
            ArenaSceneBuilder.SetPrivate(lobby, "nameText", name);
            ArenaSceneBuilder.SetPrivate(lobby, "levelText", level);
            ArenaSceneBuilder.SetPrivate(lobby, "xpBar", xpBar);
            ArenaSceneBuilder.SetPrivate(lobby, "xpText", xp);
            ArenaSceneBuilder.SetPrivate(lobby, "killsText", kills);
            ArenaSceneBuilder.SetPrivate(lobby, "matchesText", matches);
            ArenaSceneBuilder.SetPrivate(lobby, "bestWaveText", wave);
        }

        /// <summary>One stat: the number, and the word under it. Returns the number.</summary>
        private static TMP_Text Stat(Transform parent, string name, string label, Vector2 pos)
        {
            var anchor = new Vector2(0f, 1f);

            TMP_Text value = UiKit.MakeText(parent, name + "Value", "0", 32f,
                UiKit.TextWhite, anchor, pos);
            value.fontStyle = FontStyles.Bold;
            value.rectTransform.sizeDelta = new Vector2(170f, 40f);

            TMP_Text caption = UiKit.MakeText(parent, name + "Label", label, 14f,
                UiKit.TextDim, anchor, pos + new Vector2(0f, -30f));
            caption.characterSpacing = 6f;
            caption.rectTransform.sizeDelta = new Vector2(170f, 24f);

            return value;
        }

        // ----------------------------------------------------------------------- maps

        private static void BuildMaps(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0.5f, 0.5f);

            TMP_Text heading = UiKit.MakeText(parent, "MapsHeading", "SELECT MAP", 22f,
                UiKit.TextDim, anchor, new Vector2(0f, 210f));
            heading.characterSpacing = 8f;

            (Button btn, Image outline, TMP_Text status) a = MapCard(parent, "MapA",
                "ISLAND", "400m jungle island. Dense cover, short sight lines.",
                new Color(0.10f, 0.22f, 0.12f), new Vector2(-240f, 40f));

            (Button btn, Image outline, TMP_Text status) b = MapCard(parent, "MapB",
                "ARENA", "Walled box with hard cover. Fast rounds.",
                new Color(0.20f, 0.18f, 0.15f), new Vector2(240f, 40f));

            ArenaSceneBuilder.SetPrivate(lobby, "mapAButton", a.btn);
            ArenaSceneBuilder.SetPrivate(lobby, "mapAOutline", a.outline);
            ArenaSceneBuilder.SetPrivate(lobby, "mapAStatus", a.status);
            ArenaSceneBuilder.SetPrivate(lobby, "mapBButton", b.btn);
            ArenaSceneBuilder.SetPrivate(lobby, "mapBOutline", b.outline);
            ArenaSceneBuilder.SetPrivate(lobby, "mapBStatus", b.status);
        }

        /// <summary>
        /// One map card: a flat colour for a thumbnail, the name, a line about it, and an outline
        /// that goes gold when selected.
        ///
        /// The thumbnail is a colour rather than a screenshot on purpose. A real one means a
        /// menu item that opens each map, points a camera at it and writes a PNG — worth doing,
        /// and worth doing as its own thing rather than smuggled into the lobby. A stock jungle
        /// photo off the web would be the actual lie: it would show a map that is not this map.
        /// </summary>
        private static (Button, Image, TMP_Text) MapCard(Transform parent, string name,
            string title, string blurb, Color thumb, Vector2 pos)
        {
            var anchor = new Vector2(0.5f, 0.5f);
            var size = new Vector2(420f, 250f);

            Image outline = UiKit.MakeRect(parent, name + "Outline", UiKit.GoldDim,
                anchor, pos, size + new Vector2(8f, 8f));

            var go = new GameObject(name + "Btn", typeof(RectTransform),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = thumb;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            btn.colors = colors;

            // A dark band along the bottom of the card so the text is legible whatever the
            // thumbnail becomes later.
            UiKit.MakeRect(go.transform, "Band", new Color(0f, 0f, 0f, 0.72f),
                new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(size.x, 88f));

            TMP_Text titleText = UiKit.MakeText(go.transform, "Title", title, 30f,
                UiKit.TextWhite, new Vector2(0.5f, 0f), new Vector2(0f, 62f));
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 8f;
            titleText.rectTransform.sizeDelta = new Vector2(size.x - 20f, 36f);

            TMP_Text blurbText = UiKit.MakeText(go.transform, "Blurb", blurb, 15f,
                UiKit.TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 32f));
            blurbText.rectTransform.sizeDelta = new Vector2(size.x - 30f, 34f);
            blurbText.enableWordWrapping = true;

            TMP_Text status = UiKit.MakeText(parent, name + "Status", "", 15f,
                new Color(0.95f, 0.5f, 0.3f), anchor, pos + new Vector2(0f, -145f));
            status.rectTransform.sizeDelta = new Vector2(440f, 40f);
            status.enableWordWrapping = true;

            return (btn, outline, status);
        }

        // ----------------------------------------------------------------------- play

        private static void BuildPlayPanel(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0.5f, 0f);   // bottom-centre

            Button play = UiKit.MakeButton(parent, "Play", "DEPLOY",
                UiKit.Gold, new Color(0.06f, 0.05f, 0.04f),
                anchor, new Vector2(0f, 110f), new Vector2(420f, 74f), 30f);

            // The loading block, hidden until DEPLOY. Same shape as the login's, because the
            // island genuinely takes seconds and a frozen screen with no bar reads as a crash.
            GameObject loadingGo = UiKit.MakeGroup(parent, "LoadingGroup");
            var loadingGroup = loadingGo.GetComponent<CanvasGroup>();
            loadingGroup.alpha = 0f;

            Transform l = loadingGo.transform;

            TMP_Text label = UiKit.MakeText(l, "LoadingText", "DEPLOYING", 24f,
                UiKit.TextWhite, anchor, new Vector2(-300f, 150f));
            label.alignment = TextAlignmentOptions.Left;
            label.characterSpacing = 8f;
            label.rectTransform.sizeDelta = new Vector2(600f, 40f);

            TMP_Text percent = UiKit.MakeText(l, "PercentText", "0%", 28f,
                UiKit.Gold, anchor, new Vector2(300f, 150f));
            percent.alignment = TextAlignmentOptions.Right;
            percent.fontStyle = FontStyles.Bold;
            percent.rectTransform.sizeDelta = new Vector2(600f, 40f);

            Image bar = UiKit.MakeBar(l, "Loading", UiKit.Gold,
                anchor, new Vector2(0f, 118f), new Vector2(900f, 10f));

            ArenaSceneBuilder.SetPrivate(lobby, "playButton", play);
            ArenaSceneBuilder.SetPrivate(lobby, "loadingGroup", loadingGroup);
            ArenaSceneBuilder.SetPrivate(lobby, "loadingText", label);
            ArenaSceneBuilder.SetPrivate(lobby, "percentText", percent);
            ArenaSceneBuilder.SetPrivate(lobby, "loadingBar", bar);
        }

        // ------------------------------------------------------------------ top right

        private static void BuildTopRight(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(1f, 1f);

            Button settings = UiKit.MakeButton(parent, "Settings", "SETTINGS",
                new Color(0.16f, 0.15f, 0.13f), UiKit.TextWhite,
                anchor, new Vector2(-130f, -60f), new Vector2(200f, 52f), 18f);

            Button signOut = UiKit.MakeButton(parent, "SignOut", "SIGN OUT",
                new Color(0.16f, 0.13f, 0.13f), UiKit.TextDim,
                anchor, new Vector2(-130f, -120f), new Vector2(200f, 44f), 16f);

            ArenaSceneBuilder.SetPrivate(lobby, "settingsButton", settings);
            ArenaSceneBuilder.SetPrivate(lobby, "signOutButton", signOut);
        }

        private static void BuildLevelUpBanner(Transform parent, LobbyScreen lobby)
        {
            GameObject go = UiKit.MakeGroup(parent, "LevelUpBanner");
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var anchor = new Vector2(0.5f, 0.5f);

            UiKit.MakeRect(go.transform, "Band", new Color(0f, 0f, 0f, 0.8f),
                anchor, new Vector2(0f, 0f), new Vector2(700f, 140f));

            TMP_Text heading = UiKit.MakeText(go.transform, "Heading", "LEVEL UP", 24f,
                UiKit.TextDim, anchor, new Vector2(0f, 34f));
            heading.characterSpacing = 12f;

            TMP_Text level = UiKit.MakeText(go.transform, "Level", "LEVEL 2", 52f,
                UiKit.Gold, anchor, new Vector2(0f, -18f));
            level.fontStyle = FontStyles.Bold;

            ArenaSceneBuilder.SetPrivate(lobby, "levelUpBanner", group);
            ArenaSceneBuilder.SetPrivate(lobby, "levelUpText", level);
        }

        // ------------------------------------------------------------------- settings

        private static void BuildSettings(Transform parent, LobbyScreen lobby)
        {
            GameObject go = UiKit.MakeGroup(parent, "SettingsGroup");
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var panel = go.AddComponent<SettingsPanel>();
            Transform s = go.transform;

            // A full-screen dim behind the panel, so a tap outside it lands on nothing rather
            // than on the lobby underneath.
            Image blocker = UiKit.MakeRect(s, "Blocker", new Color(0f, 0f, 0f, 0.75f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
            blocker.raycastTarget = true;

            var anchor = new Vector2(0.5f, 0.5f);

            UiKit.MakeRect(s, "Panel", new Color(0.05f, 0.045f, 0.04f, 0.98f),
                anchor, Vector2.zero, new Vector2(820f, 620f));

            TMP_Text heading = UiKit.MakeText(s, "Heading", "SETTINGS", 30f,
                UiKit.Gold, anchor, new Vector2(0f, 250f));
            heading.fontStyle = FontStyles.Bold;
            heading.characterSpacing = 10f;

            // ── Volume ──
            Row(s, "VolumeLabel", "VOLUME", 170f);
            Slider volume = UiKit.MakeSlider(s, "VolumeSlider", anchor,
                new Vector2(60f, 170f), 420f, GameSettings.Volume);
            TMP_Text volumeValue = Value(s, "VolumeValue", 170f);

            // ── Sensitivity ──
            Row(s, "SensLabel", "LOOK SENSITIVITY", 100f);
            Slider sens = UiKit.MakeSlider(s, "SensSlider", anchor,
                new Vector2(60f, 100f), 420f, GameSettings.Sensitivity);
            TMP_Text sensValue = Value(s, "SensValue", 100f);

            // ── Graphics tier ──
            Row(s, "GfxLabel", "GRAPHICS", 20f);

            (Button b, Image o) smooth = TierButton(s, "Smooth", "SMOOTH", new Vector2(60f, 20f));
            (Button b, Image o) balanced = TierButton(s, "Balanced", "BALANCED", new Vector2(215f, 20f));
            (Button b, Image o) hd = TierButton(s, "HD", "HD", new Vector2(370f, 20f));

            // ── Foliage ──
            Row(s, "FoliageLabel", "FOLIAGE DETAIL", -60f);
            Slider foliage = UiKit.MakeSlider(s, "FoliageSlider", anchor,
                new Vector2(60f, -60f), 420f, GameSettings.Foliage);
            TMP_Text foliageValue = Value(s, "FoliageValue", -60f);

            TMP_Text hint = UiKit.MakeText(s, "FoliageHint",
                "Thins the undergrowth. Cover and walls are never removed.", 14f,
                UiKit.TextDim, anchor, new Vector2(60f, -95f));
            hint.rectTransform.sizeDelta = new Vector2(600f, 24f);

            Button close = UiKit.MakeButton(s, "Close", "DONE",
                UiKit.Gold, new Color(0.06f, 0.05f, 0.04f),
                anchor, new Vector2(0f, -230f), new Vector2(280f, 56f), 22f);

            ArenaSceneBuilder.SetPrivate(panel, "group", group);
            ArenaSceneBuilder.SetPrivate(panel, "closeButton", close);
            ArenaSceneBuilder.SetPrivate(panel, "volumeSlider", volume);
            ArenaSceneBuilder.SetPrivate(panel, "volumeValue", volumeValue);
            ArenaSceneBuilder.SetPrivate(panel, "sensitivitySlider", sens);
            ArenaSceneBuilder.SetPrivate(panel, "sensitivityValue", sensValue);
            ArenaSceneBuilder.SetPrivate(panel, "smoothButton", smooth.b);
            ArenaSceneBuilder.SetPrivate(panel, "balancedButton", balanced.b);
            ArenaSceneBuilder.SetPrivate(panel, "hdButton", hd.b);
            ArenaSceneBuilder.SetPrivate(panel, "smoothOutline", smooth.o);
            ArenaSceneBuilder.SetPrivate(panel, "balancedOutline", balanced.o);
            ArenaSceneBuilder.SetPrivate(panel, "hdOutline", hd.o);
            ArenaSceneBuilder.SetPrivate(panel, "foliageSlider", foliage);
            ArenaSceneBuilder.SetPrivate(panel, "foliageValue", foliageValue);

            ArenaSceneBuilder.SetPrivate(lobby, "settingsPanel", panel);
        }

        private static void Row(Transform parent, string name, string label, float y)
        {
            TMP_Text text = UiKit.MakeText(parent, name, label, 19f, UiKit.TextWhite,
                new Vector2(0.5f, 0.5f), new Vector2(-250f, y));
            text.alignment = TextAlignmentOptions.Left;
            text.characterSpacing = 5f;
            text.rectTransform.sizeDelta = new Vector2(300f, 30f);
        }

        private static TMP_Text Value(Transform parent, string name, float y)
        {
            TMP_Text text = UiKit.MakeText(parent, name, "—", 19f, UiKit.Gold,
                new Vector2(0.5f, 0.5f), new Vector2(320f, y));
            text.alignment = TextAlignmentOptions.Right;
            text.rectTransform.sizeDelta = new Vector2(120f, 30f);
            return text;
        }

        private static (Button, Image) TierButton(Transform parent, string name, string label,
                                                  Vector2 pos)
        {
            var anchor = new Vector2(0.5f, 0.5f);
            var size = new Vector2(140f, 46f);

            Image outline = UiKit.MakeRect(parent, name + "Outline", UiKit.GoldDim,
                anchor, pos, size + new Vector2(6f, 6f));

            Button btn = UiKit.MakeButton(parent, name, label,
                new Color(0.12f, 0.11f, 0.10f), UiKit.TextWhite, anchor, pos, size, 17f);

            return (btn, outline);
        }
    }
}

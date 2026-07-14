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

            // The jungle stays, but it is scenery now rather than the subject. The soldier is
            // the subject — a lobby where the biggest thing on screen is a panel is a settings
            // menu with a PLAY button on it.
            SplashBackdrop.Stage stage = SplashBackdrop.Build(lit: true);
            stage.Camera.gameObject.AddComponent<AudioListener>();

            // The splash's soldier walks off up the corridor. This is not him.
            if (stage.Soldier != null) Object.DestroyImmediate(stage.Soldier.gameObject);

            BuildStage(stage.Camera.transform);

            new GameObject("Settings", typeof(SettingsApplier));

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Canvas canvas = UiKit.MakeCanvas("LobbyCanvas");
            Transform c = canvas.transform;

            // Vignette instead of a flat scrim over everything: the middle of the screen has a
            // man standing in it now, and a wash of dark grey across his face is not a look.
            BuildVignette(c);

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

        // ------------------------------------------------------------------- vignette

        /// <summary>
        /// Darkens the edges and leaves the middle alone. A flat scrim would wash grey across the
        /// soldier's face; this pushes the panels' backgrounds down without touching him.
        /// </summary>
        private static void BuildVignette(Transform parent)
        {
            const string path = "Assets/Settings/T_LobbyVignette.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
            {
                const int size = 128;
                tex = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };

                var centre = new Vector2(size / 2f, size / 2f);
                float maxD = centre.magnitude;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), centre) / maxD;
                        float a = Mathf.SmoothStep(0f, 0.9f, (d - 0.35f) / 0.65f);
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                    }
                }

                tex.Apply();
                AssetDatabase.CreateAsset(tex, path);
            }

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            UiKit.Stretch(go.GetComponent<RectTransform>());

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }

        // ---------------------------------------------------------------------- stage

        /// <summary>
        /// The soldier, on a plinth, lit from the front. He stands where the camera is already
        /// looking, so the jungle frames him instead of competing with him.
        ///
        /// The lighting is the whole trick. The backdrop is night, and a model dropped into it
        /// unlit is a silhouette — you would have built a character nobody can see. Two lights
        /// pick him out: a key from the front and above, and a cold rim from behind that separates
        /// his edge from the trees.
        /// </summary>
        private static void BuildStage(Transform camera)
        {
            // Where he stands: a few metres down the camera's line, slightly right of centre, so
            // the profile panel on the left has somewhere to be.
            Vector3 spot = camera.position
                           + camera.forward * 4.6f
                           + camera.right * 0.55f;
            spot.y = 0f;

            // The camera looks slightly down at him from standing height, which is where a person
            // looking at another person looks.
            camera.position = new Vector3(camera.position.x, 1.5f, camera.position.z);
            camera.LookAt(spot + Vector3.up * 1.05f);

            var root = new GameObject("Stage").transform;
            root.position = spot;

            BuildPlinth(root);
            BuildStageLights(root, camera);
            BuildSoldier(root, camera);
        }

        private static void BuildPlinth(Transform parent)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Plinth";
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            disc.transform.localScale = new Vector3(2.4f, 0.04f, 2.4f);

            disc.GetComponent<Renderer>().sharedMaterial =
                ArenaSceneBuilder.MakeMaterial("M_LobbyPlinth", new Color(0.07f, 0.06f, 0.05f));

            Object.DestroyImmediate(disc.GetComponent<Collider>());

            // A gold ring around it. It is the one piece of the UI's palette that reaches into
            // the 3D, and it is what stops him from looking like he is standing in a field.
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(2.55f, 0.02f, 2.55f);

            ring.GetComponent<Renderer>().sharedMaterial =
                ArenaSceneBuilder.MakeMaterial("M_LobbyRing", new Color(0.55f, 0.42f, 0.10f));

            Object.DestroyImmediate(ring.GetComponent<Collider>());
        }

        private static void BuildStageLights(Transform parent, Transform camera)
        {
            // Key: warm, from the camera's side and above. This is the light that makes him a
            // person rather than a shape.
            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(parent, false);
            keyGo.transform.position = parent.position
                                       + (camera.position - parent.position).normalized * 3f
                                       + Vector3.up * 2.6f;
            keyGo.transform.LookAt(parent.position + Vector3.up * 1.1f);

            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.94f, 0.82f);
            key.intensity = 6f;
            key.range = 14f;
            key.spotAngle = 55f;
            key.shadows = LightShadows.Soft;

            // Rim: cold, from behind and to one side. It draws a hard edge down his back and
            // separates him from a jungle that is exactly as dark as he is.
            var rimGo = new GameObject("RimLight");
            rimGo.transform.SetParent(parent, false);
            rimGo.transform.position = parent.position
                                       - (camera.position - parent.position).normalized * 3.4f
                                       + Vector3.up * 3f
                                       + Vector3.right * 1.6f;
            rimGo.transform.LookAt(parent.position + Vector3.up * 1.2f);

            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.color = new Color(0.55f, 0.70f, 1f);
            rim.intensity = 8f;
            rim.range = 12f;
            rim.spotAngle = 50f;
            rim.shadows = LightShadows.None;
        }

        private static void BuildSoldier(Transform parent, Transform camera)
        {
            var go = new GameObject("LobbySoldier");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0f);   // on top of the plinth

            // Facing the camera. He is being looked at, and he knows it.
            Vector3 toCamera = camera.position - parent.position;
            toCamera.y = 0f;
            go.transform.rotation = Quaternion.LookRotation(toCamera);

            SoldierFactory.Build(go.transform, go.layer);

            // Everything the combat rig brings that a mannequin does not need: an EnemyAnimator
            // that reads a NavMeshAgent and an EnemyAI that are not here, and colliders for
            // bullets nobody is going to fire at him.
            foreach (var driver in go.GetComponentsInChildren<Game.Enemies.EnemyAnimator>())
                Object.DestroyImmediate(driver);

            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("Lobby: no soldier model — the lobby will be an empty stage. " +
                                 "Check Assets/Models/Swat.fbx.");
                return;
            }

            var character = go.AddComponent<LobbyCharacter>();
            ArenaSceneBuilder.SetPrivate(character, "animator", animator);
        }

        // ---------------------------------------------------------------- player card

        /// <summary>
        /// The profile panel, down the left. One card with everything inside it and a gold rule
        /// down its edge — the old version was six loose elements floating on the jungle, which
        /// is why it read as a form rather than as a card.
        /// </summary>
        private static void BuildPlayerCard(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0f, 1f);   // top-left

            // The card. Everything below is a child of this, so it moves as one thing.
            var cardGo = new GameObject("PlayerCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(parent, false);

            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = anchor;
            cardRt.anchoredPosition = new Vector2(300f, -195f);
            cardRt.sizeDelta = new Vector2(520f, 310f);

            var cardImage = cardGo.GetComponent<Image>();
            cardImage.color = new Color(0.04f, 0.035f, 0.03f, 0.88f);
            cardImage.raycastTarget = false;

            Transform card = cardGo.transform;
            var mid = new Vector2(0.5f, 0.5f);

            // A gold rule down the left edge. One line, and the card stops being a grey box.
            UiKit.MakeRect(card, "Accent", UiKit.Gold, new Vector2(0f, 0.5f),
                new Vector2(3f, 0f), new Vector2(6f, 310f));

            // Avatar. A colour derived from the name with the initial on it — there is no
            // portrait art and no pipeline to make any, and a placeholder that admits what it is
            // beats a stock face pulled off the web.
            Image avatar = UiKit.MakeRect(card, "Avatar", new Color(0.3f, 0.3f, 0.3f),
                mid, new Vector2(-170f, 78f), new Vector2(116f, 116f));

            UiKit.MakeRect(card, "AvatarFrame", new Color(0.55f, 0.42f, 0.10f, 0.6f),
                mid, new Vector2(-170f, 78f), new Vector2(124f, 124f))
                .transform.SetSiblingIndex(1);   // behind the avatar, showing as a border

            TMP_Text initial = UiKit.MakeText(card, "AvatarInitial", "S", 56f,
                UiKit.TextWhite, mid, new Vector2(-170f, 78f));
            initial.fontStyle = FontStyles.Bold;

            TMP_Text name = UiKit.MakeText(card, "PlayerName", "SOLDIER", 32f,
                UiKit.TextWhite, mid, new Vector2(60f, 104f));
            name.alignment = TextAlignmentOptions.Left;
            name.fontStyle = FontStyles.Bold;
            name.rectTransform.sizeDelta = new Vector2(300f, 42f);

            TMP_Text level = UiKit.MakeText(card, "PlayerLevel", "LVL 1", 22f,
                UiKit.Gold, mid, new Vector2(60f, 68f));
            level.alignment = TextAlignmentOptions.Left;
            level.characterSpacing = 6f;
            level.rectTransform.sizeDelta = new Vector2(300f, 30f);

            Image xpBar = UiKit.MakeBar(card, "Xp", UiKit.Gold,
                mid, new Vector2(60f, 38f), new Vector2(290f, 10f));

            TMP_Text xp = UiKit.MakeText(card, "XpText", "0 / 500 XP", 16f,
                UiKit.TextDim, mid, new Vector2(60f, 16f));
            xp.alignment = TextAlignmentOptions.Left;
            xp.rectTransform.sizeDelta = new Vector2(290f, 22f);

            // A divider, then the stats. The line is what makes them a second section rather
            // than more of the same.
            UiKit.MakeRect(card, "Divider", new Color(1f, 1f, 1f, 0.08f),
                mid, new Vector2(0f, -22f), new Vector2(460f, 2f));

            TMP_Text kills = Stat(card, "Kills", "KILLS", new Vector2(-150f, -78f));
            TMP_Text matches = Stat(card, "Matches", "MATCHES", new Vector2(0f, -78f));
            TMP_Text wave = Stat(card, "BestWave", "BEST WAVE", new Vector2(150f, -78f));

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

        /// <summary>One stat: the number, and the word under it. Returns the number, which is the
        /// only part anything ever sets — and the only part anyone actually reads.</summary>
        private static TMP_Text Stat(Transform parent, string name, string label, Vector2 pos)
        {
            var anchor = new Vector2(0.5f, 0.5f);

            TMP_Text value = UiKit.MakeText(parent, name + "Value", "0", 34f,
                UiKit.TextWhite, anchor, pos);
            value.fontStyle = FontStyles.Bold;
            value.rectTransform.sizeDelta = new Vector2(150f, 40f);

            TMP_Text caption = UiKit.MakeText(parent, name + "Label", label, 13f,
                UiKit.TextDim, anchor, pos + new Vector2(0f, -28f));
            caption.characterSpacing = 5f;
            caption.rectTransform.sizeDelta = new Vector2(150f, 22f);

            return value;
        }

        // ----------------------------------------------------------------------- maps

        /// <summary>
        /// The maps, stacked down the right. Not across the middle any more — the middle is where
        /// the soldier stands, and a lobby that puts a menu in front of its own character has
        /// misunderstood what the character is for.
        /// </summary>
        private static void BuildMaps(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(1f, 0.5f);   // right edge

            TMP_Text heading = UiKit.MakeText(parent, "MapsHeading", "SELECT MAP", 20f,
                UiKit.TextDim, anchor, new Vector2(-230f, 220f));
            heading.characterSpacing = 8f;
            heading.rectTransform.sizeDelta = new Vector2(400f, 30f);

            (Button btn, Image outline, TMP_Text status) a = MapCard(parent, "MapA",
                "ISLAND", "400m jungle island. Dense cover, short sight lines.",
                new Color(0.10f, 0.22f, 0.12f), anchor, new Vector2(-230f, 100f));

            (Button btn, Image outline, TMP_Text status) b = MapCard(parent, "MapB",
                "ARENA", "Walled box with hard cover. Fast rounds.",
                new Color(0.20f, 0.18f, 0.15f), anchor, new Vector2(-230f, -80f));

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
            string title, string blurb, Color thumb, Vector2 anchor, Vector2 pos)
        {
            var size = new Vector2(400f, 140f);

            Image outline = UiKit.MakeRect(parent, name + "Outline", UiKit.GoldDim,
                anchor, pos, size + new Vector2(6f, 6f));

            var go = new GameObject(name + "Btn", typeof(RectTransform),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = new Color(0.05f, 0.045f, 0.04f, 0.94f);

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            btn.colors = colors;

            // The thumbnail: a colour block down the left of the card, with the text beside it.
            // A colour rather than a screenshot, because a real one needs a menu item that opens
            // each map, points a camera at it and writes a PNG — worth doing, and worth doing as
            // its own thing. A stock jungle photo would be the actual lie: it would show a map
            // that is not this map.
            UiKit.MakeRect(go.transform, "Thumb", thumb, new Vector2(0f, 0.5f),
                new Vector2(70f, 0f), new Vector2(120f, 120f));

            TMP_Text titleText = UiKit.MakeText(go.transform, "Title", title, 26f,
                UiKit.TextWhite, new Vector2(0f, 0.5f), new Vector2(285f, 26f));
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 6f;
            titleText.rectTransform.sizeDelta = new Vector2(250f, 34f);

            TMP_Text blurbText = UiKit.MakeText(go.transform, "Blurb", blurb, 14f,
                UiKit.TextDim, new Vector2(0f, 0.5f), new Vector2(285f, -18f));
            blurbText.alignment = TextAlignmentOptions.TopLeft;
            blurbText.rectTransform.sizeDelta = new Vector2(250f, 60f);
            blurbText.enableWordWrapping = true;

            TMP_Text status = UiKit.MakeText(parent, name + "Status", "", 14f,
                new Color(0.95f, 0.5f, 0.3f), anchor, pos + new Vector2(0f, -84f));
            status.rectTransform.sizeDelta = new Vector2(400f, 36f);
            status.enableWordWrapping = true;

            return (btn, outline, status);
        }

        // ----------------------------------------------------------------------- play

        /// <summary>
        /// DEPLOY, bottom right, under the maps. It is the largest, brightest thing on the screen
        /// after the soldier, because it is the only button anyone came here to press.
        /// </summary>
        private static void BuildPlayPanel(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(1f, 0f);   // bottom-right

            Button play = UiKit.MakeButton(parent, "Play", "DEPLOY",
                UiKit.Gold, new Color(0.06f, 0.05f, 0.04f),
                anchor, new Vector2(-230f, 90f), new Vector2(406f, 78f), 32f);

            // The loading block, hidden until DEPLOY. Same shape as the login's, because the
            // island genuinely takes seconds and a frozen screen with no bar reads as a crash.
            GameObject loadingGo = UiKit.MakeGroup(parent, "LoadingGroup");
            var loadingGroup = loadingGo.GetComponent<CanvasGroup>();
            loadingGroup.alpha = 0f;

            Transform l = loadingGo.transform;
            var bottom = new Vector2(0.5f, 0f);

            UiKit.MakeRect(l, "LoadingBand", new Color(0f, 0f, 0f, 0.85f),
                bottom, new Vector2(0f, 90f), new Vector2(4000f, 180f));

            TMP_Text label = UiKit.MakeText(l, "LoadingText", "DEPLOYING", 24f,
                UiKit.TextWhite, bottom, new Vector2(-350f, 130f));
            label.alignment = TextAlignmentOptions.Left;
            label.characterSpacing = 8f;
            label.rectTransform.sizeDelta = new Vector2(700f, 40f);

            TMP_Text percent = UiKit.MakeText(l, "PercentText", "0%", 28f,
                UiKit.Gold, bottom, new Vector2(350f, 130f));
            percent.alignment = TextAlignmentOptions.Right;
            percent.fontStyle = FontStyles.Bold;
            percent.rectTransform.sizeDelta = new Vector2(700f, 40f);

            Image bar = UiKit.MakeBar(l, "Loading", UiKit.Gold,
                bottom, new Vector2(0f, 96f), new Vector2(1000f, 12f));

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

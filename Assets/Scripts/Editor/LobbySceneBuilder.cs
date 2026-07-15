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
            BuildBloodMoon(stage.Camera.transform);

            // Bruise the night red. SplashBackdrop lights this as a neutral-cold jungle; the
            // lobby wants the crimson of the game's own splash, with the blood moon as its
            // source. Warm fog, warm ambient, and the camera's clear colour pulled toward the
            // deep red the moon's rim sits in.
            RenderSettings.fogColor = new Color(0.10f, 0.03f, 0.03f);
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.09f, 0.07f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.05f, 0.04f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.01f, 0.01f);

            var cam = stage.Camera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.02f, 0.03f);
            }

            new GameObject("Settings", typeof(SettingsApplier));

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Canvas canvas = UiKit.MakeCanvas("LobbyCanvas");
            Transform c = canvas.transform;

            // Vignette instead of a flat scrim over everything: the middle of the screen has a
            // man standing in it now, and a wash of dark grey across his face is not a look.
            BuildVignette(c);

            BuildWatermark(c);

            var lobby = canvas.gameObject.AddComponent<LobbyScreen>();

            // ── The floating clusters, corner-anchored the way the web mockup lays them out.
            //    The middle stays the soldier; every control hugs an edge. Built in this order
            //    so later siblings draw on top — decorative panels first, live buttons after.

            // Top-left: who you are.
            BuildPlayerCard(c, lobby);   // player card + stat row
            BuildLoadoutStrip(c);        // PRIMARY / SECONDARY / MELEE (decorative)
            BuildTeamUp(c);              // TEAM UP (decorative)

            // Top-centre: the small doors (decorative).
            BuildTopCentre(c);

            // Top-right: what you own (decorative) + the live settings/sign-out buttons.
            BuildCurrencyBar(c);
            BuildRailButtons(c);         // RP / CRATE / SHOP (decorative)
            BuildPromoBanners(c);        // KAAL PASS + EVENTS (decorative)
            BuildTopRight(c, lobby);     // settings + sign out (live)

            // Bottom-left: mode card, the two real map buttons, and DEPLOY.
            BuildModeAndMaps(c, lobby);  // mode label + mapA/mapB buttons (live)
            BuildPlayPanel(c, lobby);    // DEPLOY + loading block (live)

            // Bottom-centre: squad + chat (decorative).
            BuildSquadBar(c);
            BuildChatStrip(c);

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

        // ------------------------------------------------------------------- watermark

        /// <summary>
        /// KAAL RAAT, huge and barely there, across the upper scene. The game's name written
        /// on its own sky — Roman rather than Devanagari because the project's font has no
        /// Devanagari glyphs and would render each letter as an empty box.
        ///
        /// Placed just under the top bar and behind everything else, so it reads as part of
        /// the backdrop rather than as a label.
        /// </summary>
        private static void BuildWatermark(Transform parent)
        {
            TMP_Text mark = UiKit.MakeText(parent, "Watermark", "KAAL RAAT", 150f,
                new Color(1f, 0.75f, 0.55f, 0.06f), new Vector2(0.5f, 0.72f), Vector2.zero);
            mark.fontStyle = FontStyles.Bold;
            mark.characterSpacing = 18f;
            mark.rectTransform.sizeDelta = new Vector2(1800f, 220f);

            // Behind the panels, in front of the 3D. It is the first UI child, so everything
            // built after it draws on top.
            mark.transform.SetAsFirstSibling();
        }

        // ------------------------------------------------------------------ blood moon

        /// <summary>
        /// The blood moon, low and enormous behind the soldier. He silhouettes against it —
        /// a man standing in front of the thing that turned the night red — which is the
        /// strongest image the lobby has and the one the web mockup was built around.
        ///
        /// A world-space quad, not a UI element: it sits behind the soldier in 3D so his
        /// outline actually cuts into it. Textured with a generated radial gradient — pale hot
        /// core, through orange, to a near-black bruised rim.
        /// </summary>
        private static void BuildBloodMoon(Transform camera)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BloodMoon";
            Object.DestroyImmediate(go.GetComponent<Collider>());

            // Behind and low, so the soldier's whole body cuts into it — a man in front of the
            // moon, not under it. Centred just above his feet (Y≈2.6) rather than overhead, and
            // pushed far back so the disc reads as distant and the fog between softens its edge.
            //
            // The height and distance are tied: at 30m the camera's slight downward look puts
            // the horizon low in frame, and a moon centred at 2.6 with radius ~9 sits its lower
            // half at the horizon and its upper half rising behind the soldier's torso.
            Vector3 ahead = camera.position + camera.forward * 30f + camera.right * 0.55f;
            go.transform.position = new Vector3(ahead.x, 2.6f, ahead.z);
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - camera.position);
            go.transform.localScale = Vector3.one * 18f;

            // Guarded, like every other Shader.Find in the project: a null shader makes a
            // magenta material, and a magenta disc behind the soldier is worse than no moon.
            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { mainTexture = BloodMoonTexture() };
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Texture2D BloodMoonTexture()
        {
            const string path = "Assets/Settings/T_BloodMoon.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
            };

            var centre = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // The colour ramp, core to rim. Offset the centre up-left a touch so the hot spot
            // is not dead middle — a moon lit evenly reads as a disc, not a sphere.
            var ramp = new[]
            {
                (0.00f, new Color(1.00f, 0.81f, 0.62f)),   // pale hot core
                (0.26f, new Color(1.00f, 0.58f, 0.27f)),   // orange
                (0.55f, new Color(0.90f, 0.28f, 0.16f)),   // blood
                (0.80f, new Color(0.55f, 0.13f, 0.06f)),   // deep
                (1.00f, new Color(0.25f, 0.05f, 0.02f)),   // bruised rim
            };

            var hot = new Vector2(size * 0.42f, size * 0.58f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), centre) / radius;

                    if (d > 1f)
                    {
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));   // outside the disc
                        continue;
                    }

                    // Shade from the offset hot spot, not the geometric centre.
                    float shade = Vector2.Distance(new Vector2(x, y), hot) / radius;
                    Color c = SampleRamp(ramp, Mathf.Clamp01(shade));

                    // Feather the very edge so the disc melts into the sky instead of ending
                    // on a hard circle.
                    float a = d > 0.9f ? Mathf.InverseLerp(1f, 0.9f, d) : 1f;
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            }

            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        private static Color SampleRamp((float t, Color c)[] ramp, float t)
        {
            for (int i = 1; i < ramp.Length; i++)
            {
                if (t > ramp[i].t) continue;
                float span = ramp[i].t - ramp[i - 1].t;
                float f = span < 1e-5f ? 0f : (t - ramp[i - 1].t) / span;
                return Color.Lerp(ramp[i - 1].c, ramp[i].c, f);
            }
            return ramp[^1].c;
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

        // ---------------------------------------------------------------- palette add-ons

        // The web mockup's translucent-black panel over the crimson scene, and its thin gold
        // border drawn as a slightly larger rect behind the fill. One helper so every floating
        // panel in the lobby reads as one material.
        private static readonly Color PanelBlack = new(0.02f, 0.01f, 0.01f, 0.62f);
        private static readonly Color BorderGold = new(0.92f, 0.72f, 0.15f, 0.25f);   // gold/25
        private static readonly Color BorderInk = new(0.28f, 0.25f, 0.22f, 0.6f);     // ink-700/60

        /// <summary>
        /// A floating panel: a translucent black body with a thin border drawn as a 2px-larger
        /// rect behind it. Returns the body's transform to parent children onto. The border is
        /// the first sibling so the body covers all but its rim.
        /// </summary>
        private static Transform Panel(Transform parent, string name, Vector2 anchor,
            Vector2 pos, Vector2 size, Color? border = null)
        {
            UiKit.MakeRect(parent, name + "Border", border ?? BorderGold,
                anchor, pos, size + new Vector2(4f, 4f));

            Image body = UiKit.MakeRect(parent, name, PanelBlack, anchor, pos, size);
            return body.transform;
        }

        // ---------------------------------------------------------------- player card

        /// <summary>
        /// Top-left: who you are. A small card — hex-less square avatar, name, a gold LVL badge,
        /// and a thin XP bar — matching the web mockup's PlayerCard. A tiny stat row (kills /
        /// matches / best wave) hangs under it: the web card does not show these, but the Unity
        /// LobbyScreen fills them from PlayerProfile, so they stay, small, rather than go unwired.
        /// </summary>
        private static void BuildPlayerCard(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0f, 1f);   // top-left
            var mid = new Vector2(0.5f, 0.5f);

            // The card body. Compact, hugging the corner. Children are parented to it.
            Transform card = Panel(parent, "PlayerCard", anchor,
                new Vector2(200f, -70f), new Vector2(360f, 100f));

            // Avatar: a colour derived from the name, initial on top, thin gold frame behind.
            // uGUI has no clip-path, so the web's hexagon is approximated by the square avatar
            // that is already wired — restyled a touch smaller to fit the compact card.
            UiKit.MakeRect(card, "AvatarFrame", new Color(0.55f, 0.42f, 0.10f, 0.6f),
                mid, new Vector2(-138f, 0f), new Vector2(80f, 80f));

            Image avatar = UiKit.MakeRect(card, "Avatar", new Color(0.3f, 0.3f, 0.3f),
                mid, new Vector2(-138f, 0f), new Vector2(72f, 72f));

            TMP_Text initial = UiKit.MakeText(card, "AvatarInitial", "S", 40f,
                UiKit.TextWhite, mid, new Vector2(-138f, 0f));
            initial.fontStyle = FontStyles.Bold;

            // Name, with a gold LVL badge to its right.
            TMP_Text name = UiKit.MakeText(card, "PlayerName", "SOLDIER", 24f,
                UiKit.TextWhite, mid, new Vector2(10f, 26f));
            name.alignment = TextAlignmentOptions.Left;
            name.fontStyle = FontStyles.Bold;
            name.rectTransform.sizeDelta = new Vector2(180f, 30f);

            // The LVL badge: a small gold chip carrying the level number.
            UiKit.MakeRect(card, "LevelBadge", UiKit.Gold,
                mid, new Vector2(128f, 26f), new Vector2(52f, 24f));
            TMP_Text level = UiKit.MakeText(card, "PlayerLevel", "1", 18f,
                new Color(0.08f, 0.05f, 0.02f), mid, new Vector2(128f, 26f));
            level.fontStyle = FontStyles.Bold;
            level.rectTransform.sizeDelta = new Vector2(52f, 24f);

            // A thin XP bar under the name.
            Image xpBar = UiKit.MakeBar(card, "Xp", UiKit.Gold,
                mid, new Vector2(46f, -2f), new Vector2(280f, 6f));

            TMP_Text xp = UiKit.MakeText(card, "XpText", "0 / 500 XP", 13f,
                UiKit.TextDim, mid, new Vector2(46f, -22f));
            xp.alignment = TextAlignmentOptions.Left;
            xp.rectTransform.sizeDelta = new Vector2(280f, 20f);

            // The stat row, in a slim strip below the card. Not in the web mockup, but the Unity
            // profile fills these, so they live here small rather than go unwired.
            Transform stats = Panel(parent, "StatRow", anchor,
                new Vector2(200f, -142f), new Vector2(360f, 44f), BorderInk);

            TMP_Text kills = Stat(stats, "Kills", "KILLS", new Vector2(-118f, 0f));
            TMP_Text matches = Stat(stats, "Matches", "MATCHES", new Vector2(0f, 0f));
            TMP_Text wave = Stat(stats, "BestWave", "BEST WAVE", new Vector2(118f, 0f));

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

        /// <summary>One stat in the slim row: number left of centre, caption right of it. Returns
        /// the number, which is the only part anything ever sets.</summary>
        private static TMP_Text Stat(Transform parent, string name, string label, Vector2 pos)
        {
            var anchor = new Vector2(0.5f, 0.5f);

            TMP_Text value = UiKit.MakeText(parent, name + "Value", "0", 20f,
                UiKit.TextWhite, anchor, pos + new Vector2(-20f, 0f));
            value.fontStyle = FontStyles.Bold;
            value.alignment = TextAlignmentOptions.Right;
            value.rectTransform.sizeDelta = new Vector2(40f, 26f);

            TMP_Text caption = UiKit.MakeText(parent, name + "Label", label, 9f,
                UiKit.TextDim, anchor, pos + new Vector2(24f, 0f));
            caption.characterSpacing = 2f;
            caption.alignment = TextAlignmentOptions.Left;
            caption.rectTransform.sizeDelta = new Vector2(72f, 22f);

            return value;
        }

        // ------------------------------------------------------------------ loadout strip

        /// <summary>
        /// Top-left, under the player card: the three loadout slots a shooter always shows —
        /// PRIMARY (filled, M-91), SECONDARY and MELEE (empty, dashed). Decorative: the game has
        /// one weapon, so nothing here is wired. The empty slots use the ink border to read as
        /// empty rather than borrowed.
        /// </summary>
        private static void BuildLoadoutStrip(Transform parent)
        {
            var anchor = new Vector2(0f, 1f);
            Transform strip = Panel(parent, "LoadoutStrip", anchor,
                new Vector2(200f, -196f), new Vector2(360f, 60f), BorderInk);

            LoadoutSlot(strip, "Primary", "M-91", true, new Vector2(-118f, 0f));
            LoadoutSlot(strip, "Secondary", "—", false, new Vector2(0f, 0f));
            LoadoutSlot(strip, "Melee", "—", false, new Vector2(118f, 0f));
        }

        private static void LoadoutSlot(Transform parent, string name, string label,
            bool filled, Vector2 pos)
        {
            var mid = new Vector2(0.5f, 0.5f);

            // The slot tile — a warmer ink for filled, near-black for empty, with a dim border.
            UiKit.MakeRect(parent, name + "Border", filled ? UiKit.GoldDim : BorderInk,
                mid, pos, new Vector2(108f, 46f));
            UiKit.MakeRect(parent, name + "Tile",
                filled ? new Color(0.12f, 0.11f, 0.09f, 0.9f)
                       : new Color(0.03f, 0.03f, 0.03f, 0.6f),
                mid, pos, new Vector2(104f, 42f));

            TMP_Text text = UiKit.MakeText(parent, name + "Label", label, 13f,
                filled ? UiKit.Gold : UiKit.TextDim, mid, pos + new Vector2(0f, -12f));
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 3f;
            text.rectTransform.sizeDelta = new Vector2(104f, 18f);

            TMP_Text slot = UiKit.MakeText(parent, name + "Slot", name.ToUpperInvariant(), 8f,
                UiKit.TextDim, mid, pos + new Vector2(0f, 8f));
            slot.characterSpacing = 2f;
            slot.rectTransform.sizeDelta = new Vector2(104f, 14f);
        }

        // --------------------------------------------------------------------- team up

        /// <summary>Top-left, under the loadout: a TEAM UP button. Decorative — there is no
        /// multiplayer, so it is not wired to anything.</summary>
        private static void BuildTeamUp(Transform parent)
        {
            var anchor = new Vector2(0f, 1f);
            Transform bar = Panel(parent, "TeamUp", anchor,
                new Vector2(200f, -246f), new Vector2(360f, 36f));

            TMP_Text label = UiKit.MakeText(bar, "TeamUpLabel", "TEAM UP", 15f,
                UiKit.TextWhite, new Vector2(0.5f, 0.5f), Vector2.zero);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 8f;
        }

        // ------------------------------------------------------------------ top centre

        /// <summary>
        /// Top-centre: a row of small square doors — mail, missions, an "S1 · 18d" season timer,
        /// and sound. All decorative; none is wired. Centred on the top edge so it clears the
        /// KAAL RAAT watermark below it.
        /// </summary>
        private static void BuildTopCentre(Transform parent)
        {
            var anchor = new Vector2(0.5f, 1f);
            float y = -46f;

            IconSquare(parent, "Mail", "MAIL", new Vector2(-140f, y), false);
            IconSquare(parent, "Missions", "MSN", new Vector2(-88f, y), false);
            IconSquare(parent, "Timer", "S1 · 18d", new Vector2(0f, y), true);
            IconSquare(parent, "Sound", "SND", new Vector2(88f, y), false);
        }

        private static void IconSquare(Transform parent, string name, string label,
            Vector2 pos, bool wide)
        {
            var anchor = new Vector2(0.5f, 1f);
            var size = wide ? new Vector2(120f, 40f) : new Vector2(40f, 40f);

            UiKit.MakeRect(parent, name + "Border", BorderGold, anchor, pos,
                size + new Vector2(4f, 4f));
            UiKit.MakeRect(parent, name + "Sq", PanelBlack, anchor, pos, size);

            TMP_Text text = UiKit.MakeText(parent, name + "Label", label, wide ? 13f : 10f,
                wide ? UiKit.TextWhite : UiKit.TextDim, anchor, pos);
            text.characterSpacing = 2f;
            text.rectTransform.sizeDelta = size;
        }

        // ----------------------------------------------------------------- currency bar

        /// <summary>
        /// Top-right: the two currency pills — 💎 shards and 🪙 scrip — each with a gold "+"
        /// chip. Right-aligned to the corner. Decorative: the game has no economy, so nothing
        /// here is wired.
        /// </summary>
        private static void BuildCurrencyBar(Transform parent)
        {
            var anchor = new Vector2(1f, 1f);   // top-right
            CurrencyPill(parent, "Shards", "2,480", new Color(0.4f, 0.85f, 0.95f),
                new Vector2(-360f, -44f));
            CurrencyPill(parent, "Scrip", "15,200", UiKit.Gold,
                new Vector2(-170f, -44f));
        }

        private static void CurrencyPill(Transform parent, string name, string value,
            Color dot, Vector2 pos)
        {
            var anchor = new Vector2(1f, 1f);
            var size = new Vector2(160f, 36f);

            UiKit.MakeRect(parent, name + "Border", BorderGold, anchor, pos,
                size + new Vector2(4f, 4f));
            UiKit.MakeRect(parent, name + "Pill", PanelBlack, anchor, pos, size);

            // The coloured token that stands in for the 💎 / 🪙 glyph.
            UiKit.MakeRect(parent, name + "Dot", dot, anchor,
                pos + new Vector2(-64f, 0f), new Vector2(16f, 16f));

            TMP_Text text = UiKit.MakeText(parent, name + "Value", value, 15f,
                UiKit.TextWhite, anchor, pos + new Vector2(2f, 0f));
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.sizeDelta = new Vector2(110f, 24f);

            // The gold "+" chip on the right end of the pill.
            UiKit.MakeRect(parent, name + "Plus", UiKit.Gold, anchor,
                pos + new Vector2(64f, 0f), new Vector2(28f, 34f));
            TMP_Text plus = UiKit.MakeText(parent, name + "PlusLabel", "+", 20f,
                new Color(0.08f, 0.05f, 0.02f), anchor, pos + new Vector2(64f, 0f));
            plus.fontStyle = FontStyles.Bold;
            plus.rectTransform.sizeDelta = new Vector2(28f, 34f);
        }

        // ---------------------------------------------------------------- rail buttons

        /// <summary>
        /// Top-right, under the currency: a vertical stack of three square buttons — RP, CRATE,
        /// SHOP. Decorative; none is wired. CRATE carries a small red badge like the web mockup.
        /// </summary>
        private static void BuildRailButtons(Transform parent)
        {
            var anchor = new Vector2(1f, 1f);
            RailSquare(parent, "RP", "RP", new Vector2(-60f, -104f), false);
            RailSquare(parent, "Crate", "CRATE", new Vector2(-60f, -164f), true);
            RailSquare(parent, "Shop", "SHOP", new Vector2(-60f, -224f), false);
        }

        private static void RailSquare(Transform parent, string name, string label,
            Vector2 pos, bool badge)
        {
            var anchor = new Vector2(1f, 1f);
            var size = new Vector2(52f, 52f);

            UiKit.MakeRect(parent, name + "Border", BorderGold, anchor, pos,
                size + new Vector2(4f, 4f));
            UiKit.MakeRect(parent, name + "Sq", PanelBlack, anchor, pos, size);

            TMP_Text text = UiKit.MakeText(parent, name + "Label", label, 11f,
                UiKit.TextDim, anchor, pos);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 2f;
            text.rectTransform.sizeDelta = size;

            // A red unread badge on the corner, like the web mockup's CRATE.
            if (badge)
                UiKit.MakeRect(parent, name + "Badge", new Color(0.82f, 0.1f, 0.06f),
                    anchor, pos + new Vector2(24f, 24f), new Vector2(12f, 12f));
        }

        // --------------------------------------------------------------- promo banners

        /// <summary>
        /// Right edge, above the bottom nav: the two banners the web mockup's PromoBanners draws —
        /// KAAL PASS (tier 22 + a mini progress bar) and EVENTS (with a red "3" badge). Both
        /// decorative; neither is wired.
        /// </summary>
        private static void BuildPromoBanners(Transform parent)
        {
            var anchor = new Vector2(1f, 0f);   // bottom-right, sat above the deploy row height

            // KAAL PASS.
            Transform pass = Panel(parent, "KaalPass", anchor,
                new Vector2(-170f, 340f), new Vector2(300f, 72f), BorderGold);

            TMP_Text passName = UiKit.MakeText(pass, "PassName", "KAAL PASS", 13f,
                UiKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f));
            passName.fontStyle = FontStyles.Bold;
            passName.characterSpacing = 6f;
            passName.rectTransform.sizeDelta = new Vector2(280f, 20f);

            TMP_Text tier = UiKit.MakeText(pass, "PassTier", "22", 22f,
                UiKit.TextWhite, new Vector2(0.5f, 0.5f), new Vector2(-118f, -14f));
            tier.fontStyle = FontStyles.Bold;
            tier.rectTransform.sizeDelta = new Vector2(48f, 28f);

            Image passBar = UiKit.MakeBar(pass, "PassBar", UiKit.Gold,
                new Vector2(0.5f, 0.5f), new Vector2(30f, -14f), new Vector2(180f, 6f));
            passBar.fillAmount = 0.64f;   // decorative-only; never driven at runtime

            TMP_Text passMax = UiKit.MakeText(pass, "PassMax", "50", 11f,
                UiKit.TextDim, new Vector2(0.5f, 0.5f), new Vector2(132f, -14f));
            passMax.rectTransform.sizeDelta = new Vector2(30f, 18f);

            // EVENTS.
            Transform events = Panel(parent, "Events", anchor,
                new Vector2(-170f, 274f), new Vector2(300f, 52f), BorderInk);

            TMP_Text evName = UiKit.MakeText(events, "EventsName", "EVENTS", 13f,
                UiKit.TextWhite, new Vector2(0.5f, 0.5f), new Vector2(-90f, 8f));
            evName.fontStyle = FontStyles.Bold;
            evName.characterSpacing = 6f;
            evName.alignment = TextAlignmentOptions.Left;
            evName.rectTransform.sizeDelta = new Vector2(200f, 20f);

            TMP_Text evBlurb = UiKit.MakeText(events, "EventsBlurb", "NIGHT HUNT teaser", 11f,
                UiKit.TextDim, new Vector2(0.5f, 0.5f), new Vector2(-40f, -12f));
            evBlurb.alignment = TextAlignmentOptions.Left;
            evBlurb.rectTransform.sizeDelta = new Vector2(220f, 18f);

            // The red "3" badge, top-right corner of the EVENTS banner.
            UiKit.MakeRect(parent, "EventsBadge", new Color(0.82f, 0.1f, 0.06f),
                anchor, new Vector2(-24f, 300f), new Vector2(20f, 20f));
            TMP_Text badge = UiKit.MakeText(parent, "EventsBadgeText", "3", 12f,
                UiKit.TextWhite, anchor, new Vector2(-24f, 300f));
            badge.fontStyle = FontStyles.Bold;
            badge.rectTransform.sizeDelta = new Vector2(20f, 20f);
        }

        // ------------------------------------------------------------- mode + the maps

        /// <summary>
        /// Bottom-left: the mode card (map thumbnail + SURVIVAL label + "ISLAND · Solo" + a gear
        /// glyph) sitting above the two real map buttons. The web mockup opens a picker overlay
        /// from this card; uGUI has no runtime picker in LobbyScreen and adding one would need new
        /// runtime code, so the simpler, LobbyScreen-safe path is taken: the two real map buttons
        /// (mapAButton / mapBButton) sit visible just under the mode card, small, and LobbyScreen
        /// drives them directly the way it always has. The mode card itself is decorative.
        /// </summary>
        private static void BuildModeAndMaps(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0f, 0f);   // bottom-left

            // The mode card — decorative header for the map choice below it.
            Transform mode = Panel(parent, "ModeCard", anchor,
                new Vector2(210f, 320f), new Vector2(380f, 64f));

            // The map-thumbnail block down the card's left.
            UiKit.MakeRect(mode, "ModeThumb", new Color(0.10f, 0.22f, 0.12f),
                new Vector2(0.5f, 0.5f), new Vector2(-158f, 0f), new Vector2(64f, 44f));

            TMP_Text modeName = UiKit.MakeText(mode, "ModeName", "SURVIVAL", 11f,
                UiKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(-20f, 12f));
            modeName.alignment = TextAlignmentOptions.Left;
            modeName.characterSpacing = 6f;
            modeName.rectTransform.sizeDelta = new Vector2(240f, 18f);

            TMP_Text modeMap = UiKit.MakeText(mode, "ModeMap", "ISLAND · Solo", 18f,
                UiKit.TextWhite, new Vector2(0.5f, 0.5f), new Vector2(-20f, -12f));
            modeMap.alignment = TextAlignmentOptions.Left;
            modeMap.fontStyle = FontStyles.Bold;
            modeMap.rectTransform.sizeDelta = new Vector2(240f, 24f);

            // A gear hint on the right. The project font has no ⚙ glyph and would render a tofu
            // box, so it is drawn as a small ring: a dim square with a darker square punched into
            // it. Purely a hint that this card is the door to the mode/map choice.
            UiKit.MakeRect(mode, "ModeGearRing", UiKit.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(168f, 0f), new Vector2(20f, 20f));
            UiKit.MakeRect(mode, "ModeGearHole", PanelBlack,
                new Vector2(0.5f, 0.5f), new Vector2(168f, 0f), new Vector2(10f, 10f));

            // The two real map buttons, small, under the mode card. These carry the wired
            // mapAButton / mapBButton / outlines / status texts that LobbyScreen reads.
            (Button btn, Image outline, TMP_Text status) a = MapChip(parent, "MapA",
                "ISLAND", new Color(0.10f, 0.22f, 0.12f), anchor, new Vector2(115f, 250f));

            (Button btn, Image outline, TMP_Text status) b = MapChip(parent, "MapB",
                "ARENA", new Color(0.20f, 0.18f, 0.15f), anchor, new Vector2(305f, 250f));

            ArenaSceneBuilder.SetPrivate(lobby, "mapAButton", a.btn);
            ArenaSceneBuilder.SetPrivate(lobby, "mapAOutline", a.outline);
            ArenaSceneBuilder.SetPrivate(lobby, "mapAStatus", a.status);
            ArenaSceneBuilder.SetPrivate(lobby, "mapBButton", b.btn);
            ArenaSceneBuilder.SetPrivate(lobby, "mapBOutline", b.outline);
            ArenaSceneBuilder.SetPrivate(lobby, "mapBStatus", b.status);
        }

        /// <summary>
        /// One small map button: a thumbnail colour, the map name, an outline that goes gold when
        /// selected, and a status line above it for the "NOT BUILT" message LobbyScreen writes.
        /// The status text sits above the chip so it does not collide with the DEPLOY row below.
        /// </summary>
        private static (Button, Image, TMP_Text) MapChip(Transform parent, string name,
            string title, Color thumb, Vector2 anchor, Vector2 pos)
        {
            var size = new Vector2(180f, 84f);

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

            // A colour block for the thumbnail — a real screenshot means rendering each map to a
            // PNG, its own job; a stock photo would show a map that is not this map.
            UiKit.MakeRect(go.transform, "Thumb", thumb, new Vector2(0f, 0.5f),
                new Vector2(46f, 0f), new Vector2(64f, 56f));

            TMP_Text titleText = UiKit.MakeText(go.transform, "Title", title, 18f,
                UiKit.TextWhite, new Vector2(0f, 0.5f), new Vector2(128f, 0f));
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 3f;
            titleText.rectTransform.sizeDelta = new Vector2(110f, 30f);

            TMP_Text status = UiKit.MakeText(parent, name + "Status", "", 12f,
                new Color(0.95f, 0.5f, 0.3f), anchor, pos + new Vector2(0f, 60f));
            status.rectTransform.sizeDelta = new Vector2(190f, 40f);
            status.enableWordWrapping = true;

            return (btn, outline, status);
        }

        // ----------------------------------------------------------------------- play

        /// <summary>
        /// DEPLOY, bottom-left now, under the mode card and map buttons. It is the largest,
        /// brightest thing on the screen after the soldier, because it is the only button anyone
        /// came here to press. The loading block moves with it.
        /// </summary>
        private static void BuildPlayPanel(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(0f, 0f);   // bottom-left

            // Ember orange, not gold, and big — this is the button the whole scene is lit to
            // point at, and the web mockup's blood-and-fire palette runs right through it.
            var ember = new Color(1f, 0.52f, 0.14f);

            Button play = UiKit.MakeButton(parent, "Play", "DEPLOY",
                ember, new Color(0.12f, 0.04f, 0.01f),
                anchor, new Vector2(210f, 130f), new Vector2(380f, 84f), 38f);

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

        /// <summary>
        /// The two live buttons the web mockup does not draw but LobbyScreen needs: SETTINGS
        /// (opens the settings panel) and SIGN OUT. They sit at the top-right, tucked to the left
        /// of the currency/rail column so they clear it, small and quiet — the real controls among
        /// the decorative storefront.
        /// </summary>
        private static void BuildTopRight(Transform parent, LobbyScreen lobby)
        {
            var anchor = new Vector2(1f, 1f);

            Button settings = UiKit.MakeButton(parent, "Settings", "SETTINGS",
                new Color(0.16f, 0.15f, 0.13f), UiKit.TextWhite,
                anchor, new Vector2(-500f, -44f), new Vector2(150f, 40f), 15f);

            Button signOut = UiKit.MakeButton(parent, "SignOut", "SIGN OUT",
                new Color(0.16f, 0.13f, 0.13f), UiKit.TextDim,
                anchor, new Vector2(-500f, -90f), new Vector2(150f, 36f), 14f);

            ArenaSceneBuilder.SetPrivate(lobby, "settingsButton", settings);
            ArenaSceneBuilder.SetPrivate(lobby, "signOutButton", signOut);
        }

        // ------------------------------------------------------------------ squad bar

        /// <summary>
        /// Bottom-centre, left of the chat: the squad bar — four slots, slot one is you
        /// (JASSI200), the other three pulse-empty in the web mockup. Decorative: there is no
        /// multiplayer, so the slots are drawn but not wired. Sat left of dead centre so it clears
        /// the soldier's feet, the way the web mockup positions it.
        /// </summary>
        private static void BuildSquadBar(Transform parent)
        {
            var anchor = new Vector2(0.5f, 0f);
            Transform bar = Panel(parent, "SquadBar", anchor,
                new Vector2(-360f, 66f), new Vector2(320f, 68f), BorderInk);

            SquadSlot(bar, "Slot0", "JASSI200", true, new Vector2(-114f, 0f));
            SquadSlot(bar, "Slot1", "+", false, new Vector2(-38f, 0f));
            SquadSlot(bar, "Slot2", "+", false, new Vector2(38f, 0f));
            SquadSlot(bar, "Slot3", "+", false, new Vector2(114f, 0f));
        }

        private static void SquadSlot(Transform parent, string name, string label,
            bool filled, Vector2 pos)
        {
            var mid = new Vector2(0.5f, 0.5f);

            UiKit.MakeRect(parent, name + "Border", filled ? UiKit.GoldDim : BorderInk,
                mid, pos, new Vector2(68f, 52f));
            UiKit.MakeRect(parent, name + "Tile",
                filled ? new Color(0.10f, 0.09f, 0.08f, 0.9f)
                       : new Color(0.03f, 0.03f, 0.03f, 0.55f),
                mid, pos, new Vector2(64f, 48f));

            if (filled)
            {
                TMP_Text who = UiKit.MakeText(parent, name + "Name", label, 10f,
                    UiKit.TextWhite, mid, pos + new Vector2(0f, 6f));
                who.fontStyle = FontStyles.Bold;
                who.rectTransform.sizeDelta = new Vector2(64f, 16f);

                TMP_Text lvl = UiKit.MakeText(parent, name + "Lvl", "LVL 14", 9f,
                    UiKit.Gold, mid, pos + new Vector2(0f, -8f));
                lvl.rectTransform.sizeDelta = new Vector2(64f, 14f);
            }
            else
            {
                TMP_Text plus = UiKit.MakeText(parent, name + "Plus", "+", 22f,
                    UiKit.TextDim, mid, pos);
                plus.rectTransform.sizeDelta = new Vector2(64f, 48f);
            }
        }

        // ------------------------------------------------------------------ chat strip

        /// <summary>
        /// Bottom-centre, right of the squad bar: a single-line chat strip. Decorative — no
        /// multiplayer means no chat, so it is drawn but not wired.
        /// </summary>
        private static void BuildChatStrip(Transform parent)
        {
            var anchor = new Vector2(0.5f, 0f);
            Transform strip = Panel(parent, "ChatStrip", anchor,
                new Vector2(120f, 66f), new Vector2(420f, 40f), BorderInk);

            TMP_Text name = UiKit.MakeText(strip, "ChatName", "[Vipin29]:", 13f,
                UiKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f));
            name.alignment = TextAlignmentOptions.Left;
            name.fontStyle = FontStyles.Bold;
            name.rectTransform.sizeDelta = new Vector2(120f, 20f);

            TMP_Text text = UiKit.MakeText(strip, "ChatText", "koi squad aao", 13f,
                UiKit.TextWhite, new Vector2(0.5f, 0.5f), new Vector2(30f, 0f));
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.sizeDelta = new Vector2(240f, 20f);
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

using System.IO;
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
    /// Builds the KAAL RAAT intro scene: a timed action-horror sequence (gunshots punch
    /// bullet holes into the dark, the claw emblem slams in, the title types itself out)
    /// over a drone with a heartbeat in it, while the island loads behind the branding.
    ///
    /// Every visual is generated — slash, splatter and bullet-hole sprites are rendered
    /// into textures by code, the drone is synthesised on first build, and the gunshot,
    /// roar, and tick sounds are reused from the game's own baked audio. The whole look
    /// lives in the constant block below.
    ///
    /// Run from the menu: Game > Build Splash Scene
    /// </summary>
    public static class SplashSceneBuilder
    {
        // ------------------------------------------------------------------ THE LOOK
        // Change these, rerun the menu item, done.

        private const string GameTitle = "KAAL RAAT";
        private const string Subtitle = "HORROR  •  SURVIVAL  •  BATTLE ROYALE";
        private const string Quote =
            "\"Sabse bada dushman doosre players nahi...\nwoh cheez hai jo raat ko jaagti hai.\"";
        private const string TapText = "TAP TO ENTER THE NIGHT";
        private const string VersionLabel = "v0.1 — early build";

        // BGMI-style warning & studio text
        private const string AgeWarning =
            "This game contains scenes of violence and horror.\nPlayer discretion is advised.";
        private const string StudioName = "YAARI GAMES";
        private const string PoweredBy = "POWERED BY UNITY";

        private static readonly Color Blood = new(0.55f, 0.04f, 0.03f);
        private static readonly Color BloodBright = new(0.82f, 0.10f, 0.06f);
        private static readonly Color BackgroundTop = new(0.07f, 0.015f, 0.012f);
        private static readonly Color BackgroundBottom = new(0.008f, 0.004f, 0.004f);
        private static readonly Color TitleColor = new(0.85f, 0.80f, 0.72f);   // bone
        private static readonly Color DimText = new(0.42f, 0.33f, 0.30f);
        private static readonly Color BulletPale = new(0.85f, 0.78f, 0.70f);

        // --------------------------------------------------------------------------

        private const string ScenePath = "Assets/Scenes/Splash.unity";
        private const string DronePath = "Assets/Audio/AMB_SplashDrone.wav";
        private const string BedPath = "Assets/Audio/AMB_SplashBed.wav";
        private const int DroneSampleRate = 22050;

        /// <summary>Length of the scored clip. Must track the timeline in SplashController —
        /// the swell inside it is aimed at the emblem slam, which lands at ~4.7s.</summary>
        private const float DroneSeconds = 10f;

        [MenuItem("Game/Build Splash Scene")]
        public static void Build()
        {
            if (ArenaSceneBuilder.BlockedByPlayMode()) return;

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            AudioSource drone = BuildDroneSource();
            AudioSource bed = BuildBedSource();
            AudioSource[] jungle = BuildJungleSources();

            // No shared SFX source any more: pitch lives on an AudioSource, not on a shot, so
            // one shared voice meant every re-pitched gunshot dragged the roar underneath it
            // along. The controller spawns a voice per shot instead.

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

            // ── BGMI-style intro groups (shown before the horror sequence) ──
            CanvasGroup ageWarning = BuildBgmiGroup(c, "AgeWarning", AgeWarning, 28,
                new Color(0.9f, 0.2f, 0.15f), DimText, "⚠", 60);
            CanvasGroup poweredBy = BuildBgmiGroup(c, "PoweredBy", PoweredBy, 32,
                new Color(0.6f, 0.6f, 0.6f), default, null, 0);
            CanvasGroup studio = BuildBgmiGroup(c, "Studio", StudioName, 80,
                new Color(0.95f, 0.55f, 0.05f), DimText, null, 0);

            // Content: everything that fades out at the end.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            contentGo.transform.SetParent(c, false);
            Stretch(contentGo.GetComponent<RectTransform>());
            var content = contentGo.GetComponent<CanvasGroup>();

            // ShakeRoot: what the gunshots rattle. The loading bar and prompts sit outside
            // it — UI chrome shaking with the world reads as a bug, not an impact.
            var shakeGo = new GameObject("ShakeRoot", typeof(RectTransform));
            shakeGo.transform.SetParent(contentGo.transform, false);
            Stretch(shakeGo.GetComponent<RectTransform>());
            var shakeRoot = shakeGo.GetComponent<RectTransform>();
            Transform st = shakeGo.transform;

            RectTransform[] holes = BuildBulletHoles(st);
            RectTransform emblem = BuildEmblem(st);

            var titleGroupGo = new GameObject("TitleGroup",
                typeof(RectTransform), typeof(CanvasGroup));
            titleGroupGo.transform.SetParent(st, false);
            Stretch(titleGroupGo.GetComponent<RectTransform>());
            var titleGroup = titleGroupGo.GetComponent<CanvasGroup>();

            TMP_Text title = MakeText(titleGroupGo.transform, "Title", GameTitle, 128,
                TitleColor, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 34f;

            TMP_Text subtitle = MakeText(titleGroupGo.transform, "Subtitle", Subtitle, 30,
                BloodBright, new Vector2(0.5f, 0.5f), new Vector2(0f, -165f));
            subtitle.characterSpacing = 16f;

            TMP_Text quote = MakeText(titleGroupGo.transform, "Quote", Quote, 27,
                DimText, new Vector2(0.5f, 0.5f), new Vector2(0f, -265f));
            quote.fontStyle = FontStyles.Italic;

            Image loadingFill = BuildLoadingBar(contentGo.transform);

            TMP_Text tap = MakeText(contentGo.transform, "TapPrompt", TapText, 27,
                BloodBright, new Vector2(0.5f, 0f), new Vector2(0f, 190f));
            tap.characterSpacing = 20f;

            MakeText(contentGo.transform, "Version", VersionLabel, 20, DimText,
                new Vector2(1f, 0f), new Vector2(-140f, 36f));

            // Above the content: the dark pressing in, then the flash on the very top so
            // gunshots and lightning light the whole frame, vignette included.
            BuildVignette(c);
            Image flash = BuildFlashOverlay(c);

            var controller = canvasGo.AddComponent<SplashController>();
            ArenaSceneBuilder.SetPrivate(controller, "content", content);
            ArenaSceneBuilder.SetPrivate(controller, "shakeRoot", shakeRoot);
            ArenaSceneBuilder.SetPrivate(controller, "titleGroup", titleGroup);
            ArenaSceneBuilder.SetPrivate(controller, "emblem", emblem);
            ArenaSceneBuilder.SetPrivate(controller, "title", title);
            ArenaSceneBuilder.SetPrivate(controller, "subtitle", subtitle);
            ArenaSceneBuilder.SetPrivate(controller, "quote", quote);
            ArenaSceneBuilder.SetPrivate(controller, "flashOverlay", flash);
            ArenaSceneBuilder.SetPrivate(controller, "loadingFill", loadingFill);
            ArenaSceneBuilder.SetPrivate(controller, "tapPrompt", tap);
            ArenaSceneBuilder.SetPrivate(controller, "droneSource", drone);
            ArenaSceneBuilder.SetPrivate(controller, "bedSource", bed);
            SetObjectArray(controller, "jungleSources", jungle);
            ArenaSceneBuilder.SetPrivate(controller, "nextSceneName", "Login");
            ArenaSceneBuilder.SetPrivate(controller, "ageWarningGroup", ageWarning);
            ArenaSceneBuilder.SetPrivate(controller, "studioGroup", studio);
            ArenaSceneBuilder.SetPrivate(controller, "poweredByGroup", poweredBy);
            SetObjectArray(controller, "bulletHoles", holes);

            // Supplied recordings first, stock second, synth last. The single report and the
            // burst are two different guns and stay two different clips: the volley opens on
            // aimed shots and ends on a held trigger, and playing both recordings over the
            // whole thing is what used to make it mud.
            WireClip(controller, "gunshotClip",
                     "Assets/Audio/SFX_GunShot_User.mp3",
                     "Assets/Audio/SFX_GunShot.mp3",
                     "Assets/Audio/SFX_Fire.wav");

            WireClip(controller, "gunBurstClip",
                     "Assets/Audio/SFX_GunBurst_User.mp3",
                     "Assets/Audio/SFX_GunBurst.mp3",
                     "Assets/Audio/SFX_Fire.wav");

            WireClip(controller, "explosionClip", "Assets/Audio/SFX_Explosion.mp3");

            // Three roars carry the whole first half of the sequence, each closer and pitched
            // lower than the last — of every clip in the intro, this is the one most worth
            // having a real recording of.
            WireClip(controller, "roarClip",
                     "Assets/Audio/AMB_Roar_Real.mp3",
                     "Assets/Audio/AMB_Roar.wav");

            WireClip(controller, "tickClip", "Assets/Audio/SFX_Empty.wav");

            ArenaSceneBuilder.SaveSceneChecked(scene, ScenePath);
            ArenaSceneBuilder.AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<b>Splash built.</b> Saved to {ScenePath}. Boots first, runs the " +
                      "intro, then hands off to Login. Build the Island and Login scenes " +
                      "too, and turn the sound on.");
        }

        /// <summary>
        /// Writes a private array of object references. ArenaSceneBuilder.SetPrivate handles
        /// single objects and Transform[] only, and widening it to every array type the
        /// builders might want is more machinery than a five-line loop deserves.
        /// </summary>
        private static void SetObjectArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);

            if (prop == null)
            {
                Debug.LogError($"Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires the first clip in <paramref name="paths"/> that actually loads. Ordered best
        /// to worst: a supplied recording, then a stock one, then the synthesised bake — so a
        /// project missing its audio still builds a splash that makes noise.
        /// </summary>
        private static void WireClip(Object target, string field, params string[] paths)
        {
            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                ArenaSceneBuilder.SetPrivate(target, field, clip);
                return;
            }

            Debug.LogWarning($"Splash: no clip found for {field}. Tried: " +
                             string.Join(", ", paths) + ". Bake the game audio " +
                             "(Game > Bake Weapon Audio / Bake Jungle Ambience).");
        }

        // ------------------------------------------------------------------ backdrop

        private static void BuildBackground(Transform parent)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<RawImage>().texture = MakeGradientTexture();
        }

        private static Texture2D MakeGradientTexture()
        {
            const string path = "Assets/Settings/T_SplashGradient.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int h = 256;
            var tex = new Texture2D(4, h) { wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < h; y++)
            {
                // Bright band high on the screen, like something glowing past the treeline.
                float t = Mathf.Pow(y / (float)(h - 1), 2.2f);
                Color row = Color.Lerp(BackgroundBottom, BackgroundTop, t);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, row);
            }

            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        private static void BuildVignette(Transform parent)
        {
            const string path = "Assets/Settings/T_SplashVignette.asset";
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
                        float a = Mathf.SmoothStep(0f, 0.88f, (d - 0.45f) / 0.55f);
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                    }
                }

                tex.Apply();
                AssetDatabase.CreateAsset(tex, path);
            }

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }

        private static Image BuildFlashOverlay(Transform parent)
        {
            var go = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());

            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        // -------------------------------------------------------------- bullet holes

        /// <summary>
        /// Five impacts, each rotated and sized differently so one sprite reads as five
        /// separate hits. They walk across the screen in firing order rather than landing
        /// at random — the burst should look like someone tracking a moving shape, and the
        /// last two are tight together where the panic peaks.
        /// </summary>
        private static RectTransform[] BuildBulletHoles(Transform parent)
        {
            Sprite sprite = BulletHoleSprite();

            var placements = new (Vector2 pos, float size, float rot)[]
            {
                (new Vector2(-640f, 300f), 130f, 20f),
                (new Vector2(-210f, 380f), 110f, 275f),
                (new Vector2(300f, 250f), 145f, 95f),
                (new Vector2(560f, 210f), 150f, 160f),
                (new Vector2(640f, 320f), 120f, 40f),
            };

            var holes = new RectTransform[placements.Length];
            for (int i = 0; i < placements.Length; i++)
            {
                var (pos, size, rot) = placements[i];
                RectTransform rt = MakeSpriteImage(parent, $"BulletHole_{i}", sprite,
                    BulletPale, pos, new Vector2(size, size));
                rt.localRotation = Quaternion.Euler(0f, 0f, rot);
                holes[i] = rt;
            }

            return holes;
        }

        /// <summary>A dark core with pale cracks radiating out — glass hit by a round.</summary>
        private static Sprite BulletHoleSprite()
        {
            var rng = new System.Random(7);

            // Cracks: angle, length (as fraction of half-size), width in radians.
            var cracks = new (float angle, float length, float width)[10];
            for (int i = 0; i < cracks.Length; i++)
            {
                cracks[i] = (
                    (float)rng.NextDouble() * Mathf.PI * 2f,
                    0.45f + (float)rng.NextDouble() * 0.5f,
                    0.05f + (float)rng.NextDouble() * 0.05f);
            }

            return LoadOrCreateSprite("S_BulletHole", (x, y, size) =>
            {
                var centre = new Vector2(size / 2f, size / 2f);
                var p = new Vector2(x, y) - centre;
                float d = p.magnitude / (size / 2f);          // 0 centre, 1 edge

                // Core: solid, with a soft rim.
                float a = Mathf.Clamp01((0.16f - d) * 18f);

                // Cracks: thin wedges that fade with distance.
                float angle = Mathf.Atan2(p.y, p.x);
                foreach (var (ca, length, width) in cracks)
                {
                    float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg,
                                                            ca * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    if (d < length && diff < width * (1f - d))
                        a = Mathf.Max(a, (1f - d / length) * 0.9f);
                }

                return a;
            });
        }

        // -------------------------------------------------------------------- emblem

        private static RectTransform BuildEmblem(Transform parent)
        {
            var emblem = new GameObject("Emblem", typeof(RectTransform));
            emblem.transform.SetParent(parent, false);
            var rt = emblem.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 190f);
            rt.sizeDelta = new Vector2(340f, 340f);

            MakeSpriteImage(emblem.transform, "Splatter", SplatterSprite(),
                new Color(Blood.r, Blood.g, Blood.b, 0.55f),
                new Vector2(10f, -8f), new Vector2(350f, 350f));

            Sprite slash = SlashSprite();
            MakeSpriteImage(emblem.transform, "Claw_1", slash, BloodBright,
                new Vector2(-62f, 0f), new Vector2(74f, 320f)).localRotation =
                Quaternion.Euler(0f, 0f, 14f);
            MakeSpriteImage(emblem.transform, "Claw_2", slash, BloodBright,
                new Vector2(0f, -12f), new Vector2(78f, 340f)).localRotation =
                Quaternion.Euler(0f, 0f, 12f);
            MakeSpriteImage(emblem.transform, "Claw_3", slash, Blood,
                new Vector2(62f, -4f), new Vector2(70f, 305f)).localRotation =
                Quaternion.Euler(0f, 0f, 16f);

            return rt;
        }

        private static Sprite SlashSprite()
        {
            return LoadOrCreateSprite("S_Slash", (x, y, size) =>
            {
                float v = y / (float)(size - 1);                 // 0..1 along the slash
                float taper = Mathf.Sin(v * Mathf.PI);           // pointed ends
                float halfWidth = Mathf.Max(1f, taper * size * 0.09f);

                // A slight S-curve so the cut reads as dragged, not stamped.
                float centre = size / 2f + Mathf.Sin(v * Mathf.PI * 2f) * size * 0.03f;

                float d = Mathf.Abs(x - centre);
                return Mathf.Clamp01((halfWidth - d) / 2f);
            });
        }

        private static Sprite SplatterSprite()
        {
            const int size = 256;
            var rng = new System.Random(666);

            var blobs = new (Vector2 pos, Vector2 radius)[26];
            for (int i = 0; i < blobs.Length; i++)
            {
                bool drip = i > 18;
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = (float)rng.NextDouble() * size * (drip ? 0.32f : 0.26f);

                var pos = new Vector2(
                    size / 2f + Mathf.Cos(angle) * dist,
                    size / 2f + Mathf.Sin(angle) * dist - (drip ? size * 0.12f : 0f));

                float r = drip
                    ? 2.5f + (float)rng.NextDouble() * 3f
                    : 4f + (float)rng.NextDouble() * 15f;

                blobs[i] = (pos, new Vector2(r, drip ? r * (2.5f + (float)rng.NextDouble() * 2f) : r));
            }

            return LoadOrCreateSprite("S_Splatter", (x, y, s) =>
            {
                float a = 0f;
                foreach (var (pos, radius) in blobs)
                {
                    float dx = (x - pos.x) / radius.x;
                    float dy = (y - pos.y) / radius.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Max(a, Mathf.Clamp01((1f - d) * 2.2f));
                }
                return a;
            });
        }

        private delegate float AlphaAt(int x, int y, int size);

        private static Sprite LoadOrCreateSprite(string name, AlphaAt alpha)
        {
            string spritePath = $"Assets/Settings/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (existing != null) return existing;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y, size)));

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
            rt.sizeDelta = new Vector2(520f, 5f);

            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.06f);
            trackImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            Stretch(fill.GetComponent<RectTransform>());

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = Blood;
            fillImage.raycastTarget = false;

            fillImage.sprite = SlashSprite();   // any sprite works; fill needs one to slice
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            return fillImage;
        }

        // --------------------------------------------------------------------- audio

        /// <summary>
        /// The island, heard before it is seen. It opens the sequence at ordinary volume — the
        /// sound of a place with nothing wrong with it — and the controller ducks it to almost
        /// nothing the moment the first roar lands. A forest going quiet is how you know
        /// something is in it.
        ///
        /// One source if there is a full jungle recording to use, which is the good case: a
        /// real field recording already has its birds and insects in balance, and splitting it
        /// would only re-mix what someone already mixed.
        ///
        /// Two if there isn't, because the stock clips are separate stems. Looping them on
        /// separate sources makes them drift against each other and never repeat the same
        /// pairing — which is what keeps a 40 KB insect loop from turning into an audible tick.
        /// </summary>
        private static AudioSource[] BuildJungleSources()
        {
            var root = new GameObject("Jungle").transform;

            var whole = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AMB_Jungle_User.mp3");
            if (whole != null)
                return new[] { MakeLoop(root, "Jungle", whole, 1f) };

            return new[]
            {
                MakeLoop(root, "Birds", "Assets/Audio/AMB_Birds_Real.mp3",
                         "Assets/Audio/AMB_Birds.wav", 1f),
                MakeLoop(root, "Insects", "Assets/Audio/AMB_Insects_Real.mp3",
                         "Assets/Audio/AMB_Insects.wav", 0.55f),
            };
        }

        private static AudioSource MakeLoop(Transform parent, string name, string path,
                                            string fallback, float mix)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path)
                       ?? AssetDatabase.LoadAssetAtPath<AudioClip>(fallback);

            if (clip == null)
                Debug.LogWarning($"Splash: neither {path} nor {fallback} loaded.");

            return MakeLoop(parent, name, clip, mix);
        }

        /// <param name="mix">Volume relative to the jungle as a whole. The controller scales
        /// every source by this when it ducks, so the balance survives the fade.</param>
        private static AudioSource MakeLoop(Transform parent, string name, AudioClip clip,
                                            float mix)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.volume = mix;

            source.clip = clip;   // null is survivable: silent source, warned about upstream
            return source;
        }

        /// <summary>
        /// The dread underneath, ten seconds of it: two low sines a fraction of a hertz
        /// apart (the beating between them is the unease), a rumble and a whine that both
        /// climb as the intro does, a sub-bass swell peaking under the emblem slam, and a
        /// heartbeat that accelerates from 1.7s apart to under a second. Synthesised here.
        ///
        /// Rebaked on every build rather than cached: the shape of this clip is tied to the
        /// timeline in SplashController, so a stale one from an older timing would drift
        /// out of sync with the visuals it is scored to.
        /// </summary>
        private static AudioSource BuildDroneSource()
        {
            BakeDrone();

            var go = new GameObject("Drone");
            var source = go.AddComponent<AudioSource>();

            // Not looped: the heartbeat inside it accelerates, so a loop would snap the
            // pulse back to calm exactly when the intro is at its most frantic. The clip is
            // cut to the length of the sequence and simply plays once under it.
            source.loop = false;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.volume = 0.6f;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DronePath);
            if (clip != null) source.clip = clip;
            else Debug.LogWarning("Splash: drone clip failed to load after bake.");

            return source;
        }

        /// <summary>
        /// The bed: the same two detuned sines and rumble, seamlessly loopable, and nothing
        /// else. No heartbeat and no swell — those are scored to the picture and live in the
        /// drone clip. This is only here so that a slow scene load never drops the splash
        /// into silence.
        /// </summary>
        private static AudioSource BuildBedSource()
        {
            BakeBed();

            var go = new GameObject("Bed");
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.volume = 0.32f;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BedPath);
            if (clip != null) source.clip = clip;
            else Debug.LogWarning("Splash: bed clip failed to load after bake.");

            return source;
        }

        private static void BakeBed()
        {
            const float seconds = 4f;
            int n = (int)(DroneSampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(29);

            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)DroneSampleRate;

                float drone = (Mathf.Sin(2f * Mathf.PI * 41f * t)
                             + Mathf.Sin(2f * Mathf.PI * 41.35f * t)) * 0.22f;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.015f;

                buf[i] = drone + lp * 0.45f;
            }

            // Crossfade the head over the tail so the loop seam is inaudible. This one does
            // loop forever, so the seam is the only thing that matters about it.
            int fade = (int)(DroneSampleRate * 0.5f);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                buf[i] = Mathf.Lerp(buf[n - fade + i], buf[i], t);
            }

            Normalise(buf, 0.6f);
            WriteWav(BedPath, buf);
            AssetDatabase.Refresh();
        }

        private static void BakeDrone()
        {
            const float seconds = DroneSeconds;
            int n = (int)(DroneSampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(13);

            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)DroneSampleRate;
                float through = t / seconds;                   // 0..1 across the sequence

                // The beating pair. 41 vs 41.35 Hz — a 0.35 Hz pulse you feel, not hear.
                float drone = (Mathf.Sin(2f * Mathf.PI * 41f * t)
                             + Mathf.Sin(2f * Mathf.PI * 41.35f * t)) * 0.22f;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.015f;

                // The rumble climbs as the intro does, so the floor under the whole thing
                // is rising even in the silence before the slam.
                float rumble = lp * (0.4f + through * 0.55f);

                // A sub-bass swell timed to peak under the emblem hit at ~4.7s — the drop
                // you feel in the chest a moment before the claws land.
                float slamSwell = Mathf.Exp(-Mathf.Pow((t - 4.7f) / 1.5f, 2f));
                float sub = Mathf.Sin(2f * Mathf.PI * 28f * t) * slamSwell * 0.4f;

                float swell = Mathf.Pow(
                    0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.07f * t - 1.2f), 3f);
                float whine = Mathf.Sin(2f * Mathf.PI * 587f * t)
                            * swell * (0.03f + through * 0.05f);

                buf[i] = drone + rumble + sub + whine;
            }

            // The heartbeat accelerates — 1.7s apart at the start, under 1.0s by the end.
            // A metronome pulse is atmosphere; one that speeds up is panic, and it does the
            // work of telling you this is going somewhere.
            for (float at = 0.15f, gap = 1.7f; at < seconds; at += gap, gap *= 0.86f)
            {
                float urgency = Mathf.InverseLerp(1.7f, 0.9f, gap);

                AddThump(buf, at, 0.3f + urgency * 0.22f);
                AddThump(buf, at + 0.28f, 0.19f + urgency * 0.14f);
            }

            // No loop seam to hide — this clip is a one-shot the length of the sequence, so
            // it only needs to arrive out of nothing.
            int fade = (int)(DroneSampleRate * 0.5f);
            for (int i = 0; i < fade; i++)
                buf[i] *= i / (float)fade;

            Normalise(buf, 0.75f);
            WriteWav(DronePath, buf);
            AssetDatabase.Refresh();
        }

        private static void Normalise(float[] buf, float peak)
        {
            float max = 0f;
            foreach (float s in buf) max = Mathf.Max(max, Mathf.Abs(s));

            float scale = max > 1e-5f ? peak / max : 1f;
            for (int i = 0; i < buf.Length; i++) buf[i] *= scale;
        }

        private static void AddThump(float[] buf, float atSeconds, float gain)
        {
            int start = (int)(atSeconds * DroneSampleRate);
            int length = (int)(DroneSampleRate * 0.14f);

            for (int i = 0; i < length && start + i < buf.Length; i++)
            {
                float t = i / (float)DroneSampleRate;
                float env = Mathf.Exp(-t * 34f);
                // Pitch falls through the thump — that drop is what makes it cardiac.
                buf[start + i] += Mathf.Sin(2f * Mathf.PI * (72f - t * 160f) * t) * env * gain;
            }
        }

        /// <summary>Same 16-bit mono PCM writer as the other bakers — small enough that
        /// sharing it is not worth coupling the editor tools together.</summary>
        private static void WriteWav(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var file = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(file);

            int dataBytes = samples.Length * 2;

            w.Write("RIFF".ToCharArray());
            w.Write(36 + dataBytes);
            w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(DroneSampleRate);
            w.Write(DroneSampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write(dataBytes);

            foreach (float s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
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
            rt.sizeDelta = new Vector2(1600f, 160f);

            return tmp;
        }

        private static RectTransform MakeSpriteImage(Transform parent, string name,
                                                     Sprite sprite, Color color,
                                                     Vector2 position, Vector2 size)
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

            return rt;
        }

        /// <summary>
        /// Builds a BGMI-style intro card: centered text with optional icon above it,
        /// wrapped in a CanvasGroup for fade in/out. Used for age warning, studio logo,
        /// and "powered by" screens.
        /// </summary>
        private static CanvasGroup BuildBgmiGroup(Transform parent, string name,
            string text, float fontSize, Color textColor, Color subColor,
            string icon, float iconSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;   // starts hidden

            // Dark backdrop so it covers the gradient background cleanly
            var bgGo = new GameObject("BgmiDark", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            Stretch(bgGo.GetComponent<RectTransform>());
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.95f);
            bgGo.GetComponent<Image>().raycastTarget = false;

            // Icon above text (e.g. warning symbol)
            if (!string.IsNullOrEmpty(icon))
            {
                var iconText = MakeText(go.transform, name + "_Icon", icon, iconSize,
                    textColor, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f));
                iconText.raycastTarget = false;
            }

            // Main text
            float yPos = string.IsNullOrEmpty(icon) ? 0f : -20f;
            var mainText = MakeText(go.transform, name + "_Text", text, fontSize,
                textColor, new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
            mainText.raycastTarget = false;

            // Decorative line under studio name
            if (name == "Studio")
            {
                var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
                lineGo.transform.SetParent(go.transform, false);
                var lineRt = lineGo.GetComponent<RectTransform>();
                lineRt.anchorMin = lineRt.anchorMax = new Vector2(0.5f, 0.5f);
                lineRt.anchoredPosition = new Vector2(0f, yPos - 55f);
                lineRt.sizeDelta = new Vector2(350f, 2f);
                lineGo.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.05f, 0.5f);
                lineGo.GetComponent<Image>().raycastTarget = false;

                // "PRESENTS" subtitle
                var presents = MakeText(go.transform, "Presents", "P R E S E N T S", 22,
                    new Color(0.5f, 0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, yPos - 80f));
                presents.raycastTarget = false;
            }

            return group;
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

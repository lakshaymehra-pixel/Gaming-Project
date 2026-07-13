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
    /// Builds the horror splash: black screen bleeding red at the top, claw marks over a
    /// blood splatter, a title that flickers like a dying light, a drone underneath with a
    /// heartbeat in it, and a loading bar that tracks the island loading behind the dread.
    ///
    /// Everything is generated — the slash and splatter sprites are rendered into
    /// textures by code, the drone is synthesised into a .wav on first build. The whole
    /// look lives in the constant block below.
    ///
    /// Run from the menu: Game > Build Splash Scene
    /// </summary>
    public static class SplashSceneBuilder
    {
        // ------------------------------------------------------------------ THE LOOK
        // Change these, rerun the menu item, done.

        private const string GameTitle = "SHADOW JUNGLE";
        private const string Tagline = "THE JUNGLE IS WATCHING";
        private const string VersionLabel = "v0.1 — early build";

        private static readonly Color Blood = new(0.55f, 0.04f, 0.03f);
        private static readonly Color BloodBright = new(0.78f, 0.08f, 0.05f);
        private static readonly Color BackgroundTop = new(0.07f, 0.015f, 0.012f);
        private static readonly Color BackgroundBottom = new(0.008f, 0.004f, 0.004f);
        private static readonly Color TitleColor = new(0.82f, 0.78f, 0.72f);   // bone
        private static readonly Color DimText = new(0.38f, 0.30f, 0.28f);

        // --------------------------------------------------------------------------

        private const string ScenePath = "Assets/Scenes/Splash.unity";
        private const string DronePath = "Assets/Audio/AMB_SplashDrone.wav";
        private const int DroneSampleRate = 22050;

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

            BuildDroneSource();

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

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            contentGo.transform.SetParent(c, false);
            Stretch(contentGo.GetComponent<RectTransform>());
            var content = contentGo.GetComponent<CanvasGroup>();
            Transform ct = contentGo.transform;

            RectTransform emblem = BuildEmblem(ct);

            // The title gets its own group so the flicker can dim it without touching the
            // rest of the layout — the two alphas multiply.
            var titleGroupGo = new GameObject("TitleGroup",
                typeof(RectTransform), typeof(CanvasGroup));
            titleGroupGo.transform.SetParent(ct, false);
            Stretch(titleGroupGo.GetComponent<RectTransform>());
            var titleGroup = titleGroupGo.GetComponent<CanvasGroup>();

            TMP_Text title = MakeText(titleGroupGo.transform, "Title", GameTitle, 100,
                TitleColor, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 24f;

            TMP_Text tagline = MakeText(titleGroupGo.transform, "Tagline", Tagline, 28,
                DimText, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f));
            tagline.characterSpacing = 34f;

            Image loadingFill = BuildLoadingBar(ct);

            TMP_Text tap = MakeText(ct, "TapPrompt", "TAP IF YOU DARE", 26, BloodBright,
                new Vector2(0.5f, 0f), new Vector2(0f, 190f));
            tap.characterSpacing = 24f;

            MakeText(ct, "Version", VersionLabel, 20, DimText,
                new Vector2(1f, 0f), new Vector2(-140f, 36f));

            // Vignette above everything: the dark closes in from the corners.
            BuildVignette(c);

            var controller = canvasGo.AddComponent<SplashController>();
            ArenaSceneBuilder.SetPrivate(controller, "content", content);
            ArenaSceneBuilder.SetPrivate(controller, "loadingFill", loadingFill);
            ArenaSceneBuilder.SetPrivate(controller, "tapPrompt", tap);
            ArenaSceneBuilder.SetPrivate(controller, "nextSceneName", "Island");

            var fx = canvasGo.AddComponent<SplashFx>();
            ArenaSceneBuilder.SetPrivate(fx, "flickerTarget", titleGroup);
            ArenaSceneBuilder.SetPrivate(fx, "creepTarget", emblem);

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

        /// <summary>Radial darkness pressing in from the edges. Drawn last so it sits over
        /// everything, including the title — the corners of the screen belong to the dark.</summary>
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

        // -------------------------------------------------------------------- emblem

        /// <summary>
        /// Three claw slashes over a blood splatter. The slash sprite is a tapered streak —
        /// thick in the middle, pointed at the ends — which is what separates a claw mark
        /// from a paint roller.
        /// </summary>
        private static RectTransform BuildEmblem(Transform parent)
        {
            var emblem = new GameObject("Emblem", typeof(RectTransform));
            emblem.transform.SetParent(parent, false);
            var rt = emblem.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 170f);
            rt.sizeDelta = new Vector2(320f, 320f);

            MakeSpriteImage(emblem.transform, "Splatter", SplatterSprite(),
                new Color(Blood.r, Blood.g, Blood.b, 0.55f),
                new Vector2(10f, -8f), new Vector2(330f, 330f));

            Sprite slash = SlashSprite();
            MakeSpriteImage(emblem.transform, "Claw_1", slash, BloodBright,
                new Vector2(-58f, 0f), new Vector2(70f, 300f)).localRotation =
                Quaternion.Euler(0f, 0f, 14f);
            MakeSpriteImage(emblem.transform, "Claw_2", slash, BloodBright,
                new Vector2(0f, -12f), new Vector2(74f, 320f)).localRotation =
                Quaternion.Euler(0f, 0f, 12f);
            MakeSpriteImage(emblem.transform, "Claw_3", slash, Blood,
                new Vector2(58f, -4f), new Vector2(66f, 290f)).localRotation =
                Quaternion.Euler(0f, 0f, 16f);

            return rt;
        }

        /// <summary>A vertical streak, widest at the centre, tapering to points.</summary>
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

        /// <summary>Random blobs and drips clustered around the middle.</summary>
        private static Sprite SplatterSprite()
        {
            const int size = 256;
            var rng = new System.Random(666);

            // Precompute blob list: position, radius. Drips are tall thin blobs low down.
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
        /// The dread underneath: two low sines a fraction of a hertz apart (the beating
        /// between them is the unease), a filtered rumble, a faint dissonant whine that
        /// swells and retreats, and a double-thump heartbeat. Synthesised on first build.
        /// </summary>
        private static void BuildDroneSource()
        {
            if (!File.Exists(DronePath)) BakeDrone();

            var go = new GameObject("Drone");
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0f;
            source.volume = 0.55f;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DronePath);
            if (clip != null) source.clip = clip;
            else Debug.LogWarning("Splash: drone clip failed to load after bake.");
        }

        private static void BakeDrone()
        {
            const float seconds = 9.6f;   // fits six heartbeats exactly, so the loop seam
            const float beatEvery = 1.6f; // lands between beats, not inside one
            int n = (int)(DroneSampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(13);

            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)DroneSampleRate;

                // The beating pair. 41 vs 41.35 Hz — a 0.35 Hz pulse you feel, not hear.
                float drone = (Mathf.Sin(2f * Mathf.PI * 41f * t)
                             + Mathf.Sin(2f * Mathf.PI * 41.35f * t)) * 0.22f;

                // Rumble: heavily lowpassed noise.
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.015f;
                float rumble = lp * 0.5f;

                // The whine: quiet, dissonant against the drone, swelling on its own cycle.
                float swell = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.07f * t - 1.2f), 3f);
                float whine = Mathf.Sin(2f * Mathf.PI * 587f * t) * swell * 0.035f;

                buf[i] = drone + rumble + whine;
            }

            // Heartbeat: lub-dub every beatEvery seconds.
            int beats = (int)(seconds / beatEvery);
            for (int b = 0; b < beats; b++)
            {
                AddThump(buf, b * beatEvery, 0.32f);
                AddThump(buf, b * beatEvery + 0.30f, 0.20f);
            }

            // Crossfade the seam so the loop never clicks.
            int fade = (int)(DroneSampleRate * 0.4f);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                buf[i] = Mathf.Lerp(buf[n - fade + i], buf[i], t);
            }

            // Normalise to a safe peak.
            float max = 0f;
            foreach (float s in buf) max = Mathf.Max(max, Mathf.Abs(s));
            float scale = max > 1e-5f ? 0.75f / max : 1f;
            for (int i = 0; i < buf.Length; i++) buf[i] *= scale;

            WriteWav(DronePath, buf);
            AssetDatabase.Refresh();
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
            rt.sizeDelta = new Vector2(1400f, 140f);

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

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

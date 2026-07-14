using Game.UI;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The 3D world behind the splash: a strip of night jungle, a soldier moving through it,
    /// and a camera walking in after him. The UI sits on top of this, so the intro opens on
    /// the game rather than on a logo.
    ///
    /// It is a stage set, not a level. Everything is placed relative to the camera's path and
    /// nothing exists behind it — a hundred-odd props against the Island's ~1300, because this
    /// has to be on screen the instant the app opens and it is only ever seen from one line.
    ///
    /// Reuses JungleFoliage's prop makers so the trees here are the same trees you land in.
    /// </summary>
    internal static class SplashBackdrop
    {
        /// <summary>How far the camera travels, in metres. The walk is slow and the intro is
        /// ten seconds, so this is short.</summary>
        internal const float WalkDistance = 7f;

        private const int Seed = 4471;

        // The corridor the camera walks down. Props are scattered either side of it and never
        // in it, so nothing clips through the lens.
        private const float CorridorHalfWidth = 2.6f;
        private const float StageWidth = 26f;
        private const float StageDepth = 34f;

        /// <summary>What the splash scene needs to hold on to once the set is struck.</summary>
        internal readonly struct Stage
        {
            internal readonly SplashCamera Camera;
            internal readonly SplashSoldier Soldier;   // null if the model is missing

            internal Stage(SplashCamera camera, SplashSoldier soldier)
            {
                Camera = camera;
                Soldier = soldier;
            }
        }

        internal static Stage Build()
        {
            var root = new GameObject("Backdrop").transform;
            var rng = new System.Random(Seed);

            BuildNight();
            BuildGround(root);

            JungleFoliage.Palette palette = JungleFoliage.CreatePalette();
            ScatterTrees(root, palette, rng);
            ScatterFloor(root, palette, rng);

            SplashSoldier soldier = BuildSoldier(root);
            SplashCamera camera = BuildCamera(root);

            return new Stage(camera, soldier);
        }

        // ------------------------------------------------------------------- lighting

        /// <summary>
        /// Night, and almost none of it. A weak moon from behind so the trees come out as
        /// silhouettes rather than objects — you should be reading shapes, not foliage — and
        /// fog thick enough that the jungle simply stops eight metres out. What you cannot see
        /// is the point of the whole sequence.
        /// </summary>
        private static void BuildNight()
        {
            var moonGo = new GameObject("Moon");
            Light moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.85f);      // cold, and dim
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            moonGo.transform.rotation = Quaternion.Euler(18f, 200f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.07f, 0.09f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.04f, 0.05f, 0.07f);
            RenderSettings.ambientGroundColor = new Color(0.01f, 0.01f, 0.02f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.06f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.075f;               // the jungle ends ~8m out
        }

        // --------------------------------------------------------------------- ground

        private static void BuildGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(StageWidth / 10f, 1f, StageDepth / 10f);
            ground.transform.position = new Vector3(0f, 0f, StageDepth * 0.35f);

            ground.GetComponent<Renderer>().sharedMaterial =
                ArenaSceneBuilder.MakeMaterial("M_SplashGround", new Color(0.05f, 0.06f, 0.04f));

            // Nothing walks on it and nothing is shot at it. A collider here would only cost.
            Object.DestroyImmediate(ground.GetComponent<Collider>());
        }

        // --------------------------------------------------------------------- props

        private static void ScatterTrees(Transform parent, JungleFoliage.Palette palette,
                                         System.Random rng)
        {
            var group = new GameObject("Trees").transform;
            group.SetParent(parent, false);

            const int count = 46;
            int placed = 0;

            for (int i = 0; i < count * 4 && placed < count; i++)
            {
                if (!TryPlace(rng, out Vector3 p)) continue;

                // Big ones at the back, small at the front. Depth reads from size long before
                // it reads from anything else, and fog alone will not sell it.
                float depth = Mathf.InverseLerp(-2f, StageDepth, p.z);

                GameObject tree = rng.NextDouble() < 0.22
                    ? JungleFoliage.MakePalm(palette, rng)
                    : JungleFoliage.MakeTree(palette, rng,
                          Mathf.Lerp(7f, 20f, depth),
                          Mathf.Lerp(11f, 30f, depth),
                          Mathf.Lerp(0.6f, 1.3f, depth),
                          buttressRoots: depth > 0.6f);

                tree.transform.SetParent(group, false);
                tree.transform.position = p;
                tree.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                placed++;
            }

            Debug.Log($"Splash backdrop: {placed} trees.");
        }

        private static void ScatterFloor(Transform parent, JungleFoliage.Palette palette,
                                         System.Random rng)
        {
            var group = new GameObject("Floor").transform;
            group.SetParent(parent, false);

            const int count = 70;

            for (int i = 0; i < count * 3; i++)
            {
                if (group.childCount >= count) break;

                // Ground cover is allowed closer to the corridor than trees are — brushing
                // past a fern is what a walk through undergrowth should look like.
                if (!TryPlace(rng, out Vector3 p, halfWidth: 1.3f)) continue;

                GameObject cover = JungleFoliage.MakeGroundCover(palette, rng);
                cover.transform.SetParent(group, false);
                cover.transform.position = p;
                cover.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            }
        }

        /// <summary>
        /// Finds a spot on the stage that is not in the camera's corridor. Returns false rather
        /// than nudging the point aside: a rejected sample is cheaper than a prop that ends up
        /// against the lens, and the caller simply asks again.
        /// </summary>
        private static bool TryPlace(System.Random rng, out Vector3 point,
                                     float halfWidth = CorridorHalfWidth)
        {
            float x = ((float)rng.NextDouble() - 0.5f) * StageWidth;
            float z = ((float)rng.NextDouble()) * StageDepth - 3f;

            point = new Vector3(x, 0f, z);

            // The corridor only needs protecting where the camera actually is — past the end of
            // its walk, props can close in and shut the view down.
            bool inCorridorLength = z > -4f && z < WalkDistance + 9f;
            return !(inCorridorLength && Mathf.Abs(x) < halfWidth);
        }

        // -------------------------------------------------------------------- soldier

        /// <summary>
        /// A man ahead of you in the trees. He is the reason the gunfire in the sequence has
        /// somewhere to come from — without him the shots are a sound effect; with him they are
        /// something happening to someone.
        ///
        /// He is a prop, not an enemy. SoldierFactory hands back a soldier wired for combat —
        /// an EnemyAnimator that reads a NavMeshAgent, an EnemyAI and a Health that do not
        /// exist here, plus colliders for bullets that will never be fired at him. All of it
        /// comes straight back off. SplashSoldier walks him on a fixed line instead.
        ///
        /// This scene is on screen the instant the app opens, and every system it does not
        /// strictly need is a system that can fail on a cold start.
        /// </summary>
        private static SplashSoldier BuildSoldier(Transform parent)
        {
            var go = new GameObject("Soldier");
            go.transform.SetParent(parent, false);

            // Ahead up the corridor and walking AWAY — 25° of yaw, so he drifts across the view
            // rather than marching down its centre line. He does not know the camera is there,
            // and a man walking toward the lens is a man posing for it.
            //
            // Close, because the fog gives out around eight metres and he is the only thing on
            // this set that has to be legible as a person. He still walks away from the camera
            // faster than it follows, so he fades into that fog on his own — which is what you
            // want: a man you can just make out, getting harder to see.
            go.transform.position = new Vector3(1.1f, 0f, 4.2f);
            go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            SoldierFactory.Build(go.transform, go.layer);

            Strip<Game.Enemies.EnemyAnimator>(go);   // wants a NavMeshAgent and an EnemyAI
            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);        // nothing is shot at, nothing collides

            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("Splash backdrop: no soldier model, so the trees have to " +
                                 "carry the shot. Check Assets/Models/Swat.fbx.");
                return null;
            }

            var walker = go.AddComponent<SplashSoldier>();
            ArenaSceneBuilder.SetPrivate(walker, "animator", animator);

            return walker;
        }

        private static void Strip<T>(GameObject go) where T : Component
        {
            foreach (T c in go.GetComponentsInChildren<T>())
                Object.DestroyImmediate(c);
        }

        // --------------------------------------------------------------------- camera

        private static SplashCamera BuildCamera(Transform parent)
        {
            var go = new GameObject("BackdropCamera");
            go.transform.SetParent(parent, false);

            // Eye height, at the mouth of the corridor, facing down it.
            go.transform.position = new Vector3(0f, 1.65f, -1.5f);
            go.transform.rotation = Quaternion.Euler(2f, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.04f);
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 90f;

            var rig = go.AddComponent<SplashCamera>();
            ArenaSceneBuilder.SetPrivate(rig, "walkDistance", WalkDistance);

            return rig;
        }
    }
}

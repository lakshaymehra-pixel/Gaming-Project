using System.Linq;
using Game.Core;
using Game.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the enemy's body. The preferred path is the rigged SWAT model from
    /// Quaternius (CC0, Assets/Models/Swat.fbx) with its embedded animation set driven by
    /// EnemyAnimator; if the file is missing the factory falls back to the primitive
    /// soldier so a fresh clone still produces a playable enemy.
    ///
    /// Hit logic is identical on both paths: one body collider on the root, one collider
    /// riding the head (tagged "Head") for the headshot multiplier, limbs catch nothing.
    /// On the model path the head collider is parented to the head BONE, so headshots track
    /// the animation instead of a fixed point in space.
    /// </summary>
    public static class SoldierFactory
    {
        private const string ModelPath = "Assets/Models/Swat.fbx";
        private const string ControllerPath = "Assets/Settings/EnemySoldier.controller";
        private const float SoldierHeight = 1.8f;

        public readonly struct Rig
        {
            public readonly Transform Eyes;
            public Rig(Transform eyes) { Eyes = eyes; }
        }

        public static Rig Build(Transform root, int layer)
        {
            // Import settings first: SaveAndReimport invalidates any object loaded from the
            // asset beforehand, so the load has to come after.
            EnsureImportSettings();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogWarning(
                    $"{ModelPath} not found — building the primitive soldier instead.");
                return BuildPrimitive(root, layer);
            }

            GameObject instance = (GameObject)Object.Instantiate(model, root);
            instance.name = "Model";
            SetLayerRecursive(instance.transform, layer);

            NormalizeHeight(instance);

            // Body hits: one capsule on the root, roughly the torso column. Same shape the
            // old capsule enemy presented to bullets, so weapon balance is untouched.
            var body = root.gameObject.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.95f, 0f);
            body.radius = 0.32f;
            body.height = 1.9f;

            AttachHeadCollider(instance, layer);

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadOrBuildController();
            animator.applyRootMotion = false;   // the NavMeshAgent owns movement

            var driver = root.gameObject.AddComponent<EnemyAnimator>();
            ArenaSceneBuilder.SetPrivate(driver, "animator", animator);

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            eyes.localPosition = new Vector3(0f, 1.6f, 0.15f);

            return new Rig(eyes);
        }

        // -------------------------------------------------------------------- model path

        /// <summary>
        /// One-time import configuration: mark the locomotion and idle clips as looping.
        /// FBX clips import with loopTime off, which would leave a runner frozen on the
        /// last frame of his first stride. Guarded so it only reimports once.
        /// </summary>
        private static void EnsureImportSettings()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null || importer.clipAnimations.Length > 0) return;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.Contains("Idle")
                             || clip.name.Contains("Run")
                             || clip.name.Contains("Walk");
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>Scales the instance so it stands SoldierHeight tall with feet at y=0,
        /// whatever unit system the FBX arrived in.</summary>
        private static void NormalizeHeight(GameObject instance)
        {
            Bounds bounds = ComputeBounds(instance);
            if (bounds.size.y < 0.01f) return;

            float scale = SoldierHeight / bounds.size.y;
            instance.transform.localScale *= scale;

            bounds = ComputeBounds(instance);
            instance.transform.localPosition -= new Vector3(0f, bounds.min.y, 0f);
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>
        /// Rides the head bone so the headshot zone follows the animation — a soldier mid
        /// run-cycle bobs, and a fixed sphere would drift off his skull.
        /// </summary>
        private static void AttachHeadCollider(GameObject instance, int layer)
        {
            Transform headBone = instance.GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => t.name == "Head");

            if (headBone == null)
            {
                Debug.LogWarning("Soldier model has no 'Head' bone; headshots disabled.");
                return;
            }

            var hit = new GameObject("HeadHit") { tag = "Head", layer = layer };
            hit.transform.SetParent(headBone, false);

            var sphere = hit.AddComponent<SphereCollider>();
            // The bone's world scale includes the height normalisation, so express the
            // world-space radius we want in the bone's local units.
            float worldRadius = 0.16f;
            float boneScale = headBone.lossyScale.y;
            sphere.radius = boneScale > 0.0001f ? worldRadius / boneScale : worldRadius;
        }

        /// <summary>
        /// The controller is just named states over the FBX's embedded clips — no
        /// transitions, no parameters. EnemyAnimator crossfades between states by name, so
        /// the logic lives in one C# file instead of being split with a state-machine graph.
        /// </summary>
        private static RuntimeAnimatorController LoadOrBuildController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;

            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview"))
                .ToList();

            AnimationClip Find(params string[] suffixes) =>
                suffixes.Select(s => clips.FirstOrDefault(c => c.name.EndsWith(s)))
                        .FirstOrDefault(c => c != null);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;

            void AddState(string name, AnimationClip clip)
            {
                if (clip == null)
                {
                    Debug.LogWarning($"Soldier controller: no clip found for '{name}'.");
                    return;
                }

                var state = machine.AddState(name);
                state.motion = clip;

                if (name == "Idle") machine.defaultState = state;
            }

            AddState("Idle",  Find("|Idle_Gun", "|Idle"));
            AddState("Run",   Find("|Run"));
            AddState("Shoot", Find("|Idle_Gun_Shoot", "|Gun_Shoot"));
            AddState("Hit",   Find("|HitRecieve"));
            AddState("Death", Find("|Death"));

            return controller;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursive(child, layer);
        }

        private class Palette
        {
            public Material Skin, Uniform, UniformDark, Vest, Helmet, Boot, Rifle;
        }

        private static Palette CreatePalette() => new Palette
        {
            Skin        = ArenaSceneBuilder.MakeMaterial("M_SolSkin",    new Color(0.78f, 0.58f, 0.45f)),
            Uniform     = ArenaSceneBuilder.MakeMaterial("M_SolUniform", new Color(0.30f, 0.32f, 0.22f)),
            UniformDark = ArenaSceneBuilder.MakeMaterial("M_SolPants",   new Color(0.22f, 0.24f, 0.17f)),
            Vest        = ArenaSceneBuilder.MakeMaterial("M_SolVest",    new Color(0.14f, 0.14f, 0.12f)),
            Helmet      = ArenaSceneBuilder.MakeMaterial("M_SolHelmet",  new Color(0.20f, 0.23f, 0.16f)),
            Boot        = ArenaSceneBuilder.MakeMaterial("M_SolBoot",    new Color(0.10f, 0.09f, 0.08f)),
            Rifle       = ArenaSceneBuilder.MakeMaterial("M_SolRifle",   new Color(0.12f, 0.12f, 0.13f)),
        };

        // ---------------------------------------------------------------- primitive path

        /// <summary>
        /// The original primitive soldier, kept as the fallback when Swat.fbx is absent —
        /// a fresh clone with no models must still produce a playable enemy.
        /// </summary>
        private static Rig BuildPrimitive(Transform root, int layer)
        {
            Palette p = CreatePalette();

            // ---- legs (pivoted at the hip so the walker can swing them) ----
            Transform leftLeg  = MakeLegPivot(root, p, new Vector3(-0.11f, 0.92f, 0f), layer, "L");
            Transform rightLeg = MakeLegPivot(root, p, new Vector3( 0.11f, 0.92f, 0f), layer, "R");

            // ---- torso ----
            GameObject torso = Box(root, "Torso", p.Uniform, layer,
                new Vector3(0f, 1.2f, 0f), new Vector3(0.44f, 0.56f, 0.26f));

            // The one solid part of the body. Slightly taller than the mesh so shots that
            // graze the hips still land.
            var torsoCollider = torso.AddComponent<BoxCollider>();
            torsoCollider.center = new Vector3(0f, -0.35f, 0f);
            torsoCollider.size = new Vector3(1.05f, 2.2f, 1.1f);

            // Vest: a darker, slightly fatter shell over the chest. The colour break at the
            // chest is most of what reads as "soldier" at 30 metres.
            Box(torso.transform, "Vest", p.Vest, layer,
                new Vector3(0f, 0.04f, 0f), new Vector3(1.12f, 0.72f, 1.18f));

            // ---- head (own collider, tagged for the headshot multiplier) ----
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.tag = "Head";
            head.layer = layer;
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.66f, 0f);
            head.transform.localScale = Vector3.one * 0.27f;
            head.GetComponent<Renderer>().sharedMaterial = p.Skin;
            // The primitive sphere collider stays — that IS the headshot zone.

            // Helmet: a flattened sphere sitting high on the skull.
            GameObject helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            helmet.name = "Helmet";
            helmet.layer = layer;
            helmet.transform.SetParent(head.transform, false);
            helmet.transform.localPosition = new Vector3(0f, 0.22f, -0.03f);
            helmet.transform.localScale = new Vector3(1.25f, 0.85f, 1.25f);
            helmet.GetComponent<Renderer>().sharedMaterial = p.Helmet;
            Object.DestroyImmediate(helmet.GetComponent<SphereCollider>());

            // ---- arms, frozen onto the rifle ----
            // Both forearms angle in toward the gun. They do not swing: a soldier at combat
            // ready keeps both hands on the weapon.
            MakeArm(root, p, layer, left: true);
            MakeArm(root, p, layer, left: false);

            // ---- rifle held in front ----
            GameObject rifle = Box(root, "Rifle", p.Rifle, layer,
                new Vector3(0.05f, 1.28f, 0.34f), new Vector3(0.07f, 0.11f, 0.62f));
            rifle.transform.localRotation = Quaternion.Euler(0f, 4f, 0f);

            Box(rifle.transform, "Stock", p.Rifle, layer,
                new Vector3(0f, -0.25f, -0.55f), new Vector3(0.8f, 1.4f, 0.25f));

            // ---- eyes: where the AI raycasts and shoots from ----
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            eyes.localPosition = new Vector3(0f, 1.62f, 0.16f);

            // ---- walk animation ----
            var walker = root.gameObject.AddComponent<ProceduralWalker>();
            ArenaSceneBuilder.SetPrivate(walker, "leftLeg", leftLeg);
            ArenaSceneBuilder.SetPrivate(walker, "rightLeg", rightLeg);

            return new Rig(eyes);
        }

        // ------------------------------------------------------------------------- pieces

        private static Transform MakeLegPivot(Transform root, Palette p, Vector3 hip,
                                              int layer, string side)
        {
            // The pivot sits at the hip; the leg hangs below it, so rotating the pivot
            // swings the whole leg like a pendulum.
            var pivot = new GameObject($"LegPivot_{side}").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = hip;

            Box(pivot, "Thigh", p.UniformDark, layer,
                new Vector3(0f, -0.24f, 0f), new Vector3(0.17f, 0.48f, 0.19f));

            Box(pivot, "Shin", p.UniformDark, layer,
                new Vector3(0f, -0.68f, 0f), new Vector3(0.14f, 0.42f, 0.16f));

            Box(pivot, "Boot", p.Boot, layer,
                new Vector3(0f, -0.89f, 0.05f), new Vector3(0.16f, 0.09f, 0.3f));

            return pivot;
        }

        private static void MakeArm(Transform root, Palette p, int layer, bool left)
        {
            float sign = left ? -1f : 1f;

            var pivot = new GameObject($"ArmPivot_{(left ? "L" : "R")}").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(sign * 0.28f, 1.42f, 0f);

            // Angled forward and inward so both hands land on the rifle in front of the chest.
            pivot.localRotation = Quaternion.Euler(52f, sign * -18f, 0f);

            Box(pivot, "UpperArm", p.Uniform, layer,
                new Vector3(0f, -0.17f, 0f), new Vector3(0.13f, 0.34f, 0.13f));

            Box(pivot, "Forearm", p.Skin, layer,
                new Vector3(sign * -0.05f, -0.42f, 0.02f), new Vector3(0.11f, 0.28f, 0.11f));
        }

        /// <summary>A colliderless textured box — the basic unit every body part is made of.</summary>
        private static GameObject Box(Transform parent, string name, Material mat, int layer,
                                      Vector3 localPos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Limbs and gear must not catch bullets — the torso and head colliders own all
            // hit detection, exactly as the capsule did.
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }
    }
}

using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Assembles a soldier body out of primitives: helmet, head, torso with a vest, arms
    /// held onto a rifle, legs that ProceduralWalker swings. It is deliberately game-y
    /// rather than realistic — believable silhouette and readable at range is the bar, and
    /// that is a matter of proportions and colour breaks, not polygon count.
    ///
    /// Hit logic is preserved from the capsule days: the torso carries the body collider,
    /// the head carries its own collider tagged "Head" for the headshot multiplier, and the
    /// limbs collide with nothing.
    /// </summary>
    public static class SoldierFactory
    {
        public readonly struct Rig
        {
            public readonly Transform Eyes;
            public Rig(Transform eyes) { Eyes = eyes; }
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

        /// <summary>
        /// Builds the body under <paramref name="root"/> and returns the rig. Everything is
        /// placed for a 1.8m soldier standing at the root's origin.
        /// </summary>
        public static Rig Build(Transform root, int layer)
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

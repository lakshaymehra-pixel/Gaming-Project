using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Grows the jungle. Density is the whole point — a rainforest is not trees on a lawn,
    /// it is a wall of green you cannot see fifteen metres into, which is also what makes it
    /// a good place to fight.
    ///
    /// Four layers, because that is how a real forest is built and how it reads:
    ///   emergent  — a few giants that break the canopy and give the skyline shape
    ///   canopy    — the dense roof, most of the trees
    ///   understory— young trees and palms in the gloom below
    ///   floor     — ferns, bushes, fallen logs, the stuff you actually walk through
    ///
    /// Every prop is built from primitives, so nothing is downloaded. When real tree assets
    /// arrive, only MakeTree/MakePalm/MakeFern need replacing — the layout, the density
    /// falloff, the collider rules, and the LOD budget all stay.
    /// </summary>
    public static class JungleFoliage
    {
        /// <summary>How the layers differ. Kept as data so the forest is tuned in one place.</summary>
        private readonly struct Layer
        {
            public readonly string Name;
            public readonly int Count;
            public readonly float MinHeight, MaxHeight;
            public readonly float MinSlope, MaxSlope;
            public readonly bool Collides;

            public Layer(string name, int count, float minH, float maxH,
                         float minSlope, float maxSlope, bool collides)
            {
                Name = name; Count = count;
                MinHeight = minH; MaxHeight = maxH;
                MinSlope = minSlope; MaxSlope = maxSlope;
                Collides = collides;
            }
        }

        public class Palette
        {
            public Material Trunk, TrunkDark, Canopy, CanopyDark, CanopyLight,
                            Fern, Bush, Log, Vine;
        }

        public static Palette CreatePalette()
        {
            return new Palette
            {
                // Two trunk tones and three greens: a forest where every tree is the same
                // colour reads as wallpaper, however many of them there are.
                Trunk      = ArenaSceneBuilder.MakeMaterial("M_JTrunk",      new Color(0.26f, 0.19f, 0.13f)),
                TrunkDark  = ArenaSceneBuilder.MakeMaterial("M_JTrunkDark",  new Color(0.17f, 0.13f, 0.10f)),
                Canopy     = ArenaSceneBuilder.MakeMaterial("M_JCanopy",     new Color(0.13f, 0.31f, 0.11f)),
                CanopyDark = ArenaSceneBuilder.MakeMaterial("M_JCanopyDark", new Color(0.08f, 0.21f, 0.09f)),
                CanopyLight= ArenaSceneBuilder.MakeMaterial("M_JCanopyLite", new Color(0.22f, 0.42f, 0.16f)),
                Fern       = ArenaSceneBuilder.MakeMaterial("M_JFern",       new Color(0.18f, 0.36f, 0.14f)),
                Bush       = ArenaSceneBuilder.MakeMaterial("M_JBush",       new Color(0.11f, 0.26f, 0.10f)),
                Log        = ArenaSceneBuilder.MakeMaterial("M_JLog",        new Color(0.22f, 0.17f, 0.12f)),
                Vine       = ArenaSceneBuilder.MakeMaterial("M_JVine",       new Color(0.15f, 0.30f, 0.12f)),
            };
        }

        /// <param name="densityMultiplier">Scales every layer. Drop it if the phone chokes.</param>
        public static void Grow(IslandTerrain island, Palette palette, System.Random rng,
                                float densityMultiplier = 1f)
        {
            var root = new GameObject("Jungle").transform;

            var layers = new[]
            {
                //          name          count  minH  maxH  minSlope maxSlope collides
                new Layer("Emergent",       28,   6f,  60f,  0f,      22f,     true),
                new Layer("Canopy",        340,   4f,  60f,  0f,      30f,     true),
                new Layer("Understory",    260,   3f,  60f,  0f,      34f,     true),
                new Layer("Palms",         120,   3f,  18f,  0f,      26f,     true),
                new Layer("Floor",         520,   3f,  60f,  0f,      38f,     false),
                new Layer("Logs",           45,   3f,  40f,  0f,      20f,     true),
            };

            foreach (Layer layer in layers)
            {
                var group = new GameObject(layer.Name).transform;
                group.SetParent(root, false);

                int count = Mathf.RoundToInt(layer.Count * densityMultiplier);
                int placed = 0;

                for (int i = 0; i < count; i++)
                {
                    if (!island.TryFindFlatLand(rng,
                            island.SeaLevel + layer.MinHeight,
                            layer.MaxSlope, 10f, 12, out Vector3 p))
                        continue;

                    if (island.HeightAt(p.x, p.z) > layer.MaxHeight) continue;

                    GameObject prop = layer.Name switch
                    {
                        "Emergent"   => MakeTree(palette, rng, 22f, 34f, 1.5f, true),
                        "Canopy"     => MakeTree(palette, rng, 11f, 18f, 1.0f, true),
                        "Understory" => MakeTree(palette, rng, 5f, 9f, 0.6f, false),
                        "Palms"      => MakePalm(palette, rng),
                        "Floor"      => MakeGroundCover(palette, rng),
                        "Logs"       => MakeLog(palette, rng),
                        _            => null,
                    };

                    if (prop == null) continue;

                    prop.name = $"{layer.Name}_{placed}";
                    prop.transform.SetParent(group, false);
                    prop.transform.position = p;
                    prop.transform.rotation = Quaternion.Euler(
                        0f, (float)rng.NextDouble() * 360f, 0f);

                    placed++;
                }

                Debug.Log($"Jungle: {layer.Name} — placed {placed}/{count}");
            }
        }

        // --------------------------------------------------------------------------- props

        /// <summary>
        /// A tree: a leaning trunk under three or four offset canopy blobs, plus buttress
        /// roots on the big ones. The lean and the asymmetric canopy are what stop a hundred
        /// of these reading as a hundred copies of one tree.
        /// </summary>
        private static GameObject MakeTree(Palette p, System.Random rng,
                                           float minHeight, float maxHeight,
                                           float canopyScale, bool buttressRoots)
        {
            var root = new GameObject("Tree");

            float height = minHeight + (float)rng.NextDouble() * (maxHeight - minHeight);
            float radius = Mathf.Lerp(0.18f, 0.75f, (height - 5f) / 30f)
                         * (0.8f + (float)rng.NextDouble() * 0.5f);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(radius, height * 0.5f, radius);

            // A slight lean, different on every tree.
            trunk.transform.localRotation = Quaternion.Euler(
                ((float)rng.NextDouble() - 0.5f) * 7f,
                0f,
                ((float)rng.NextDouble() - 0.5f) * 7f);

            trunk.GetComponent<Renderer>().sharedMaterial =
                rng.NextDouble() > 0.5 ? p.Trunk : p.TrunkDark;

            GameObjectUtility.SetStaticEditorFlags(trunk, StaticEditorFlags.NavigationStatic);

            // Buttress roots — the flared bases of rainforest giants. Cheap, and they sell
            // the scale of the emergents better than making them taller would.
            if (buttressRoots)
            {
                int roots = 3 + rng.Next(3);
                for (int i = 0; i < roots; i++)
                {
                    float angle = (i / (float)roots) * 360f
                                + (float)rng.NextDouble() * 30f;

                    GameObject root3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    root3.name = $"Root_{i}";
                    root3.transform.SetParent(root.transform, false);

                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    root3.transform.localPosition = dir * radius * 1.4f + Vector3.up * 0.7f;
                    root3.transform.localScale = new Vector3(0.35f, 1.6f, radius * 2.2f);
                    root3.transform.localRotation = Quaternion.Euler(0f, angle, 8f);
                    root3.GetComponent<Renderer>().sharedMaterial = p.TrunkDark;

                    GameObjectUtility.SetStaticEditorFlags(
                        root3, StaticEditorFlags.NavigationStatic);
                }
            }

            // Canopy: overlapping squashed spheres. No colliders — you shoot through leaves
            // and walk under them, and colliding foliage would make the NavMesh a maze.
            int blobs = 3 + rng.Next(3);
            for (int i = 0; i < blobs; i++)
            {
                GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blob.name = $"Canopy_{i}";
                blob.transform.SetParent(root.transform, false);

                float spread = radius * 6f * canopyScale;
                blob.transform.localPosition = new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * spread,
                    height + ((float)rng.NextDouble() - 0.3f) * height * 0.18f,
                    ((float)rng.NextDouble() - 0.5f) * spread);

                float r = (3f + (float)rng.NextDouble() * 3f) * canopyScale;
                blob.transform.localScale = new Vector3(r, r * 0.62f, r);

                Material[] greens = { p.Canopy, p.CanopyDark, p.CanopyLight };
                blob.GetComponent<Renderer>().sharedMaterial = greens[rng.Next(3)];

                Object.DestroyImmediate(blob.GetComponent<SphereCollider>());
            }

            // Vines hanging off the big ones.
            if (buttressRoots && rng.NextDouble() > 0.45)
            {
                int vines = 1 + rng.Next(3);
                for (int i = 0; i < vines; i++)
                {
                    GameObject vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    vine.name = $"Vine_{i}";
                    vine.transform.SetParent(root.transform, false);

                    float len = 3f + (float)rng.NextDouble() * 6f;
                    float angle = (float)rng.NextDouble() * 360f;
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                    vine.transform.localPosition =
                        dir * radius * 3f + Vector3.up * (height - len * 0.5f);
                    vine.transform.localScale = new Vector3(0.06f, len * 0.5f, 0.06f);
                    vine.GetComponent<Renderer>().sharedMaterial = p.Vine;

                    Object.DestroyImmediate(vine.GetComponent<CapsuleCollider>());
                }
            }

            return root;
        }

        /// <summary>Palm: a bare curving trunk with fronds fanning off the top.</summary>
        private static GameObject MakePalm(Palette p, System.Random rng)
        {
            var root = new GameObject("Palm");

            float height = 7f + (float)rng.NextDouble() * 6f;
            int segments = 5;
            float lean = ((float)rng.NextDouble() - 0.5f) * 22f;

            // Stacking short segments, each leaning slightly more than the last, gives the
            // curve a palm needs. A single tilted cylinder looks like a fallen pole.
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"Trunk_{i}";
                seg.transform.SetParent(root.transform, false);

                float segHeight = height / segments;
                float bend = lean * t * t;

                seg.transform.localPosition = new Vector3(
                    Mathf.Sin(bend * Mathf.Deg2Rad) * height * t * 0.5f,
                    segHeight * (i + 0.5f),
                    0f);
                seg.transform.localScale = new Vector3(0.16f, segHeight * 0.5f, 0.16f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, bend);
                seg.GetComponent<Renderer>().sharedMaterial = p.Trunk;

                if (i == 0)
                    GameObjectUtility.SetStaticEditorFlags(
                        seg, StaticEditorFlags.NavigationStatic);
                else
                    Object.DestroyImmediate(seg.GetComponent<CapsuleCollider>());
            }

            // Fronds: flattened stretched cubes drooping away from the crown.
            float topLean = Mathf.Sin(lean * Mathf.Deg2Rad) * height * 0.5f;
            var crown = new Vector3(topLean, height, 0f);

            int fronds = 6 + rng.Next(4);
            for (int i = 0; i < fronds; i++)
            {
                float angle = (i / (float)fronds) * 360f + (float)rng.NextDouble() * 20f;

                GameObject frond = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frond.name = $"Frond_{i}";
                frond.transform.SetParent(root.transform, false);

                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                float len = 2.5f + (float)rng.NextDouble() * 1.8f;

                frond.transform.localPosition = crown + dir * len * 0.5f + Vector3.up * 0.2f;
                frond.transform.localScale = new Vector3(0.5f, 0.06f, len);
                frond.transform.localRotation = Quaternion.Euler(
                    22f + (float)rng.NextDouble() * 18f, angle, 0f);

                frond.GetComponent<Renderer>().sharedMaterial =
                    rng.NextDouble() > 0.5 ? p.Canopy : p.CanopyLight;

                Object.DestroyImmediate(frond.GetComponent<BoxCollider>());
            }

            return root;
        }

        /// <summary>
        /// Undergrowth: ferns and bushes. These have no colliders — a forest floor you have
        /// to walk around is a forest floor you cannot fight in.
        /// </summary>
        private static GameObject MakeGroundCover(Palette p, System.Random rng)
        {
            var root = new GameObject("Cover");
            bool isFern = rng.NextDouble() > 0.45;

            if (isFern)
            {
                int leaves = 5 + rng.Next(5);
                for (int i = 0; i < leaves; i++)
                {
                    float angle = (i / (float)leaves) * 360f;

                    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leaf.name = $"Frond_{i}";
                    leaf.transform.SetParent(root.transform, false);

                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    float len = 0.9f + (float)rng.NextDouble() * 0.8f;

                    leaf.transform.localPosition = dir * len * 0.4f + Vector3.up * 0.35f;
                    leaf.transform.localScale = new Vector3(0.28f, 0.05f, len);
                    leaf.transform.localRotation = Quaternion.Euler(
                        -28f - (float)rng.NextDouble() * 20f, angle, 0f);

                    leaf.GetComponent<Renderer>().sharedMaterial = p.Fern;
                    Object.DestroyImmediate(leaf.GetComponent<BoxCollider>());
                }
            }
            else
            {
                int blobs = 2 + rng.Next(3);
                for (int i = 0; i < blobs; i++)
                {
                    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blob.name = $"Bush_{i}";
                    blob.transform.SetParent(root.transform, false);

                    blob.transform.localPosition = new Vector3(
                        ((float)rng.NextDouble() - 0.5f) * 1.4f,
                        0.4f + (float)rng.NextDouble() * 0.3f,
                        ((float)rng.NextDouble() - 0.5f) * 1.4f);

                    float r = 0.7f + (float)rng.NextDouble() * 0.8f;
                    blob.transform.localScale = new Vector3(r, r * 0.7f, r);
                    blob.GetComponent<Renderer>().sharedMaterial = p.Bush;

                    Object.DestroyImmediate(blob.GetComponent<SphereCollider>());
                }
            }

            return root;
        }

        /// <summary>A fallen trunk. Solid — this is cover you can actually crouch behind.</summary>
        private static GameObject MakeLog(Palette p, System.Random rng)
        {
            var root = new GameObject("Log");

            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.transform.SetParent(root.transform, false);

            float length = 4f + (float)rng.NextDouble() * 5f;
            float radius = 0.35f + (float)rng.NextDouble() * 0.3f;

            log.transform.localPosition = Vector3.up * radius;
            log.transform.localScale = new Vector3(radius, length * 0.5f, radius);
            // Lying down, with a slight roll so it does not read as machined.
            log.transform.localRotation = Quaternion.Euler(
                90f, (float)rng.NextDouble() * 180f, (float)rng.NextDouble() * 8f);

            log.GetComponent<Renderer>().sharedMaterial = p.Log;
            GameObjectUtility.SetStaticEditorFlags(log, StaticEditorFlags.NavigationStatic);

            return root;
        }
    }
}

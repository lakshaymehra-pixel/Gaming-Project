using System.Collections.Generic;
using Game.Core;
using Game.Enemies;
using Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the island map: a real terrain with a coastline, sea, scattered cover, and
    /// ruins to fight around. The player rig, enemy prefab, spawner, game loop, and HUD are
    /// reused from ArenaSceneBuilder â€” only the world changes, so the two maps cannot drift
    /// apart in how they play.
    ///
    /// Everything here is generated rather than modelled: no downloaded assets, nothing that
    /// eats disk, and the whole map is reproducible from a seed.
    ///
    /// Run from the menu: Game > Build Island Scene
    /// </summary>
    public static class IslandSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Island.unity";

        // Terrain shape. 400m across is large enough to feel open on foot without the
        // NavMesh bake or the heightmap becoming a problem on an 8 GB machine.
        private const float IslandSize = 400f;
        private const float MaxHeight = 45f;
        private const float SeaLevel = 3f;
        private const int HeightmapResolution = 257;
        private const int Seed = 20260711;

        /// <summary>Scales every foliage layer at once. Turn this down before touching the
        /// tree models if a phone drops frames.</summary>
        private const float JungleDensity = 1f;

        [MenuItem("Game/Build Island Scene")]
        public static void Build()
        {
            ArenaSceneBuilder.EnsureLayersAndTags();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var island = new IslandTerrain(
                IslandSize, MaxHeight, SeaLevel, HeightmapResolution, Seed);

            var mats = ArenaSceneBuilder.CreateMaterials();
            var islandMats = CreateIslandMaterials();

            BuildLighting();
            Terrain terrain = BuildTerrain(island, islandMats);
            BuildSea(island, islandMats);

            var rng = new System.Random(Seed);

            // Density is the whole point of a jungle. If a phone cannot hold frame rate,
            // this multiplier is the dial to turn â€” not the tree models.
            JungleFoliage.Grow(island, JungleFoliage.CreatePalette(), rng, JungleDensity);

            ScatterRocks(island, islandMats, rng);
            BuildRuins(island, islandMats, rng);
            BuildAmbience(island);

            Transform playerSpawn = PlacePlayerSpawn(island, rng);
            PlayerController player = ArenaSceneBuilder.BuildPlayer(playerSpawn, mats, out _);

            EnemyAI enemyPrefab = ArenaSceneBuilder.BuildEnemyPrefab(mats);
            Transform[] spawnPoints = PlaceEnemySpawns(island, playerSpawn.position, rng);

            WaveSpawner spawner = ArenaSceneBuilder.BuildSpawner(
                enemyPrefab, spawnPoints, player.transform);
            GameLoop loop = ArenaSceneBuilder.BuildGameLoop(player, spawner, playerSpawn);

            ArenaSceneBuilder.BuildHud(loop, player);

            // The terrain has to be flagged navigation-static before the bake, or the agents
            // get a NavMesh with a hole where the whole island should be.
            GameObjectUtility.SetStaticEditorFlags(
                terrain.gameObject, StaticEditorFlags.NavigationStatic);

            MarkFoliageStatic();

            ArenaSceneBuilder.BakeNavMesh();

            EditorSceneManager.SaveScene(scene, ScenePath);
            ArenaSceneBuilder.AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<b>Island built.</b> Saved to {ScenePath}. Press Play to test.");
        }

        /// <summary>
        /// Flags every piece of foliage as batching-static. A thousand trees is several
        /// thousand renderers, and issuing a draw call per leaf-blob is what would actually
        /// kill the frame rate on a phone — not the triangle count. Batching collapses them
        /// into a handful of calls per material.
        ///
        /// Run after the props exist and before the NavMesh bake, so the bake sees the final
        /// flags.
        /// </summary>
        private static void MarkFoliageStatic()
        {
            GameObject jungle = GameObject.Find("Jungle");
            if (jungle == null) return;

            int count = 0;

            foreach (Transform t in jungle.GetComponentsInChildren<Transform>())
            {
                if (t.GetComponent<Renderer>() == null) continue;

                // Preserve whatever navigation flag the prop already carries — trunks and logs
                // are navigation-static so agents path around them, leaves are not — and add
                // batching on top.
                StaticEditorFlags flags =
                    GameObjectUtility.GetStaticEditorFlags(t.gameObject);

                GameObjectUtility.SetStaticEditorFlags(
                    t.gameObject, flags | StaticEditorFlags.BatchingStatic);

                count++;
            }

            Debug.Log($"Jungle: marked {count} renderers batching-static.");
        }

        // --------------------------------------------------------------------- materials

        private class IslandMats
        {
            public Material Sea, Trunk, Leaves, Rock, Ruin;
        }

        private static IslandMats CreateIslandMaterials() => new IslandMats
        {
            Sea    = ArenaSceneBuilder.MakeMaterial("M_Sea",    new Color(0.13f, 0.42f, 0.58f)),
            Trunk  = ArenaSceneBuilder.MakeMaterial("M_Trunk",  new Color(0.30f, 0.22f, 0.15f)),
            Leaves = ArenaSceneBuilder.MakeMaterial("M_Leaves", new Color(0.20f, 0.42f, 0.18f)),
            Rock   = ArenaSceneBuilder.MakeMaterial("M_Rock",   new Color(0.42f, 0.42f, 0.44f)),
            Ruin   = ArenaSceneBuilder.MakeMaterial("M_Ruin",   new Color(0.58f, 0.55f, 0.50f)),
        };

        // ---------------------------------------------------------------------- lighting

        /// <summary>
        /// Rainforest light: a low, warm sun raking through the canopy, and ambient light
        /// that is green from above and near-black from below â€” under a closed canopy the
        /// only light that reaches you has already passed through leaves.
        ///
        /// The fog is doing most of the work. Sight lines in a jungle are short, and the haze
        /// is what makes the density read as depth rather than as clutter.
        /// </summary>
        private static void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.color = new Color(1f, 0.93f, 0.72f);          // warm, low, late-afternoon
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(26f, 132f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.34f, 0.45f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.27f, 0.18f);
            RenderSettings.ambientGroundColor  = new Color(0.07f, 0.09f, 0.06f);
            RenderSettings.ambientIntensity = 0.9f;

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.44f, 0.53f, 0.42f);   // green haze
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.012f;                          // ~80m visibility
        }

        /// <summary>
        /// The soundbed. Insects, birds, and wind are 2D and ride with the listener; the
        /// river is a positioned source that rolls off, so it tells you where the water is.
        /// </summary>
        private static void BuildAmbience(IslandTerrain island)
        {
            var root = new GameObject("Ambience");

            AudioSource insects = MakeAmbientSource(root.transform, "Insects", "AMB_Insects", 0.30f);
            AudioSource birds   = MakeAmbientSource(root.transform, "Birds",   "AMB_Birds",   0.22f);
            AudioSource wind    = MakeAmbientSource(root.transform, "Wind",    "AMB_Wind",    0.26f);

            // Water sits at the shoreline on one side of the island, spatialised, so walking
            // toward the coast brings it up.
            AudioSource water = MakeAmbientSource(root.transform, "Water", "AMB_Water", 0.55f);
            if (water != null)
            {
                water.spatialBlend = 1f;
                water.rolloffMode = AudioRolloffMode.Linear;
                water.minDistance = 8f;
                water.maxDistance = 70f;
                water.loop = true;
                water.playOnAwake = true;
                water.transform.position = new Vector3(
                    island.Size * 0.5f, island.SeaLevel + 1f, island.Size * 0.08f);
            }

            var roarGo = new GameObject("Roar");
            roarGo.transform.SetParent(root.transform, false);
            var roar = roarGo.AddComponent<AudioSource>();
            roar.playOnAwake = false;
            roar.spatialBlend = 1f;
            roar.rolloffMode = AudioRolloffMode.Linear;
            roar.minDistance = 15f;
            roar.maxDistance = 120f;
            roar.volume = 0.8f;

            var controller = root.AddComponent<AmbienceController>();
            ArenaSceneBuilder.SetPrivate(controller, "insects", insects);
            ArenaSceneBuilder.SetPrivate(controller, "birds", birds);
            ArenaSceneBuilder.SetPrivate(controller, "wind", wind);
            ArenaSceneBuilder.SetPrivate(controller, "roarSource", roar);

            var roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/AMB_Roar.wav");
            if (roarClip != null)
                ArenaSceneBuilder.SetPrivate(controller, "roarClip", roarClip);
            else
                Debug.LogWarning("Ambience: AMB_Roar not found. Run Game > Bake Jungle Ambience.");

            // The listener is the camera, which is buried in the player rig; the controller
            // needs it to place the roar relative to the player.
            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener != null)
                ArenaSceneBuilder.SetPrivate(controller, "listener", listener.transform);
        }

        private static AudioSource MakeAmbientSource(Transform parent, string name,
                                                     string clipName, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;      // AmbienceController starts them
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = volume;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{clipName}.wav");
            if (clip == null)
            {
                Debug.LogWarning(
                    $"Ambience: {clipName} not found. Run Game > Bake Jungle Ambience, " +
                    "then rebuild the island.");
                return source;
            }

            source.clip = clip;
            return source;
        }

        // ----------------------------------------------------------------------- terrain

        private static Terrain BuildTerrain(IslandTerrain island, IslandMats mats)
        {
            var data = new TerrainData
            {
                heightmapResolution = island.Resolution,
                size = new Vector3(island.Size, island.MaxHeight, island.Size),
            };

            data.SetHeights(0, 0, island.Heights);

            AssetDatabase.CreateAsset(data, "Assets/Settings/IslandTerrainData.asset");

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Island";
            go.transform.position = Vector3.zero;

            Terrain terrain = go.GetComponent<Terrain>();

            // Layer-splatting the sand/grass/rock bands would need textures on disk. A single
            // flat layer keeps the look deliberately plain and the repo asset-free â€” the
            // gameplay silhouette comes from the heightfield, not the texture.
            var layer = new TerrainLayer
            {
                diffuseTexture = MakeSolidTexture(new Color(0.38f, 0.45f, 0.28f)),
                tileSize = new Vector2(16f, 16f),
            };
            AssetDatabase.CreateAsset(layer, "Assets/Settings/IslandLayer.terrainlayer");
            data.terrainLayers = new[] { layer };

            // Long view distances on a 400m island, but capped so a mid-range phone is not
            // drawing every blade of the heightfield at full LOD.
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 200f;
            terrain.detailObjectDistance = 80f;
            terrain.treeDistance = 200f;

            return terrain;
        }

        private static Texture2D MakeSolidTexture(Color c)
        {
            var tex = new Texture2D(8, 8);
            var pixels = new Color[64];
            for (int i = 0; i < 64; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();

            AssetDatabase.CreateAsset(tex, "Assets/Settings/T_Ground.asset");
            return tex;
        }

        private static void BuildSea(IslandTerrain island, IslandMats mats)
        {
            // Oversized so the horizon stays water from every point on the island.
            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            sea.transform.position = new Vector3(
                island.Size * 0.5f, island.SeaLevel, island.Size * 0.5f);
            sea.transform.localScale = Vector3.one * (island.Size / 10f) * 3f;
            sea.GetComponent<Renderer>().sharedMaterial = mats.Sea;

            // A collider here would let bullets and NavMesh rays hit the sea surface, so the
            // plane is visual only. Drowning is not a mechanic yet.
            Object.DestroyImmediate(sea.GetComponent<MeshCollider>());
        }

        // ------------------------------------------------------------------------- props

        private static void ScatterRocks(IslandTerrain island, IslandMats mats,
                                         System.Random rng)
        {
            var root = new GameObject("Rocks").transform;
            const int count = 70;

            for (int i = 0; i < count; i++)
            {
                // Rocks are allowed on the shoreline and on steeper ground than trees.
                if (!island.TryFindFlatLand(rng, island.SeaLevel + 0.5f, 40f, 8f, 20,
                                            out Vector3 p))
                    continue;

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"Rock_{i}";
                rock.transform.SetParent(root, false);

                float s = 1.2f + (float)rng.NextDouble() * 3.5f;
                rock.transform.position = p + Vector3.up * (s * 0.25f);
                rock.transform.localScale = new Vector3(
                    s, s * (0.5f + (float)rng.NextDouble() * 0.5f), s * 0.9f);

                // Tilted off-axis so a box reads as a boulder rather than a crate.
                rock.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 25f - 12f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 25f - 12f);

                rock.GetComponent<Renderer>().sharedMaterial = mats.Rock;
                GameObjectUtility.SetStaticEditorFlags(
                    rock, StaticEditorFlags.NavigationStatic);
            }
        }

        /// <summary>
        /// Roofless walled compounds. These are the fights worth having â€” somewhere to break
        /// line of sight, corners to hold, and a reason to move rather than snipe from a hill.
        /// </summary>
        private static void BuildRuins(IslandTerrain island, IslandMats mats,
                                       System.Random rng)
        {
            var root = new GameObject("Ruins").transform;
            const int compounds = 5;

            for (int i = 0; i < compounds; i++)
            {
                if (!island.TryFindFlatLand(rng, island.SeaLevel + 3f, 12f, 45f, 60,
                                            out Vector3 centre))
                    continue;

                var compound = new GameObject($"Ruin_{i}").transform;
                compound.SetParent(root, false);
                compound.position = centre;
                compound.rotation = Quaternion.Euler(
                    0f, (float)rng.NextDouble() * 360f, 0f);

                float w = 10f + (float)rng.NextDouble() * 8f;
                float d = 10f + (float)rng.NextDouble() * 8f;
                const float h = 3.5f;
                const float t = 0.6f;

                // Four walls, each with a gap punched in it, so a compound is enterable from
                // every side and never becomes a safe box.
                MakeWallWithGap(compound, mats, new Vector3(0f, h / 2f, d / 2f),
                                new Vector3(w, h, t), rng, horizontal: true);
                MakeWallWithGap(compound, mats, new Vector3(0f, h / 2f, -d / 2f),
                                new Vector3(w, h, t), rng, horizontal: true);
                MakeWallWithGap(compound, mats, new Vector3(w / 2f, h / 2f, 0f),
                                new Vector3(t, h, d), rng, horizontal: false);
                MakeWallWithGap(compound, mats, new Vector3(-w / 2f, h / 2f, 0f),
                                new Vector3(t, h, d), rng, horizontal: false);

                // A block or two inside for cover from someone already through the door.
                int inner = 1 + rng.Next(2);
                for (int k = 0; k < inner; k++)
                {
                    GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = $"Block_{k}";
                    block.transform.SetParent(compound, false);
                    block.transform.localPosition = new Vector3(
                        ((float)rng.NextDouble() - 0.5f) * (w - 4f),
                        1f,
                        ((float)rng.NextDouble() - 0.5f) * (d - 4f));
                    block.transform.localScale = new Vector3(
                        1.5f + (float)rng.NextDouble() * 2f, 2f,
                        1.5f + (float)rng.NextDouble() * 2f);
                    block.GetComponent<Renderer>().sharedMaterial = mats.Ruin;
                    GameObjectUtility.SetStaticEditorFlags(
                        block, StaticEditorFlags.NavigationStatic);
                }
            }
        }

        private static void MakeWallWithGap(Transform parent, IslandMats mats, Vector3 centre,
                                            Vector3 size, System.Random rng, bool horizontal)
        {
            float span = horizontal ? size.x : size.z;
            float gap = 3f;                                    // wide enough to run through
            float gapCentre = ((float)rng.NextDouble() - 0.5f) * (span - gap - 2f);

            float leftEnd = -span / 2f;
            float rightEnd = span / 2f;
            float gapLeft = gapCentre - gap / 2f;
            float gapRight = gapCentre + gap / 2f;

            MakeSegment(parent, mats, centre, size, horizontal, leftEnd, gapLeft, "A");
            MakeSegment(parent, mats, centre, size, horizontal, gapRight, rightEnd, "B");
        }

        private static void MakeSegment(Transform parent, IslandMats mats, Vector3 centre,
                                        Vector3 size, bool horizontal, float from, float to,
                                        string suffix)
        {
            float length = to - from;
            if (length <= 0.3f) return;                        // gap ate the whole segment

            float offset = (from + to) * 0.5f;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"Wall_{suffix}";
            seg.transform.SetParent(parent, false);

            seg.transform.localPosition = horizontal
                ? centre + new Vector3(offset, 0f, 0f)
                : centre + new Vector3(0f, 0f, offset);

            seg.transform.localScale = horizontal
                ? new Vector3(length, size.y, size.z)
                : new Vector3(size.x, size.y, length);

            seg.GetComponent<Renderer>().sharedMaterial = mats.Ruin;
            GameObjectUtility.SetStaticEditorFlags(seg, StaticEditorFlags.NavigationStatic);
        }

        // ------------------------------------------------------------------------ spawns

        private static Transform PlacePlayerSpawn(IslandTerrain island, System.Random rng)
        {
            var go = new GameObject("PlayerSpawn");

            if (island.TryFindFlatLand(rng, island.SeaLevel + 4f, 10f, 60f, 200,
                                       out Vector3 p))
            {
                go.transform.position = p + Vector3.up * 1.2f;
            }
            else
            {
                // The generator should always find somewhere on a 400m island, but a spawn in
                // the sea is unrecoverable, so fall back to the highest point of the centre.
                float cx = island.Size * 0.5f, cz = island.Size * 0.5f;
                go.transform.position = new Vector3(cx, island.HeightAt(cx, cz) + 1.2f, cz);
                Debug.LogWarning("Island: no flat spawn found; using the centre.");
            }

            // Face the middle of the island, so the player opens looking at the map rather
            // than out to sea.
            Vector3 toCentre = new Vector3(island.Size * 0.5f, 0f, island.Size * 0.5f)
                             - go.transform.position;
            toCentre.y = 0f;
            if (toCentre.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(toCentre);

            return go.transform;
        }

        private static Transform[] PlaceEnemySpawns(IslandTerrain island, Vector3 playerSpawn,
                                                    System.Random rng)
        {
            var root = new GameObject("EnemySpawns").transform;
            var points = new List<Transform>();

            const int wanted = 10;
            const float minDistanceFromPlayer = 60f;

            int guard = 0;
            while (points.Count < wanted && guard++ < 400)
            {
                if (!island.TryFindFlatLand(rng, island.SeaLevel + 2f, 22f, 25f, 20,
                                            out Vector3 p))
                    continue;

                // Nothing may spawn on top of the player.
                if (Vector3.Distance(p, playerSpawn) < minDistanceFromPlayer) continue;

                var go = new GameObject($"Spawn_{points.Count}");
                go.transform.SetParent(root, false);
                go.transform.position = p + Vector3.up * 0.5f;
                points.Add(go.transform);
            }

            if (points.Count == 0)
            {
                Debug.LogError("Island: could not place any enemy spawns.");
            }
            else if (points.Count < wanted)
            {
                Debug.LogWarning(
                    $"Island: placed {points.Count}/{wanted} enemy spawns â€” the coastline " +
                    "may be tighter than the spawn constraints allow.");
            }

            return points.ToArray();
        }
    }
}

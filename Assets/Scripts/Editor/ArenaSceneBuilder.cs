using System.Collections.Generic;
using Game.Core;
using Game.Enemies;
using Game.Player;
using Game.UI;
using Game.Weapons;
using TMPro;
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the whole playable arena from code: geometry, player rig, enemy prefab,
    /// spawner, HUD, and a baked NavMesh. Wiring this by hand in the Inspector is dozens of
    /// error-prone drag-and-drops; generating it means the scene can be rebuilt from source
    /// at any time and the repo stays diffable.
    ///
    /// Run from the menu: Game > Build Arena Scene
    /// </summary>
    public static class ArenaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Arena.unity";
        private const string PrefabDir = "Assets/Prefabs";
        private const string MaterialDir = "Assets/Materials";
        private const string SettingsDir = "Assets/Settings";

        private const int PlayerLayer = 8;
        private const int EnemyLayer = 9;

        [MenuItem("Game/Build Arena Scene")]
        public static void Build()
        {
            EnsureLayersAndTags();

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mats = CreateMaterials();

            BuildLighting();
            BuildArenaGeometry(mats);

            Transform playerSpawn = CreatePlayerSpawn();
            GameObject weaponDataOwner;
            PlayerController player = BuildPlayer(playerSpawn, mats, out weaponDataOwner);

            EnemyAI enemyPrefab = BuildEnemyPrefab(mats);
            Transform[] spawnPoints = CreateEnemySpawnPoints();

            WaveSpawner spawner = BuildSpawner(enemyPrefab, spawnPoints, player.transform);
            GameLoop loop = BuildGameLoop(player, spawner, playerSpawn);

            BuildHud(loop, player);

            BakeNavMesh();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<b>Arena built.</b> Saved to {ScenePath}. Press Play to test " +
                      "(WASD + mouse; click in the Game view first to lock the cursor).");
        }

        // ------------------------------------------------------------------ layers, tags

        private static void EnsureLayersAndTags()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayer(layers, PlayerLayer, "Player");
            SetLayer(layers, EnemyLayer, "Enemy");

            SerializedProperty tags = tagManager.FindProperty("tags");
            AddTag(tags, "Head");

            tagManager.ApplyModifiedProperties();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (layer.stringValue != name) layer.stringValue = name;
        }

        private static void AddTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }

        // ---------------------------------------------------------------------- materials

        private class Mats
        {
            public Material Floor, Wall, Crate, Player, Enemy, EnemyHead, Gun, Tracer;
        }

        private static Mats CreateMaterials()
        {
            EnsureFolder(MaterialDir);

            return new Mats
            {
                Floor     = MakeMaterial("M_Floor",     new Color(0.22f, 0.24f, 0.27f)),
                Wall      = MakeMaterial("M_Wall",      new Color(0.32f, 0.34f, 0.38f)),
                Crate     = MakeMaterial("M_Crate",     new Color(0.45f, 0.36f, 0.24f)),
                Player    = MakeMaterial("M_Player",    new Color(0.20f, 0.55f, 0.85f)),
                Enemy     = MakeMaterial("M_Enemy",     new Color(0.75f, 0.20f, 0.18f)),
                EnemyHead = MakeMaterial("M_EnemyHead", new Color(0.90f, 0.35f, 0.30f)),
                Gun       = MakeMaterial("M_Gun",       new Color(0.15f, 0.15f, 0.17f)),
                Tracer    = MakeTracerMaterial(),
            };
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP if present, built-in otherwise — the project template decides which is live.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            var mat = new Material(shader) { name = name };
            mat.SetColor(mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material MakeTracerMaterial()
        {
            string path = $"{MaterialDir}/M_Tracer.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");

            var mat = new Material(shader) { name = "M_Tracer" };
            var hot = new Color(1f, 0.85f, 0.35f);
            mat.SetColor(mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", hot);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ----------------------------------------------------------------------- lighting

        private static void BuildLighting()
        {
            var sunGo = new GameObject("Directional Light");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.42f, 0.46f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.31f, 0.34f);
            RenderSettings.ambientGroundColor  = new Color(0.16f, 0.16f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.35f, 0.38f, 0.44f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 130f;
        }

        // ----------------------------------------------------------------------- geometry

        private static void BuildArenaGeometry(Mats mats)
        {
            var root = new GameObject("Arena").transform;

            const float half = 30f;
            const float wallH = 6f;

            MakeBox(root, "Floor", Vector3.zero,
                    new Vector3(half * 2f, 1f, half * 2f), mats.Floor, yOffset: -0.5f);

            MakeBox(root, "Wall_N", new Vector3(0f, wallH / 2f, half),
                    new Vector3(half * 2f, wallH, 1f), mats.Wall);
            MakeBox(root, "Wall_S", new Vector3(0f, wallH / 2f, -half),
                    new Vector3(half * 2f, wallH, 1f), mats.Wall);
            MakeBox(root, "Wall_E", new Vector3(half, wallH / 2f, 0f),
                    new Vector3(1f, wallH, half * 2f), mats.Wall);
            MakeBox(root, "Wall_W", new Vector3(-half, wallH / 2f, 0f),
                    new Vector3(1f, wallH, half * 2f), mats.Wall);

            // Cover, laid out asymmetrically so no two firing lanes feel the same.
            var cover = new (Vector3 pos, Vector3 size)[]
            {
                (new Vector3(-12f, 1f,  8f),  new Vector3(6f, 2f, 2f)),
                (new Vector3( 10f, 1.5f, 14f), new Vector3(2f, 3f, 8f)),
                (new Vector3(  0f, 1f,  0f),  new Vector3(8f, 2f, 8f)),
                (new Vector3(-18f, 1.5f, -12f), new Vector3(4f, 3f, 4f)),
                (new Vector3( 16f, 1f, -6f),  new Vector3(2f, 2f, 10f)),
                (new Vector3(  6f, 1.5f, -18f), new Vector3(10f, 3f, 2f)),
                (new Vector3(-6f, 1f, 20f),   new Vector3(3f, 2f, 3f)),
                (new Vector3( 22f, 1f,  20f), new Vector3(5f, 2f, 5f)),
                (new Vector3(-22f, 1f,  22f), new Vector3(4f, 2f, 6f)),
            };

            for (int i = 0; i < cover.Length; i++)
                MakeBox(root, $"Cover_{i}", cover[i].pos, cover[i].size, mats.Crate);
        }

        private static GameObject MakeBox(Transform parent, string name, Vector3 pos,
                                          Vector3 size, Material mat, float yOffset = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * yOffset;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Everything in the arena is walkable-around geometry the NavMesh must respect.
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
            return go;
        }

        // ------------------------------------------------------------------------- player

        private static Transform CreatePlayerSpawn()
        {
            var go = new GameObject("PlayerSpawn");
            go.transform.position = new Vector3(0f, 1.1f, -24f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            return go.transform;
        }

        private static PlayerController BuildPlayer(Transform spawn, Mats mats,
                                                    out GameObject weaponOwner)
        {
            var root = new GameObject("Player") { layer = PlayerLayer };
            root.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;

            var health = root.AddComponent<Health>();
            SetPrivate(health, "maxHealth", 100f);
            SetPrivate(health, "regenerates", true);
            SetPrivate(health, "regenPerSecond", 18f);
            SetPrivate(health, "regenDelayAfterHit", 4f);

            // Camera rig: pivot handles pitch, recoil holder sits under it so the kick is a
            // pure visual offset that never fights the aim.
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.62f, 0f);

            var recoilHolder = new GameObject("RecoilHolder").transform;
            recoilHolder.SetParent(pivot, false);
            var recoil = recoilHolder.gameObject.AddComponent<RecoilController>();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(recoilHolder, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();

            var gunAudio = camGo.AddComponent<AudioSource>();
            gunAudio.playOnAwake = false;
            gunAudio.spatialBlend = 0f;

            // Viewmodel: a simple block gun so there is something on screen to read recoil from.
            GameObject gunModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gunModel.name = "GunModel";
            gunModel.transform.SetParent(camGo.transform, false);
            gunModel.transform.localPosition = new Vector3(0.28f, -0.22f, 0.55f);
            gunModel.transform.localScale = new Vector3(0.1f, 0.12f, 0.5f);
            gunModel.GetComponent<Renderer>().sharedMaterial = mats.Gun;
            Object.DestroyImmediate(gunModel.GetComponent<BoxCollider>());

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(camGo.transform, false);
            muzzle.localPosition = new Vector3(0.28f, -0.19f, 0.85f);

            TracerPool tracers = BuildTracerPool(mats);

            WeaponData data = CreateWeaponData();
            var weapon = camGo.AddComponent<Weapon>();
            SetPrivate(weapon, "data", data);
            SetPrivate(weapon, "shootCamera", cam);
            SetPrivate(weapon, "muzzle", muzzle);
            SetPrivate(weapon, "audioSource", gunAudio);
            SetPrivate(weapon, "tracerPool", tracers);
            SetPrivate(weapon, "headColliderTag", "Head");
            // Bullets must not collide with the shooter's own capsule.
            SetPrivate(weapon, "hitMask", (LayerMask)(~(1 << PlayerLayer)));

            root.AddComponent<PlayerInputHub>();

            var motor = root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();
            SetPrivate(look, "cameraPivot", pivot);

            var controller = root.AddComponent<PlayerController>();
            SetPrivate(controller, "weapon", weapon);
            SetPrivate(controller, "recoil", recoil);
            SetPrivate(controller, "viewCamera", cam);

            weaponOwner = camGo;
            return controller;
        }

        private static TracerPool BuildTracerPool(Mats mats)
        {
            EnsureFolder(PrefabDir);

            string path = $"{PrefabDir}/Tracer.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                var temp = new GameObject("Tracer");
                var lr = temp.AddComponent<LineRenderer>();
                lr.sharedMaterial = mats.Tracer;
                lr.widthMultiplier = 0.035f;
                lr.numCapVertices = 0;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.positionCount = 2;

                prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                Object.DestroyImmediate(temp);
            }

            var poolGo = new GameObject("TracerPool");
            var pool = poolGo.AddComponent<TracerPool>();
            SetPrivate(pool, "tracerPrefab", prefab.GetComponent<LineRenderer>());
            SetPrivate(pool, "poolSize", 24);
            SetPrivate(pool, "lifetime", 0.05f);
            return pool;
        }

        private static WeaponData CreateWeaponData()
        {
            EnsureFolder(SettingsDir);

            string path = $"{SettingsDir}/Rifle.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing != null) return existing;

            var data = ScriptableObject.CreateInstance<WeaponData>();
            data.displayName = "AR-15";
            data.fireMode = FireMode.Auto;
            data.fireRate = 620f;
            data.damage = 24f;
            data.headshotMultiplier = 2.2f;
            data.range = 120f;
            data.hipSpread = 2.4f;
            data.aimSpread = 0.5f;
            data.spreadPerShot = 0.32f;
            data.maxBloom = 5f;
            data.spreadRecovery = 6.5f;
            data.recoilVertical = 1.15f;
            data.recoilHorizontal = 0.4f;
            data.magazineSize = 30;
            data.reserveAmmo = 240;
            data.reloadSeconds = 1.9f;

            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        // -------------------------------------------------------------------------- enemy

        private static EnemyAI BuildEnemyPrefab(Mats mats)
        {
            EnsureFolder(PrefabDir);
            string path = $"{PrefabDir}/Enemy.prefab";

            var root = new GameObject("Enemy") { layer = EnemyLayer };

            // Body: a capsule with its collider kept, so bullets have something to strike.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.layer = EnemyLayer;
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = mats.Enemy;

            // Head: separate collider tagged so Weapon can score a headshot multiplier.
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.tag = "Head";
            head.layer = EnemyLayer;
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            head.transform.localScale = Vector3.one * 0.5f;
            head.GetComponent<Renderer>().sharedMaterial = mats.EnemyHead;

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root.transform, false);
            eyes.localPosition = new Vector3(0f, 1.8f, 0.3f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 3.6f;
            agent.angularSpeed = 320f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.5f;
            agent.radius = 0.4f;
            agent.height = 2f;

            var health = root.AddComponent<Health>();
            SetPrivate(health, "maxHealth", 100f);
            SetPrivate(health, "regenerates", false);

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.maxDistance = 45f;
            audio.rolloffMode = AudioRolloffMode.Linear;

            var ai = root.AddComponent<EnemyAI>();
            SetPrivate(ai, "eyes", eyes);
            SetPrivate(ai, "sightRange", 45f);
            SetPrivate(ai, "attackRange", 18f);
            SetPrivate(ai, "damage", 9f);
            SetPrivate(ai, "secondsBetweenShots", 0.95f);
            SetPrivate(ai, "aimSpreadDegrees", 3.5f);
            SetPrivate(ai, "audioSource", audio);
            // An enemy's shot must not be blocked by its own body, or by a squadmate's.
            SetPrivate(ai, "sightBlockers", (LayerMask)(~(1 << EnemyLayer)));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return prefab.GetComponent<EnemyAI>();
        }

        private static Transform[] CreateEnemySpawnPoints()
        {
            var root = new GameObject("EnemySpawns").transform;

            var positions = new[]
            {
                new Vector3(-25f, 1f,  25f),
                new Vector3( 25f, 1f,  25f),
                new Vector3(-25f, 1f,   0f),
                new Vector3( 25f, 1f,   0f),
                new Vector3(  0f, 1f,  26f),
                new Vector3(-25f, 1f, -20f),
                new Vector3( 25f, 1f, -20f),
                new Vector3( 12f, 1f,  26f),
            };

            var points = new List<Transform>();
            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"Spawn_{i}");
                go.transform.SetParent(root, false);
                go.transform.position = positions[i];
                points.Add(go.transform);
            }

            return points.ToArray();
        }

        // ---------------------------------------------------------------- spawner + loop

        private static WaveSpawner BuildSpawner(EnemyAI prefab, Transform[] points,
                                                Transform target)
        {
            var go = new GameObject("WaveSpawner");
            var spawner = go.AddComponent<WaveSpawner>();

            SetPrivate(spawner, "enemyPrefab", prefab);
            SetPrivate(spawner, "spawnPoints", points);
            SetPrivate(spawner, "target", target);
            SetPrivate(spawner, "enemiesInFirstWave", 4);
            SetPrivate(spawner, "extraEnemiesPerWave", 2);
            SetPrivate(spawner, "maxAliveAtOnce", 8);
            SetPrivate(spawner, "secondsBetweenSpawns", 0.8f);
            SetPrivate(spawner, "secondsBetweenWaves", 5f);

            return spawner;
        }

        private static GameLoop BuildGameLoop(PlayerController player, WaveSpawner spawner,
                                              Transform playerSpawn)
        {
            var go = new GameObject("GameLoop");
            var loop = go.AddComponent<GameLoop>();

            SetPrivate(loop, "player", player);
            SetPrivate(loop, "spawner", spawner);
            SetPrivate(loop, "playerSpawnPoint", playerSpawn);
            SetPrivate(loop, "respawnOnDeath", true);
            SetPrivate(loop, "respawnDelay", 3f);
            SetPrivate(loop, "pointsPerKill", 100);

            return loop;
        }

        // ---------------------------------------------------------------------------- HUD

        private static void BuildHud(GameLoop loop, PlayerController player)
        {
            var canvasGo = new GameObject("HUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            Transform c = canvasGo.transform;

            RectTransform crosshair = MakeImage(c, "Crosshair", new Color(1f, 1f, 1f, 0.8f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));

            CanvasGroup hitmarker = MakeGroupImage(c, "Hitmarker", new Color(1f, 0.3f, 0.2f, 0.9f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));

            CanvasGroup vignette = MakeGroupImage(c, "DamageVignette", new Color(0.8f, 0f, 0f, 0.35f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
            vignette.blocksRaycasts = false;
            vignette.interactable = false;

            // Health, bottom-left
            MakeImage(c, "HealthBg", new Color(0f, 0f, 0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(220f, 60f), new Vector2(320f, 26f));
            RectTransform healthFillRt = MakeImage(c, "HealthFill", new Color(0.2f, 0.85f, 0.35f, 0.95f),
                new Vector2(0f, 0f), new Vector2(220f, 60f), new Vector2(320f, 26f));
            var healthFill = healthFillRt.GetComponent<Image>();
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            TMP_Text healthLabel = MakeText(c, "HealthLabel", "100", 30,
                new Vector2(0f, 0f), new Vector2(220f, 100f), TextAlignmentOptions.Left);

            // Ammo, bottom-right
            TMP_Text ammoLabel = MakeText(c, "AmmoLabel", "30 / 240", 44,
                new Vector2(1f, 0f), new Vector2(-220f, 70f), TextAlignmentOptions.Right);

            TMP_Text reloadText = MakeText(c, "ReloadPrompt", "RELOAD", 34,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), TextAlignmentOptions.Center);
            reloadText.color = new Color(1f, 0.75f, 0.2f);

            // Match state, top
            TMP_Text waveLabel  = MakeText(c, "WaveLabel", "WAVE 1", 40,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), TextAlignmentOptions.Center);
            TMP_Text killsLabel = MakeText(c, "KillsLabel", "0", 34,
                new Vector2(0f, 1f), new Vector2(160f, -60f), TextAlignmentOptions.Left);
            TMP_Text scoreLabel = MakeText(c, "ScoreLabel", "0", 34,
                new Vector2(1f, 1f), new Vector2(-160f, -60f), TextAlignmentOptions.Right);

            // Touch controls
            VirtualJoystick joystick = BuildJoystick(c);
            TouchLookArea lookArea = BuildLookArea(c);
            HoldButton fire   = BuildHoldButton(c, "FireButton",   "FIRE",   new Vector2(1f, 0f), new Vector2(-190f, 190f), 150f);
            HoldButton aim    = BuildHoldButton(c, "AimButton",    "AIM",    new Vector2(1f, 0f), new Vector2(-370f, 300f), 110f);
            HoldButton reload = BuildHoldButton(c, "ReloadButton", "RELOAD", new Vector2(1f, 0f), new Vector2(-190f, 380f), 110f);

            // Game over
            var gameOverGo = new GameObject("GameOverPanel", typeof(Image));
            gameOverGo.transform.SetParent(c, false);
            var goRt = gameOverGo.GetComponent<RectTransform>();
            Stretch(goRt);
            gameOverGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            MakeText(gameOverGo.transform, "GameOverTitle", "YOU DIED", 80,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), TextAlignmentOptions.Center);
            TMP_Text stats = MakeText(gameOverGo.transform, "GameOverStats", "", 40,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), TextAlignmentOptions.Center);

            gameOverGo.SetActive(false);

            var hud = canvasGo.AddComponent<HudController>();
            SetPrivate(hud, "game", loop);
            SetPrivate(hud, "player", player);
            SetPrivate(hud, "healthFill", healthFill);
            SetPrivate(hud, "healthLabel", healthLabel);
            SetPrivate(hud, "damageVignette", vignette);
            SetPrivate(hud, "ammoLabel", ammoLabel);
            SetPrivate(hud, "reloadPrompt", reloadText.gameObject);
            SetPrivate(hud, "killsLabel", killsLabel);
            SetPrivate(hud, "waveLabel", waveLabel);
            SetPrivate(hud, "scoreLabel", scoreLabel);
            SetPrivate(hud, "crosshair", crosshair);
            SetPrivate(hud, "hitmarker", hitmarker);
            SetPrivate(hud, "gameOverPanel", gameOverGo);
            SetPrivate(hud, "gameOverStats", stats);

            var input = player.GetComponent<PlayerInputHub>();
            SetPrivate(input, "moveJoystick", joystick);
            SetPrivate(input, "lookArea", lookArea);
            SetPrivate(input, "fireButton", fire);
            SetPrivate(input, "aimButton", aim);
            SetPrivate(input, "reloadButton", reload);

            // The look area must sit behind every button, or it swallows their touches.
            lookArea.transform.SetAsFirstSibling();
            vignette.transform.SetAsFirstSibling();
        }

        private static VirtualJoystick BuildJoystick(Transform parent)
        {
            var area = new GameObject("MoveJoystick", typeof(RectTransform), typeof(Image));
            area.transform.SetParent(parent, false);
            var areaRt = area.GetComponent<RectTransform>();
            areaRt.anchorMin = areaRt.anchorMax = new Vector2(0f, 0f);
            areaRt.pivot = new Vector2(0.5f, 0.5f);
            areaRt.anchoredPosition = new Vector2(260f, 260f);
            areaRt.sizeDelta = new Vector2(420f, 420f);
            var areaImg = area.GetComponent<Image>();
            areaImg.color = new Color(1f, 1f, 1f, 0f); // invisible but raycastable

            RectTransform bg = MakeImage(area.transform, "Base", new Color(1f, 1f, 1f, 0.18f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
            RectTransform knob = MakeImage(bg, "Knob", new Color(1f, 1f, 1f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));

            var stick = area.AddComponent<VirtualJoystick>();
            SetPrivate(stick, "background", bg);
            SetPrivate(stick, "knob", knob);
            SetPrivate(stick, "deadZone", 0.12f);
            SetPrivate(stick, "dynamicOrigin", true);

            return stick;
        }

        private static TouchLookArea BuildLookArea(Transform parent)
        {
            var go = new GameObject("LookArea", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            // Right half of the screen.
            rt.anchorMin = new Vector2(0.45f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            return go.AddComponent<TouchLookArea>();
        }

        private static HoldButton BuildHoldButton(Transform parent, string name, string label,
                                                  Vector2 anchor, Vector2 pos, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            MakeText(go.transform, "Label", label, 24,
                new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center);

            return go.AddComponent<HoldButton>();
        }

        // ---------------------------------------------------------------- UI primitives

        private static RectTransform MakeImage(Transform parent, string name, Color color,
                                               Vector2 anchor, Vector2 pos, Vector2 size,
                                               bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                Stretch(rt);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
            return rt;
        }

        private static CanvasGroup MakeGroupImage(Transform parent, string name, Color color,
                                                  Vector2 anchor, Vector2 pos, Vector2 size,
                                                  bool stretch = false)
        {
            RectTransform rt = MakeImage(parent, name, color, anchor, pos, size, stretch);
            return rt.gameObject.AddComponent<CanvasGroup>();
        }

        private static TMP_Text MakeText(Transform parent, string name, string content,
                                         float fontSize, Vector2 anchor, Vector2 pos,
                                         TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(400f, 80f);

            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------------ navmesh

        private static void BakeNavMesh()
        {
            // Qualified because UnityEngine.AI declares a NavMeshBuilder too, and the file
            // imports both namespaces. The editor one is the baker; the runtime one builds
            // NavMeshData at play time and cannot write the scene's baked surface.
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        }

        // -------------------------------------------------------------------------- utils

        /// <summary>
        /// Writes a [SerializeField] private backing field. The gameplay scripts keep their
        /// fields private on purpose; this is the editor-only escape hatch that lets the
        /// builder wire them without widening the runtime API.
        /// </summary>
        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);

            if (prop == null)
            {
                Debug.LogError($"Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            switch (value)
            {
                case bool b:        prop.boolValue = b; break;
                case int i:         prop.intValue = i; break;
                case float f:       prop.floatValue = f; break;
                case string s:      prop.stringValue = s; break;
                case LayerMask lm:  prop.intValue = lm.value; break;
                case Object o:      prop.objectReferenceValue = o; break;
                case Transform[] arr:
                    prop.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++)
                        prop.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
                default:
                    Debug.LogError($"SetPrivate: unhandled type {value?.GetType()}");
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

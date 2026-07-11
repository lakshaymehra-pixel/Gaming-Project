using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the island's heightfield. Kept apart from the scene builder because the
    /// shape is the one thing worth tuning on its own — spawn placement, NavMesh baking,
    /// and prop scattering all query it, so they need a single source of truth for "how
    /// high is the ground here, and is it land".
    ///
    /// The profile is a radial falloff (a disc of land ringed by sea) with layered value
    /// noise on top, so the coastline is ragged rather than a perfect circle and the
    /// interior has ridges to fight over.
    /// </summary>
    public class IslandTerrain
    {
        public readonly float Size;          // world units across, square
        public readonly float MaxHeight;
        public readonly float SeaLevel;

        private readonly int _resolution;
        private readonly float[,] _heights;  // [z, x] normalized 0..1, matching Unity's layout
        private readonly int _seed;

        public IslandTerrain(float size, float maxHeight, float seaLevel, int resolution, int seed)
        {
            Size = size;
            MaxHeight = maxHeight;
            SeaLevel = seaLevel;
            _resolution = resolution;
            _seed = seed;
            _heights = new float[resolution, resolution];

            Generate();
        }

        public float[,] Heights => _heights;
        public int Resolution => _resolution;

        private void Generate()
        {
            // Offsets derived from the seed keep every octave sampling a different region of
            // the noise field; without them the octaves would all peak in the same places.
            var rng = new System.Random(_seed);
            float o1x = (float)rng.NextDouble() * 1000f, o1z = (float)rng.NextDouble() * 1000f;
            float o2x = (float)rng.NextDouble() * 1000f, o2z = (float)rng.NextDouble() * 1000f;
            float o3x = (float)rng.NextDouble() * 1000f, o3z = (float)rng.NextDouble() * 1000f;

            for (int z = 0; z < _resolution; z++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    float u = x / (float)(_resolution - 1);
                    float v = z / (float)(_resolution - 1);

                    float ridges =
                        Mathf.PerlinNoise(o1x + u * 3.5f,  o1z + v * 3.5f)  * 0.55f +
                        Mathf.PerlinNoise(o2x + u * 8f,    o2z + v * 8f)    * 0.30f +
                        Mathf.PerlinNoise(o3x + u * 18f,   o3z + v * 18f)   * 0.15f;

                    _heights[z, x] = Mathf.Clamp01(ridges * Falloff(u, v));
                }
            }
        }

        /// <summary>
        /// 1 in the interior, easing to 0 at the edges. The noise term wobbles the radius so
        /// the shoreline reads as coast rather than as a drawn circle.
        /// </summary>
        private float Falloff(float u, float v)
        {
            float dx = (u - 0.5f) * 2f;
            float dz = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(dx * dx + dz * dz);

            float wobble = (Mathf.PerlinNoise(u * 4f + _seed, v * 4f + _seed) - 0.5f) * 0.22f;
            r += wobble;

            // Flat top out to 0.45, then a smooth roll into the water by 0.95.
            return 1f - Mathf.SmoothStep(0.45f, 0.95f, r);
        }

        /// <summary>World-space ground height under a point, sampled bilinearly.</summary>
        public float HeightAt(float worldX, float worldZ)
        {
            float u = Mathf.Clamp01(worldX / Size);
            float v = Mathf.Clamp01(worldZ / Size);

            float fx = u * (_resolution - 1);
            float fz = v * (_resolution - 1);

            int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
            int x1 = Mathf.Min(x0 + 1, _resolution - 1);
            int z1 = Mathf.Min(z0 + 1, _resolution - 1);

            float tx = fx - x0, tz = fz - z0;

            float h = Mathf.Lerp(
                Mathf.Lerp(_heights[z0, x0], _heights[z0, x1], tx),
                Mathf.Lerp(_heights[z1, x0], _heights[z1, x1], tx),
                tz);

            return h * MaxHeight;
        }

        public bool IsLand(float worldX, float worldZ) =>
            HeightAt(worldX, worldZ) > SeaLevel + 0.5f;

        /// <summary>Ground steepness in degrees. Props and spawns avoid cliffs.</summary>
        public float SlopeAt(float worldX, float worldZ)
        {
            const float e = 2f;
            float hL = HeightAt(worldX - e, worldZ), hR = HeightAt(worldX + e, worldZ);
            float hD = HeightAt(worldX, worldZ - e), hU = HeightAt(worldX, worldZ + e);

            var normal = new Vector3((hL - hR) / (2f * e), 1f, (hD - hU) / (2f * e)).normalized;
            return Vector3.Angle(normal, Vector3.up);
        }

        /// <summary>
        /// Finds a spot that is dry, walkable, and above the tideline. Returns false if it
        /// cannot find one, rather than dropping the caller's object into the sea.
        /// </summary>
        public bool TryFindFlatLand(System.Random rng, float minHeight, float maxSlope,
                                    float margin, int attempts, out Vector3 point)
        {
            for (int i = 0; i < attempts; i++)
            {
                float x = margin + (float)rng.NextDouble() * (Size - margin * 2f);
                float z = margin + (float)rng.NextDouble() * (Size - margin * 2f);

                float h = HeightAt(x, z);
                if (h < minHeight) continue;
                if (SlopeAt(x, z) > maxSlope) continue;

                point = new Vector3(x, h, z);
                return true;
            }

            point = Vector3.zero;
            return false;
        }
    }
}

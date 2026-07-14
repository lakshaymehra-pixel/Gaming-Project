using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Thins the undergrowth to whatever the phone can afford, at scene load.
    ///
    /// The jungle costs draw calls, not triangles — a thousand small props is a thousand things
    /// to submit, and that is what kills a handset. This turns some of them off.
    ///
    /// It only ever touches layers with no collider: ferns, leaf canopies, fronds, grass. That
    /// is not a detail, it is the whole safety argument. Cull a trunk and you have removed cover
    /// the player can see the enemy walking around, and a wall the NavMesh was baked against.
    /// Cull a fern and you have removed a fern.
    /// </summary>
    public class FoliageDensity : MonoBehaviour
    {
        [Tooltip("The foliage groups that may be thinned. Only groups whose props carry no " +
                 "collider belong here — nothing that blocks a bullet or a path.")]
        [SerializeField] private Transform[] cullableGroups;

        private void Awake()
        {
            float keep = GameSettings.Foliage;
            if (keep >= 0.999f) return;   // nothing to do at full detail

            int hidden = 0;
            int total = 0;

            foreach (Transform group in cullableGroups)
            {
                if (group == null) continue;

                for (int i = 0; i < group.childCount; i++)
                {
                    total++;

                    // Deterministic, not random: the same props vanish at the same setting every
                    // time, so a lower tier reads as a coarser version of the same jungle rather
                    // than as a different one each launch.
                    //
                    // Keeping every Nth rather than dropping every Nth spreads the survivors
                    // evenly through the group, which is what stops thinning from carving a bald
                    // patch out of one corner.
                    bool keepThis = Mathf.Repeat(i * keep, 1f) < keep;

                    if (keepThis) continue;

                    group.GetChild(i).gameObject.SetActive(false);
                    hidden++;
                }
            }

            if (hidden > 0)
                Debug.Log($"Foliage: {total - hidden}/{total} props kept " +
                          $"({keep:P0} detail). Cover and walls untouched.");
        }
    }
}

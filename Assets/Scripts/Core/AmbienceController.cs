using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Plays the jungle soundbed. The looping layers run flat out; the roar fires at random
    /// intervals from a random direction, because a threat you can time is not a threat.
    ///
    /// Water is the exception: it is positioned in the world and rolls off with distance, so
    /// it tells you where the river is rather than following you around.
    /// </summary>
    public class AmbienceController : MonoBehaviour
    {
        [Header("Looping beds (2D, follow the listener)")]
        [SerializeField] private AudioSource insects;
        [SerializeField] private AudioSource birds;
        [SerializeField] private AudioSource wind;

        [Header("Occasional")]
        [SerializeField] private AudioSource roarSource;
        [SerializeField] private AudioClip roarClip;
        [SerializeField] private Transform listener;
        [SerializeField] private float minRoarGap = 25f;
        [SerializeField] private float maxRoarGap = 70f;
        [SerializeField] private float roarDistance = 45f;

        private float _nextRoar;

        private void Start()
        {
            PlayLoop(insects);
            PlayLoop(birds);
            PlayLoop(wind);

            ScheduleRoar();
        }

        private static void PlayLoop(AudioSource source)
        {
            if (source == null || source.clip == null) return;

            source.loop = true;
            source.spatialBlend = 0f;     // 2D: the forest is everywhere, not over there
            source.Play();
        }

        private void Update()
        {
            if (roarSource == null || roarClip == null || listener == null) return;
            if (Time.time < _nextRoar) return;

            // Put the roar somewhere out in the trees, at a bearing the player has no reason
            // to expect. Spatialised, so it actually comes from that direction.
            Vector2 bearing = Random.insideUnitCircle.normalized * roarDistance;
            roarSource.transform.position = listener.position
                                          + new Vector3(bearing.x, 2f, bearing.y);

            roarSource.pitch = Random.Range(0.85f, 1.1f);
            roarSource.PlayOneShot(roarClip);

            ScheduleRoar();
        }

        private void ScheduleRoar()
        {
            _nextRoar = Time.time + Random.Range(minRoarGap, maxRoarGap);
        }
    }
}

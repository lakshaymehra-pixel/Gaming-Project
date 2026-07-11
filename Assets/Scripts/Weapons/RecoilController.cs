using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Rotates the camera holder to sell recoil, then springs it back to zero. This is a
    /// visual offset applied on top of the player's aim — it never fights PlayerLook for
    /// ownership of the transform, because it rotates a child of the pivot.
    /// </summary>
    public class RecoilController : MonoBehaviour
    {
        [Header("Spring")]
        [SerializeField] private float snappiness = 12f;
        [SerializeField] private float returnSpeed = 6f;

        private Vector3 _current;
        private Vector3 _target;

        /// <param name="kick">x = pitch up (degrees), y = yaw (degrees).</param>
        public void AddRecoil(Vector2 kick)
        {
            _target += new Vector3(-kick.x, kick.y, 0f);
        }

        private void Update()
        {
            _target = Vector3.Lerp(_target, Vector3.zero, returnSpeed * Time.deltaTime);
            _current = Vector3.Slerp(_current, _target, snappiness * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(_current);
        }
    }
}

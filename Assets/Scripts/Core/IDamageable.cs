using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Anything a bullet can hurt. Keeping this an interface lets weapons hit players,
    /// enemies, and destructibles through the same raycast path.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount, GameObject source);
    }
}

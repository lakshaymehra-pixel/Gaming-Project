using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Applies the saved settings when a scene opens. One of these goes in every scene the
    /// builders generate.
    ///
    /// Not a DontDestroyOnLoad singleton: every scene in this project is self-contained
    /// generated output, and adding a persistent object that survives scene loads means adding
    /// a lifecycle to reason about — one that would have to be created exactly once, in whatever
    /// scene happened to boot first, which on a phone is the splash and in the editor is
    /// whatever the developer had open. A component per scene has no such question.
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        private void Awake()
        {
            GameSettings.Apply();
        }
    }
}

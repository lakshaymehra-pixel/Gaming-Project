using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Turns Firebase on, and says why it will not turn on.
    ///
    /// The setup is six steps across two consoles and every one of them fails quietly: the
    /// SDK imports fine without a config file, the config file sits there without the define,
    /// the define compiles without the Android libraries, and none of it complains until a
    /// sign-in fails on a device with a stack trace that names none of the above.
    ///
    /// So this checks all of it in one place, and sets the one thing that can be set from
    /// script — the define — because typing FIREBASE_AUTH into the right one of several
    /// per-platform text boxes and remembering to press Enter is a step people get wrong.
    ///
    /// Menu: Game > Firebase > ...
    /// </summary>
    public static class FirebaseSetup
    {
        private const string Define = "FIREBASE_AUTH";
        private const string ConfigPath = "Assets/google-services.json";
        private const string SdkMarker = "Assets/Firebase/Plugins/Firebase.Auth.dll";

        [MenuItem("Game/Firebase/Enable (set FIREBASE_AUTH)")]
        public static void Enable()
        {
            if (!AssetDatabase.LoadAssetAtPath<Object>(SdkMarker))
            {
                EditorUtility.DisplayDialog("Firebase SDK not found",
                    "Firebase.Auth.dll is not in Assets/Firebase/Plugins.\n\n" +
                    "Import FirebaseAuth.unitypackage first:\n" +
                    "Assets > Import Package > Custom Package",
                    "OK");
                return;
            }

            // Android and iOS separately: the symbols are per-platform, and setting them on
            // Standalone — which is the tab Player Settings opens on — does nothing whatsoever
            // for a phone build. That is the single most common way this gets missed.
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS,
                                           NamedBuildTarget.Standalone })
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);

                var set = new List<string>(defines);
                if (set.Contains(Define)) continue;

                set.Add(Define);
                PlayerSettings.SetScriptingDefineSymbols(target, set.ToArray());
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<b>Firebase enabled.</b> {Define} is set for Android, iOS and " +
                      "Standalone. Unity will recompile — AuthService switches to the real " +
                      "backend and the login screen drops its OFFLINE MODE notice.");

            Check();
        }

        [MenuItem("Game/Firebase/Disable (back to offline accounts)")]
        public static void Disable()
        {
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS,
                                           NamedBuildTarget.Standalone })
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);

                string[] without = defines.Where(d => d != Define).ToArray();
                if (without.Length == defines.Length) continue;

                PlayerSettings.SetScriptingDefineSymbols(target, without);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Firebase disabled. Accounts are local again (PlayerPrefs).");
        }

        /// <summary>
        /// Reports every piece the sign-in needs, present or missing. Written to be run when
        /// something does not work, which is when the pieces stop being obvious.
        /// </summary>
        [MenuItem("Game/Firebase/Check setup")]
        public static void Check()
        {
            var report = new System.Text.StringBuilder("<b>Firebase setup</b>\n");

            bool sdk = AssetDatabase.LoadAssetAtPath<Object>(SdkMarker) != null;
            report.AppendLine(Line(sdk, "SDK imported (Firebase.Auth.dll)",
                "Assets > Import Package > Custom Package > FirebaseAuth.unitypackage"));

            bool config = File.Exists(ConfigPath);
            report.AppendLine(Line(config, "google-services.json in Assets/",
                "Download it from the Firebase console and drop it in Assets/"));

            if (config) report.AppendLine(CheckPackageName());

            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android,
                                                     out string[] androidDefines);
            bool define = androidDefines.Contains(Define);
            report.AppendLine(Line(define, $"{Define} defined for Android",
                "Game > Firebase > Enable"));

            // Not fatal in the editor — the managed DLL is enough to compile and to run in
            // play mode — but a device build without the resolved .aar files dies on the first
            // Firebase call with DllNotFoundException, and nothing before that hints at it.
            bool resolved = Directory.Exists("Assets/Plugins/Android") &&
                            Directory.GetFiles("Assets/Plugins/Android", "*.aar").Length > 0;
            report.AppendLine(Line(resolved, "Android libraries resolved (.aar files)",
                "Assets > External Dependency Manager > Android Resolver > Force Resolve " +
                "— needed for a phone build, not for the editor"));

            report.AppendLine();
            report.AppendLine(sdk && config && define
                ? "Ready. Remember Email/Password must also be enabled in the Firebase " +
                  "console: Build > Authentication > Sign-in method."
                : "Not ready — see above.");

            Debug.Log(report.ToString());
        }

        private static string CheckPackageName()
        {
            string json = File.ReadAllText(ConfigPath);
            string unity = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);

            // The config lists the package it was issued for. If it does not match this
            // project's, the SDK will initialise and then fail every call, and the error will
            // not mention the package name.
            bool match = json.Contains($"\"package_name\": \"{unity}\"");

            return Line(match, $"package name matches ({unity})",
                "The google-services.json was issued for a different package. Re-register the " +
                "app in the Firebase console with this exact package name, or fix it in " +
                "Player Settings.");
        }

        private static string Line(bool ok, string what, string fix) =>
            ok ? $"  OK      {what}"
               : $"  MISSING {what}\n          → {fix}";
    }
}

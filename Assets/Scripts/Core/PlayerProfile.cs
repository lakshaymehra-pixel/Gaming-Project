using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Who the player is between matches: their name, their level, and what they have done.
    ///
    /// Stored as one JSON blob rather than a scatter of PlayerPrefs keys. That is the whole
    /// reason the move to Firestore later is small: a document store wants a document, and this
    /// already is one. Add a field to ProfileData and both backends carry it.
    ///
    /// Local today. The cloud path is stubbed behind FIREBASE_FIRESTORE and cannot do anything
    /// useful yet — a guest has no UID to key a document on, and until Google Sign-In works,
    /// guest is the only way anybody gets in.
    /// </summary>
    public static class PlayerProfile
    {
        [Serializable]
        public class ProfileData
        {
            public string displayName = "Soldier";
            public int level = 1;
            public int xp;

            public int totalKills;
            public int matchesPlayed;
            public int bestWave;
            public int totalScore;

            /// <summary>Which map they picked last, so the lobby opens where they left it.</summary>
            public string lastMap = "Island";
        }

        private static ProfileData _current;

        /// <summary>The signed-in player. Loads on first access, so nothing has to remember to.</summary>
        public static ProfileData Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        /// <summary>Raised when a match is recorded, so the lobby can refresh without polling.</summary>
        public static event Action Changed;

        /// <summary>True when the level went up on the last RecordMatch. The lobby reads it once
        /// and clears it — a level-up is worth an animation, and it is worth showing exactly
        /// once rather than every time the lobby loads.</summary>
        public static bool PendingLevelUp { get; private set; }

        // --------------------------------------------------------------------- load

        public static void Load()
        {
            string name = PlayerPrefs.GetString("PlayerName", "Soldier");
            string json = PlayerPrefs.GetString(KeyFor(name), null);

            if (string.IsNullOrEmpty(json))
            {
                _current = new ProfileData { displayName = name };
                return;
            }

            // A corrupt blob is not worth crashing the lobby over. Start them fresh and say so:
            // silently resetting somebody's level is the kind of thing they notice and we do not.
            try
            {
                _current = JsonUtility.FromJson<ProfileData>(json);
                if (_current == null) throw new Exception("null");
                _current.displayName = name;   // the account is the source of truth for the name
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Profile for '{name}' could not be read ({e.Message}). " +
                                 "Starting a fresh one.");
                _current = new ProfileData { displayName = name };
            }
        }

        /// <summary>
        /// Writes the profile. Does not flush — PlayerPrefs.Save is a synchronous write of the
        /// entire prefs file, and calling it on every map-card tap is a disk hit per tap.
        /// </summary>
        public static void Save()
        {
            if (_current == null) return;

            PlayerPrefs.SetString(KeyFor(_current.displayName), JsonUtility.ToJson(_current));

#if FIREBASE_FIRESTORE
            PushToCloud(_current);
#endif
        }

        /// <summary>
        /// Writes and flushes to disk. For the moments worth paying for: a finished match, and
        /// leaving the lobby. Unity flushes on a clean quit, but an Android task-swipe is not
        /// one, and losing a match to that would be unforgivable.
        /// </summary>
        public static void Flush()
        {
            Save();
            PlayerPrefs.Save();
        }

        private static string KeyFor(string name) => "profile_" + name.ToLowerInvariant();

        // ------------------------------------------------------------------- record

        /// <summary>
        /// Folds a finished match into the profile. Called once per match, from GameLoop, when
        /// the match actually ends — which is a thing that had to be built, because the player
        /// used to respawn forever and no match ever ended.
        /// </summary>
        public static void RecordMatch(int kills, int score, int wave)
        {
            ProfileData p = Current;

            p.totalKills += kills;
            p.totalScore += score;
            p.matchesPlayed++;
            p.bestWave = Mathf.Max(p.bestWave, wave);

            int before = p.level;
            AddXp(XpFor(kills, wave));
            PendingLevelUp = p.level > before;

            Flush();   // a match is worth a disk write; losing one to a task-swipe is not ok
            Changed?.Invoke();
        }

        /// <summary>
        /// What a match was worth. Kills and waves, not score — score is already kills*100, and
        /// paying for the same kill twice makes the number meaningless.
        /// </summary>
        public static int XpFor(int kills, int wave) => kills * 10 + wave * 50;

        private static void AddXp(int amount)
        {
            ProfileData p = Current;
            p.xp += amount;

            // While, not if: a long match can be worth more than one level, and stopping at one
            // would quietly bank the rest against a level-up that never arrives.
            while (p.xp >= XpForLevel(p.level))
            {
                p.xp -= XpForLevel(p.level);
                p.level++;
            }
        }

        /// <summary>
        /// XP needed to leave a level. Linear on purpose — 500, 750, 1000, ... A curve is a
        /// tuning conversation, and there is nothing to tune it against until people play.
        /// </summary>
        public static int XpForLevel(int level) => 500 + 250 * (level - 1);

        /// <summary>How far through the current level, 0..1. The lobby's XP bar.</summary>
        public static float LevelProgress =>
            Mathf.Clamp01(Current.xp / (float)XpForLevel(Current.level));

        public static void ClearLevelUpFlag() => PendingLevelUp = false;

        // -------------------------------------------------------------------- cloud

#if FIREBASE_FIRESTORE
        /// <summary>
        /// Mirrors the profile to Firestore under the signed-in user's UID.
        ///
        /// Fire and forget: a failed write must not block the lobby, and the local copy is
        /// already saved by the time this runs. The cost of that is last-write-wins across
        /// devices — acceptable for a solo game, and the thing to revisit if it ever is not.
        /// </summary>
        private static void PushToCloud(ProfileData data)
        {
            var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;

            // Guests have no UID. There is nothing to key a document on, and inventing one would
            // just make an orphan document per install.
            if (user == null) return;

            Firebase.Firestore.FirebaseFirestore.DefaultInstance
                .Collection("profiles").Document(user.UserId)
                .SetAsync(new System.Collections.Generic.Dictionary<string, object>
                {
                    ["displayName"] = data.displayName,
                    ["level"] = data.level,
                    ["xp"] = data.xp,
                    ["totalKills"] = data.totalKills,
                    ["matchesPlayed"] = data.matchesPlayed,
                    ["bestWave"] = data.bestWave,
                    ["totalScore"] = data.totalScore,
                })
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Debug.LogWarning("Profile did not reach the cloud: " + t.Exception?.Message);
                });
        }
#endif
    }
}

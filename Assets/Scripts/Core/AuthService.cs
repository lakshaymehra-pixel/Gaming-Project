using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Sign-in, behind one interface so the game does not care who is doing it.
    ///
    /// Right now nobody is: there is no Firebase SDK in this project, so this runs a local
    /// account store in PlayerPrefs and the game is playable and demoable today. When the SDK
    /// is imported, define FIREBASE_AUTH and the real calls take over — the callers do not
    /// change, because they only ever see SignIn/Register/SignInAsGuest and a result.
    ///
    /// The local store is NOT security. It is a stand-in that behaves like a login: it rejects
    /// a wrong password, it rejects an unknown user, it refuses to register a name twice. What
    /// it cannot do is stop someone editing their own PlayerPrefs, which is exactly why the
    /// real thing has to live on a server and not in here.
    /// </summary>
    public static class AuthService
    {
        public readonly struct Result
        {
            public readonly bool Success;
            public readonly string DisplayName;
            public readonly string Error;      // null when Success

            private Result(bool success, string displayName, string error)
            {
                Success = success;
                DisplayName = displayName;
                Error = error;
            }

            public static Result Ok(string name) => new(true, name, null);
            public static Result Fail(string error) => new(false, null, error);
        }

        /// <summary>True when a real backend is compiled in. The UI reads this to decide
        /// whether to admit that it is running offline.</summary>
        public static bool IsLive =>
#if FIREBASE_AUTH
            true;
#else
            false;
#endif

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Signs an existing user in. Coroutine rather than async so the caller can drive a
        /// progress bar off it without pulling a threading model into the UI.
        /// </summary>
        public static IEnumerator SignIn(string user, string password, Action<Result> done)
        {
            string invalid = Validate(user, password);
            if (invalid != null)
            {
                done(Result.Fail(invalid));
                yield break;
            }

#if FIREBASE_AUTH
            yield return FirebaseSignIn(user, password, done);
#else
            // A real network round-trip is not instant, and a login that returns on the same
            // frame reads as a login that did not happen.
            yield return new WaitForSeconds(0.6f);

            string stored = PlayerPrefs.GetString(KeyFor(user), null);

            if (string.IsNullOrEmpty(stored))
                done(Result.Fail("No account with that name. Register instead?"));
            else if (stored != Hash(password, user))
                done(Result.Fail("Wrong password."));
            else
                done(Result.Ok(user));
#endif
        }

        public static IEnumerator Register(string user, string password, Action<Result> done)
        {
            string invalid = Validate(user, password);
            if (invalid != null)
            {
                done(Result.Fail(invalid));
                yield break;
            }

#if FIREBASE_AUTH
            yield return FirebaseRegister(user, password, done);
#else
            yield return new WaitForSeconds(0.6f);

            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(KeyFor(user), null)))
            {
                done(Result.Fail("That name is taken."));
                yield break;
            }

            PlayerPrefs.SetString(KeyFor(user), Hash(password, user));
            PlayerPrefs.Save();

            done(Result.Ok(user));
#endif
        }

        /// <summary>No account, no password, straight in. Always available — a horror shooter
        /// that makes you register before it will let you see it is a horror shooter nobody
        /// sees.</summary>
        public static IEnumerator SignInAsGuest(Action<Result> done)
        {
            yield return new WaitForSeconds(0.3f);
            done(Result.Ok("Soldier_" + UnityEngine.Random.Range(1000, 9999)));
        }

        // ------------------------------------------------------------------ validation

        /// <summary>Returns the reason this is not acceptable, or null if it is.</summary>
        private static string Validate(string user, string password)
        {
            if (string.IsNullOrWhiteSpace(user)) return "Enter a username.";
            if (user.Length < 3) return "Username needs at least 3 characters.";
            if (user.Length > 20) return "Username is too long (20 max).";
            if (string.IsNullOrEmpty(password)) return "Enter a password.";
            if (password.Length < 6) return "Password needs at least 6 characters.";
            return null;
        }

        // ------------------------------------------------------------------ local store

        private static string KeyFor(string user) => "auth_" + user.ToLowerInvariant();

        /// <summary>
        /// Salted SHA-256. The password is never written down, even here — the local store is
        /// a stand-in and someone can still edit their own prefs, but a stand-in that keeps
        /// plaintext passwords on disk is a habit that survives into the version that ships,
        /// and people reuse passwords.
        ///
        /// The username is the salt: same password, different accounts, different hashes.
        /// </summary>
        private static string Hash(string password, string user)
        {
            using var sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(user.ToLowerInvariant() + ":" + password);
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }

#if FIREBASE_AUTH
        // ------------------------------------------------------------------ firebase
        //
        // Reached only once the Firebase Unity SDK is imported and FIREBASE_AUTH is defined in
        // Player Settings > Scripting Define Symbols. Until then none of this compiles, which
        // is deliberate: the project has no dependency on a package it does not have.
        //
        // Firebase wants an email, so a username becomes one against a domain you own.

        private const string Domain = "@kaalraat.local";

        private static IEnumerator FirebaseSignIn(string user, string password,
                                                  Action<Result> done)
        {
            var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            var task = auth.SignInWithEmailAndPasswordAsync(user + Domain, password);

            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled)
                done(Result.Fail(Describe(task.Exception)));
            else
                done(Result.Ok(user));
        }

        private static IEnumerator FirebaseRegister(string user, string password,
                                                    Action<Result> done)
        {
            var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            var task = auth.CreateUserWithEmailAndPasswordAsync(user + Domain, password);

            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled)
                done(Result.Fail(Describe(task.Exception)));
            else
                done(Result.Ok(user));
        }

        /// <summary>Firebase's own messages are written for developers. These are written for
        /// whoever is standing at the login screen.</summary>
        private static string Describe(AggregateException e)
        {
            foreach (Exception inner in e.Flatten().InnerExceptions)
            {
                if (inner is not Firebase.FirebaseException fe) continue;

                switch ((Firebase.Auth.AuthError)fe.ErrorCode)
                {
                    case Firebase.Auth.AuthError.WrongPassword:      return "Wrong password.";
                    case Firebase.Auth.AuthError.UserNotFound:       return "No account with that name.";
                    case Firebase.Auth.AuthError.EmailAlreadyInUse:  return "That name is taken.";
                    case Firebase.Auth.AuthError.WeakPassword:       return "Password is too weak.";
                    case Firebase.Auth.AuthError.NetworkRequestFailed: return "No connection.";
                }
            }

            return "Sign-in failed. Try again.";
        }
#endif
    }
}

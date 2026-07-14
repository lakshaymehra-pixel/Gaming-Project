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

        // ------------------------------------------------------------------ providers

        /// <summary>The one-tap sign-ins. Each needs its own SDK and its own console setup,
        /// which is why each has its own define rather than riding on FIREBASE_AUTH.</summary>
        public enum Provider { Google, PlayGames, Facebook }

        /// <summary>
        /// Whether a provider can actually be used right now. The login screen greys out the
        /// ones that cannot — a button that looks live and then explains it is not is worse
        /// than a button that never invited the tap.
        /// </summary>
        public static bool IsAvailable(Provider provider) => provider switch
        {
#if GOOGLE_SIGNIN
            Provider.Google => true,
#endif
#if PLAY_GAMES_SIGNIN
            Provider.PlayGames => true,
#endif
#if FACEBOOK_SIGNIN
            Provider.Facebook => true,
#endif
            _ => false,
        };

        /// <summary>
        /// Signs in with a provider. Every one of these ends up in the same place — a Firebase
        /// credential — so the game never learns which button was pressed.
        ///
        /// Nothing here works in the editor. Google, Play Games and Facebook all sign in
        /// through the Android account system, so this only runs on a device. That is not a
        /// limitation to work around; it is what these providers are.
        /// </summary>
        public static IEnumerator SignInWith(Provider provider, Action<Result> done)
        {
            if (!IsAvailable(provider))
            {
                done(Result.Fail(SetupHint(provider)));
                yield break;
            }

#if GOOGLE_SIGNIN
            if (provider == Provider.Google) { yield return GoogleSignIn(done); yield break; }
#endif
#if PLAY_GAMES_SIGNIN
            if (provider == Provider.PlayGames) { yield return PlayGamesSignIn(done); yield break; }
#endif
#if FACEBOOK_SIGNIN
            if (provider == Provider.Facebook) { yield return FacebookSignIn(done); yield break; }
#endif

            done(Result.Fail(SetupHint(provider)));
        }

        /// <summary>
        /// Says what is missing, on screen, in words. The alternative — a dead button, or worse,
        /// one that silently drops the player into a guest session — is how someone spends an
        /// afternoon wondering why "sign in with Google" made them Soldier_4471.
        /// </summary>
        private static string SetupHint(Provider provider) => provider switch
        {
            Provider.Google =>
                "Google sign-in is not set up. See the README: it needs a keystore, its SHA-1 " +
                "in the Firebase console, and the GOOGLE_SIGNIN define.",
            Provider.PlayGames =>
                "Play Games sign-in is not set up. It needs a game configured in the Play " +
                "Console before it can be enabled.",
            Provider.Facebook =>
                "Facebook sign-in is not set up. It needs a Facebook app and the Facebook SDK.",
            _ => "That sign-in method is not available.",
        };

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

        /// <summary>
        /// Hands a provider's credential to Firebase. Every one-tap sign-in funnels through
        /// here: Google, Play Games and Facebook each produce a different kind of token, and
        /// this is where they stop being different — past this line there is only a Firebase
        /// user, and the game never learns which button was pressed.
        /// </summary>
        private static IEnumerator SignInWithCredential(Firebase.Auth.Credential credential,
                                                        Action<Result> done)
        {
            var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            var task = auth.SignInAndRetrieveDataWithCredentialAsync(credential);

            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled)
            {
                done(Result.Fail(Describe(task.Exception)));
                yield break;
            }

            Firebase.Auth.FirebaseUser user = task.Result.User;

            // Providers do not all give a name. Facebook usually does, Play Games gives a gamer
            // tag, Google gives whatever is on the account — and any of them can be blank.
            string name = !string.IsNullOrEmpty(user.DisplayName)
                ? user.DisplayName
                : "Soldier_" + UnityEngine.Random.Range(1000, 9999);

            done(Result.Ok(name));
        }
#endif

#if GOOGLE_SIGNIN
        // Needs the googlesignin-unity plugin, a keystore, and that keystore's SHA-1 registered
        // against the Android app in the Firebase console. Without the SHA-1 the token comes
        // back and Firebase rejects it — with an error that does not mention fingerprints.
        private static IEnumerator GoogleSignIn(Action<Result> done)
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                RequestIdToken = true,     // the only part Firebase wants
                WebClientId = GoogleWebClientId,
            };

            var task = GoogleSignIn.DefaultInstance.SignIn();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled)
            {
                // Cancelling is not failing. Somebody opened the account picker and changed
                // their mind, and a red error under the buttons for that is just rude.
                done(task.IsCanceled
                    ? Result.Fail("")
                    : Result.Fail("Google sign-in failed. Check the connection and try again."));
                yield break;
            }

            var credential = Firebase.Auth.GoogleAuthProvider.GetCredential(
                task.Result.IdToken, null);

            yield return SignInWithCredential(credential, done);
        }

        /// <summary>
        /// The OAuth web client ID from google-services.json — the entry with client_type 3.
        /// Not the Android one: Firebase wants the *web* client here even on Android, which is
        /// the single most confusing thing about this whole setup.
        /// </summary>
        private const string GoogleWebClientId =
            "273472432892-gq3lt3p2n4lnfehcukt1e35o2i9v23fk.apps.googleusercontent.com";
#endif

#if PLAY_GAMES_SIGNIN
        // Needs the Google Play Games plugin for Unity, and a game set up in the Play Console
        // with Play Games Services linked to this Firebase project.
        private static IEnumerator PlayGamesSignIn(Action<Result> done)
        {
            string authCode = null;
            bool finished = false;

            GooglePlayGames.PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status == GooglePlayGames.BasicApi.SignInStatus.Success)
                {
                    GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(
                        forceRefreshToken: false,
                        code => { authCode = code; finished = true; });
                }
                else
                {
                    finished = true;
                }
            });

            yield return new WaitUntil(() => finished);

            if (string.IsNullOrEmpty(authCode))
            {
                done(Result.Fail("Play Games sign-in failed. Is Play Games set up on this phone?"));
                yield break;
            }

            var credential = Firebase.Auth.PlayGamesAuthProvider.GetCredential(authCode);
            yield return SignInWithCredential(credential, done);
        }
#endif

#if FACEBOOK_SIGNIN
        // Needs the Facebook SDK for Unity, a Facebook app, and — before it works for anyone
        // but you — Facebook's app review, which wants a privacy policy URL and takes weeks.
        private static IEnumerator FacebookSignIn(Action<Result> done)
        {
            if (!Facebook.Unity.FB.IsInitialized)
            {
                bool ready = false;
                Facebook.Unity.FB.Init(() => ready = true);
                yield return new WaitUntil(() => ready);
            }

            Facebook.Unity.FB.ActivateApp();

            Facebook.Unity.ILoginResult login = null;
            Facebook.Unity.FB.LogInWithReadPermissions(
                new[] { "public_profile", "email" },
                r => login = r);

            yield return new WaitUntil(() => login != null);

            if (login.Cancelled)
            {
                done(Result.Fail(""));   // they backed out; not an error
                yield break;
            }

            if (!Facebook.Unity.FB.IsLoggedIn)
            {
                done(Result.Fail("Facebook sign-in failed. Try again."));
                yield break;
            }

            var credential = Firebase.Auth.FacebookAuthProvider.GetCredential(
                Facebook.Unity.AccessToken.CurrentAccessToken.TokenString);

            yield return SignInWithCredential(credential, done);
        }
#endif
    }
}

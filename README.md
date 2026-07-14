# Gaming-Project

**KAAL RAAT** — a mobile FPS (Android + iOS) built in **Unity 6**. A wave-based
jungle-island shooter: move, aim, shoot, slide, survive rounds against animated soldier
enemies on a generated island, under a generated soundscape.

Everything in this repo is source. The maps are **generated from code** (menu:
`Game → Build Island Scene`), the intro's ten-second score is synthesised, and the only
binary asset is one CC0 character model (8 MB). Real audio files (gunshots, jungle
ambience, sea waves, explosions) are included for a polished sound mix.
A fresh clone plus a few menu clicks produces the whole game.

### Key Features
- **BGMI-style TPP/FPP** — third-person by default, switches to first-person on AIM
- **Horror-themed splash screen** — BGMI-style age warning → studio logo → cinematic
  intro with panic gunfire, creature roars, claw emblem slam, and lightning storm
- **Real audio** — downloaded gunshot bursts, jungle nature recordings, sea waves,
  explosions layered over synthesised ambience
- **Wave-based survival** — escalating enemy waves on a procedural jungle island
- **Full mobile touch controls** — joystick, fire, aim, reload, sprint, slide, jump

---

## Setting up from scratch (new PC)

### 1. Install Unity

- Download **Unity Hub**: https://unity.com/download
- In the Hub: **Installs → Install Editor → Unity 6000.5.3f1**
  (this exact version — the project is pinned to it in `ProjectSettings/ProjectVersion.txt`)
- Tick these modules during install:
  - ✅ **Android Build Support** (and inside it: **Android SDK & NDK Tools**, **OpenJDK**)
  - ✅ **iOS Build Support**
  - ❌ nothing else (WebGL/Linux/Mac just eat disk)
- Disk needed: ~25 GB for Unity + ~5 GB for this project's Library cache.

### 2. Clone and open

```
git clone https://github.com/lakshaymehra-pixel/Gaming-Project.git
```

- Unity Hub → **Projects → Add** → pick the cloned folder → open it.
- First import takes 5–10 minutes (the Library cache is being built). This is normal.
- If Unity offers **"Enter Safe Mode?"** on first open, something failed to compile —
  see Troubleshooting below. On a clean clone it should not appear.

### 3. One-time steps inside the editor

1. **Window → TextMeshPro → Import TMP Essential Resources** — without the font assets
   the HUD renders no text at all.
2. **Game → Build Island Scene** — generates the whole level (terrain, jungle, enemies,
   HUD, NavMesh). Takes 1–3 minutes; the editor looks frozen while ~1300 props are
   placed. Let it finish. The Console should print `Jungle: Canopy — placed 340/340`
   style lines and end with `Island built.`
3. **Game → Build Login Scene** — the BGMI-style login screen (username/guest/Google).
4. **Game → Build Splash Scene** — the intro. Build it *after* the others because it
   loads the Login scene behind itself.
5. Press **▶ Play** from the Splash scene. The flow is:
   **Splash → Login → Island (game)**. Click inside the Game view once so the cursor
   locks.

The audio (.wav) files are committed, so no baking is needed on a fresh clone. If you ever
delete `Assets/Audio`, regenerate with **Game → Bake Weapon Audio** and
**Game → Bake Jungle Ambience**, then rebuild the scenes so the clips re-wire. (The
splash's own two clips are re-synthesised on every splash build regardless — they are
scored to its timeline, so a stale one would drift out of sync.)

> ⚠️ Scene builders refuse to run in Play mode (a dialog will tell you). Stop the game
> first, then build.

### 4. Git identity (before your first commit on a new PC)

```
git config --global user.name  "lakshaymehra-pixel"
git config --global user.email "social.marketing@salarytopup.com"
```

---

## Controls

| Action | Desktop | Touch |
|---|---|---|
| Move | `WASD` | Left joystick |
| Look / aim | Mouse | Drag the right half of the screen |
| Fire | Left mouse | `FIRE` button |
| Aim (TPP→FPP) | Right mouse (hold) | `AIM` button (hold) |
| Reload | `R` | `RELOAD` button |
| Sprint | `Shift` (hold) | `RUN` button (hold) |
| Jump | `Space` | `JUMP` button |
| Slide | `Ctrl` or `C` | `SLIDE` button |

**Camera:** The game starts in **TPP** (third-person). Hold **AIM** to switch to **FPP**
(first-person) — release to go back. This mirrors the BGMI/PUBG camera system.

A slide only starts out of a grounded sprint, carries you along the direction you were
already running, and decays — it is a commitment, not a free dodge. Jumping out of one
cancels it and keeps the speed. Firing is blocked while sliding.

---

## What is in the game right now

**Intro** — a BGMI-style splash sequence with horror elements:

1. **Age/content warning** (BGMI style) — fades in/out
2. **"Powered by Unity"** screen
3. **"YAARI GAMES PRESENTS"** studio logo with gold accent
4. **Horror sequence** — escalating creature roars (three, each closer and deeper),
   panic gunfire burst with real gun sounds and explosion, silence, then the claw emblem
   slams in from 4.2x scale with a pitched-down roar and 60-unit screen shake
5. **Title typewriter** — "KAAL RAAT" types itself out letter by letter with gunshot
   bursts after, under an aggressive lightning storm with random close-strike roars
6. **Quote reveal** — Hindi horror quote typewriter
7. **Loading bar** with "TAP TO ENTER THE NIGHT" pulse

Real audio: gun burst, single gunshot, explosion, and creature roar clips play alongside
a synthesised heartbeat-drone whose pulse accelerates from 1.7s to under 1s. Tap to skip.

**Player** — BGMI-style **TPP/FPP** camera system. Default view is **third-person**
(camera behind and above the player, body model visible). Pressing **AIM** smoothly
transitions to **first-person** (camera at eye level, gun viewmodel visible). Releasing
AIM returns to TPP. Camera collision prevents clipping through walls. Movement includes
acceleration, gravity, jump, sprint, and a capsule-shrinking slide; smoothed split
yaw/pitch look; FOV that widens on sprint and tightens on ADS.

**Weapon** — hitscan full-auto rifle: bloom that grows while the trigger is held, spring
recoil, magazine + reserve + timed reload, headshot multiplier, pooled tracers, muzzle
flash, synthesised fire/reload/dry-click sounds. The viewmodel is animated procedurally —
reload dip, per-shot kick, walk bob, look sway — no animation clips involved. Tuning lives
in `Assets/Settings/Rifle.asset`; new guns are **Create → Game → Weapon Data**.

**Enemies** — the Quaternius SWAT model (rigged, CC0) driven by a NavMesh
chase/attack state machine with a line-of-sight gate and deliberately imperfect aim.
Animations (idle, run, shoot, death) are crossfaded in code from what the AI is actually
doing — the Animator asset has no transitions to maintain. The headshot collider rides the
head bone, so the zone bobs with the run cycle. If `Assets/Models/Swat.fbx` is missing, a
primitive soldier is built as fallback.

**Waves** — each round spawns more enemies than the last, capped on concurrent agents so
a phone can hold frame rate; the next wave gates on the island being cleared.

**HUD** — health bar + damage vignette, ammo, kills, score, wave, a crosshair that opens
with real spread, hitmarkers, a north-up radar minimap (second orthographic camera +
UI blips that pin to the rim when enemies are out of range), and a game-over screen.

**Sound** — a mix of real downloaded audio and synthesised fallbacks:
- **Weapons**: real gunshot, gun burst, reload, and explosion clips (`.mp3`)
- **Ambience**: real jungle nature, birds, insects, wind, and sea wave recordings,
  plus a synthesised distant roar that fires at random intervals from a random bearing
- **Splash**: scored drone with accelerating heartbeat, real gunfire bursts, creature
  roars, and explosion impacts
- Synthesised `.wav` fallbacks are generated by `GunAudioBaker` and `AmbienceBaker`
  and used when real clips are missing. Four ambient loops of different lengths so they
  never realign.

---

## The two maps

**Island** (`Game → Build Island Scene`) — a 400 m heightfield with a ragged coastline and
sea on every side, grown over with a four-layer jungle: emergent giants with buttress
roots and hanging vines, a closed canopy, understory and palms, ferns/bushes/fallen logs
on the floor, boulders, and five roofless walled compounds with gaps punched in every
wall so no building is a safe box.

It reads as rainforest because of **density and light**, not detail: ~1300 props, short
sight lines, green exponential fog (~80 m visibility), warm low sun, near-black ambient
from below. Everything is generated from one seed (`Seed` in `IslandSceneBuilder`), so
the map is reproducible.

Collider rules do double duty: canopy blobs, fronds, vines and ferns have **no collider**
(you shoot through leaves and walk under them), while trunks, logs and boulders are solid.
That single distinction also decides navigation — the bake is a `NavMeshSurface` collecting
physics colliders, so real cover carves the NavMesh and leaves do not, with nothing to keep
in sync by hand.

**Arena** (`Game → Build Arena Scene`) — a walled box with cover crates. Faster to iterate
on when testing a weapon or AI change rather than the level.

### Tuning

- `JungleDensity` (in `IslandSceneBuilder`) scales every foliage layer at once. If a phone
  drops frames, turn this down first — the cost is draw calls, not triangles, which is
  why every prop is flagged batching-static.
- `Seed` changes the whole island layout.
- Wave pacing lives on the `WaveSpawner` fields set in `ArenaSceneBuilder.BuildSpawner`.

### Swapping in real art later

- **Trees:** `JungleFoliage.MakeTree / MakePalm / MakeGroundCover` are the only places
  foliage geometry is built. Swap their bodies for `Instantiate(prefab)` against an
  imported pack; layout, density, collider rules and batching all stay.
- **Enemy:** drop a different rigged FBX at `Assets/Models/Swat.fbx` (or point
  `SoldierFactory.ModelPath` elsewhere). Requirements: a bone named `Head`, and clips
  whose names end with `Idle_Gun`, `Run`, `Gun_Shoot`, `HitRecieve`, `Death`.

---

## Repo layout

```
Assets/
  Scenes/      Splash.unity, Island.unity — generated output, committed
  Audio/       real + synthesised clips (committed; synth regenerable from the Game menu)
               Splash/   splash-screen-only audio (jungle, guns, heartbeat, etc.)
  Models/      Swat.fbx — Quaternius, CC0 (see Models/README.md)
  Materials/   generated flat-colour materials
  Settings/    WeaponData, terrain data, minimap RT, animator controller,
               splash sprites + textures (all generated)
  Scripts/
    Core/      Health, IDamageable, GameLoop, AmbienceController, ProceduralWalker
    Player/    PlayerController, PlayerMotor (slide/jump), PlayerLook, PlayerInputHub
    Weapons/   Weapon, WeaponData, WeaponAnimator, RecoilController, TracerPool
    Enemies/   EnemyAI, EnemyAnimator, WaveSpawner
    UI/        SplashController, HudController, Minimap, VirtualJoystick,
               TouchLookArea, HoldButton
    Editor/    ArenaSceneBuilder, IslandSceneBuilder, SplashSceneBuilder, IslandTerrain,
               JungleFoliage, SoldierFactory, GunAudioBaker, AmbienceBaker
               (all under the Game menu)
```

`Assets/Scenes/` **is** committed, so a clone runs without building anything first. The
builders remain the source of truth though: the scenes are generated output, and the way to
change a level is to change its builder and re-run the menu item, not to hand-edit the
scene and hope the next build doesn't overwrite it (it will).

`Island.unity` is ~24 MB because the terrain heightfield is embedded in it. That is under
GitHub's limits but it re-uploads in full on every terrain change — worth moving to Git LFS
if the repo starts to drag.

---

## Login and Firebase

The login screen asks for a username and password and means it — a wrong password is
rejected, a taken name cannot be registered again. What it does *not* have out of the box is
anywhere to check those against.

**Without Firebase** (how a fresh clone runs): `AuthService` keeps accounts in `PlayerPrefs`
on the device, with salted SHA-256 hashes. The screen says **OFFLINE MODE** so nobody
mistakes it for the real thing. Perfectly good for playing and for demos; useless as
security, since a player can edit their own prefs.

**With Firebase**: real accounts, in the cloud, shared across devices. The code is already
written — it sits behind `#if FIREBASE_AUTH` in
[AuthService.cs](Assets/Scripts/Core/AuthService.cs). Nothing else in the game changes: the
login screen only ever calls `SignIn` / `Register` / `SignInAsGuest`.

### Turning Firebase on

The app's package name is **`com.yaarigames.kaalraat`**. Firebase asks for it, and it must
match exactly or the SDK will not connect.

1. **Make the project** — [console.firebase.google.com](https://console.firebase.google.com)
   → *Add project* → name it → Analytics is optional, skip it if you like. Free tier is far
   more than this needs.

2. **Turn on email sign-in** — in the project: *Build → Authentication → Get started →
   Sign-in method → Email/Password → Enable → Save*. Nothing works until this is on.

3. **Register the Android app** — *Project settings* (gear icon) → *Your apps* → the Android
   icon. It asks three things:
   - **Android package name**: `com.yaarigames.kaalraat` ← must be exact
   - **App nickname**: anything, it is only a label
   - **SHA-1**: leave blank. It is only needed for Google Sign-In, which this does not use.

4. **Download `google-services.json`** and drop it in `Assets/`. It is gitignored — it ties
   the repo to one Firebase project, and each developer downloads their own.

5. **Import the SDK** — [firebase.google.com/download/unity](https://firebase.google.com/download/unity),
   unzip, and in Unity: *Assets → Import Package → Custom Package* → `FirebaseAuth.unitypackage`.
   Import everything it offers. It pulls in the External Dependency Manager, which will want
   to resolve Android libraries; let it.

6. **Flip the switch** — *Edit → Project Settings → Player → Android tab → Other Settings →
   Scripting Define Symbols* → add `FIREBASE_AUTH` → press Enter, then Apply. Unity
   recompiles, and the Firebase branch of `AuthService` comes alive.

The OFFLINE MODE notice disappears on its own once it does — `AuthService.IsLive` is what the
screen reads, and that is the define.

> Firebase's Android API key ships inside every APK and is not a secret in the way a server
> key is. It still identifies and bills *your* project, which is why `google-services.json`
> stays out of the repo rather than sitting in a public one inviting strangers to point their
> builds at it.

---

## Building to a phone

### Android

1. **File → Build Profiles** → select **Android** → **Switch Platform** (first switch
   re-imports assets, give it a few minutes).
2. On the phone: enable Developer Options (tap Build Number 7×) → enable USB Debugging.
3. Plug in via USB, accept the phone's prompt, then **Build And Run**.

Package name is `com.yaarigames.kaalraat`, set in *Player Settings → Other Settings*. Once
an app is on the Play Store this can never be changed, so change it now or not at all.

### iOS

Unity exports an Xcode project, but Apple only permits iOS compilation on a Mac with
Xcode. The project is kept iOS-ready so that step is mechanical when a Mac is available.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| "Enter Safe Mode?" on open | Compile error. Open anyway, check the Console; `Library/PackageCache` errors → delete `Packages/packages-lock.json` + `Library/PackageCache`, reopen. |
| HUD shows no text | TMP fonts missing → **Window → TextMeshPro → Import TMP Essential Resources**, rebuild scene. |
| "Stop Play mode first" dialog | You ran a scene builder while the game was running. Stop (■), rerun. |
| No gun/ambience sound | Clips missing or scene built before they existed → run both **Bake** menu items, rebuild scene. |
| Enemy is giant / tiny / grey / stuck in T-pose | Model import issue — delete `Assets/Settings/EnemySoldier.controller`, select `Assets/Models/Swat.fbx`, Reimport, rebuild scene. |
| Player falls through the ground | TerrainCollider stripped → make sure `com.unity.modules.terrainphysics` is in `Packages/manifest.json` (it is, unless edited). |
| Phone build is laggy | Lower `JungleDensity` (0.6 is a good first try), rebuild scene. |
| Login still says OFFLINE MODE after the Firebase setup | `FIREBASE_AUTH` is not in *Player Settings → Other Settings → Scripting Define Symbols*, or it is set on the wrong platform tab — the symbols are per-platform, so adding it to Standalone does nothing for an Android build. |
| Firebase compiles but every sign-in fails | Email/Password is not enabled in *Authentication → Sign-in method*, or `google-services.json` is missing from `Assets/`, or its package name does not match `com.yaarigames.kaalraat` exactly. |
| "DllNotFoundException: FirebaseCppApp" | The SDK imported but its Android libraries were never resolved → *Assets → External Dependency Manager → Android Resolver → Force Resolve*. |
| Enemies stand still, Console warns "no NavMesh within 2m" | The scene has no baked surface, or the spawn points sit off it → **Game → Build Island Scene** to re-bake. An enemy that can't find the mesh disables itself rather than spamming an error every frame. |
| Splash plays but never enters the game | The Island isn't in the build settings → build the Island scene first, then the splash (it loads the Island behind itself). |

---

## Roadmap (not built yet)

Main menu, pause, settings; weapon switching and pickups; ~~player hands/body model~~
(done — TPP body visible); grenades; better enemy variety; realistic character models
(Blender/Mixamo); save/highscores; multiplayer; store-ready polish (icons, signing).

---

## Credits

- **SWAT character** — [Quaternius](https://quaternius.com/), Ultimate Modular Men Pack,
  CC0, via [Poly Pizza](https://poly.pizza/m/Btfn3G5Xv4).
- Everything else — generated in-project.

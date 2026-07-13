# Gaming-Project

**KAAL RAAT** — a mobile FPS (Android + iOS) built in **Unity 6**. A wave-based
jungle-island shooter: move, aim, shoot, slide, survive rounds against animated soldier
enemies on a generated island, under a generated soundscape.

Everything in this repo is source. The maps are **generated from code** (menu:
`Game → Build Island Scene`), every sound is **synthesised from code** — including the
intro's ten-second score — and the only binary asset is one CC0 character model (8 MB).
A fresh clone plus a few menu clicks produces the whole game.

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
3. **Game → Build Splash Scene** — the intro. Build it *after* the Island, because it
   loads the Island behind itself and needs it present in the build settings.
4. Press **▶ Play** from the Splash scene. Click inside the Game view once so the cursor
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
| Aim down sights | Right mouse | `AIM` button |
| Reload | `R` | `RELOAD` button |
| Sprint | `Shift` (hold) | `RUN` button (hold) |
| Jump | `Space` | `JUMP` button |
| Slide | `Ctrl` or `C` | `SLIDE` button |

A slide only starts out of a grounded sprint, carries you along the direction you were
already running, and decays — it is a commitment, not a free dodge. Jumping out of one
cancels it and keeps the speed. Firing is blocked while sliding.

---

## What is in the game right now

**Intro** — a ten-second action-horror sequence that boots first and loads the island
behind itself. It escalates rather than repeating: a distant roar in the dark, then a
panicked fusillade whose gaps close as it goes (0.34s between the first two shots, 0.1s
between the last — a man firing at something he can't see doesn't pace himself), then a
full second of silence, and the claw emblem slams into that silence from 4.2× scale with
the roar pitched down to 0.6. The title spells itself out under a storm that is already
running. Every visual is drawn in code — the claw, the splatter, the bullet holes are all
generated textures — and the audio is two synthesised clips: a scored ten seconds whose
heartbeat accelerates from 1.7s apart to under a second and whose sub-bass swell peaks
under the slam, plus a seamless bed loop underneath so a slow scene load never drops the
screen into silence. Tap to skip.

The whole look lives in the constant block at the top of `SplashSceneBuilder`; the timing
lives in `SplashController.Run()`.

**Player** — first-person movement with acceleration, gravity, jump, sprint, and a
capsule-shrinking slide; smoothed split yaw/pitch look; FOV that widens on sprint and
tightens on ADS.

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

**Sound** — everything synthesised: rifle report (noise burst over a pitch-dropping
thump), reload as four timed mechanical clicks, and a jungle bed of cicadas, birdsong,
wind, positioned river noise, and a distant roar that fires at random intervals from a
random bearing. Four loops of different lengths so they never realign.

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
  Audio/       synthesised .wav clips (committed; regenerable from the Game menu)
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

## Building to a phone

### Android

1. **File → Build Profiles** → select **Android** → **Switch Platform** (first switch
   re-imports assets, give it a few minutes).
2. On the phone: enable Developer Options (tap Build Number 7×) → enable USB Debugging.
3. Plug in via USB, accept the phone's prompt, then **Build And Run**.

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
| Enemies stand still, Console warns "no NavMesh within 2m" | The scene has no baked surface, or the spawn points sit off it → **Game → Build Island Scene** to re-bake. An enemy that can't find the mesh disables itself rather than spamming an error every frame. |
| Splash plays but never enters the game | The Island isn't in the build settings → build the Island scene first, then the splash (it loads the Island behind itself). |

---

## Roadmap (not built yet)

Main menu, pause, settings; weapon switching and pickups; player hands/body model;
grenades; better enemy variety; save/highscores; multiplayer; store-ready polish
(icons, signing).

---

## Credits

- **SWAT character** — [Quaternius](https://quaternius.com/), Ultimate Modular Men Pack,
  CC0, via [Poly Pizza](https://poly.pizza/m/Btfn3G5Xv4).
- Everything else — generated in-project.

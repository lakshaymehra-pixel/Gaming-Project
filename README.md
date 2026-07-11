# Gaming-Project

Mobile FPS (Android + iOS) built in Unity 6. Wave-based arena shooter — the Milestone 1
playable core: move, aim, shoot, reload, kill enemies, survive rounds.

## Requirements

- Unity **6000.5.3f1** with **Android Build Support** (SDK, NDK, OpenJDK) and
  **iOS Build Support**
- An Android device for testing (an emulator will not do the frame rate justice)

## First run

1. Open the project in Unity Hub (`Add` → pick this folder).
2. Wait for the initial import — several minutes, it is compiling packages and shaders.
3. **Window → TextMeshPro → Import TMP Essential Resources.** Without the font assets the
   HUD renders nothing.
4. **Game → Bake Weapon Audio** and **Game → Bake Jungle Ambience.** Synthesise the
   gunfire, reload, and soundbed clips into `Assets/Audio`. Do this *before* building a
   scene — the builder wires whatever clips exist at the time, and warns if they are
   missing.
5. **Game → Build Island Scene** (or **Build Arena Scene** for the boxed test map).
6. Press **Play**. Click inside the Game view first so the cursor locks.

Both scenes are generated rather than committed as binary `.unity` blobs, so the levels
stay reviewable as source and can be rebuilt at any time. Steps 3 and 4 only need doing
once; step 5 can be re-run whenever the builders change.

## The two maps

**Island** — a 400 m heightfield with a ragged coastline and sea on every side, grown over
with jungle: emergent giants with buttress roots and hanging vines, a closed canopy,
understory, palms, ferns and bushes on the floor, fallen logs, boulders, and five roofless
compounds. Every compound wall has a gap in it, so no building is a safe box.

The foliage reads as a rainforest because of density and light, not detail. Sight lines are
short, the fog is green, and the only ambient light from below is near-black — under a
closed canopy nothing reaches you that has not already passed through leaves.

Everything is generated from one seed and built from primitives, so nothing is downloaded
and the map is reproducible. Canopy blobs, ferns, vines, and fronds carry no colliders —
you shoot through leaves and walk under them; trunks, logs, and boulders are solid and
navigation-static, so they are real cover and the NavMesh carves around them.

**Arena** — a walled box with cover crates. Faster to iterate on when the thing being
tested is a weapon or an AI change rather than the level.

### Tuning the jungle

`JungleDensity` in `IslandSceneBuilder` scales every foliage layer at once. If a phone drops
frames, turn that down before touching anything else — the cost is draw calls, not
triangles, which is why every prop is flagged batching-static.

### Replacing the trees with real assets

`JungleFoliage.MakeTree`, `MakePalm`, and `MakeGroundCover` are the only places that build
geometry. Swapping them for `Instantiate(prefab)` against an imported tree pack leaves the
layout, the density falloff, the collider rules, and the batching untouched.

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
cancels it and keeps the speed.

## What is in Milestone 1

- First-person movement with acceleration and gravity
- Split yaw/pitch look with smoothing and pitch clamps
- Hitscan rifle: full-auto, bloom that grows while firing, spring recoil, headshot zone
- Ammo, magazines, reserve, timed reload
- Enemies: NavMesh chase, line-of-sight check, hold-and-fire with imperfect aim
- Waves that grow each round and gate on the arena being cleared
- HUD: health bar, ammo, kills, score, wave, dynamic crosshair, hitmarker, damage vignette
- Death, respawn, game-over screen

## Layout

```
Assets/Scripts/
  Core/      Health, IDamageable, GameLoop
  Player/    PlayerController, PlayerMotor, PlayerLook, PlayerInputHub
  Weapons/   Weapon, WeaponData, RecoilController, TracerPool
  Enemies/   EnemyAI, WaveSpawner
  UI/        HudController, VirtualJoystick, TouchLookArea, HoldButton
  Editor/    ArenaSceneBuilder  (menu: Game → Build Arena Scene)
```

Weapons are tuned through `Assets/Settings/Rifle.asset` — a `WeaponData` asset. New guns
are a right-click away: **Create → Game → Weapon Data**.

## Building to Android

1. **File → Build Profiles** → select **Android** → **Switch Platform**.
2. Enable Developer Options and USB Debugging on the phone, plug it in, accept the prompt.
3. **Build And Run**.

## Building to iOS

Unity exports an Xcode project, but the final build has to be produced on a Mac with
Xcode — Apple does not permit iOS compilation on Windows. The project is kept iOS-ready
so that step is mechanical when a Mac is available.

## Not in scope yet

Menus, multiplayer, weapon switching, character art, and real audio all come after the
core loop feels right.

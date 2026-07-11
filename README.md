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
3. Menu bar → **Game → Build Arena Scene**. This generates `Assets/Scenes/Arena.unity`
   with the geometry, player rig, enemy prefab, spawner, HUD, and a baked NavMesh.
4. Press **Play**. Click inside the Game view first so the cursor locks.

The scene is generated rather than committed as a binary `.unity` blob, so the whole level
stays reviewable as source and can be rebuilt at any time.

## Controls

| Action | Desktop | Touch |
|---|---|---|
| Move | `WASD` | Left joystick |
| Look / aim | Mouse | Drag the right half of the screen |
| Fire | Left mouse | `FIRE` button |
| Aim down sights | Right mouse | `AIM` button |
| Reload | `R` | `RELOAD` button |

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

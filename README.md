# Temnorid Conquest

**Take the village. Defeat its Lord.**

Wars for territory and the capture of settlements. A [Temnorid](https://temnorid.com) game for **Windows**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** in development, first playable not released yet. Website: https://conquest.temnorid.com · Download: [Temnorid-Conquest-Setup.exe](https://github.com/WebArtWork/conquest.temnorid.com/releases/latest/download/Temnorid-Conquest-Setup.exe)

## What it is

Command troops, plan sieges, attack enemy villages and cities, and defend your own lands. The outcome of each battle changes the political map of Temnorid.

## The first playable

The first playable is a single village assault led by you: one Lord, fighting in person, against guards and an enemy Lord.

### How a session goes

1. Start outside the village as a Lord with a sword and one special strike, the Charge.
2. Fight your way in. Guards spot you, chase you and attack; you command only your own Lord.
3. The enemy Lord defends the village. He is tougher than the guards.
4. You win in either of two ways: defeat the enemy Lord, or get into the hall at the heart of the village.
5. If you fall, you see the defeat screen and can retry at once.

### Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Mouse | Look around |
| Shift | Run |
| Space | Jump |
| E | Interact |
| Left mouse (hold) | Sword attack in front of you |
| Right mouse | Charge: rush forward about 8 metres, hitting everyone you run into; recharges in 5 seconds |
| V | Switch between third and first person |
| Esc | Pause menu (resume, save, quit) |

### Win and lose

- **Win:** Defeat the enemy Lord or enter the hall.
- **Lose:** Your health reaches zero. Retry restarts the assault.

### The scene

One small village: a wall, 3 to 5 houses, guards, an enemy Lord in a different colour and a clearly marked hall. All made of blocks and capsules.

## How it connects to Lords

In Lords, battles are for settlements. Results of Conquest assaults will later change the political map of the shared world.

This is for a later phase: [Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **This version is fully offline and does not talk to Lords**; progress is saved locally on the player's PC. Keep identifiers and save data simple and stable so the connection is cheap to add later.

## Scope of this version

- One Lord fighting in person, guards, enemy Lord, two ways to win, retry.
- No army command, siege planning or faction economy yet.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows only. Keyboard and mouse. One map, offline, local saves.
- **Starts from:** the IT Kamianets 3D Engine Character View starter. [AGENTS.md](AGENTS.md) is still the starter's generic guide; this README is the game's own.
- **Engine pieces this game uses:** the Character View set (`3d-unity-character`, the camera and controls), `3d-unity-core`, `3d-unity-input`, `3d-unity-save`, and the shared `3d-unity-combat` (health, teams, melee cone and area attacks) and `3d-unity-ai` (noticing, chasing over a NavMesh, attack requests, patrolling), first written for Temnorid Dungeons. The guards, the enemy Lord's heavy strike, the hall trigger, the two ways to win and the screens are written in this game (`Assets/Scripts`).
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** the packages sit in the shared workspace folder next to the games (`../3d-unity-core`, `../3d-unity-input`, `../3d-unity-character`, the two Character View packages, `../3d-unity-save`, `../3d-unity-combat`, `../3d-unity-ai`, and `../3d-unity-inventory` which the manifest carries along) and `Packages/manifest.json` points at them with `file:../../<package>`. When they get their own repositories, only these paths change.
- **Level building:** menu **Temnorid > Conquest > Build Scene** (or `-executeMethod Temnorid.Conquest.EditorTools.ConquestSceneBuilder.Build`) rebuilds `Assets/Scenes/Main.unity` from scratch: the ground and road, the palisade with its gate, four houses, the hall with its doorway trigger, six guards (two of them patrolling), the enemy Lord, the player's Lord, camera and light, all from primitives. Do not edit that scene by hand. The NavMesh is baked at game start from the ground and wall colliders, and the defenders are switched on after it exists.
- **Code map (`Assets/Scripts`):**
  - `ConquestSession`: the record of taken villages with no Unity in them (how each was won, the best time) and `ConquestSaveStore`.
  - `ConquestGame`: the assault (bakes the NavMesh, wakes the defenders, `Win` for either way of winning, defeat, pause, saving). `PlayerLord`: sword and Charge. `Soldier`: what a defender's attack does and the Lord's heavy strike with its red warning on the ground. `HallTrigger`, `HealthBar3D`, `ConquestHud` (all screens, plain uGUI), `Fx`, `AssetIdTag` (stable ids like `units/guard` for future models).
- **Tests:** `Assets/Tests/EditMode` covers the record and the save file (and `3d-unity-combat` has its own tests, run through `testables` in the manifest); `Assets/Tests/PlayMode` loads the scene, checks every defender stands on the NavMesh, that guards chase and hurt the player, and that defeating the Lord, getting into the hall and dying each end the run correctly.
- **Save file:** `<persistent data path>/conquest/default.json` (on Windows `%USERPROFILE%\AppData\LocalLow\Temnorid\Temnorid Conquest\conquest\default.json`): villages taken, how, and the best time.
- **Windows build:** **Temnorid > Conquest > Build Windows Player** (or `-executeMethod Temnorid.Conquest.EditorTools.ConquestReleaseBuilder.BuildWindows`) writes `Builds/Windows/TemnoridConquest` and a portable zip. `installer/Conquest.iss` turns the folder into `Builds/Installer/Temnorid-Conquest-Setup.exe` with Inno Setup.
- **Do not add:** networking, Firebase, accounts, or any link to Lords in this version.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://conquest.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/conquest.temnorid.com/releases/latest/download/Temnorid-Conquest-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-Conquest-Setup.exe`.
3. Create a GitHub Release (tag `v0.1.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).

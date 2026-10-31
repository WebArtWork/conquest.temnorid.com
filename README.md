# Temnorid Conquest

**Take the village. Defeat its Lord.**

Wars for territory and the capture of settlements. A [Temnorid](https://temnorid.com) game for **Windows** (with Android and iOS builds from the same project), built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** in development, version 0.3.0 (your home village, the shell, playing with others, phones and self-update are in). Website: https://conquest.temnorid.com · Download: [Temnorid-Conquest-Setup.exe](https://github.com/WebArtWork/conquest.temnorid.com/releases/latest/download/Temnorid-Conquest-Setup.exe)

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
| Esc | Pause menu (resume, objectives, main menu). The mouse is free while it is open |

### Playing with others

Up to four people can storm the same village together. On the first screen choose **Play with others**, then either **Host a game here** (friends on the same network type the address it shows) or **Host online** (friends type a short code; needs the game linked to Unity Cloud). Everybody presses **Ready** and the host starts. You all enter the same village and fight the same guards and Lord; the guards chase whoever is nearest. Any of you can win it: defeat the Lord or get one hero into the hall. A hero who falls comes back at the gate after a few seconds, so nobody sits out; the assault is lost only when every hero is down at once. The host can start another assault from the end screen. If the host leaves, the game ends for everybody. In a shared assault the pause menu does not stop the world.

### Updates

When a newer release is published on GitHub, the Windows game downloads and installs it by itself the next time it starts, and starts again. Turn it off in **Profile** if you prefer. On Android and iOS the update comes from the store or the release page.

### Phones

Android and iOS builds come from the same project (Unity Build Profiles: Android, iOS; the application id is `com.itkamianets.conquest`, landscape only). On a phone or tablet the screen has a move stick (left thumb), Attack, Charge, Jump and Run buttons (right thumb), and dragging anywhere on the right side looks around. The shell's bottom bar switches between the assault and the Objectives panel; opening it pauses a solo game.

### Win and lose

- **Win:** Defeat the enemy Lord or enter the hall.
- **Lose:** Alone: your health reaches zero, and Retry restarts the assault. With others: every hero is down at the same moment.

### The scene

One small village: a wall, 3 to 5 houses, guards, an enemy Lord in a different colour and a clearly marked hall. All made of blocks and capsules.

## How it connects to Lords

In Lords, battles are for settlements. Results of Conquest assaults will later change the political map of the shared world.

[Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **Since 0.3.0 Conquest reads your Lords village** for the home village (below), through the shared `temnorid-village` package, and **only reads it**: it never writes to Lords, not even the stock. Conquest records stay on the player's device.

### Your home village

Playing alone, **Play** takes you to your village first (scene `Village`), then the gate marches out to the assault (`Main`). The village and farm look as in Lords and Temnorid Craft: signed in with Google and a Lords save exists, you see **your** village and farm, your lord's name and your six stocks (wood, stone, iron, water, grain, clay); a **guest**, a signed-out or offline player, or someone with no Lords save sees the standard village with the standard stock and a line saying why. A Google player's village loads in the background (a few seconds at most, then the standard village) under "Loading your village…". Walk through the red gate or press **March out** (or the **Assault** place) to storm the village; after a victory or defeat **Return to the village** (or the **Village** place of the assault) brings you home. The hub is solo only for now: the online lobby starts the assault directly. Rewards from conquests do not change the stock yet.

## Scope of this version

- One Lord fighting in person (up to four together), guards, enemy Lord, two ways to win, retry, and a home village (yours from Lords, or the standard one) to march out from alone.
- No army command, siege planning or faction economy yet.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (keyboard and mouse), Android and iOS (touch). One map, solo or up to four players, local saves. The game follows the shared Temnorid structure: scenes `Boot` (splash, update check), `Menu` (landing, onboarding, lobby, profile), `Village` (the solo home hub) and `Main` (the assault), built in that order, from `3d-unity-shell`; `ConquestShell` sets its title, `GameId` `conquest`, `Repo` `WebArtWork/conquest.temnorid.com`, `GameScene` = `Village` (solo Play) and its places: Assault and Objectives (plus Village, alone) in the assault, Village and Assault in the hub. Online, the lobby still starts `GameScene`, so `VillageGame` sends anybody who arrives online straight on to `Main` (the shell has no separate online scene).
- **Starts from:** the IT Kamianets 3D Engine Character View starter. [AGENTS.md](AGENTS.md) is still the starter's generic guide; this README is the game's own.
- **Engine pieces this game uses:** the Character View set (`3d-unity-character`, the camera and controls), `3d-unity-core`, `3d-unity-input`, `3d-unity-save`, and the shared `3d-unity-combat` (health, teams, melee cone and area attacks) and `3d-unity-ai` (noticing, chasing over a NavMesh, attack requests, patrolling), first written for Temnorid Dungeons. The guards, the enemy Lord's heavy strike, the hall trigger, the two ways to win and the screens are written in this game (`Assets/Scripts`).
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** the packages sit in the shared workspace folder next to the games (`../3d-unity-core`, `../3d-unity-input`, `../3d-unity-character`, the two Character View packages, `../3d-unity-save`, `../3d-unity-combat`, `../3d-unity-ai`, and `../3d-unity-inventory` which the manifest carries along) and `Packages/manifest.json` points at them with `file:../../<package>`. When they get their own repositories, only these paths change.
- **Level building:** menu **Temnorid > Conquest > Build Scene** (or `-executeMethod Temnorid.Conquest.EditorTools.ConquestSceneBuilder.Build`) rebuilds `Assets/Scenes/Main.unity` and `Assets/Scenes/Village.unity` (the village view, hero, camera and HUD; the village itself is drawn at run time) from scratch: the ground and road, the palisade with its gate, four houses, the hall with its doorway trigger, six guards (two of them patrolling), the enemy Lord, the player's Lord, camera and light, all from primitives. Do not edit that scene by hand. The NavMesh is baked at game start from the ground and wall colliders, and the defenders are switched on after it exists.
- **Code map (`Assets/Scripts`):**
  - `ConquestSession`: the record of taken villages with no Unity in them (how each was won, the best time) and `ConquestSaveStore`.
  - `VillageGame` / `VillageHud` / `MarchGate` / `HubHero`: the home hub (loads the village like Craft does: guest and tests get the standard village in the first frame, a Google player asynchronously with a short timeout; `VillageGame.VillageLoader` lets tests bring a fake save). Shared `temnorid-village` package supplies `VillageSource`, `VillageView` and the stock; the hub only reads it.
  - `ConquestGame`: the assault (bakes the NavMesh, wakes the defenders, `Win` for either way of winning, defeat, pause, saving). `PlayerLord`: sword and Charge. `Soldier`: what a defender's attack does and the Lord's heavy strike with its red warning on the ground. `HallTrigger`, `HealthBar3D`, `ConquestHud` (all screens, plain uGUI), `Fx`, `AssetIdTag` (stable ids like `units/guard` for future models).
- **Tests:** `Assets/Tests/EditMode/VillageHubTests` and `Assets/Tests/PlayMode/VillageHubTests` cover the hub with the package's in-memory fake save (default village in the first frame, a Lords village and stock with nothing written, the gate and the Assault place, Return to the village, the online skip; online test port **7804**). `Assets/Tests/EditMode` covers the record and the save file (and `3d-unity-combat` has its own tests, run through `testables` in the manifest); `Assets/Tests/EditMode/ConquestHitTests` covers the message a guest sends the host; `Assets/Tests/PlayMode/OnlineConquestTests` hosts the real scene with a second session in the same process (the guest hero is shown on the host from its pose, defenders reach the guest as entities, a hit is checked and applied by the host, damage to the guest hero reaches the guest, respawn and team wipe, the end is announced); `TouchControlsTests` builds the phone controls and presses Attack on the virtual touch device; `Assets/Tests/PlayMode` loads the scene, checks every defender stands on the NavMesh, that guards chase and hurt the player, and that defeating the Lord, getting into the hall and dying each end the run correctly.
- **Save file:** `<persistent data path>/conquest/default.json` (on Windows `%USERPROFILE%\AppData\LocalLow\Temnorid\Temnorid Conquest\conquest\default.json`): villages taken, how, and the best time.
- **Version:** 0.3.0, set in `ConquestSceneBuilder` (`ShellSceneKit.ApplyPlatformSettings`), `installer/Conquest.iss` (`AppVersion`), this README and `docs/index.html`. Bump the minor in all four together.
- **Windows build:** **Temnorid > Conquest > Build Windows Player** (or `-executeMethod Temnorid.Conquest.EditorTools.ConquestReleaseBuilder.BuildWindows`) writes `Builds/Windows/TemnoridConquest` and a portable zip. `installer/Conquest.iss` turns the folder into `Builds/Installer/Temnorid-Conquest-Setup.exe` with Inno Setup.
- **Online code (`3d-unity-net`):** `ConquestNet` is the whole link. No network objects or prefabs. **The host is the authority** (`NetSession.IsAuthority`): only it runs the guards and the Lord (NavMesh AI, the Lord's telegraphed heavy strike), decides who is hit, keeps the clock, and decides victory and defeat. It publishes about ten times a second the objective (entity 0: clock, guards left, phase, how it was won, Lord alive) and each defender (entity 1000 + its index in the scene: position, health, heavy-strike warning, a blow landing); a guest never runs AI (its defenders are `Soldier.Remote`, no NavMesh is baked there) and only shows them. Each player's hero is simulated by its owner and published as a pose about fifteen times a second (position, facing, alive, charging, a swing counter, health); the others show it with `RemoteBody` (`RemoteHero`). On the host such a hero is also a body the guards can see (`AiTarget`) and hit; the damage it takes is not applied there but sent to its owner with `SendTo` (`cq.dmg`). A blow a guest lands (sword or Charge) is an event to the host (`cq.hit`: defender index, damage, weapon; format in `ConquestHit`) which accepts it only if the sender is alive and in reach of that defender, the damage is not more than that weapon does, and it is not faster than the weapon can be used. The host decides the hall is entered from the guest's pose and that every hero is down, and announces the end (`cq.end`). A fallen hero respawns after `ConquestGame.RespawnSeconds` (6); nobody is stuck. There is no catch-up or rubber-banding against people. Test port base: **7803** (`OnlineConquestTests`). Limits: no joining a running assault, no reconnect, positions are not predicted (a guest sees defenders about a tenth of a second late), guards hit a guest hero only when the host sees it close.
- **Do not add:** writes to Lords (no `StockSyncer`, no `SaveStockAsync`): Conquest only reads it for now. Firebase and accounts come only through `3d-unity-account` and `temnorid-village`.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://conquest.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/conquest.temnorid.com/releases/latest/download/Temnorid-Conquest-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (**Temnorid > Conquest > Build Windows Player**; it builds the Boot, Menu and Main scenes from the build settings).
2. Wrap the build in a Windows installer named `Temnorid-Conquest-Setup.exe`.
3. Create a GitHub Release (tag `v0.3.0`; installed games update to it by themselves) and attach that file and the zip. Players on 0.2.x cannot join 0.3.x (same major.minor is required to play together).
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).

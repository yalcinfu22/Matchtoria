# Matchtoria

A match-3 puzzle prototype built in Unity as a two-person Dream Games case-study project. Players swap tiles, clear obstacles and use special tiles to complete level objectives within a limited number of moves.

The main engineering focus is the connection between board logic and animation: gameplay changes become timestamped commands, which the view plays through a DOTween sequence.

<p align="center">
  <img src="docs/media/match3-demo.gif" alt="Matchtoria gameplay demo" width="360">
</p>

The demo uses Dream Games visual assets. Those assets are not included in this public repository.

## What is included

- Match detection, cascading matches, falling tiles and board refills.
- Rocket, TNT and ColorBomb logic, alongside box, vase and stone obstacles.
- Ten JSON level files containing board layouts, move limits and objectives.
- Level selection, progress saved through `PlayerPrefs`, and win/lose screens.
- Tile object pools and separate classes for board state, command playback and level management.

**Built with:** C#, Unity **6000.2.10f1**, Universal Render Pipeline **17.2.0**, DOTween and Newtonsoft JSON.

## How it works

`BoardModel` resolves swaps and cascades, then returns commands describing the changes. `BoardManager` connects player input to the model and passes the results to `BoardView`. The view schedules movement and other tile effects using each command's timestamp.

This keeps gameplay decisions separate from animation code. The model still uses Unity types such as `Vector2Int`.

If you are reading the code, these are useful starting points:

| File | What to look for |
|---|---|
| [BoardModel.cs](Assets/Scripts/Models/BoardModel.cs) | Swap handling and cascade resolution |
| [BoardView.cs](Assets/Scripts/Views/BoardView.cs) | Command playback and animation timing |
| [LevelSceneManager.cs](Assets/Scripts/Managers/LevelSceneManager.cs) | Level setup and connections between systems |

The [architecture notes](docs/architecture.md) cover matching rules, event flow, pooling and the level format in more detail.

## Running locally

The public checkout includes the source, scenes, prefabs and level data. It needs additional setup before it can be played:

1. Open the project in Unity **6000.2.10f1** through Unity Hub.
2. Let Unity resolve the packages listed in `Packages/manifest.json`.
3. Import and set up [DOTween](https://dotween.demigiant.com/). Its library is not included in this checkout, although the source uses it.
4. Add your own sprite assets and repair the missing scene and prefab references.
5. Open `Assets/Scenes/Bootstrap.unity` and enter Play mode once dependencies and references are resolved.

The normal scene flow is `Bootstrap → MainMenuScene → LevelScene`. A separate `TestScene` is included for experiments.

## Current scope

This is a prototype. Special-tile combination logic exists in the model, but the current view skips `Merge` commands, so that interaction is unfinished. The components under `Assets/Scripts/Tests/` are runtime experiments; a standalone automated test runner is not included.

## Credits

Developed as a **two-person project**.

The sprites and other visual assets shown in the demo belong to **Dream Games**. They are omitted from the repository; the GIF shows the project with those assets in place.

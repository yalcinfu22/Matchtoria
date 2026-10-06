# Matchtoria

A match-3 puzzle prototype built with **C# and Unity** as a **two-person Dream Games case-study project**. Players swap tiles, clear obstacles and use special tiles to complete level objectives within a limited number of moves.

The project focuses on gameplay class responsibilities: board, tile and level objects hold game state and rules, while timestamped commands connect board resolution to animation playback.

<p align="center">
  <img src="docs/media/match3-demo.gif" alt="Matchtoria gameplay demo" width="360">
</p>

The demo shows Dream Games visual assets. The original sprite assets are excluded from this public repository. A fresh checkout generates simple placeholder graphics for running the game locally.

**Built with:** C#, Unity 6, Universal Render Pipeline, DOTween and Newtonsoft JSON.

## Gameplay

- Match detection, cascading matches, obstacle-aware falling and board refills.
- Rocket, TNT and ColorBomb behavior, alongside box, vase and stone obstacles with their own damage rules.
- JSON-defined levels with board layouts, move limits and objectives.
- Level selection, saved progress and win/lose screens.
- Tile object pooling for reuse of view objects.

## Architecture

The gameplay model applies **Domain-Driven Design (DDD) principles**: classes represent game concepts and contain behavior as well as state. Model / Command / View describes the presentation split; the design also addresses which class should own each gameplay rule.

- **Gameplay responsibilities:** tile classes decide how they respond to damage and triggering. `Level` owns move counts, objective updates and win/lose decisions, rather than leaving those rules to the UI.
- **Tile capabilities:** `IMatchable`, `IMovable`, `IDamageable` and `ITriggerable` describe what each tile can do. Matching, falling and damage code operate on these gameplay objects.
- **Board resolution:** `BoardModel` coordinates swaps and cascades with `MatchManager` and `FallManager`. The model produces commands describing the resulting movements and effects.
- **Animation playback:** `BoardView` sorts commands by timestamp and schedules them in a DOTween `Sequence`. `BoardManager` connects player input to the model and supplies timed callbacks so objective updates align with tile destruction.

The main interaction flow is:

```text
Player input
  -> BoardManager
  -> BoardModel.ProcessSwap
  -> Timestamped commands
  -> BoardView.ExecuteCommands
  -> DOTween animation and timed callbacks
```

Gameplay resolution is separate from scene objects and tween playback, but **the model is not fully independent of Unity**. It still uses Unity types and utilities, and its command timing references animation constants. Overlapping animations in a sequence do not imply background-thread execution.

### Code starting points

| File | Responsibility |
| --- | --- |
| [BoardModel.cs](Assets/Scripts/Models/BoardModel.cs) | Swaps, cascades and command generation |
| [NodeModel.cs](Assets/Scripts/Models/Nodes/NodeModel.cs) | Layered board cells and tile interactions |
| [Matchable.cs](Assets/Scripts/Models/Tiles/Matchable.cs) | Tile capabilities and source-dependent damage rules |
| [Level.cs](Assets/Scripts/Level/Level.cs) | Objectives, move counts and win/lose decisions |
| [BoardView.cs](Assets/Scripts/Views/BoardView.cs) | Timed command playback and animation |
| [LevelSceneManager.cs](Assets/Scripts/Managers/LevelSceneManager.cs) | Construction and event wiring of level systems |

## Running locally

The current editor version is **Unity 6000.6.4f1**, recorded in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). The package manifest currently specifies **URP 17.6.0**; see [Packages/manifest.json](Packages/manifest.json) for the complete package list. DOTween is included under [Assets/DOTween](Assets/DOTween).

1. Install **Unity Hub** and **Unity 6000.6.4f1**. The .NET SDK is only needed for the standalone tests below.
2. Download and extract [the source ZIP](https://github.com/yalcinfu22/Matchtoria/archive/refs/heads/main.zip), or clone the repository:

   ```sh
   git clone https://github.com/yalcinfu22/Matchtoria.git
   ```

3. In Unity Hub, choose **Add project from disk** and select the folder containing `Assets`, `Packages` and `ProjectSettings`.
4. Open the project with the recorded editor version and wait for package resolution, compilation and asset import. The first import needs an internet connection to download Unity packages. DOTween is already included.
5. When the original sprite library is absent, the editor creates placeholder tiles, board graphics and UI artwork automatically. No separate art download or manual reference repair is required. The placeholders use simple shapes and colors; the GIF above shows the original presentation.
6. Open `Assets/Scenes/Bootstrap.unity`, enter **Play** mode and click the level button. Click two adjacent tiles in sequence to swap them. A portrait Game view such as **1080 × 1920** matches the intended layout.

The normal scene flow is `Bootstrap -> MainMenuScene -> LevelScene`. A separate `TestScene` and runtime experiment scripts are also included.

Placeholder graphics are generated locally under the ignored `Assets/Sprites` folder. Existing artwork is preserved. Use **Tools > Matchtoria > Create Placeholder Art** to run setup again if an import was interrupted. **Tools > Reset Player Data** resets saved level progress.

To create a desktop build, install the matching platform support module through Unity Hub, open **File > Build Profiles**, select the desktop platform and build into `Builds/`. The shared scene list already contains `Bootstrap`, `MainMenuScene` and `LevelScene` in that order.

## Tests

[Tests/PureLogic](Tests/PureLogic) contains a **.NET 8 / NUnit** test project with tests for swaps, matching, falling, damage, cascade timing and tile creation. It links selected gameplay source files from `Assets/Scripts`.

The tests use the .NET test runner rather than Unity's test runner, but they **still reference Unity managed assemblies**. Install the .NET 8 SDK and ensure `UnityManaged` points to the directory containing `UnityEngine.dll` and `UnityEngine.CoreModule.dll`.

From the repository root:

```sh
dotnet test Tests/PureLogic/DreamGamesCase.PureLogic.Tests.csproj
```

The [test project file](Tests/PureLogic/DreamGamesCase.PureLogic.Tests.csproj) reads the editor version from `ProjectVersion.txt` and uses the standard Unity Hub installation path on Windows, macOS or Linux. For a custom installation, pass `-p:UnityManaged="<path-to-UnityEngine-managed-directory>"` to the command above. A missing installation produces an error with the expected path.

The components in `Assets/Scripts/Tests` are separate runtime experiments, not the standalone NUnit suite.

## Current scope

This is a prototype. Special-tile combination logic exists in the model, but the current view skips `Merge` commands, so swapping two special tiles together is unfinished. This is distinct from triggering special tiles through damage.

## Credits

Developed as a **two-person project**.

**My main contribution** was defining gameplay class responsibilities and deciding which behaviors should belong to which domain classes. The architecture description covers the collaborative project as a whole, not individual authorship of every mechanism.

The sprites and other visual assets shown in the demo belong to **Dream Games**. They are not original artwork by the project contributors; the original sprites are omitted from this public checkout. The generated placeholder graphics are separate geometric artwork and contain no Dream Games image data.

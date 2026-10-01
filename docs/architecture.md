# Matchtoria architecture

[Back to the project overview](../README.md)

These notes describe the gameplay code and the connections between its main systems. Setup requirements and visual-asset attribution are in the project README.

## 1. Scene setup and lifetime

The application moves through three scenes:

```text
Bootstrap → MainMenuScene → LevelScene
```

[GameInitiator](../Assets/Scripts/Initiator/GameInitiator.cs) stays alive with `DontDestroyOnLoad`. It creates the player-data manager and the level/scene loaders, then starts the splash-to-menu sequence.

- [PlayerDataManager](../Assets/Scripts/Managers/PlayerDataManager.cs) loads the current level from `PlayerPrefs` and saves progress when a level is completed.
- [LevelLoader](../Assets/Scripts/Loader/LevelLoader.cs) reads `Resources/Levels/LevelN.json` and deserializes it with Newtonsoft JSON.
- [SceneLoader](../Assets/Scripts/Loader/SceneLoader.cs) loads scenes additively, unloads the previous scene and controls loading-screen timing.

[LevelSceneManager](../Assets/Scripts/Managers/LevelSceneManager.cs) creates the objects needed for each level. It reads the level data, constructs the logic and tile pools, instantiates the board and UI, adjusts the camera, and connects the events. Its `OnDestroy` method removes those level-event subscriptions.

## 2. Board logic and input

The main classes have distinct responsibilities:

| Class | Responsibility |
|---|---|
| [BoardModel](../Assets/Scripts/Models/BoardModel.cs) | Board state, swaps, cascades and command generation |
| [BoardManager](../Assets/Scripts/Managers/BoardManager.cs) | Player selections, model/view coordination and gameplay events |
| [BoardView](../Assets/Scripts/Views/BoardView.cs) | Tile views and timed animation playback |
| [BoardPoolManager](../Assets/Scripts/Managers/Pooling/BoardPoolManager.cs) | Reuse of tile view objects |

The input path is:

```text
BoardView.OnTileClicked
  → BoardManager.HandleTileClicked
  → BoardModel.ProcessSwap
  → BoardView.ExecuteCommands
  → BoardManager.OnBoardSettled
```

`BoardManager` collects two adjacent selections and ignores clicks while the view is busy. `ProcessSwap` returns a `SwapResult` containing commands and optional merge metadata. A swap with no match or trigger is reverted; `BoardManager` avoids charging a move for a result containing only swap commands.

The model does not own scene objects, but it depends on Unity types such as `Vector2Int` and `Vector2`.

## 3. Matching, falling and tile behavior

[NodeModel](../Assets/Scripts/Models/Nodes/NodeModel.cs) gives each grid cell three tile slots: top, middle and bottom. Movable gameplay tiles use the middle layer.

Tile behavior is expressed through interfaces including `IMatchable`, `IMovable`, `IDamagable` and `ITriggerable`. Colored tiles can match, move and take damage. Rocket, TNT and ColorBomb implement movement and triggering; obstacles have their own damage behavior.

[MatchManager](../Assets/Scripts/Managers/MatchManager.cs) has a full-board scan and a scan for the rows and columns affected by a swap. It processes matches in this order:

1. Straight lines of five or more produce ColorBomb candidates.
2. Intersecting horizontal and vertical matches produce TNT candidates.
3. Straight lines of four produce Rocket candidates.
4. Remaining matches are collected.

The finder removes or splits overlapping match segments as it processes them. Tiles marked as moving are excluded from match detection.

[FallManager](../Assets/Scripts/Managers/FallManager.cs) performs one fall iteration at a time. An empty cell takes a movable tile from directly above. If a non-movable tile blocks that route, it checks the upper-right and then upper-left cell. Empty positions in the top row receive newly generated color tiles. The model uses these iterations while resolving cascades.

[DamagePatterns](../Assets/Scripts/Models/DamagePatterns.cs) contains the effects used by special tiles, including row/column sweeps, area explosions and color clearing. Combination patterns are also present, but the view's merge animation is unfinished.

## 4. Commands and animation timing

[Commands.cs](../Assets/Scripts/Models/Commands.cs) defines the data passed from gameplay logic to the view:

| Field | Purpose |
|---|---|
| `StartPosition`, `TargetPosition` | Source and destination board coordinates |
| `CommandType` | The change to display |
| `startTimeStamp` | When playback should begin |
| `Layer` | The cell layer to update |
| `TileType` | Tile identity, including newly spawned tiles |
| `Health` | Health information for damage effects |

`BoardView.ExecuteCommands` sorts commands by timestamp and builds a DOTween sequence:

| Handler | Commands and behavior |
|---|---|
| `HandleMove` | `Move`, `Fall`, `FallLeft`, `FallRight`: update tile references and insert a movement tween |
| `HandleSwap` | Read both tile references, exchange them and animate both tiles |
| `HandleSpawn` | Borrow a tile from the pool, then animate its arrival or scale-in |
| `HandleStatic` | Schedule destruction, damage and trigger callbacks |

Commands sharing a timestamp can play together. `GameConfig.COMMAND_TIME_BUMP` separates operations where order matters, such as vacating a top-row cell before refilling it.

The view sets `IsBusy` during playback and clears it when the sequence completes. Timed gameplay callbacks run in that same sequence.

**Current limitations:** `Merge` produces a warning and is skipped by the view. `ExplosionStart` and `ExplosionEnd` are defined enum values but have no cases in the current playback switch.

## 5. Objectives, moves and game end

[Level](../Assets/Scripts/Level/Level.cs) stores the move count and remaining objectives. `LevelSceneManager` connects it to the board and UI:

| Event | Result |
|---|---|
| `OnSwapCompleted` | Consume a move |
| `OnTilesDestroyed` | Reduce the relevant objective counters |
| `OnBoardSettled` | Check whether the level has ended |
| `OnMovesChanged`, `OnRequirementChanged` | Refresh the UI |
| `OnLevelWon` | Lock input, save progress and show the result |
| `OnLevelLost` | Lock input and show the result |

`BoardManager` groups destruction commands by timestamp and target type, then passes those groups to the view as callbacks. This aligns objective updates with the corresponding visual changes.

`Level.CheckGameEnd` checks completed objectives before the remaining move count, so clearing the final objective on the last move can win the level.

## 6. Tile pooling

`BoardPoolManager` uses Unity's `ObjectPool<TileView>`. Prefabs are supplied through the level scene's Inspector configuration.

Initial pool sizes are based on board size and content:

- Colored tiles: the number of cells plus one extra row.
- Rockets and TNT: 20% of the cell count, rounded up.
- ColorBomb: 10% of the cell count, rounded up.
- Obstacles: the number present in the level, with a minimum of one.

The manager creates an initial set of inactive tiles. `Get` activates and configures a tile; `Return` releases it to the appropriate pool. Colored tile types share one pool, while obstacles and special tiles use their own pool types.

Both model creation and initial view setup use [TileIdParser](../Assets/Scripts/Factories/TileIdParser.cs) to interpret tile IDs and health suffixes.

## 7. Level format

Levels live in [Assets/Resources/Levels](../Assets/Resources/Levels). The repository includes `Level1.json` through `Level10.json`. [Level1.json](../Assets/Resources/Levels/Level1.json) is a complete example; [LevelData.cs](../Assets/Data/LevelData.cs) defines the data fields.

| Field | Meaning |
|---|---|
| `level_number` | Level identifier |
| `grid_width`, `grid_height` | Board dimensions |
| `move_count` | Available moves |
| `requirements` | Target type and count pairs |
| `grid_top`, `grid_middle`, `grid_bottom` | Tile IDs for each layer |

Each layer array has `grid_width × grid_height` entries. Positions use row-major indexing: `index = y × grid_width + x`. A null entry represents an empty slot.

The current parser recognizes these case-sensitive IDs:

| Category | IDs |
|---|---|
| Colors | `red`, `green`, `blue`, `yellow` |
| Obstacles | `box`, `vase`, `stone` |
| Special tiles | `ro_h`, `ro_v`, `TNT`, `ColorBomb` |

A numeric suffix supplies a health value, such as `box3`. [TileFactory](../Assets/Scripts/Factories/TileFactory.cs) also accepts `random` when generating model tiles; it is used for board refills. The initial view's parser does not recognize `random`, so explicit tile IDs should be used in level layouts.

## 8. Dependencies and experiments

The recorded editor version is Unity **6000.2.10f1**. [Packages/manifest.json](../Packages/manifest.json) lists the Unity packages, including URP **17.2.0**, Newtonsoft JSON and the sprite pipeline packages. DOTween is referenced by the source but its library is absent from the public checkout.

[Assets/Scripts/Tests](../Assets/Scripts/Tests) contains runtime experiments. The model also exposes test hooks, including board injection and a random-number override, but a standalone test project or runner is not included in this checkout.

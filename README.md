# Flick Fest

A click-target Unity demo that showcases the
[HighScoreServer (HSS)](https://github.com/EricMay256/HighScoreServer)
leaderboard backend. The game is intentionally simple — the backend
integration is the point, not the gameplay complexity.

Three modes ship out of the box:

| Mode      | End Condition       | Wins by              |
| --------- | ------------------- | -------------------- |
| Precision | 30s time limit      | most points          |
| Blitz     | clear 20 targets    | lowest elapsed time  |
| Flood     | 30s time limit      | most points (positive targets only — negatives penalize) |

Score submission, leaderboard fetch, and silent guest auth all go through
the HSS client lifted from `HighScoreServer/UnityClient/`.

## Requirements

- **Unity 6000.3.13f1** (Unity 6.3). Other Unity 6 versions should work
  but are untested.
- A reachable HSS instance, if you want scores to persist. The game is
  fully playable offline; it just can't submit scores in that mode.

## Project layout

```
Assets/
  Scripts/
    Core/           # Pure logic — game modes, scoring, spawner, session
    Presentation/   # MonoBehaviours — UI, target views, input routing
    Network/        # HSS client (verbatim from UnityClient/)
  Editor/           # Scene-builder tool
  Tests/EditMode/   # NUnit tests for ScoreKeeper and TargetSpawner
  Scenes/           # FlickFest.unity is produced by the scene builder
  Generated/        # Assets emitted by the scene builder (configs, prefabs, sprite)
```

The three runtime layers are independent assemblies (`FlickFest.Core`,
`FlickFest.Presentation`, `FlickFest.Network`) so the dependency arrows
stay one-way: Presentation → Core → Network.

## Opening and running the demo

1. Clone the repo and open it in Unity 6000.3.13f1 (Unity Hub will
   prompt to install if missing).
2. When prompted, **import TextMeshPro Essentials** (Window → TextMeshPro
   → Import TMP Essential Resources). The bundled TMP that ships with
   `com.unity.ugui` 2.0 needs this one-time import for the default font
   asset to exist; without it, label text renders as boxes.
3. From the menu bar, run **FlickFest → Build Demo Scene**. This
   regenerates `Assets/Scenes/FlickFest.unity`, the three
   `GameModeDefinition` assets, the leaderboard config, the target
   sprite, and the prefabs (Target, ModeButton, LeaderboardRow). It is
   idempotent — re-run after pulling new code.
4. Open `Assets/Scenes/FlickFest.unity` and edit the
   `Assets/Generated/LeaderboardConfig.asset` so its `BaseUrl` points at
   your HSS instance. (Leave the placeholder URL to test offline.)
5. Make sure the three game modes are registered on your HSS instance
   (one-time setup, requires the API key):
   ```
   POST /api/leaderboard/game_modes  body: {"name":"precision","sort_order":"DESC","label":"Precision"}
   POST /api/leaderboard/game_modes  body: {"name":"blitz",    "sort_order":"ASC", "label":"Blitz"}
   POST /api/leaderboard/game_modes  body: {"name":"flood",    "sort_order":"DESC","label":"Flood"}
   ```
6. Press Play. Guest auth runs silently in the background on the menu
   screen; if it fails you'll see "Offline mode — scores won't be
   saved" but gameplay still works.

## Running the tests

EditMode tests cover `ScoreKeeper` (combo brackets, speed bonus, score
floor, negative penalty) and `TargetSpawner` (sequential vs concurrent
spawn behavior, edge margins, active-count flooring).

- Window → General → **Test Runner** → EditMode tab → **Run All**.

There are no PlayMode tests; the spec's logic is fully separable from
MonoBehaviours and exercised by the EditMode suite.

## Dependencies

- `com.unity.nuget.newtonsoft-json` was added to `Packages/manifest.json`.
  HSS's `LeaderboardService` uses `Newtonsoft.Json` for JSON
  (de)serialization. Newtonsoft is not pulled in transitively by any of
  the other packages, so an explicit dependency is required for the
  client to compile.

Otherwise the project sticks to the packages that shipped with the
Unity 6.3 2D template (Input System, Universal RP, TMP via UGUI 2.0,
Test Framework, etc.).

## License

This demo is provided as-is for evaluation purposes alongside the
HighScoreServer project.

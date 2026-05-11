# Flick Fest — Implementation Spec

A click-target game built in Unity to showcase the HighScoreServer leaderboard backend. The game is intentionally simple — the backend integration is the point, not the game complexity. Users will click on targets \- while avoiding negative value targets in some modes \- and receive a score that is then automatically submitted to the high score server. Users can change their names and register an email address and password.

---

## File Tree

Assets/

└── \_Scripts/

    ├── Core/

    │   ├── EndCondition.cs

    │   ├── TargetType.cs

    │   ├── TargetData.cs

    │   ├── HitResult.cs

    │   ├── GameModeDefinition.cs

    │   ├── ScoreKeeper.cs

    │   ├── TargetSpawner.cs

    │   └── GameSession.cs

    ├── Presentation/

    │   ├── TargetView.cs

    │   ├── SpawnManager.cs

    │   ├── MisclickCatcher.cs

    │   ├── HUD.cs

    │   ├── GameOverPanel.cs

    │   ├── LeaderboardRow.cs

    │   ├── MainMenu.cs

    │   └── ModeButton.cs

    └── Network/

        ├── LeaderboardService.cs      \# Already exists (UnityClient/)

        ├── LeaderboardModels.cs       \# Already exists (UnityClient/)

        └── LeaderboardConfig.cs       \# Already exists (UnityClient/)

The three layers are intentionally independent:

- **Core** — pure logic, no MonoBehaviours (except GameSession), no visuals, no network. Testable in isolation.  
- **Presentation** — MonoBehaviours that render the game and handle input. Subscribes to Core events. Never calls the network directly.  
- **Network** — the existing LeaderboardService. Touched by exactly two scripts: MainMenu (auth) and GameSession (score submission \+ leaderboard fetch, exposed to GameOverPanel via wrapper methods).

---

## Game Modes

Three modes ship, each registered on the server via `POST /api/leaderboard/game_modes`.

| Server `name` | `sort_order` | `label` | End Condition | Duration | Description |
| :---- | :---- | :---- | :---- | :---- | :---- |
| `precision` | `DESC` | Precision | Time limit | 30s | One target at a time. Points for hits, penalties for misses. Combo multiplier. |
| `blitz` | `ASC` | Blitz | Target count (20) | N/A | One target at a time. Score \= elapsed milliseconds. Lowest wins. |
| `flood` | `DESC` | Flood | Time limit | 30s | Many targets at a time (5–8 cap). Negative targets penalize on hit. Combo multiplier. |

### Server Registration

Three `POST /api/leaderboard/game_modes` calls (idempotent — safe to re-run):

POST /api/leaderboard/game\_modes  (X-API-Key header)

{"name": "precision", "sort\_order": "DESC", "label": "Precision"}

POST /api/leaderboard/game\_modes  (X-API-Key header)

{"name": "blitz", "sort\_order": "ASC", "label": "Blitz"}

POST /api/leaderboard/game\_modes  (X-API-Key header)

{"name": "flood", "sort\_order": "DESC", "label": "Flood"}

---

## Core Layer

### EndCondition (enum)

TimeLimit     — session ends when the clock reaches zero (Precision, Flood)

TargetCount   — session ends when the player hits N targets (Blitz)

### TargetType (enum)

Positive   — awards points on hit

Negative   — penalizes on hit; correct play is to ignore

### TargetData (class, not MonoBehaviour)

Pure data describing a live target. Created by TargetSpawner, consumed by TargetView and GameSession.

| Field | Type | Description |
| :---- | :---- | :---- |
| `Id` | `int` | Unique within the session. Used to match TargetView back to data. |
| `NormalizedPosition` | `Vector2` | 0–1 on both axes. Presentation maps to world coords. |
| `SpawnTime` | `float` | `Time.time` at spawn. Used for reaction time calculation. |
| `Lifetime` | `float` | Seconds before expiry. |
| `Type` | `TargetType` | Positive or Negative. |
| `Radius` | `float` | Normalized (0–1 relative to play area width). |

Constructor takes all fields. Immutable after creation.

### HitResult (class)

Returned by ScoreKeeper on a hit. Carries everything the presentation layer needs for feedback without re-deriving scoring logic.

| Field | Type | Description |
| :---- | :---- | :---- |
| `PointsDelta` | `int` | Points awarded or deducted for this hit. |
| `ComboCount` | `int` | Current streak length after this hit. |
| `WasNegative` | `bool` | True if a negative target was hit. |
| `ReactionMs` | `float` | Milliseconds from spawn to click. |

### GameModeDefinition (ScriptableObject)

`[CreateAssetMenu(menuName = "FlickFest/Game Mode")]`

One asset per mode. Contains everything the client needs to run a session. The server knows none of this — it only stores name \+ sort\_order \+ label.

**Server mapping fields:**

| Field | Type | Description |
| :---- | :---- | :---- |
| `GameModeName` | `string` | Must match the `name` registered on the server. Passed to `SubmitScore`. |
| `DisplayLabel` | `string` | Shown in the mode selector UI. |

**Session rules:**

| Field | Type | Default | Description |
| :---- | :---- | :---- | :---- |
| `EndCondition` | `EndCondition` | — | TimeLimit or TargetCount. |
| `Duration` | `float` | 30 | Session length in seconds (TimeLimit only). |
| `TargetGoal` | `int` | 20 | Targets to hit before session ends (TargetCount only). |

**Spawning:**

| Field | Type | Default | Description |
| :---- | :---- | :---- | :---- |
| `MaxActiveTargets` | `int` | 1 | Max targets alive simultaneously. 1 \= sequential (wait for resolution). \>1 \= concurrent (spawn on timer). |
| `SpawnInterval` | `float` | 0.5 | Seconds between spawn attempts when below cap. Only meaningful when MaxActiveTargets \> 1\. |
| `TargetLifetime` | `float` | 2.0 | How long a target lives before expiring. |
| `TargetRadius` | `float` | 0.05 | Normalized radius of spawned targets. |

**Scoring:**

| Field | Type | Default | Description |
| :---- | :---- | :---- | :---- |
| `HitPoints` | `int` | 100 | Points per positive target hit. |
| `MissPenalty` | `int` | 50 | Points deducted per miss (misclick or expired target). |
| `NegativePenalty` | `int` | 200 | Points deducted for hitting a negative target. |
| `ComboEnabled` | `bool` | true | Whether combo multiplier is active. |

**Target variety:**

| Field | Type | Default | Description |
| :---- | :---- | :---- | :---- |
| `NegativeTargetChance` | `float` | 0 | Probability (0–1) that a spawned target is negative. 0 disables. |

**Per-mode asset values:**

| Field | Precision | Blitz | Flood |
| :---- | :---- | :---- | :---- |
| `GameModeName` | `precision` | `blitz` | `flood` |
| `DisplayLabel` | `Precision` | `Blitz` | `Flood` |
| `EndCondition` | TimeLimit | TargetCount | TimeLimit |
| `Duration` | 30 | *(unused)* | 30 |
| `TargetGoal` | *(unused)* | 20 | *(unused)* |
| `MaxActiveTargets` | 1 | 1 | 5–8 |
| `SpawnInterval` | *(N/A)* | *(N/A)* | 0.5 |
| `TargetLifetime` | 2.0 | 2.0 | 1.5 |
| `HitPoints` | 100 | *(unused)* | 100 |
| `MissPenalty` | 50 | *(unused)* | 50 |
| `NegativePenalty` | *(unused)* | *(unused)* | 200 |
| `ComboEnabled` | true | false | true |
| `NegativeTargetChance` | 0 | 0 | 0.15–0.2 |

### ScoreKeeper (class, not MonoBehaviour)

Accumulates score for a single session. Pure logic, no Unity dependencies.

**Events:**

| Event | Signature | Description |
| :---- | :---- | :---- |
| `OnScoreChanged` | `Action<int>` | Fires when the total score changes. Carries the new total. |
| `OnComboChanged` | `Action<int>` | Fires when the combo streak changes. Carries the new count. |

**Public state:**

| Property | Type |
| :---- | :---- |
| `CurrentScore` | `int` |
| `ComboCount` | `int` |

**Constructor:** Takes a `GameModeDefinition`. Initializes score and combo to 0\.

**Methods:**

`HitResult RegisterHit(TargetData target, float reactionMs, float clickOffsetNormalized)`

- If `target.Type == Negative`: deduct `NegativePenalty`, reset combo to 0, return HitResult with negative delta.  
- If `target.Type == Positive`:  
  1. Increment combo.  
  2. Calculate base points \= `HitPoints`.  
  3. **Combo multiplier** (if `ComboEnabled` and `ComboCount > 5`): `multiplier = min(ComboCount / 5, 5)`. Hits 1–5 \= 1x, 6–10 \= 2x, 11–15 \= 3x, etc. Capped at 5x. Multiply base points by multiplier.  
  4. **Speed bonus**: if `reactionMs < 1000`, award up to 50 bonus points on a linear falloff. `bonus = (int)(50 * (1 - reactionMs / 1000))`.  
  5. Add total to `CurrentScore`. Fire `OnScoreChanged`.  
  6. Return HitResult with the points delta, combo count, and reaction time.

`void RegisterMiss()`

- Reset combo to 0\. Fire `OnComboChanged`.  
- If `MissPenalty > 0`: deduct from `CurrentScore`. Floor at 0 (no negative scores). Fire `OnScoreChanged`.

**`clickOffsetNormalized` parameter:** Accepted but currently unused. Reserved for a future center-accuracy bonus feature. Flows through the full call chain so the signature is stable when the feature is added.

**Score floor at zero:** `Math.Max(0, ...)` on all deductions. Prevents negative total scores from creating confusing rank/upsert behavior on the server.

**Blitz and ScoreKeeper:** Blitz sessions still route hits through `RegisterHit`. The `CurrentScore` value is accumulated but ignored at submission time — the final score is elapsed milliseconds, computed by GameSession. Routing through ScoreKeeper means the combo counter and events still work, so the HUD can display a streak in Blitz.

### TargetSpawner (class, not MonoBehaviour)

Decides when and where targets appear. Pure logic. Positions are normalized (0–1).

**Constructor:** Takes a `GameModeDefinition`.

**Internal state:**

| Field | Type | Initial | Description |
| :---- | :---- | :---- | :---- |
| `_activeCount` | `int` | 0 | Currently alive targets. |
| `_nextId` | `int` | 0 | Auto-incrementing target ID. |
| `_lastSpawnTime` | `float` | `−∞` | Time.time of last spawn. |
| `_awaitingResolution` | `bool` | false | Sequential mode: blocked until resolved. |

**Methods:**

`TargetData TrySpawn(float currentTime)`

Two spawn behaviors, inferred from `MaxActiveTargets`:

- **Sequential** (`MaxActiveTargets == 1`): spawns one target, then blocks (`_awaitingResolution = true`) until `NotifyResolved()` is called. Used by Precision and Blitz.  
- **Concurrent** (`MaxActiveTargets > 1`): spawns on a timer (respects `SpawnInterval`) up to the cap. Used by Flood.

Returns a new `TargetData` if a spawn should occur, or `null` if conditions aren't met. Logic:

1. If `_activeCount >= MaxActiveTargets` → return null.  
2. If sequential and `_awaitingResolution` → return null.  
3. If concurrent and `currentTime - _lastSpawnTime < SpawnInterval` → return null.  
4. Create target: random position (with edge margin of 0.08 on all sides), roll target type against `NegativeTargetChance`, use mode's lifetime and radius.  
5. Increment `_activeCount`, set `_lastSpawnTime`, set `_awaitingResolution` if sequential.  
6. Return the new TargetData.

`void NotifyResolved()`

- Decrement `_activeCount` (floor at 0).  
- Set `_awaitingResolution = false`.

Must be called for every target that is resolved (hit or expired), by both sequential and concurrent modes.

**No overlap prevention.** `RandomPosition()` doesn't check for overlaps with existing targets. In sequential modes this is impossible (only one target). In Flood, collisions are rare at the current radius. Defer overlap rejection unless playtesting shows it's a visible problem.

**Uses `UnityEngine.Random`**, not `System.Random`. Fine for gameplay. Not suitable for deterministic replay (not a requirement).

### GameSession (MonoBehaviour)

Central state machine. Owns the lifecycle, emits events, delegates to ScoreKeeper and TargetSpawner. Never touches visuals directly.

**States:**

MainMenu → Countdown → Playing → GameOver

**Events:**

| Event | Signature | When |
| :---- | :---- | :---- |
| `OnStateChanged` | `Action<GameState>` | Every state transition. UI panels bind here. |
| `OnTargetSpawned` | `Action<TargetData>` | Core wants a target displayed. |
| `OnTargetHit` | `Action<TargetData, HitResult>` | Target was clicked. |
| `OnTargetExpired` | `Action<TargetData>` | Target lifetime ran out. |
| `OnMisclick` | `Action` | Player clicked empty space. |
| `OnScoreChanged` | `Action<int>` | Forwarded from ScoreKeeper. |
| `OnComboChanged` | `Action<int>` | Forwarded from ScoreKeeper. |
| `OnTimerTick` | `Action<float>` | Fires every frame during Playing. Carries remaining seconds (TimeLimit) or elapsed seconds (TargetCount). |
| `OnGameOver` | `Action<int, string>` | Session ended. Carries final score and game mode name. |

**Public properties:**

| Property | Type |
| :---- | :---- |
| `CurrentState` | `GameState` |
| `ActiveMode` | `GameModeDefinition` |

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_leaderboardService` | `LeaderboardService` |

**Public methods — called by presentation layer:**

`void StartGame(GameModeDefinition mode)`

- Stores mode reference. Creates new ScoreKeeper and TargetSpawner.  
- Subscribes ScoreKeeper events → forward to own events.  
- Resets `_targetsHit` to 0\.  
- Starts `CountdownSequence` coroutine.

`void ReportHit(TargetData target, float clickOffsetNormalized)`

- Guard: return if not `_sessionActive`.  
- Calculate `reactionMs = (Time.time - target.SpawnTime) * 1000f`.  
- Call `_scoreKeeper.RegisterHit(target, reactionMs, clickOffsetNormalized)`.  
- Fire `OnTargetHit`.  
- If Blitz (`EndCondition.TargetCount`): increment `_targetsHit`, end session if `>= TargetGoal`.

`void ReportExpired(TargetData target)`

- Guard: return if not `_sessionActive`.  
- Call `_scoreKeeper.RegisterMiss()`.  
- Fire `OnTargetExpired`.

`void ReportMisclick()`

- Guard: return if not `_sessionActive`.  
- Call `_scoreKeeper.RegisterMiss()`.  
- Fire `OnMisclick`.

`TargetData TrySpawnTarget()`

- Guard: return null if not `_sessionActive`.  
- Call `_spawner.TrySpawn(Time.time)`.  
- If non-null, fire `OnTargetSpawned`.  
- Return the result.

This is **pull-based** — the presentation layer (SpawnManager) calls it on its own Update loop. GameSession doesn't drive spawns via coroutines.

`void NotifyTargetResolved()`

- Forwards to `_spawner.NotifyResolved()`.

`void SubmitScore(int score, string gameMode, Action<ApiResult<ScoreResponse>> callback)`

- Thin wrapper: `StartCoroutine(_leaderboardService.SubmitScore(...))`.  
- Exists so GameOverPanel doesn't need its own LeaderboardService reference.

`void FetchLeaderboard(string gameMode, Action<ApiResult<LeaderboardResponse>> callback, string period = "alltime")`

- Thin wrapper: `StartCoroutine(_leaderboardService.GetScores(...))`.

**Lifecycle — private:**

`IEnumerator CountdownSequence()`

- Set state to `Countdown`.  
- `yield return new WaitForSeconds(3f)`.  
- Call `StartSession()`.

`void StartSession()`

- Set `_timeRemaining` from mode's Duration.  
- Set `_sessionActive = true`.  
- Set state to `Playing`.

`void Update()`

- Guard: return if not `_sessionActive`.  
- **TimeLimit modes**: decrement `_timeRemaining` by `Time.deltaTime`. Fire `OnTimerTick(max(0, remaining))`. End session if `<= 0`.  
- **TargetCount modes** (Blitz): increment `_timeRemaining` by `Time.deltaTime`. Fire `OnTimerTick(elapsed)`. (Hit count checked in `ReportHit`.)

`void EndSession()`

- Set `_sessionActive = false`.  
- Calculate final score:  
  - **TargetCount**: `Mathf.RoundToInt(_timeRemaining * 1000f)` — elapsed milliseconds.  
  - **TimeLimit**: `_scoreKeeper.CurrentScore` — accumulated points.  
- Set state to `GameOver`.  
- Fire `OnGameOver(finalScore, ActiveMode.GameModeName)`.

**Blitz timer direction:** `_timeRemaining` counts *up* in Blitz. The final score is elapsed milliseconds. The server's `ASC` sort order puts the fastest player first. The upsert's "only update if improvement" logic works correctly — lower ms beats higher ms.

---

## Presentation Layer

### TargetView (MonoBehaviour)

Visual representation of a target. Instantiated by SpawnManager.

**Requires:** Collider2D on the same GameObject (for click detection).

**Serialized fields:**

| Field | Type | Default |
| :---- | :---- | :---- |
| `_positiveColor` | `Color` | green-ish (0.2, 0.8, 0.3) |
| `_negativeColor` | `Color` | red-ish (0.9, 0.2, 0.2) |
| `_spawnAnimDuration` | `float` | 0.1 |

**`void Initialize(TargetData data, GameSession session, Vector3 worldPosition, float worldRadius)`**

Called by SpawnManager immediately after instantiation. Sets position, scale (`worldRadius * 2f` — assumes a 1-unit-diameter circle sprite at scale 1), and color based on TargetType.

**`Update()`:**

- Guard: return if `_resolved`.  
- Check lifetime: if `Time.time >= _expiryTime`, call `Resolve()`, report expired to session, call `NotifyTargetResolved()`, destroy GameObject.  
- **Visual feedback hooks** (natural extension points):  
  - Lifetime indicator: fade alpha from 1.0 → 0.3 as time runs out.  
  - Spawn animation: scale up from 0 to full size over `_spawnAnimDuration`.  
  - Shrinking targets: commented-out code path — shrink scale proportionally to remaining lifetime.

**`OnMouseDown()`:**

- Guard: return if `_resolved`.  
- Call `Resolve()`.  
- Calculate click offset: distance from click to target center, normalized by collider radius. 0 \= dead center, 1 \= edge.  
- Report hit to session with offset. Call `NotifyTargetResolved()`.  
- Destroy GameObject (replace with hit animation later).

**`_resolved` guard:** Prevents double-reporting if `OnMouseDown` and the expiry check in `Update` race on the same frame. The first one wins.

**Click detection uses `OnMouseDown`**, which requires a Collider2D but not a Physics2D Raycaster. Simplest path for 2D sprite-based targets. If you later switch to UI-based targets (Canvas), swap to `IPointerClickHandler`.

### SpawnManager (MonoBehaviour)

Polls `GameSession.TrySpawnTarget()` each frame, instantiates TargetView prefabs, converts normalized positions to world coordinates.

**Attach to the play area GameObject.** Requires a `BoxCollider2D` on the same object for bounds derivation. If you resize the collider, spawn positions adjust automatically.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_session` | `GameSession` |
| `_targetPrefab` | `GameObject` |

**`Awake()`:** Cache `BoxCollider2D.bounds` as `_playBounds`.

**`Update()`:**

1. `TargetData data = _session.TrySpawnTarget()`.  
2. If null, return.  
3. Convert normalized position to world coords: `Lerp(bounds.min, bounds.max, normalized)`.  
4. Convert normalized radius to world radius: `radius * bounds.size.x`.  
5. `Instantiate(_targetPrefab, worldPos, Quaternion.identity, transform)`.  
6. Call `GetComponent<TargetView>().Initialize(data, session, worldPos, worldRadius)`.

Targets are parented to the play area transform.

### MisclickCatcher (MonoBehaviour)

Catches clicks on empty space. Attach to the **same play area GameObject** as SpawnManager. The BoxCollider2D serves double duty: SpawnManager reads bounds, MisclickCatcher uses it for click detection.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_session` | `GameSession` |

**`OnMouseDown()`:** Call `_session.ReportMisclick()`.

**Relies on sorting order:** Target sprites must render above the play area's sprite. When a click hits a target, `TargetView.OnMouseDown` fires and Unity doesn't propagate to the play area. When nothing is under the click, the play area collider catches it.

### HUD (MonoBehaviour)

In-game heads-up display. Subscribes to GameSession events. No game logic.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_session` | `GameSession` |
| `_scoreLabel` | `TextMeshProUGUI` |
| `_comboLabel` | `TextMeshProUGUI` |
| `_timerLabel` | `TextMeshProUGUI` |
| `_countdownLabel` | `TextMeshProUGUI` |
| `_modeLabel` | `TextMeshProUGUI` |
| `_comboDisplayThreshold` | `int` (default: 2\) |

All TMP fields are null-safe — any field left unassigned is silently skipped. Build the UI incrementally without null reference exceptions.

**Subscribes in `OnEnable`, unsubscribes in `OnDisable`:**

- `OnStateChanged` → show/hide appropriate elements per state.  
- `OnScoreChanged` → update score label (`score.ToString("N0")`).  
- `OnComboChanged` → show combo label only when `>= _comboDisplayThreshold`. Format: `x{count}`.  
- `OnTimerTick` → format depends on mode:  
  - TimeLimit: bare countdown, e.g. `"12.3"` (creates urgency).  
  - TargetCount (Blitz): elapsed with suffix, e.g. `"4.2s"` (reads as stopwatch).

**Countdown display:**

On `GameState.Countdown`, start a coroutine: `3` → wait 1s → `2` → wait 1s → `1` → wait 1s → `GO!`. The `GO!` clears when `GameState.Playing` hides the countdown label.

This runs in parallel with GameSession's `WaitForSeconds(3f)`. Synchronized by state transitions, not coupled directly. If they drift by a frame, it's invisible.

**On `GameState.GameOver`:** Hide all HUD elements. GameOverPanel takes over.

### GameOverPanel (MonoBehaviour)

Shown on game over. Three-phase display:

1. Show local score immediately (no network wait)  
2. Submit to server, show rank/percentile when response arrives  
3. Optionally fetch and display the full leaderboard

This is the primary integration point with LeaderboardService.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_session` | `GameSession` |
| `_finalScoreLabel` | `TextMeshProUGUI` |
| `_scoreContextLabel` | `TextMeshProUGUI` |
| `_rankLabel` | `TextMeshProUGUI` |
| `_percentileLabel` | `TextMeshProUGUI` |
| `_personalBestLabel` | `TextMeshProUGUI` |
| `_statusLabel` | `TextMeshProUGUI` |
| `_leaderboardContainer` | `Transform` |
| `_rowPrefab` | `LeaderboardRow` |
| `_leaderboardError` | `TextMeshProUGUI` |
| `_playAgainButton` | `GameObject` |
| `_leaderboardButton` | `GameObject` |

**Subscribe to `OnGameOver` in `OnEnable`.**

#### Phase 1 — Immediate (HandleGameOver)

Triggered by `OnGameOver(int finalScore, string gameMode)`.

- Activate the panel. Clear all server-response fields.  
- Display the score immediately:  
  - **Blitz:** format as seconds (`finalScore / 1000f` → `"{value:F2}s"`). Context label: `"Time to clear all targets"`.  
  - **Points modes:** format with thousands separator (`.ToString("N0")`). Context label: `"{mode.DisplayLabel} mode"`.  
- Set status to `"Submitting..."`.  
- Hide Play Again and Leaderboard buttons.  
- Call `_session.SubmitScore(finalScore, gameMode, OnScoreSubmitted)`.

#### Phase 2 — Server Response (OnScoreSubmitted)

Callback receives `ApiResult<ScoreResponse>`.

**On failure:**

- Set status to `"Offline — score not submitted"`.  
- Log warning. Show buttons (player can still play again).

**On success:**

The critical subtlety: **the ScoreResponse contains the player's *best* score, not necessarily the score just submitted.** The server upserts — if the player already had a better score, the existing record is returned with its rank and percentile.

- `ScoreResponse.Score` \= player's best score (may differ from submitted score).  
- `ScoreResponse.Rank` \= rank of the best score.  
- `ScoreResponse.Percentile` \= percentile of the best score (100 \= best).

**New personal best detection:** Compare `response.Score == submittedScore`. If equal, it's a new best (the server accepted and stored it). If different, the server kept the old better score.

- Display rank as `"#{rank}"`.  
- Display percentile as `"Top {100 - percentile:F0}%"` (inverted for natural reading).  
- If new best: `"New personal best!"`.  
- If not new best: show stored best, formatted per mode (`"Best: 1,200"` or `"Best: 4.25s"`).  
- Show buttons.

#### Phase 3 — Leaderboard (OnLeaderboardButtonPressed)

User-initiated via button press. Not automatic.

- Hide leaderboard button. Set status to `"Loading leaderboard..."`.  
- Clear existing rows from container.  
- Call `_session.FetchLeaderboard(gameMode, OnLeaderboardReceived)`.

**OnLeaderboardReceived callback:**

Receives `ApiResult<LeaderboardResponse>`.

`LeaderboardResponse` contains:

- `.Scores` — `List<ScoreResponse>` ordered by rank.  
- `.TotalCount` — total players with a score in this mode/period.

Each `ScoreResponse` contains:

- `.Player` — username (resolved server-side from JWT, never client-supplied).  
- `.Score` — the player's best score.  
- `.Rank` — position on the board.  
- `.Percentile` — percentile standing.

**On failure:** Show error, re-enable leaderboard button for retry.

**On success with no scores:** `"No scores yet — you're the first!"`.

**On success:** Instantiate `LeaderboardRow` prefabs into `_leaderboardContainer`. Format score per mode (seconds for Blitz, thousands-separated for points modes).

#### Buttons

- **Play Again** (`OnPlayAgainPressed`): hide panel, clear leaderboard rows, call `_session.StartGame(_session.ActiveMode)`.  
- **View Leaderboard** (`OnLeaderboardButtonPressed`): described above.

### LeaderboardRow (MonoBehaviour)

Prefab for a single leaderboard entry.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_rankLabel` | `TextMeshProUGUI` |
| `_playerLabel` | `TextMeshProUGUI` |
| `_scoreLabel` | `TextMeshProUGUI` |

**`void Populate(int rank, string player, string formattedScore)`**

Sets the three labels. Score formatting is the caller's responsibility (GameOverPanel knows whether to show seconds or points).

### MainMenu (MonoBehaviour)

Title screen. Two responsibilities: silent auth and mode selection.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_session` | `GameSession` |
| `_leaderboardService` | `LeaderboardService` |
| `_modes` | `List<GameModeDefinition>` |
| `_modeButtonContainer` | `Transform` |
| `_modeButtonPrefab` | `ModeButton` |
| `_titleLabel` | `TextMeshProUGUI` |
| `_statusLabel` | `TextMeshProUGUI` |
| `_playButton` | `GameObject` |

**`OnEnable()`:**

1. Set title to `"Flick Fest"`.  
2. Hide play button. Set status to `"Connecting..."`.  
3. Build mode selector buttons from `_modes` list.  
4. Call `_leaderboardService.EnsureAuthenticated(OnAuthenticated)`.

**Mode definitions are local, not fetched from the server.** The server's `GET /game_modes` returns `{name, sort_order, label, requires_auth}`. That's sufficient for server-side concerns but insufficient for gameplay — the client needs spawn rules, scoring params, end conditions, etc. So the client ships `GameModeDefinition` ScriptableObject assets.

The server endpoint is useful for future validation (confirm modes are registered) but not needed to play.

**`OnAuthenticated(ApiResult<bool> result)`:**

- On success: clear status. Enable play button if a mode is selected.  
- On failure: set status to `"Offline mode — scores won't be saved"`. **Still enable play.** Don't gate gameplay on network availability.

**Mode selection:**

- Default to first mode in the list on panel enable.  
- `ModeButton` instances call back with their `GameModeDefinition` on click.  
- Play button enables once both a mode is selected and auth has resolved (or failed).

**`OnPlayPressed()`:** Hide menu panel, call `_session.StartGame(_selectedMode)`.

**`Show()`:** Re-enables the panel. `OnEnable` fires, which re-runs auth (cheap — immediate callback if token is stored) and rebuilds the mode selector. Handles the edge case of token expiry during a long play session.

### ModeButton (MonoBehaviour)

Individual mode selector button. Instantiated by MainMenu from a prefab.

**Serialized fields:**

| Field | Type |
| :---- | :---- |
| `_label` | `TextMeshProUGUI` |

**`void Initialize(GameModeDefinition mode, Action<GameModeDefinition> onSelected)`:**

Stores mode and callback. Sets label text to `mode.DisplayLabel`.

**`void OnClick()`:** Wire to Button.onClick in the prefab. Invokes the stored callback.

---

## Network Integration Summary

Only two touchpoints with LeaderboardService:

### 1\. Authentication (MainMenu)

- `EnsureAuthenticated` on menu enable.  
- If token exists in PlayerPrefs → immediate callback, no network call.  
- If no token → `GuestLogin()` silently creates a guest account, stores tokens.  
- Player never sees a login screen.

### 2\. Score Submission & Leaderboard (GameSession → GameOverPanel)

GameSession exposes two wrapper methods so GameOverPanel doesn't need its own LeaderboardService reference:

**`SubmitScore(int score, string gameMode, callback)`**

- Calls `_leaderboardService.SubmitScore(score, gameMode, callback)`.  
- `score` is an integer: points for Precision/Flood, milliseconds for Blitz.  
- `gameMode` is the string key (`"precision"`, `"blitz"`, `"flood"`).  
- Server upserts: only stores if it's an improvement (DESC: higher, ASC: lower).  
- Response is the player's *best* score with rank and percentile.

**`FetchLeaderboard(string gameMode, callback, string period)`**

- Calls `_leaderboardService.GetScores(gameMode, callback, period)`.  
- Default period is `"alltime"`. Also supports `"daily"` and `"weekly"`.  
- Response is a ranked list of ScoreResponse objects.

**No network calls during gameplay.** The game is fully playable offline — it just can't submit scores.

---

## Compatible But Not Required Features

All of these live entirely in the presentation layer. None affect core scoring logic or the network contract.

| Feature | Where it plugs in | Core impact |
| :---- | :---- | :---- |
| Growing/shrinking targets | `TargetView.Update()` — animate `localScale` based on remaining lifetime | None |
| Center-accuracy bonus | `TargetView.OnMouseDown()` already computes `clickOffsetNormalized` and passes it through. Activate in `ScoreKeeper.RegisterPositiveHit()` when ready. | ScoreKeeper gets a multiplier based on the float. |
| Sound effects | `TargetView` hit/miss/expire events. Add an `AudioManager` singleton or per-target AudioSource. | None |
| Visual effects | `TargetView` — particle burst on hit, shake on miss. Delay `Destroy()` to let animations play. | None |
| Positive/negative target visuals | `TargetView.Initialize()` already sets color by type. Extend with different sprites, outlines, icons. | None |

---

## Scene Setup Notes

- **GameSession**: persistent GameObject, references LeaderboardService.  
- **LeaderboardService**: persistent GameObject with LeaderboardConfig ScriptableObject assigned.  
- **Play area**: GameObject with BoxCollider2D, SpawnManager, and MisclickCatcher. Collider defines spawn bounds AND catches empty-space clicks.  
- **Target prefab**: sprite with CircleCollider2D and TargetView. Sorting order must be higher than the play area's.  
- **UI Canvas**: MainMenu panel, HUD panel, GameOverPanel (initially inactive). All reference GameSession.  
- **Three GameModeDefinition assets**: create via Assets → Create → FlickFest → Game Mode. Assign to MainMenu's `_modes` list in the order you want them displayed.


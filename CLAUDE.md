# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**The Paper Weaver's Tale** is a 2D narrative puzzle adventure game built in Unity 2022.3.62f2 (LTS). The game features a main hub scene and a combined minigame scene that runs two puzzles in sequence: **Kintsugi** (gold seam tracing) → **Fold It** (paper folding).

## Unity Workflow

This is a Unity project — there are no standalone build/lint/test CLI commands. Development happens through the Unity Editor.

- **Open project:** Launch Unity Hub → open the project folder
- **Scenes:** `Assets/Scenes/MainScene.unity` (hub), `Assets/Scenes/MiniGameScene.unity` (combined sequence)
- **Standalone fallbacks:** `Assets/Scenes/FoldItScene.unity`, `Assets/Scenes/Kitsungi.unity`
- **Play/test:** Use Unity Editor Play Mode
- **Build:** File → Build Settings in the Unity Editor (recommended order: Main=0, MiniGame=1, FoldIt=2, Kitsungi=3)
- **Tests:** `com.unity.test-framework` is included; run via Window → General → Test Runner
- **IDE:** Visual Studio or JetBrains Rider (both configured in manifest)

## Architecture

### Full Game Flow

```
[MainScene loads — Level 1]
  CharacterMovement walks path[] waypoints automatically (no button press needed)
  On stopHere waypoint → StopMoving() + GameEvents.BreakpointReached()
    → Moveable.HandleBreakpointReached() activates obstacles (Rock/Tree/Door/Barrel)
  On playMinigameHere waypoint → MinigameMenuController.OpenMenu(level) directly

[Player interacts with Moveables]
  Click Rock/Tree → DOTween slides obstacle aside → CollectibleAnimator.Activate()
  Click Door → doorOpenChild activated → CollectibleAnimator.Activate()
  Click Barrel → linkedCollectible activated immediately (no click needed)
  Click collectible → CollectibleAnimator.OnMouseDown
    → CollectibleManager.Collect(go, levelIndex) — increments count, updates HUD
  Click gold item → CollectibleManager.CollectGoldItem()
  All regular AND gold collected → MinigameMenuController.OpenMenu(_currentLevelIndex)

[MinigameMenuController.OpenMenu(int level)]
  → GameEvents.CurrentLevel = level
  → dimOverlay activates (black loading screen)
  → SceneManager.LoadSceneAsync("MiniGameScene", LoadSceneMode.Additive)
  → loadOp.completed: ConfigureMinigameCamera(), sets isEmbedded = true on MiniGameSequenceController

[MiniGameScene runs (additively, MainScene stays loaded)]
  MiniGameSequenceController.Start() (exec order +100):
    → reads GameEvents.CurrentLevel
    → calls kintsugiGenerator.Initialize(level) + foldItGenerator.Initialize(level)
    → (currently: kintsugiRoot hidden, FoldIt shown immediately for testing)

  [FoldIt phase]
    Player swipes paper piece → GameController.ProcessSwipe → PaperFold.RotatePaper()
    Correct order: animate fold, increment currentOrderIndex
    Incorrect: RotateBackToIndex(0) undoes all folds
    All folds correct → GameController fires onComplete

  [MiniGameSequenceController.OnFoldItComplete → GameEvents.MinigameWon()]

[MinigameMenuController.HandleMinigameWon() → WinSequence coroutine]
  → UnloadSceneAsync("MiniGameScene")
  → restore main camera culling mask, hide dimOverlay
  → victoryText shown for victoryDisplayDuration seconds
  → GameEvents.LevelProgression(completedLevel)

[LevelProgressionController.OnLevelProgression(completedLevel)]
  Level 1 complete:
    → swap collectibles/moveables to level-2 set
    → BridgeRepairReward.Play() → SwapBackground(1) → characterMovement.ResumeMoving()

  Level 2 complete:
    → swap to level-3 background + objects
    → CatSequenceController.PlaySequence() → characterMovement.ResumeMoving()
    → _waitingForL3UnlockPoint = true; next breakpoint reveals Level3A batch

  Level 3 complete (after two sequential collection batches 3A → 3B):
    → brokenBoat activated, SwapBackground(3)
    → characterMovement.ResumeMoving() → next breakpoint triggers BoatMovement.StartSailing()
    → character parented to boat, sails to moveTarget

[LevelProgressionController.OnBreakpointReached]
  If _waitingForL3UnlockPoint: reveal Level3A collectibles/moveables; set override → RevealLevel3B
  If _waitingForBoatBreakpoint: BoatMovement.StartSailing()
```

### Script Organization (`Assets/Scripts/`)

```
Scripts/
├── Core/
│   ├── GameEvents.cs                  # Static event bus + CurrentLevel int
│   └── CameraAutoFit.cs               # [ExecuteAlways] orthographic fit to background sprite
├── MainScene/
│   ├── CharacterMovement.cs           # Path-based locomotion; PathPoint[] waypoints with flags
│   ├── MinigameMenuController.cs      # Singleton: additive load/unload, camera composite, UI
│   ├── CollectibleAnimator.cs         # Visual pulse + OnMouseDown → CollectibleManager.Collect
│   ├── CollectibleLevel.cs            # Data container: levelIndex (legacy, mostly replaced)
│   ├── CollectibleManager.cs          # Singleton: count, gold gate, HUD, SetupForLevel
│   ├── LevelProgressionController.cs  # Orchestrates 3-level MainScene flow
│   ├── Moveable.cs                    # Click-to-move obstacles (Rock/Barrel/Tree/Door)
│   ├── GoldCollectibleItem.cs         # Gold item (special collectible, triggers CollectGoldItem)
│   ├── BridgeRepairReward.cs          # Animated bridge-repair reward cutscene
│   ├── CatSequenceController.cs       # Cat animation sequence between level 2→3
│   ├── BoatMovement.cs                # DOTween boat sails to target; parents character to boat
│   └── BoatRepairReward.cs            # Animated boat-repair reward → reparents sprite to boat
├── MiniGame/
│   └── MiniGameSequenceController.cs  # Orchestrates Kintsugi→FoldIt; exec order +100
├── MiniGameFoldIt/
│   ├── GameController.cs              # Fold puzzle manager, swipe input, onComplete Action
│   ├── PaperFold.cs                   # Per-piece fold animation + order validation
│   ├── ClickDetector.cs               # Data container (holds PaperFold ref)
│   ├── FoldItLevelData.cs             # JSON data models
│   └── FoldItPuzzleGenerator.cs       # Builds quad meshes + PaperFold components from JSON
└── MiniGameKintsugi/
    ├── Triangulator.cs
    ├── KintsugiPuzzleConfig.cs
    ├── KintsugiPiece.cs
    ├── KintsugiPuzzleGenerator.cs     # Creates meshes + gold seam LineRenderers from JSON
    └── KintsugiGameController.cs      # onComplete Action; seam tracing input
```

> Full per-script API reference: see `SCRIPTS.md`.

### Core Systems

**`GameEvents.cs`** — Static event bus. Three events:
- `OnMinigameWon` / `MinigameWon()` — fired by `MiniGameSequenceController` on puzzle complete.
- `OnBreakpointReached` / `BreakpointReached()` — fired when character reaches a `stopHere` waypoint; activates Moveables.
- `OnLevelProgression(int)` / `LevelProgression(int)` — fired after victory display; `LevelProgressionController` handles level transitions.
- `CurrentLevel` (int) — set by `MinigameMenuController.OpenMenu()` before async load.

**`MinigameMenuController.cs`** — Singleton (`Instance`). `OpenMenu(int level)` sets `CurrentLevel`, additively loads `MiniGameScene`, calls `ConfigureMinigameCamera()` (depth compositing + culling masks), sets `isEmbedded = true` on `MiniGameSequenceController`. `WinSequence()`: unloads scene, restores camera, shows `victoryText`, then fires `GameEvents.LevelProgression(completedLevel)`.

**`CharacterMovement.cs`** — Path-based locomotion. `PathPoint[]` array; each point has flags: `stopHere` (fires `BreakpointReached`), `rotateHere` (swaps sprite), `playMinigameHere` (opens minigame directly). Moves via `Vector2.MoveTowards` in `Update()` (no Rigidbody2D). `ResumeMoving()` — public; called by `LevelProgressionController` after rewards/transitions.

**`CollectibleManager.cs`** — Singleton. `SetupForLevel(collectibles[], levelIndex)` — called by `LevelProgressionController` to register current batch. `Collect()` gates completion on BOTH `_collectedCount >= totalCollectibles` AND `_goldItemCollected`. `CollectGoldItem()` sets `_goldItemCollected`, checks completion. `onAllCollectedOverride` Action — if set, called instead of `OpenMenu` (used by `LevelProgressionController` for sequential Level 3 batches); auto-cleared after use. `goldHudText` TMP field for gold counter HUD.

**`LevelProgressionController.cs`** — Orchestrates the 3-level MainScene flow. Subscribes to `OnLevelProgression` + `OnBreakpointReached`. Manages collectible/moveable group swaps, background swaps, reward cutscenes, and character resume between levels. Level 3 has two sequential collection batches (3A → 3B) via `onAllCollectedOverride` chain. After level 3: shows broken boat → `BoatRepairReward.Play()` → next breakpoint → `BoatMovement.StartSailing()`.

**`Moveable.cs`** — Click-to-interact obstacle component. `MoveableType`: `Rock` (slides out, DOTween OutQuad; returns InQuad after collect), `Tree` (slides out, OutBack; returns InBack), `Door` (activates `doorOpenChild`), `Barrel` (collectible visible immediately, no click needed). `HandleBreakpointReached()` — activated by `OnBreakpointReached` event. `MakeInteractable()` — called directly by `LevelProgressionController` for mid-game reveals. `linkedCollectible` + `linkedGoldCollectible` wired in Inspector; calls `OnCollectableCollected()` to track when to revert.

**`MiniGameSequenceController.cs`** — Exec order +100 (sub-controllers initialize first). Phases: `Kintsugi → FoldIt → Win`. Sets `onComplete` delegates on each sub-controller instead of using `isEmbedded`. Fields: `kintsugiRoot`, `foldItRoot`, `kintsugiController`, `foldItController`, `kintsugiGenerator`, `foldItGenerator`, `transitionText`, `transitionDuration=2.5`.

### Boat Systems (`Assets/Scripts/MainScene/`)

**`BoatMovement.cs`** — `StartSailing()`: parents `character` Transform to the boat (world position stays), then DOTweens the boat to `moveTarget.position` over `moveDuration` seconds. Called by `LevelProgressionController` on the post-level-3 breakpoint.

**`BoatRepairReward.cs`** — Animated reward cutscene (DOTween Sequence): scales up + spins `rewardRenderer`, shrinks back, snaps to `snapTarget`. On complete: swaps to `finalSprite`, reparents sprite to `reparentTo` (the boat GO) so it sails away. After 1s delay, fires `onComplete`. Fields: `rewardRenderer`, `finalSprite`, `snapTarget`, `reparentTo`, `scaleDuration`, `scaleBackDuration`, `snapDuration`, `bigScale`, `snapScale`.

### Fold It Minigame (`Assets/Scripts/MiniGameFoldIt/`)

**`GameController.cs`** — Ordered list of `PaperFold` objects, `currentOrderIndex`, `clickBlocked` (blocks input during animation). `onComplete` Action called on all-folds-correct (set by `MiniGameSequenceController`). `ReinitializePieces()` repopulates `_allFolds` after `FoldItPuzzleGenerator` runs.

**`PaperFold.cs`** — `orderIndex`, `rotAxis`, `angleChange`, `rotDuration`, `corners`, `mainLine`/`lines` (LineRenderers). Null guards on `mainLine`/`lines`. `RotatePaper(bool back)` entry point; `FoldAroundAxis()` coroutine with AnimationCurve easing.

**`FoldItPuzzleGenerator.cs`** — `Initialize(level)` loads `Assets/Resources/FoldIt/Level{N}.json`, clears `piecesRoot` children, builds quad meshes in `FoldItRoot` LOCAL XZ plane (`y=DepthOffset=0.01`), adds MeshCollider (layer=0), adds PaperFold, calls `gameController.ReinitializePieces()`. **`piecesRoot` must be a dedicated child GO of FoldItRoot** (e.g. "Pieces") — never FoldItRoot itself (generator would destroy itself).

**FoldItRoot coordinate system:** `-90°` X rotation. Local XZ → world XY. Winding `{0,3,1,1,3,2}` gives local +Y normals (camera-visible). Material must be Cull Off so back face is visible pre-fold.

### Kintsugi Minigame (`Assets/Scripts/MiniGameKintsugi/`)

**`KintsugiGameController.cs`** — `onComplete` Action set by `MiniGameSequenceController`. `SeamTraceState` (top-level class in same file): `goldLR`, `guideLR`, `points[]`, `progress`, `isActive`. `traceProximity=0.25` world units. `TryPickUp` returns bool; if false, `TryStartTrace` called. `TryCheckCompletion()` gates win on all seams fully traced.

**`KintsugiPuzzleGenerator.cs`** — `Initialize(level)` loads `Assets/Resources/Kintsugi/Level{N}.json` (L1=4 pieces, L2=5, L3=6). `CreateGoldSeams()` creates two LineRenderers per seam, calls `gameController.SetSeams()`. Snap calls `ActivateAdjacentSeams()` on `KintsugiPiece`.

### JSON Level Data

All level files live under `Assets/Resources/<MinigameName>/` and are loaded via `Resources.Load<TextAsset>`.

#### Kintsugi — `Assets/Resources/Kintsugi/Level{N}.json`

Fields: `levelIndex`, `pieceRects[]` (`x,y,w,h` in 0–1 UV space), `puzzleWorldWidth/Height`, `tearSubdivisions`, `tearAmplitude`, `randomSeed`, `snapThreshold`, `snapDuration`, `scatterRadius`, `goldSeamWidth`.

| Level | Pieces | puzzleWorld | tearAmplitude | snapThreshold | scatterRadius | randomSeed |
|-------|--------|-------------|---------------|---------------|---------------|------------|
| 1     | 4      | 2×2         | 0.12          | 2.0           | 2.5           | 42         |
| 2     | 5      | 2×2         | 0.08          | 1.5           | 6.0           | 137        |
| 3     | 6      | 2×2         | 0.08          | 1.0           | 2.5           | 271        |

Piece layout (UV rects):
```
Level 1: 4 pieces — 2×2 grid (top-left, top-right, bottom-left, bottom-right)
Level 2: 5 pieces — top row 2 pieces (halves), bottom row 3 pieces (thirds)
Level 3: 6 pieces — 3 rows × 2 columns (each row 1/3 height)
```

#### Fold It — `Assets/Resources/FoldIt/Level{N}.json`

Top-level fields: `levelIndex`, `textureIndex` (into `levelTextures[]`), `backTextureIndex` (into `backTextures[]`), `puzzleWorldWidth`, `puzzleWorldHeight`, `maxRotations`.

Each entry in `pieces[]`:

| Field                  | Type        | Description                                                        |
|------------------------|-------------|--------------------------------------------------------------------|
| `uvX, uvY, uvW, uvH`   | float       | UV-space rect (0–1); multiplied by puzzleWorld to get world size   |
| `foldEdge`             | string      | `"top"`, `"bottom"`, `"left"`, `"right"`, or `"companion"`        |
| `angleChange`          | float       | Degrees to rotate (180 = full fold)                                |
| `rotDuration`          | float       | Animation duration in seconds                                      |
| `rotationOrderIndices` | int[]       | Which sequence indices this piece is valid for (empty on companions)|
| `swipeDirX, swipeDirY` | float       | Screen-space swipe direction to fold (negated in generator)        |
| `companions`           | int[]       | Indices of companion pieces that fold with this edge piece         |
| `isCompanion`          | bool        | True for corner pieces driven by adjacent edge folds               |

Piece layout (8 pieces each: 4 edge + 4 corner companions):
```
Layout (UV space, Y=0 bottom):

  [4:TL corner] [0:top edge  ] [5:TR corner]     ← Level 1 (uvY 0.75–1.0)
  [1:left edge] [   CENTER   ] [2:right edge]     ← Level 1 (uvY 0.25–0.75)
  [6:BL corner] [3:bottom edge] [7:BR corner]    ← Level 1 (uvY 0.0–0.25)

Corner companion relationships:
  top edge (0)    → companions [4,5]  (TL, TR corners)
  left edge (1)   → companions [4,6]  (TL, BL corners)
  right edge (2)  → companions [5,7]  (TR, BR corners)
  bottom edge (3) → companions [6,7]  (BL, BR corners)
```

| Level | puzzleWorld | Fold order (rotationOrderIndices) | textureIndex |
|-------|-------------|-----------------------------------|--------------|
| 1     | 2×2         | top→left→right→bottom (0,1,2,3)   | 0            |
| 2     | 3×3         | bottom→top→left→right (0,1,2,3) — note: bottom is piece[0] here | 1 |
| 3     | 3×3         | same as Level 2                   | 2            |

### Assets Layout

```
Assets/
├── Scripts/           # See Script Organization above
├── Scenes/            # MainScene.unity, MiniGameScene.unity, FoldItScene.unity, Kitsungi.unity
├── Meshes/            # 9 FBX meshes: base, top, bottom, left, right, + 4 corners
├── Materials/         # Paper.mat, Base.mat, Plane.mat, CustomDashLine.mat
│                      #   + DashLineShader.shader
├── Prefabs/           # Collectible.prefab
├── Sprites/
│   ├── Boat/          # boat-broken.png, boat-pieces-1/2.png, kintsugi variants, coord sheets
│   ├── Suns/          # Sun sprite variants
│   └── (other 2D character and background art)
├── Sounds/            # Bg.mp3 (background music)
├── Resources/
│   ├── Kintsugi/      # Level1.json, Level2.json, Level3.json
│   └── FoldIt/        # Level1.json, Level2.json, Level3.json
├── TIleMaps/ & Tiles/ # Tilemap palette for MainScene
└── MiniGame/          # MiniGame.unitypackage (exported for reuse)
```

### Key Packages

- `com.unity.feature.2d` — 2D tools (sprites, tilemaps, animation, PSD/Aseprite importers)
- `com.unity.textmeshpro` — UI text (TMP used for transition/victory/instruction text)
- `com.unity.timeline` — cutscene/animation timelines
- `com.unity.visualscripting` — visual scripting nodes

## Conventions

- Scripts live under `Assets/Scripts/<SystemName>/` — new minigames follow the `MiniGameFoldIt` folder pattern
- New cross-cutting events go in `Assets/Scripts/Core/GameEvents.cs`
- **Minigame completion:** `onComplete` delegate checked first (set by `MiniGameSequenceController`); fallback to `isEmbedded`→`GameEvents.MinigameWon()` or `SceneManager.LoadScene`
- **Level progression:** `MinigameMenuController.WinSequence()` fires `GameEvents.LevelProgression(level)` after victory text; `LevelProgressionController` handles the transition
- **Collectible gating:** `CollectibleManager` requires ALL regular collectibles AND the gold item before opening the minigame; use `SetupForLevel()` + `onAllCollectedOverride` for sequential batches
- **Moveables:** activated via `GameEvents.OnBreakpointReached` (for the main stop) or `MakeInteractable()` directly (for mid-game reveals); use DOTween; require `linkedCollectible` or `linkedGoldCollectible`
- `DashLineShader.shader` drives fold guide visuals via `LineRenderer`; `_AnimSpeed` property (0–4) controls dash scroll speed
- Scene transitions use `SceneManager`; minigames load **additively** so MainScene stays active
- `MiniGameSequenceController` script execution order = **+100** (sub-controllers must initialize first)
- `piecesRoot` for FoldIt generator must be a dedicated child GO — never the root that holds GameController
- Level JSON files live in `Assets/Resources/<MinigameName>/` and are loaded via `Resources.Load<TextAsset>`

## MiniGameScene Editor Wiring Checklist

1. Hierarchy: Main Camera, Canvas > TransitionText (TMP, inactive), KintsugiRoot, FoldItRoot, SequenceController GO.
2. Assign all refs on SequenceController: `kintsugiRoot`, `foldItRoot`, `kintsugiController`, `foldItController`, `kintsugiGenerator`, `foldItGenerator`, `transitionText`, `transitionDuration=2.5`.
3. On `KintsugiPuzzleGenerator`: assign `levelTextures[]`, `goldSeamMaterial`, `snapCurve` (EaseInOut).
4. Add `FoldItPuzzleGenerator` to a child GO inside FoldItRoot ("FoldItGenerator"); assign `levelTextures[]`, `paperMaterialTemplate` (Paper.mat), `gameController`, `piecesRoot` (dedicated "Pieces" child GO).
5. Edit > Project Settings > Script Execution Order: `MiniGameSequenceController = +100`.
6. File > Build Settings: Main=0, MiniGame=1, FoldIt=2, Kitsungi=3.
7. In MainScene, add `CollectibleLevel` component to each Collectible; set `levelIndex` (1/2/3).

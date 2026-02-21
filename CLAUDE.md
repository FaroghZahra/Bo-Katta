# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**The Paper Weaver's Tale** is a 2D narrative puzzle adventure game built in Unity 2022.3.62f2 (LTS). The game features a main hub scene and minigames — the first being "Fold It", a paper-folding puzzle.

## Unity Workflow

This is a Unity project — there are no standalone build/lint/test CLI commands. Development happens through the Unity Editor.

- **Open project:** Launch Unity Hub → open the project folder
- **Scenes:** `Assets/Scenes/MainScene.unity` (hub), `Assets/Scenes/FoldItScene.unity` (minigame)
- **Play/test:** Use Unity Editor Play Mode
- **Build:** File → Build Settings in the Unity Editor
- **Tests:** `com.unity.test-framework` is included; run via Window → General → Test Runner
- **IDE:** Visual Studio or JetBrains Rider (both configured in manifest)

## Architecture

### Full Game Flow

```
[MainScene loads]
  CharacterMovement calculates bridge slope to Collectible target
  MinigameMenuController subscribes to GameEvents.OnMinigameWon

[Player presses Forward button]
  CharacterMovement.StartMoving() → Rigidbody2D moves toward Collectible

[Character's trigger collides with Collectible tag]
  MinigameMenuController.OpenMenu()
    → overlayPanel activates (black loading screen)
    → SceneManager.LoadSceneAsync("FoldItScene", LoadSceneMode.Additive)
    → GameController.isEmbedded = true

[FoldItScene runs (additively, MainScene stays loaded)]
  User clicks paper piece → Raycast hits ClickDetector
    → PaperFold.RotatePaper() validates order index
    → Correct: animate fold, increment currentOrderIndex
    → Incorrect: reset currentOrderIndex = 0 (undo all folds)
  All folds correct → GameEvents.MinigameWon() fires

[MinigameMenuController.HandleMinigameWon()]
  → SceneManager.UnloadSceneAsync("FoldItScene")
  → collectibleObject.SetActive(false)
  → victoryText shown for 3 seconds
```

### Script Organization (`Assets/Scripts/`)

```
Scripts/
├── Core/
│   └── GameEvents.cs           # Static event bus
├── MainScene/
│   ├── CharacterMovement.cs    # 2D character locomotion
│   └── MinigameMenuController.cs  # Singleton: minigame embed/unload + UI
└── MiniGameFoldIt/
    ├── GameController.cs       # Fold puzzle manager, raycast input
    ├── PaperFold.cs            # Per-piece fold animation + order validation
    └── ClickDetector.cs        # Collider data container (holds PaperFold ref)
```

### Core Systems

**`GameEvents.cs`** — Static event bus decoupling minigame win from scene management. Any minigame calls `GameEvents.MinigameWon()` to signal completion; `MinigameMenuController` handles the response. New minigames must call this instead of managing scene transitions themselves.

**`MinigameMenuController.cs`** — Singleton (`Instance` static property). Manages the additive load/unload lifecycle of minigame scenes. Key fields: `minigameSceneName` (defaults to `"FoldItScene"`), `overlayPanel`, `victoryText`, `victoryDisplayDuration` (3s), `collectibleObject`. Subscribes/unsubscribes to `GameEvents.OnMinigameWon` in `OnEnable`/`OnDisable`.

**`CharacterMovement.cs`** — Moves the 2D character along the bridge slope (slope calculated from spawn position to collectible at `Start()`). `StartMoving()` is wired to the Forward button's `OnClick`. `OnTriggerEnter2D` checks for `"Collectible"` tag to trigger `MinigameMenuController.OpenMenu()`.

### Fold It Minigame (`Assets/Scripts/MiniGameFoldIt/`)

**`GameController.cs`** — Holds ordered list of `PaperFold` objects, tracks `currentOrderIndex`, blocks clicks during animation via `clickBlocked`. Key fields: `rotationCurve` (AnimationCurve for easing), `maxRotations`, `isEmbedded` (set by MinigameMenuController — suppresses its own scene transition). On completion calls `GameEvents.MinigameWon()` when embedded, or `SceneManager.LoadScene(nextSceneName)` when standalone.

**`PaperFold.cs`** — Per-piece folding logic. Key fields: `orderIndex` (sequence position), `rotationOrderIndices` (valid order positions for this piece), `rotAxis`, `angleChange`, `rotDuration`, `corners` (child GameObjects used as rotation anchors), `mainLine`/`lines` (LineRenderers for fold guides). `RotatePaper(bool back)` is the entry point; `FoldAroundAxis()` is the animation coroutine using AnimationCurve easing. Supports undo (`back=true`) for sequence reset.

**`ClickDetector.cs`** — Data container only. Holds a `PaperFold` reference; `GameController` raycasts to find it via `Physics.Raycast`.

### Adding a New Minigame

1. Create `Assets/Scripts/MiniGame<Name>/` with a `GameController` that calls `GameEvents.MinigameWon()` on completion and respects an `isEmbedded` flag (skip its own scene load when true).
2. Create the scene and add it to Build Settings.
3. In MainScene, set `MinigameMenuController.minigameSceneName` to the new scene name.
4. Add a new `Collectible` trigger that calls `MinigameMenuController.OpenMenu()`.

### Assets Layout

```
Assets/
├── Scripts/           # See Script Organization above
├── Scenes/            # MainScene.unity + FoldItScene.unity
├── Meshes/            # 9 FBX meshes: base, top, bottom, left, right, + 4 corners
├── Materials/         # Paper.mat, Base.mat, Plane.mat, CustomDashLine.mat
│                      #   + DashLineShader.shader (animated UV scroll, transparent)
├── Prefabs/           # Collectible.prefab
├── Sprites/           # 2D character and background art
├── TIleMaps/ & Tiles/ # Tilemap palette for MainScene
└── MiniGame/          # MiniGame.unitypackage (exported for reuse)
```

### Key Packages

- `com.unity.feature.2d` — 2D tools (sprites, tilemaps, animation, PSD/Aseprite importers)
- `com.unity.textmeshpro` — UI text (`victoryText` is a `TextMeshProUGUI`)
- `com.unity.timeline` — cutscene/animation timelines
- `com.unity.visualscripting` — visual scripting nodes

## Conventions

- Scripts live under `Assets/Scripts/<SystemName>/` — new minigames follow the `MiniGameFoldIt` folder pattern
- New cross-cutting events go in `Assets/Scripts/Core/GameEvents.cs`
- Paper mesh pieces are separate FBX files per face/corner, composited in the scene hierarchy
- `DashLineShader.shader` drives fold guide visuals via `LineRenderer`; `_AnimSpeed` property (0–4) controls dash scroll speed
- Scene transitions use `UnityEngine.SceneManagement.SceneManager`; minigames load **additively** so MainScene stays active

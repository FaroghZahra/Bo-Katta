# Script Reference — Boo Katta

Complete description of every C# script under `Assets/Scripts/`. Written for Claude so it never has to re-read a file just to understand its purpose.

---

## `Assets/Scripts/Core/`

### `GameEvents.cs`
**What it is:** A pure static event bus — no MonoBehaviour, no scene presence.

**What it does:**
- Declares `static event Action OnMinigameWon` — any listener subscribes to this to know when a minigame ends.
- `MinigameWon()` — static helper that fires the event. Every minigame calls this (or delegates through `onComplete`) to signal completion.
- `CurrentLevel` — static int (default 1). `MinigameMenuController.OpenMenu(level)` sets this before the additive load so `MiniGameSequenceController.Start()` can read which level to generate.

**Used by:** `MinigameMenuController` (subscribes), `MiniGameSequenceController` (fires), `GameController` (fires), `KintsugiGameController` (fires), `CollectibleManager` (subscribes to reset collectibles).

---

### `CameraAutoFit.cs`
**What it is:** A utility MonoBehaviour placed on the Main Camera. Marked `[ExecuteAlways]` so it works in the Editor too.

**What it does:**
- In `Start()`, reads the `background` SpriteRenderer's world bounds.
- Calculates whether the screen is wider or taller than the sprite's aspect ratio.
- Sets `Camera.orthographicSize` so the background fills the screen without cropping (letter-/pillar-box if needed).

**Fields:** `background` (SpriteRenderer) — assign the scene background sprite in the Inspector.

---

## `Assets/Scripts/MainScene/`

### `CharacterMovement.cs`
**What it is:** Controls the 2D player character walking along the bridge in MainScene.

**What it does:**
- Requires `Rigidbody2D`. In `Start()`, calculates a normalised `moveDirection` from the character's spawn position toward `destination` (the Collectible transform), optionally rotated by `directionAngleOffset` degrees.
- `FixedUpdate()`: while `isMoving`, sets `rb.velocity = moveDirection * moveSpeed`.
- `StartMoving()` — public method wired to the Forward button's OnClick in the Inspector. Sets `isMoving = true`.
- `StopMoving()` — public, zeroes velocity.
- **Note:** The old trigger-collision logic for opening the minigame was removed; `CollectibleAnimator.OnMouseDown` now drives minigame opening instead.

**Fields:** `moveSpeed`, `destination` (Transform), `directionAngleOffset` (float, degrees).

---

### `MinigameMenuController.cs`
**What it is:** Singleton (`Instance`) that owns the additive load/unload lifecycle of the minigame scene, camera compositing, and victory UI.

**What it does:**
- `Awake()`: registers singleton, finds the MainScene camera (iterates all cameras, picks the one in this scene — avoids `Camera.main` ambiguity under additive loading).
- `OnEnable/OnDisable`: subscribes/unsubscribes `HandleMinigameWon` to `GameEvents.OnMinigameWon`.
- `OpenMenu(int level)`:
  1. Guards against double-load.
  2. Sets `GameEvents.CurrentLevel = level`.
  3. Activates `dimOverlay` (black loading screen).
  4. Calls `SceneManager.LoadSceneAsync(minigameSceneName, Additive)`.
  5. On `loadOp.completed`: calls `ConfigureMinigameCamera()`, then finds `MiniGameSequenceController` and sets `isEmbedded = true` on it (or falls back to `GameController` / `KintsugiGameController`).
- `ConfigureMinigameCamera()`: Finds every camera NOT in MainScene and sets it to `ClearFlags.Depth` at `mainDepth + 1`, restricts its culling mask to layers "MiniGame" (9) and "KintsugiPieces" (8). Also removes those layers from the main camera's culling mask so each camera only renders its own objects.
- `HandleMinigameWon()` → `WinSequence()` coroutine: restores main camera culling mask, unloads the minigame scene, hides `dimOverlay`, shows `victoryText` for `victoryDisplayDuration` seconds.

**Fields:** `dimOverlay`, `victoryText` (TMP), `minigameSceneName` (default "MiniGameScene"), `victoryDisplayDuration` (default 3s).

---

### `CollectibleLevel.cs`
**What it is:** A tiny data container (3 lines). No logic.

**What it does:** Holds `levelIndex` (int). Placed on each Collectible GameObject in MainScene. `CollectibleAnimator.OnMouseDown` reads it to pass the right level number to `CollectibleManager.Collect()`.

---

### `CollectibleAnimator.cs`
**What it is:** Visual + input handler for individual Collectibles. Depends on DOTween.

**What it does:**
- `Start()`: starts an infinite Yoyo scale pulse (`pulseScale=1.15, pulseDuration=0.6s`) via `DOTween`.
- `OnDisable / OnDestroy`: kills the DOTween on this transform to prevent leaks.
- `OnMouseDown()`: reads `CollectibleLevel.levelIndex` from the same GameObject (defaults to 1 if missing), calls `CollectibleManager.Instance.Collect(gameObject, levelIndex)`.
- `Reactivate()`: kills existing tween, resets scale to 1, restarts pulse — called by `CollectibleManager.ResetAll()` after a minigame win.

---

### `CollectibleManager.cs`
**What it is:** Singleton tracking how many collectibles the player has picked up. Drives the HUD counter and triggers the minigame.

**What it does:**
- `Awake()`: finds all GameObjects tagged "Collectible" via `FindGameObjectsWithTag`, builds `_allCollectibles` list and sets `totalCollectibles` from the count.
- `Start()`: calls `UpdateHUD()`.
- `OnEnable/OnDisable`: subscribes/unsubscribes `ResetAll` to `GameEvents.OnMinigameWon`.
- `Collect(GameObject go, int levelIndex)`: increments `_collectedCount`, deactivates the collectible, updates HUD. If count reaches `totalCollectibles`, calls `MinigameMenuController.Instance.OpenMenu(levelIndex)`.
- `ResetAll()`: resets count, re-activates all collectibles, calls `Reactivate()` on their animators.
- `UpdateHUD()`: sets `hudCounterText.text = "{collected}/{total}"`.

**Fields:** `totalCollectibles` (int, auto-set at runtime), `hudCounterText` (TMP).

---

## `Assets/Scripts/MiniGame/`

### `MiniGameSequenceController.cs`
**What it is:** The conductor for MiniGameScene. Execution order +100 so it runs after all sub-controllers finish their `Awake`/`Start`. Orchestrates Kintsugi → transition → FoldIt → win.

**What it does:**
- **`Phase` enum:** `Kintsugi`, `Transition`, `FoldIt` — tracks current game state.
- `Start()`:
  1. Calls `kintsugiGenerator.Initialize(GameEvents.CurrentLevel)` — builds the Kintsugi puzzle.
  2. Calls `foldItGenerator.Initialize(GameEvents.CurrentLevel)` if assigned — builds the FoldIt puzzle.
  3. Wires `kintsugiController.onComplete = OnKintsugiComplete` and `foldItController.onComplete = OnFoldItComplete`.
  4. **Currently:** hides `kintsugiRoot`, sets transition text to "fold to reveal" and shows FoldIt immediately (Kintsugi phase skipped for testing).
- `OnKintsugiComplete()`: starts `TransitionToFoldIt()` coroutine — hides kintsugiRoot, waits `transitionDuration`, shows foldItRoot.
- `OnFoldItComplete()`: hides transition text. If `isEmbedded`, fires `GameEvents.MinigameWon()`; otherwise loads `nextSceneName`.

**Fields:** `isEmbedded`, `nextSceneName`, `kintsugiRoot/foldItRoot` (GameObjects), `kintsugiController`, `foldItController`, `kintsugiGenerator`, `foldItGenerator`, `transitionText` (TMP), `transitionDuration`.

---

## `Assets/Scripts/MiniGameFoldIt/`

### `FoldItLevelData.cs`
**What it is:** Plain serializable data models used by `JsonUtility.FromJson`. No MonoBehaviour, no logic.

**`FoldItPieceData` fields:**
- `uvX, uvY, uvW, uvH` — normalized (0–1) UV footprint of the piece on the puzzle texture (bottom-left origin).
- `foldEdge` — which edge is the hinge: `"bottom"`, `"top"`, `"left"`, or `"right"`.
- `angleChange` — fold angle in degrees (typically ±180).
- `rotDuration` — animation length in seconds.
- `rotationOrderIndices` — list of `currentOrderIndex` values at which folding this piece counts as correct.
- `swipeDirX, swipeDirY` — screen-space direction the player must swipe to fold this piece.
- `isCompanion` — true for corner pieces that are auto-folded (not player-triggered).
- `companions` — list of piece indices (into `pieces[]`) to auto-fold when this edge piece folds.

**`FoldItLevelData` fields:**
- `levelIndex`, `textureIndex`, `backTextureIndex` — which texture array slot to use.
- `puzzleWorldWidth, puzzleWorldHeight` — world-unit dimensions of the full puzzle quad.
- `maxRotations` — informational only; overwritten at runtime by the count of non-companion pieces.
- `pieces` — list of `FoldItPieceData`.

---

### `GameController.cs`
**What it is:** The central manager for the FoldIt minigame. Handles swipe input, fold sequence tracking, and game completion.

**What it does:**
- **Swipe input:** `Update()` routes to `HandleMouseSwipe()` (Editor/Standalone) or `HandleTouchSwipe()` (mobile). Both detect a swipe delta ≥ `swipeThreshold` pixels, then call `ProcessSwipe(delta)`.
- `ProcessSwipe(delta)`: normalises the swipe, dot-products it against every `PaperFold.swipeDirection`. The highest-scoring unfolded piece that exceeds `swipeDirectionDotThreshold` becomes the fold target. The highest-scoring *folded* piece with opposing dot product is the unfold target. Folding takes priority. Calls `RotatePaper(false)` or `RotatePaper(true)`.
- `RotateBackToIndex(int index)` coroutine: walks back through `paperFolds` list, waiting for each undo to complete, until `currentOrderIndex == index`. Used when unfolding multiple steps.
- `ReinitializePieces()`: called by `FoldItPuzzleGenerator` after building all pieces. Calls `FindObjectsOfType<PaperFold>()` to repopulate `_allFolds`, clears `paperFolds` list, resets `currentOrderIndex = 0`, unblocks clicks.
- `CheckIfRotatedCorrectly()`: called by `PaperFold.FoldAroundAxis` when `currentOrderIndex == maxRotations`. Checks `rightOrder` on every piece. If all correct: fires `onComplete`, or `GameEvents.MinigameWon()` if embedded, or loads `nextSceneName`. If wrong: `RotateBackToIndex(0)`.
- `clickBlocked` — set `true` during any animation coroutine; `Update()` returns immediately when true.

**Fields:** `currentOrderIndex`, `paperFolds` (List<PaperFold>), `rotationCurve` (AnimationCurve), `clickBlocked`, `maxRotations`, `nextSceneName`, `isEmbedded`, `onComplete` (Action, hidden in Inspector), `swipeThreshold`, `swipeDirectionDotThreshold`.

---

### `PaperFold.cs`
**What it is:** Per-piece component that owns the fold animation and companion-fold logic.

**What it does:**
- `Awake()`: caches `_restPosition = transform.position` (world position before any fold).
- **`RotatePaper(bool back)`** — main entry point called by `GameController`:
  - `back=false` (fold): captures `orderIndex = currentOrderIndex`, adds self to `gameController.paperFolds`, hides `mainLine` (fold guide), starts `FoldAroundAxis` coroutine, sets `rotated = true`, checks `rotationOrderIndices` to set `rightOrder`, then calls `FoldWithParent` on each companion in `companionFolds`.
  - `back=true` (unfold): resets `orderIndex = -1`, starts `FoldAroundAxis` coroutine with `-angleChange`, removes self from `gameController.paperFolds`, sets `rotated = false`, `rightOrder = false`, calls `UnfoldWithParent` on companions.
- **`FoldAroundAxis` coroutine:** Sets `clickBlocked = true`. Lerps rotation and position using `rotationCurve.Evaluate(t/duration) * angle` over `rotDuration` seconds. On finish: if `back`, decrements `currentOrderIndex`, restores fold guide visibility; if forward, increments and checks if `maxRotations` reached (calls `CheckIfRotatedCorrectly`). Sets `clickBlocked = false`.
- **Companion system:**
  - `FoldWithParent(parent, pivot, axis, angle, duration)`: called by an adjacent edge piece. Stores the fold state keyed by parent piece. Starts `FoldAroundWorldPoint` coroutine. Supports two parents folding the same corner independently (folds compound).
  - `UnfoldWithParent(parent)`: removes the parent's fold state. Only unfolds if no other parent is still folding this corner.
  - `FoldAroundWorldPoint` coroutine: uses `Transform.RotateAround(pivot, axis, delta)` frame-by-frame (not absolute rotation) to avoid fighting other active rotations on the same object.
- **`lines` / `extraLineIndices` / `extraLinesStartPos` / `extraLinesEndPos`**: legacy fields for adjusting LineRenderer positions as folds happen. Now mostly empty (generator uses `mainLine` only).

**Fields:** `orderIndex`, `corners` (List<GameObject>), `gameController`, `rotAxis`, `rotDuration`, `angleChange`, `rotated`, `mainLine` (fold guide GO), `lines` (List<LineRenderer>), `rotationOrderIndices`, `rightOrder`, `swipeDirection`, `isCompanionPiece`, `companionFolds`.

---

### `FoldItPuzzleGenerator.cs`
**What it is:** Runtime mesh builder for the FoldIt puzzle. Reads JSON, generates GameObjects, wires `PaperFold`.

**Coordinate system:** `FoldItRoot` has X rotation = -90°. Its local XZ plane maps to world XY (camera view). Local +Y → world -Z. Vertices built in local XZ (y=0) appear flat and face the camera. `DepthOffset = 0.01` in local Y puts pieces at world z=-0.01 (in front of the background plane at z=0).

**What it does:**
- `Awake()`: caches `_foldItRoot = transform.parent`. Warns if `piecesRoot == _foldItRoot` (would destroy the generator itself).
- `Start()`: standalone fallback — if no `MiniGameSequenceController` found, calls `Initialize(1)`.
- **`Initialize(int levelIndex)`** (public API):
  1. Loads `Resources/FoldIt/Level{N}.json`, deserialises to `FoldItLevelData`.
  2. Picks front/back textures from `levelTextures[]`/`backTextures[]` by `data.textureIndex`.
  3. `ClearPiecesRoot()` — destroys all existing children of `piecesRoot`.
  4. Loops over `data.pieces`, calls `CreatePiece` or `CreateCompanionPiece` per entry.
  5. Wires companion references: for each edge piece that lists companions, adds the corresponding `PaperFold` to `companionFolds`.
  6. Sets `gameController.maxRotations` = count of non-companion pieces.
  7. Calls `gameController.ReinitializePieces()`.
- **`CreatePiece`**: converts UV rect → world dimensions. Switches on `foldEdge` ("bottom"/"top"/"left"/"right") to place the pivot at the hinge edge and build 4 vertices relative to that pivot in local XZ. Creates the GO (layer 0 Default), adds `MeshFilter + MeshRenderer` with a 2-submesh mesh (submesh 0 = back-face winding for before fold, submesh 1 = front-face for after fold), two material instances (`backMat + frontMat`), a `MeshCollider`, a `FoldGuide` child GO with `LineRenderer` (uses `dashLineMaterial`), and finally a `PaperFold` component with all fields wired.
- **`CreateCompanionPiece`**: similar but centred on UV rect centre (no pivot offset), no `rotationOrderIndices`, two cross-guide LineRenderers (`HGuide` and `VGuide`) along the two inner hinge edges, `isCompanionPiece = true`, `swipeDirection = Vector2.zero`.
- **`BuildMesh`**: 4 verts, 2 submeshes. Submesh 0 winding `{0,3,1,1,3,2}` → local +Y normals (faces camera before fold). Submesh 1 reversed → visible after 180° fold.

**Fields:** `gameController`, `baseMaterialTemplate`, `paperMaterialTemplate`, `piecesRoot`, `levelTextures[]`, `backTextures[]`, `guideLineWidth`, `dashLineMaterial`.

---

## `Assets/Scripts/MiniGameKintsugi/`

### `KintsugiLevelData.cs`
**What it is:** Serializable data models for `Resources/Kintsugi/Level{N}.json`.

**`KintsugiRectData`:** `x, y, w, h` (normalized 0–1, matches Unity `Rect` layout).

**`KintsugiLevelData` fields:** `levelIndex`, `pieceRects` (List<KintsugiRectData>), `puzzleWorldWidth/Height`, `tearSubdivisions`, `tearAmplitude`, `randomSeed`, `snapThreshold`, `snapDuration`, `scatterRadius`, `goldSeamWidth`.

---

### `KintsugiPuzzleConfig.cs`
**What it is:** A `ScriptableObject` that was the original configuration mechanism before JSON levels were added. **Still in the project** but largely superseded — the generator now reads from JSON instead.

**What it contains:** Same fields as `KintsugiLevelData` plus `puzzleTexture`, `goldSeamMaterial`, `snapCurve`, and three static preset methods (`Preset4Pieces()`, `Preset5Pieces()`, `Preset6Pieces()`) that return ready-made `List<Rect>` layouts. Useful as a designer reference for what piece layouts look like.

---

### `Triangulator.cs`
**What it is:** Pure static utility class — no MonoBehaviour, no state, no Unity lifecycle.

**What it does:** Implements the **ear-clipping triangulation** algorithm for simple (non-self-intersecting) polygons with any number of vertices.
- `Triangulate(List<Vector2> vertices)` → `int[]` triangle indices in **Unity CW left-handed winding order**.
- Internally corrects CCW→CW via signed-area shoelace check.
- Iterates the vertex ring, finds "ears" (convex vertices with no other vertex inside their triangle), emits them in reversed order (CW), removes them from the ring, repeats until 3 remain.
- Returns flat array `[tri0v0, tri0v1, tri0v2, tri1v0, ...]`.
- Used exclusively by `KintsugiPuzzleGenerator.BuildMesh()`.

---

### `KintsugiPiece.cs`
**What it is:** Per-piece behaviour component. Receives commands from `KintsugiGameController` — never reads input itself.

**What it does:**
- `Initialize(gc, snapThreshold, snapDuration, snapCurve, targetPos, index, scatteredPos)`: stores all config and the world-space target position (where the piece belongs in the solved puzzle).
- `UpdateDragPosition(cursorWorld)`: sets `transform.position = cursorWorld + dragOffset` with z=-0.5 (drag layer, in front of everything). Called every frame by `KintsugiGameController` while dragging.
- `TrySnap()`: on mouse-up, checks distance to `targetWorldPosition`. If ≤ `_snapThreshold`, starts `SnapToTarget()` coroutine and returns true.
- `SnapToTarget()` coroutine: `clickBlocked = true`, lerps position using `_snapCurve`, sets `isSnapped = true`, calls `ActivateAdjacentSeams()`, then `_gc.OnPieceSnapped(this)`, then `clickBlocked = false`.
- `ActivateAdjacentSeams()`: loops `adjacentSeams` dictionary; for each neighbour that is already snapped, sets `state.isActive = true` and enables `guideLR` (the faint gold hint LineRenderer).
- `TryActivateSeamWith(neighborIdx)`: called by a neighbour piece when it snaps — activates the seam between them from this side.

**Fields:** `pieceIndex`, `targetWorldPosition`, `isSnapped`, `isDragging`, `dragOffset`, `adjacentSeams` (Dictionary<int, SeamTraceState>).

---

### `KintsugiPuzzleGenerator.cs`
**What it is:** The full Kintsugi puzzle factory. Called once per game via `Initialize(levelIndex)`.

**What it does (in order):**

1. **`Initialize(levelIndex)`**: loads JSON, converts rects, picks texture, then calls in sequence: `GenerateTearEdges()`, `CreateBackground()`, `CreatePieces()`, `CreateGoldSeams()`, `ScatterPieces()`, and finally `gameController.SetPieces(_pieces)`.

2. **`GenerateTearEdges()`**: for every pair of pieces (a,b) that share an edge, calls `TryGetSharedEdge` to find the overlap segment, then `GenerateTearProfile` to displace subdivision points perpendicularly. Stores the profile both directions (`"a_b"` and `"b_a"` reversed) in `_tearEdges` dictionary.

3. **`GenerateTearProfile`**: creates a seeded `System.Random` (`seed + edgeHash`), linearly subdivides the edge into `tearSubdivisions` points, offsets each by up to `±tearAmplitude` in the perpendicular direction, clamps to [0,1].

4. **`CreateBackground()`**: creates a `SpriteRenderer` GO at z=+0.5 (behind pieces), scaled to fill `puzzleWorldWidth × puzzleWorldHeight`, with `color.a = backgroundAlpha` (faint ghost image showing the solved state).

5. **`CreatePieces()`**: for each rect, calls `BuildPieceOutline(i)` (walks all 4 sides, substituting tear profiles for shared edges), then `BuildMesh` (triangulates via `Triangulator`, builds vertex positions relative to piece centre, UV = normalized rect coordinates). Creates GO on layer `KintsugiLayer` (8), adds `MeshFilter + MeshRenderer`, `PolygonCollider2D` (path from outline in local space), and `KintsugiPiece`.

6. **`BuildPieceOutline(i)`**: walks the 4 sides of the piece's rect. For each side, `FindAllNeighborsOnEdge` finds all tear segments from `_tearEdges` that lie on that side, sorts them in the correct direction, and concatenates them. Outer edges (no tear) just use the straight corner.

7. **`CreateGoldSeams()`**: for every a<b pair with a tear, creates two `LineRenderer`s — `goldLR` (gold, starts disabled, grows as the player traces) and `guideLR` (semi-transparent, starts disabled, shown after both adjacent pieces snap). Builds a `SeamTraceState` holding both LRs, the baked world-space points, and progress. Registers the state in both `_pieces[a].adjacentSeams[b]` and `_pieces[b].adjacentSeams[a]`.

8. **`ScatterPieces()`**: places each piece at a random position within `_data.scatterRadius` (polar coords, seeded RNG), with a small z stagger (`zBase + i*0.01`) for Z-ordering.

**Layers used:** `KintsugiLayer = 8` ("KintsugiPieces", physics overlap queries), `MiniGameLayer = 9` ("MiniGame", rendering — seen only by the minigame camera).

**Fields:** `gameController`, `pieceMaterial`, `puzzleCenter`, `goldSeamsParent`, `levelTextures[]`, `goldSeamMaterial`, `snapCurve`, `backgroundAlpha`.

---

### `KintsugiGameController.cs`
**What it is:** Central input controller and completion arbiter for the Kintsugi puzzle.

**Defines `SeamTraceState`** (top-level class in same file):
- `goldLR` — LineRenderer that grows point-by-point as the player drags.
- `guideLR` — faint hint LineRenderer showing the full seam path.
- `points[]` — baked world-space points along the tear.
- `progress` — how many points have been confirmed traced (0 = untouched, `points.Length` = done).
- `isActive` — true once both adjacent pieces are snapped.
- `IsFullyTraced` property — `progress >= points.Length`.

**What the controller does:**
- `Awake()`: finds the scene-local camera (avoids `Camera.main` ambiguity).
- `SetPieces(list)`, `SetSeams(list)`: called by the generator; stores pieces and seams, resets counts.
- `GetPiece(index)`: used by `KintsugiPiece.ActivateAdjacentSeams` to look up neighbour pieces.
- **Input (mouse/touch split):** `HandleMouseInput` / `HandleTouchInput`:
  - Down: `TryPickUp` first (Physics2D.OverlapPoint on layer 8); if nothing under cursor, `TryStartTrace`.
  - Held: if dragging → `UpdateDragPosition`; if tracing → `UpdateTrace`.
  - Up: `TryRelease` (calls `piece.TrySnap`), `EndTrace`.
- **`TryPickUp`**: `Physics2D.OverlapPoint` on `PieceLayerMask` (1<<8). Gets `KintsugiPiece`, stores drag offset, returns true.
- **`TryRelease`**: clears `_draggedPiece`, calls `piece.TrySnap()`.
- **`TryStartTrace`**: scans all active, incomplete seams for the nearest point within `traceProximity * 2`. If found: if seam is fresh (progress=0), checks if click is on the far half and reverses points array accordingly, sets `goldLR.positionCount=1`. If partial, re-bakes existing progress. Sets `_activeSeam`.
- **`UpdateTrace`**: advances `_activeSeam.progress` while the cursor's projection along the current segment reaches the segment's midpoint. Re-writes all `goldLR` positions from `points[]`. When `IsFullyTraced`, hides guideLR and calls `TryCheckCompletion`.
- **`OnPieceSnapped(piece)`**: increments `_snappedCount`, notifies neighbours to activate seams via `TryActivateSeamWith`, calls `TryCheckCompletion`.
- **`TryCheckCompletion`**: gates on `_snappedCount >= _totalPieces` AND all seams `IsFullyTraced`. Then calls `CheckCompletion`.
- **`CheckCompletion`**: fires `onComplete` delegate, or `GameEvents.MinigameWon()` if embedded, or `SceneManager.LoadScene(nextSceneName)`.

**Fields:** `isEmbedded`, `nextSceneName`, `onComplete` (Action), `clickBlocked`, `traceProximity` (0.25 world units default).

---

## Data Flow Summary

```
CollectibleAnimator.OnMouseDown
  └─ CollectibleManager.Collect(go, levelIndex)
       └─ MinigameMenuController.OpenMenu(level)
            ├─ GameEvents.CurrentLevel = level
            ├─ LoadSceneAsync("MiniGameScene", Additive)
            └─ loadOp.completed
                 ├─ ConfigureMinigameCamera() — depth composite + culling masks
                 └─ MiniGameSequenceController.isEmbedded = true

MiniGameSequenceController.Start() [order +100]
  ├─ kintsugiGenerator.Initialize(CurrentLevel) — builds Kintsugi puzzle
  ├─ foldItGenerator.Initialize(CurrentLevel)   — builds FoldIt puzzle
  ├─ kintsugiController.onComplete = OnKintsugiComplete
  └─ foldItController.onComplete   = OnFoldItComplete

  [Currently: kintsugiRoot hidden, FoldIt shown immediately]

FoldIt play:
  GameController.ProcessSwipe → PaperFold.RotatePaper
    └─ FoldAroundAxis coroutine → CheckIfRotatedCorrectly
         └─ onComplete() → MiniGameSequenceController.OnFoldItComplete
              └─ GameEvents.MinigameWon()

MinigameMenuController.HandleMinigameWon → WinSequence
  ├─ UnloadSceneAsync("MiniGameScene")
  ├─ dimOverlay.SetActive(false)
  └─ victoryText shown for victoryDisplayDuration seconds

CollectibleManager.ResetAll (also triggered by OnMinigameWon)
  └─ re-activates all collectibles + restarts their pulse animations
```

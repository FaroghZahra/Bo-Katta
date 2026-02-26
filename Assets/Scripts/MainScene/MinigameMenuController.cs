using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MinigameMenuController : MonoBehaviour
{
    public static MinigameMenuController Instance { get; private set; }

    [Header("Dim Overlay")]
    public GameObject dimOverlay;             // Semi-transparent dark panel (Screen Space - Camera canvas)

    [Header("Victory UI")]
    public TextMeshProUGUI victoryText;        // "Congratulations!" text, starts inactive

    [Header("HUD")]
    public GameObject seekFragmentsText;       // "Seek the fragments" label — hidden during minigame

    [Header("Scene")]
    public string minigameSceneName = "MiniGameScene";

    [Header("Timing")]
    public float victoryDisplayDuration = 3f;

    private bool minigameIsLoaded = false;
    private Camera _mainCamera;
    private int _originalMainCullingMask = -1;
    private PinchZoomController _cameraZoom;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Find the camera that lives in this (MainScene) scene.
        // Do NOT rely on Camera.main / "MainCamera" tag — they may be unset or ambiguous.
        foreach (Camera c in FindObjectsOfType<Camera>())
        {
            if (c.gameObject.scene == gameObject.scene) { _mainCamera = c; break; }
        }
        if (_mainCamera != null)
            _cameraZoom = _mainCamera.GetComponent<PinchZoomController>();
    }

    private void OnEnable()  => GameEvents.OnMinigameWon += HandleMinigameWon;
    private void OnDisable() => GameEvents.OnMinigameWon -= HandleMinigameWon;

    public void OpenMenu(int level = 1)
    {
        if (minigameIsLoaded) return;
        Debug.Log($"[MinigameMenuController] Opening level {level}");
        GameEvents.CurrentLevel = level;
        _cameraZoom?.SetZoomEnabled(false);
        dimOverlay?.SetActive(true);
        seekFragmentsText?.SetActive(false);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(minigameSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"Cannot load '{minigameSceneName}'. Add it to Build Settings.");
            dimOverlay?.SetActive(false);
            return;
        }
        minigameIsLoaded = true;
        loadOp.completed += _ =>
        {
            ConfigureMinigameCamera();

            MiniGameSequenceController seq = FindObjectOfType<MiniGameSequenceController>();
            if (seq != null)
            {
                seq.isEmbedded = true;
            }
            else
            {
                GameController gc = FindObjectOfType<GameController>();
                if (gc != null) gc.isEmbedded = true;

                KintsugiGameController kgc = FindObjectOfType<KintsugiGameController>();
                if (kgc != null) kgc.isEmbedded = true;
            }
        };
    }

    // Composites the minigame camera on top of the main scene instead of replacing it.
    // - Depth Only: preserves the main scene render in the colour buffer.
    // - Culling mask restricted to "MiniGame" layer: stops the minigame camera from
    //   re-rendering main scene objects at its own (different) zoom level.
    // - Main camera culling mask excludes "MiniGame" layer: stops main camera from
    //   rendering minigame objects at its own (different) zoom level.
    private void ConfigureMinigameCamera()
    {
        float mainDepth = _mainCamera != null ? _mainCamera.depth : 0f;

        // Build a mask covering all layers owned by the minigame.
        // "MiniGame" (layer 9)   — static scene objects, FoldIt pieces
        // "KintsugiPieces" (layer 8) — runtime-generated Kintsugi piece meshes
        int minigameLayer   = LayerMask.NameToLayer("MiniGame");
        int kintsugiLayer   = LayerMask.NameToLayer("KintsugiPieces");
        int minigameMask    = 0;
        if (minigameLayer >= 0)  minigameMask |= 1 << minigameLayer;
        if (kintsugiLayer >= 0)  minigameMask |= 1 << kintsugiLayer;

        foreach (Camera cam in FindObjectsOfType<Camera>())
        {
            // Skip any camera that lives in the same scene as this controller (MainScene).
            if (cam.gameObject.scene == gameObject.scene) continue;

            cam.clearFlags = CameraClearFlags.Depth;
            cam.depth = mainDepth + 1f;

            if (minigameMask != 0)
                cam.cullingMask = minigameMask;
        }

        // Exclude ALL minigame layers from the main camera so it doesn't render
        // minigame objects at the wrong (main-scene) zoom level.
        if (minigameMask != 0 && _mainCamera != null)
        {
            _originalMainCullingMask = _mainCamera.cullingMask;
            _mainCamera.cullingMask &= ~minigameMask;
        }
    }

    private void HandleMinigameWon() => StartCoroutine(WinSequence());

    private IEnumerator WinSequence()
    {
        // Restore main camera culling mask before unloading minigame scene.
        if (_mainCamera != null && _originalMainCullingMask != -1)
        {
            _mainCamera.cullingMask = _originalMainCullingMask;
            _originalMainCullingMask = -1;
        }

        AsyncOperation unload = SceneManager.UnloadSceneAsync(minigameSceneName);
        if (unload != null) yield return unload;
        minigameIsLoaded = false;

        dimOverlay?.SetActive(false);
        seekFragmentsText?.SetActive(true);
        _cameraZoom?.SetZoomEnabled(true);

        yield return StartCoroutine(ShowVictoryMessage());

        GameEvents.LevelProgression(GameEvents.CurrentLevel);
    }

    private IEnumerator ShowVictoryMessage()
    {
        if (victoryText != null)
        {
            victoryText.gameObject.SetActive(true);
            yield return new WaitForSeconds(victoryDisplayDuration);
            victoryText.gameObject.SetActive(false);
        }
    }
}

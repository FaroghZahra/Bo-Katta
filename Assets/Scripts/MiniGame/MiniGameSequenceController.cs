using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Orchestrates the two-phase minigame sequence.
/// Level 1: Kintsugi → FoldIt
/// Level 2: Kintsugi → OrigamiSwipe
/// Level 3: Kintsugi only (immediate win)
/// Set execution order to +100 so sub-controllers initialise first.
/// </summary>
public class MiniGameSequenceController : MonoBehaviour
{
    public enum Phase { Kintsugi, Transition, FoldIt, OrigamiSwipe }

    [Header("Embed / Standalone")]
    public bool   isEmbedded    = false;
    public string nextSceneName = "MainScene";

    [Header("Phase Roots")]
    public GameObject kintsugiRoot;
    public GameObject foldItRoot;

    [Header("Controllers")]
    public KintsugiGameController kintsugiController;

    [Header("FoldIt Level Prefabs")]
    [Tooltip("Index 0 = Level 1, 1 = Level 2, 2 = Level 3")]
    public GameObject[] foldItLevelPrefabs;

    [Header("Generator")]
    public KintsugiPuzzleGenerator kintsugiGenerator;

    private GameController foldItController;

    [Header("OrigamiSwipe (Level 2)")]
    public GameObject            origamiSwipeRoot;
    public OrigamiSwipeController origamiSwipeController;

    [Header("Transition UI")]
    public TextMeshProUGUI transitionText;
    public float           transitionDuration = 2.5f;

    private Phase _currentPhase = Phase.Kintsugi;

    void Start()
    {
        kintsugiGenerator.Initialize(GameEvents.CurrentLevel);

        if (GameEvents.CurrentLevel != 2)
        {
            // Remove the orange background Plane and any scene-baked level prefab instance
            // that were placed in FoldItRoot during editor setup. Boundary colliders are kept.
            for (int i = foldItRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = foldItRoot.transform.GetChild(i);
                if (child.name == "Plane" || child.name.StartsWith("FoldItLevel"))
                    Destroy(child.gameObject);
            }

            // Instantiate the correct level prefab while foldItRoot is inactive
            // so Awake/Start on the prefab don't run until we activate foldItRoot.
            int prefabIndex = Mathf.Clamp(GameEvents.CurrentLevel - 1, 0, foldItLevelPrefabs.Length - 1);
            GameObject foldItInstance = Instantiate(foldItLevelPrefabs[prefabIndex], foldItRoot.transform);
            foldItController = foldItInstance.GetComponentInChildren<GameController>(true);
            foldItController.onComplete = OnFoldItComplete;

            foldItRoot.SetActive(false);

            if (origamiSwipeRoot != null) origamiSwipeRoot.SetActive(false);
        }
        else
        {
            // Level 2 — use OrigamiSwipe instead of FoldIt
            if (foldItRoot != null) foldItRoot.SetActive(false);

            if (origamiSwipeController != null)
                origamiSwipeController.onComplete = OnOrigamiSwipeComplete;
            else
                Debug.LogError("[MiniGameSequenceController] origamiSwipeController is not assigned for Level 2.");

            if (origamiSwipeRoot != null) origamiSwipeRoot.SetActive(false);
        }

        kintsugiController.onComplete = OnKintsugiComplete;

        // Show the Kintsugi phase label immediately.
        if (transitionText != null)
        {
            transitionText.text = "bring the pieces together";
            transitionText.gameObject.SetActive(true);
        }
    }

    void OnKintsugiComplete()
    {
        if (GameEvents.CurrentLevel == 3)
        {
            if (transitionText != null)
                transitionText.gameObject.SetActive(false);

            if (isEmbedded)
                GameEvents.MinigameWon();
            else
                SceneManager.LoadScene(nextSceneName);
            return;
        }

        _currentPhase = Phase.Transition;

        if (GameEvents.CurrentLevel == 2)
        {
            StartCoroutine(TransitionToOrigamiSwipe());
            return;
        }

        StartCoroutine(TransitionToFoldIt());   // Level 1
    }

    IEnumerator TransitionToFoldIt()
    {
        kintsugiRoot.SetActive(false);

        if (transitionText != null)
        {
            yield return new WaitForSeconds(transitionDuration);
        }

        _currentPhase = Phase.FoldIt;
        foldItRoot.SetActive(true);

        if (transitionText != null)
        {
            transitionText.text = "fold to reveal";
            transitionText.gameObject.SetActive(true);
        }
    }

    void OnFoldItComplete()
    {
        if (transitionText != null)
            transitionText.gameObject.SetActive(false);

        if (isEmbedded)
            GameEvents.MinigameWon();
        else
            SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator TransitionToOrigamiSwipe()
    {
        kintsugiRoot.SetActive(false);

        if (transitionText != null)
        {
            transitionText.gameObject.SetActive(false);
            yield return new WaitForSeconds(transitionDuration);
        }

        _currentPhase = Phase.OrigamiSwipe;
        if (origamiSwipeRoot != null) origamiSwipeRoot.SetActive(true);

        if (transitionText != null)
        {
            transitionText.text = "unfold the sun";
            transitionText.gameObject.SetActive(true);
        }
    }

    void OnOrigamiSwipeComplete()
    {
        if (transitionText != null)
            transitionText.gameObject.SetActive(false);

        if (isEmbedded)
            GameEvents.MinigameWon();
        else
            SceneManager.LoadScene(nextSceneName);
    }
}

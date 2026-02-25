using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Orchestrates the two-phase minigame sequence: Kintsugi → Fold It.
/// Set execution order to +100 so sub-controllers initialise first.
/// Both phases use the same static camera — no camera switching needed.
/// </summary>
public class MiniGameSequenceController : MonoBehaviour
{
    public enum Phase { Kintsugi, Transition, FoldIt }

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

    [Header("Transition UI")]
    public TextMeshProUGUI transitionText;
    public float           transitionDuration = 2.5f;

    private Phase _currentPhase = Phase.Kintsugi;

    void Start()
    {
        kintsugiGenerator.Initialize(GameEvents.CurrentLevel);

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

        kintsugiController.onComplete = OnKintsugiComplete;
        foldItController.onComplete   = OnFoldItComplete;

        foldItRoot.SetActive(false);

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
        StartCoroutine(TransitionToFoldIt());
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

        // Swap label for the FoldIt phase.
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
}

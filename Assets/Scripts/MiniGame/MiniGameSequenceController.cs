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
    public GameController         foldItController;

    [Header("Transition UI")]
    public TextMeshProUGUI transitionText;
    public float           transitionDuration = 2.5f;

    private Phase _currentPhase = Phase.Kintsugi;

    void Start()
    {
        kintsugiController.onComplete = OnKintsugiComplete;
        foldItController.onComplete   = OnFoldItComplete;

        // Both roots are active in the editor so their Start() runs first (execution order +100).
        // Hide FoldIt after all child Start() calls have finished initialising.
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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MinigameMenuController : MonoBehaviour
{
    public static MinigameMenuController Instance { get; private set; }

    [Header("Overlay UI")]
    public GameObject overlayPanel;           // Fullscreen black panel, starts inactive
    public TextMeshProUGUI victoryText;        // "Congratulations!" text, starts inactive

    [Header("Scene")]
    public string minigameSceneName = "FoldItScene";

    [Header("World Objects")]
    public GameObject collectibleObject;      // The Collectible instance in the scene

    [Header("Timing")]
    public float victoryDisplayDuration = 3f;

    private bool minigameIsLoaded = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => GameEvents.OnMinigameWon += HandleMinigameWon;
    private void OnDisable() => GameEvents.OnMinigameWon -= HandleMinigameWon;

    public void OpenMenu()
    {
        if (minigameIsLoaded) return;
        overlayPanel?.SetActive(true);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(minigameSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"Cannot load '{minigameSceneName}'. Add it to Build Settings.");
            overlayPanel?.SetActive(false);
            return;
        }
        minigameIsLoaded = true;
        loadOp.completed += _ =>
        {
            GameController gc = FindObjectOfType<GameController>();
            if (gc != null) gc.isEmbedded = true;
        };
    }

    private void HandleMinigameWon() => StartCoroutine(WinSequence());

    private IEnumerator WinSequence()
    {
        AsyncOperation unload = SceneManager.UnloadSceneAsync(minigameSceneName);
        if (unload != null) yield return unload;
        minigameIsLoaded = false;

        overlayPanel?.SetActive(false);
        collectibleObject?.SetActive(false);

        yield return StartCoroutine(ShowVictoryMessage());
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

using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashScreenController : MonoBehaviour
{
    [SerializeField] private string    mainSceneName  = "MainScene";
    [SerializeField] private Transform exploreButton;
    [SerializeField] private float     pulseScale     = 1.08f;
    [SerializeField] private float     pulseDuration  = 0.7f;
    [SerializeField] private GameObject playAgainButton;

    void Start()
    {
        if (exploreButton != null)
            exploreButton.DOScale(pulseScale, pulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

        if (playAgainButton != null)
            playAgainButton.SetActive(PlayerPrefs.GetInt("GameCompleted", 0) == 1);
    }

    void OnDestroy()
    {
        if (exploreButton != null)
            exploreButton.DOKill();
    }

    public void OnExploreClicked()
    {
        SceneManager.LoadScene(mainSceneName);
    }

    public void OnPlayAgainClicked()
    {
        PlayerPrefs.DeleteKey("GameCompleted");
        PlayerPrefs.Save();
        GameEvents.GameCompleted = false;
        GameEvents.CurrentLevel = 1;
        SceneManager.LoadScene(mainSceneName);
    }
}

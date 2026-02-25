using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CollectibleManager : MonoBehaviour
{
    public static CollectibleManager Instance { get; private set; }

    [Header("Settings")]
    public int totalCollectibles = 4;

    [Header("HUD")]
    public TextMeshProUGUI hudCounterText;

    private int _collectedCount = 0;
    private int _currentLevelIndex = 1;
    private List<GameObject> _allCollectibles = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        UpdateHUD();
    }


    public void Collect(GameObject go, int levelIndex)
    {
        if (!go.activeSelf) return;

        _collectedCount++;
        go.SetActive(false);
        UpdateHUD();

        if (_collectedCount >= totalCollectibles)
            MinigameMenuController.Instance?.OpenMenu(_currentLevelIndex);
    }

    public void ResetAll()
    {
        _collectedCount = 0;
        UpdateHUD();

        foreach (GameObject go in _allCollectibles)
        {
            if (go == null) continue;
            go.SetActive(true);
            CollectibleAnimator anim = go.GetComponent<CollectibleAnimator>();
            anim?.Reactivate();
        }
    }

    public void SetupForLevel(GameObject[] collectibles, int levelIndex = 1)
    {
        _collectedCount = 0;
        _currentLevelIndex = levelIndex;
        _allCollectibles = new List<GameObject>(collectibles);
        totalCollectibles = _allCollectibles.Count;
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (hudCounterText != null)
            hudCounterText.text = $"{_collectedCount}/{totalCollectibles}";
    }
}

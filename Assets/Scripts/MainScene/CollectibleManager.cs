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
    public TextMeshProUGUI goldHudText;

    // If set, called instead of OpenMenu when all collectibles (+ gold, if required) are collected.
    // Automatically cleared after one use.
    public System.Action onAllCollectedOverride;

    private int _collectedCount = 0;
    private bool _goldItemCollected = false;
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
        UpdateGoldHUD();
    }


    public void Collect(GameObject go, int levelIndex)
    {
        if (!go.activeSelf) return;

        _collectedCount++;
        go.SetActive(false);
        UpdateHUD();

        if (_collectedCount >= totalCollectibles && _goldItemCollected)
        {
            if (onAllCollectedOverride != null)
            {
                var cb = onAllCollectedOverride;
                onAllCollectedOverride = null;
                cb.Invoke();
            }
            else
            {
                MinigameMenuController.Instance?.OpenMenu(_currentLevelIndex);
            }
        }
    }

    public void ResetAll()
    {
        _collectedCount = 0;
        UpdateHUD();
        // collectibles are restored by their Moveables
    }

    public void SetupForLevel(GameObject[] collectibles, int levelIndex = 1)
    {
        _collectedCount = 0;
        _currentLevelIndex = levelIndex;
        _allCollectibles = new List<GameObject>(collectibles);
        totalCollectibles = _allCollectibles.Count;
        onAllCollectedOverride = null;  // clear stale callbacks
        UpdateHUD();
    }

    public void CollectGoldItem()
    {
        _goldItemCollected = true;
        UpdateGoldHUD();
        if (_collectedCount >= totalCollectibles)
        {
            if (onAllCollectedOverride != null)
            {
                var cb = onAllCollectedOverride;
                onAllCollectedOverride = null;
                cb.Invoke();
            }
            else
            {
                MinigameMenuController.Instance?.OpenMenu(_currentLevelIndex);
            }
        }
    }

    private void UpdateHUD()
    {
        if (hudCounterText != null)
            hudCounterText.text = $"{_collectedCount}/{totalCollectibles}";
    }

    private void UpdateGoldHUD()
    {
        if (goldHudText != null)
            goldHudText.text = _goldItemCollected ? "1/1" : "0/1";
    }
}

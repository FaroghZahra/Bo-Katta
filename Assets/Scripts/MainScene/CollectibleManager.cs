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
    private List<GameObject> _allCollectibles = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        GameObject[] found = GameObject.FindGameObjectsWithTag("Collectible");
        _allCollectibles = new List<GameObject>(found);
        if (_allCollectibles.Count > 0)
            totalCollectibles = _allCollectibles.Count;
    }

    private void Start()
    {
        UpdateHUD();
    }

    private void OnEnable()  => GameEvents.OnMinigameWon += ResetAll;
    private void OnDisable() => GameEvents.OnMinigameWon -= ResetAll;

    public void Collect(GameObject go, int levelIndex)
    {
        if (!go.activeSelf) return;

        _collectedCount++;
        go.SetActive(false);
        UpdateHUD();

        if (_collectedCount >= totalCollectibles)
            MinigameMenuController.Instance?.OpenMenu(levelIndex);
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

    private void UpdateHUD()
    {
        if (hudCounterText != null)
            hudCounterText.text = $"{_collectedCount}/{totalCollectibles}";
    }
}

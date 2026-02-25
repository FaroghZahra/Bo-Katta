using UnityEngine;

public class LevelProgressionController : MonoBehaviour
{
    [Header("Background")]
    public SpriteRenderer backgroundRenderer;
    public Sprite[] backgroundSprites;   // [0]=level-1, [1]=level-2, [2]=level-3, [3]=after-level-3

    [Header("Collectible Groups")]
    public GameObject collectiblesLevel1;
    public GameObject collectiblesLevel2;
    public GameObject collectiblesLevel3;

    [Header("Character")]
    public CharacterMovement characterMovement;

    private void Awake()
    {
        collectiblesLevel2?.SetActive(false);
        collectiblesLevel3?.SetActive(false);
    }

    private void Start()
    {
        RegisterGroupWithManager(collectiblesLevel1, 1);
    }

    private void OnEnable()  => GameEvents.OnLevelProgression += OnLevelProgression;
    private void OnDisable() => GameEvents.OnLevelProgression -= OnLevelProgression;

    private void OnLevelProgression(int completedLevel)
    {
        if (completedLevel == 1)
        {
            SwapBackground(1);
            collectiblesLevel1?.SetActive(false);
            collectiblesLevel2?.SetActive(true);
            RegisterGroupWithManager(collectiblesLevel2, 2);
            characterMovement?.ResumeMoving();
        }
        else if (completedLevel == 2)
        {
            SwapBackground(2);
            collectiblesLevel2?.SetActive(false);
            collectiblesLevel3?.SetActive(true);
            RegisterGroupWithManager(collectiblesLevel3, 3);
            characterMovement?.ResumeMoving();
        }
        else if (completedLevel == 3)
        {
            SwapBackground(3);
            collectiblesLevel3?.SetActive(false);
            characterMovement?.ResumeMoving();
        }
    }

    private void RegisterGroupWithManager(GameObject group, int levelIndex)
    {
        if (group == null || CollectibleManager.Instance == null) return;
        int count = group.transform.childCount;
        GameObject[] children = new GameObject[count];
        for (int i = 0; i < count; i++)
            children[i] = group.transform.GetChild(i).gameObject;
        CollectibleManager.Instance.SetupForLevel(children, levelIndex);
    }

    private void SwapBackground(int spriteIndex)
    {
        if (backgroundRenderer == null || backgroundSprites == null) return;
        if (spriteIndex >= backgroundSprites.Length) return;
        backgroundRenderer.sprite = backgroundSprites[spriteIndex];
        CameraAutoFit fit = backgroundRenderer.GetComponentInParent<CameraAutoFit>();
        if (fit == null) fit = FindObjectOfType<CameraAutoFit>();
        fit?.Refit();
    }
}

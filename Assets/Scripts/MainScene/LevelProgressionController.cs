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

    [Header("Moveable Groups")]
    public GameObject moveablesLevel1;
    public GameObject moveablesLevel2;
    public GameObject moveablesLevel3;

    [Header("Level 3 — Sequential Batches")]
    public GameObject moveablesLevel3A;
    public GameObject collectiblesLevel3A;
    public GameObject moveablesLevel3B;
    public GameObject collectiblesLevel3B;

    [Header("Character")]
    public CharacterMovement characterMovement;

    [Header("Rewards")]
    public BridgeRepairReward bridgeRepairReward;
    public CatSequenceController catSequenceController;

    [Header("Boat")]
    public GameObject brokenBoat;
    public BoatRepairReward boatRepairReward;
    public BoatMovement boatMovement;

    private bool _waitingForL3UnlockPoint  = false;
    private bool _waitingForBoatBreakpoint = false;

    private void Awake()
    {
        brokenBoat?.SetActive(false);
        collectiblesLevel2?.SetActive(false);
        collectiblesLevel3?.SetActive(false);
        moveablesLevel2?.SetActive(false);
        moveablesLevel3?.SetActive(false);
        collectiblesLevel3A?.SetActive(false);
        collectiblesLevel3B?.SetActive(false);
        moveablesLevel3A?.SetActive(false);
        moveablesLevel3B?.SetActive(false);
    }

    private void Start()
    {
        RegisterGroupWithManager(collectiblesLevel1, 1);
    }

    private void OnEnable()
    {
        GameEvents.OnLevelProgression += OnLevelProgression;
        GameEvents.OnBreakpointReached += OnBreakpointReached;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelProgression -= OnLevelProgression;
        GameEvents.OnBreakpointReached -= OnBreakpointReached;
    }

    private void OnLevelProgression(int completedLevel)
    {
        if (completedLevel == 1)
        {
            collectiblesLevel1?.SetActive(false);
            collectiblesLevel2?.SetActive(true);
            moveablesLevel1?.SetActive(false);
            moveablesLevel2?.SetActive(true);
            RegisterGroupWithManager(collectiblesLevel2, 2);

            if (bridgeRepairReward != null)
                bridgeRepairReward.Play(onComplete: () =>
                {
                    SwapBackground(1);
                    catSequenceController?.gameObject.SetActive(true);
                    characterMovement?.ResumeMoving();
                });
            else
            {
                SwapBackground(1);
                catSequenceController?.gameObject.SetActive(true);
                characterMovement?.ResumeMoving();
            }
        }
        else if (completedLevel == 2)
        {
            SwapBackground(2);
            collectiblesLevel2?.SetActive(false);
            moveablesLevel2?.SetActive(false);
            collectiblesLevel3?.SetActive(true);
            moveablesLevel3?.SetActive(true);
            RegisterGroupWithManager(collectiblesLevel3, 3);
            _waitingForL3UnlockPoint = true;
            if (catSequenceController != null)
                catSequenceController.PlaySequence(() => characterMovement?.ResumeMoving());
            else
                characterMovement?.ResumeMoving();
        }
        else if (completedLevel == 3)
        {
            collectiblesLevel3A?.SetActive(false);
            collectiblesLevel3B?.SetActive(false);
            moveablesLevel3A?.SetActive(false);
            moveablesLevel3B?.SetActive(false);

            if (boatRepairReward != null)
                boatRepairReward.Play(onComplete: () =>
                {
                    _waitingForBoatBreakpoint = true;
                    characterMovement?.ResumeMoving();
                });
            else
            {
                _waitingForBoatBreakpoint = true;
                characterMovement?.ResumeMoving();
            }
        }
    }

    private void OnBreakpointReached()
    {
        if (_waitingForL3UnlockPoint)
        {
            _waitingForL3UnlockPoint = false;

            moveablesLevel3A?.SetActive(true);
            collectiblesLevel3A?.SetActive(true);
            ActivateMoveablesInGroup(moveablesLevel3A);
            RegisterGroupWithManager(collectiblesLevel3A, 3);
            CollectibleManager.Instance.onAllCollectedOverride = RevealLevel3B;
            return;
        }

        if (_waitingForBoatBreakpoint)
        {
            _waitingForBoatBreakpoint = false;
            boatMovement?.StartSailing();
        }
    }

    private void RevealLevel3B()
    {
        moveablesLevel3B?.SetActive(true);
        collectiblesLevel3B?.SetActive(true);
        ActivateMoveablesInGroup(moveablesLevel3B);
        RegisterGroupWithManager(collectiblesLevel3B, 3);
        CollectibleManager.Instance.onAllCollectedOverride = OnLevel3CollectionsDone;
    }

    private void OnLevel3CollectionsDone()
    {
        brokenBoat?.SetActive(true);
        SwapBackground(3);
        characterMovement?.ResumeMoving();
    }

    private void ActivateMoveablesInGroup(GameObject group)
    {
        if (group == null) return;
        // If the group object itself is a Moveable (single-object reference), use it directly.
        var self = group.GetComponent<Moveable>();
        if (self != null) { self.MakeInteractable(); return; }
        foreach (Transform child in group.transform)
            child.GetComponent<Moveable>()?.MakeInteractable();
    }

    private void RegisterGroupWithManager(GameObject group, int levelIndex)
    {
        if (group == null || CollectibleManager.Instance == null) return;
        // If the group object itself is a collectible (single-object reference), register it directly.
        if (group.GetComponent<CollectibleAnimator>() != null)
        {
            CollectibleManager.Instance.SetupForLevel(new GameObject[] { group }, levelIndex);
            return;
        }
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
